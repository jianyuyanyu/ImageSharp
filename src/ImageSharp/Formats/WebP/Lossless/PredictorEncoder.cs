// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    /// <summary>
    /// Image transform methods for the lossless webp encoder.
    /// </summary>
    internal static unsafe class PredictorEncoder
    {
        private const int GreenRedToBlueNumAxis = 8;

        private const int GreenRedToBlueMaxIters = 7;

        private const float MaxDiffCost = 1e30f;

        private const uint MaskAlpha = 0xff000000;

        private const float SpatialPredictorBias = 15.0f;

        /// <summary>
        /// Finds the best predictor for each tile, and converts the image to residuals
        /// with respect to predictions. If nearLosslessQuality &lt; 100, applies
        /// near lossless processing, shaving off more bits of residuals for lower qualities.
        /// </summary>
        public static void ResidualImage(
            int width,
            int height,
            int bits,
            Span<uint> argb,
            Span<uint> argbScratch,
            Span<uint> image,
            int nearLosslessQuality,
            bool exact,
            bool usedSubtractGreen)
        {
            int tilesPerRow = LosslessUtils.SubSampleSize(width, bits);
            int tilesPerCol = LosslessUtils.SubSampleSize(height, bits);
            int maxQuantization = 1 << LosslessUtils.NearLosslessBits(nearLosslessQuality);

            // TODO: Can we optimize this?
            var histo = new int[4][];
            for (int i = 0; i < 4; i++)
            {
                histo[i] = new int[256];
            }

            // TODO: Low Effort
            for (int tileY = 0; tileY < tilesPerCol; ++tileY)
            {
                for (int tileX = 0; tileX < tilesPerRow; ++tileX)
                {
                    int pred = GetBestPredictorForTile(
                        width,
                        height,
                        tileX,
                        tileY,
                        bits,
                        histo,
                        argbScratch,
                        argb,
                        maxQuantization,
                        exact,
                        usedSubtractGreen,
                        image);

                    image[(tileY * tilesPerRow) + tileX] = (uint)(WebPConstants.ArgbBlack | (pred << 8));
                }
            }

            CopyImageWithPrediction(
                width,
                height,
                bits,
                image,
                argbScratch,
                argb,
                maxQuantization,
                exact,
                usedSubtractGreen);
        }

        public static void ColorSpaceTransform(int width, int height, int bits, int quality, Span<uint> argb, Span<uint> image)
        {
            int maxTileSize = 1 << bits;
            int tileXSize = LosslessUtils.SubSampleSize(width, bits);
            int tileYSize = LosslessUtils.SubSampleSize(height, bits);
            int[] accumulatedRedHisto = new int[256];
            int[] accumulatedBlueHisto = new int[256];
            var prevX = default(Vp8LMultipliers);
            var prevY = default(Vp8LMultipliers);
            for (int tileY = 0; tileY < tileYSize; tileY++)
            {
                for (int tileX = 0; tileX < tileXSize; tileX++)
                {
                    int tileXOffset = tileX * maxTileSize;
                    int tileYOffset = tileY * maxTileSize;
                    int allXMax = GetMin(tileXOffset + maxTileSize, width);
                    int allYMax = GetMin(tileYOffset + maxTileSize, height);
                    int offset = (tileY * tileXSize) + tileX;
                    if (tileY != 0)
                    {
                        LosslessUtils.ColorCodeToMultipliers(image[offset - tileXSize], ref prevY);
                    }

                    prevX = GetBestColorTransformForTile(
                        tileX,
                        tileY,
                        bits,
                        prevX,
                        prevY,
                        quality,
                        width,
                        height,
                        accumulatedRedHisto,
                        accumulatedBlueHisto,
                        argb);

                    image[offset] = MultipliersToColorCode(prevX);
                    CopyTileWithColorTransform(width, height, tileXOffset, tileYOffset, maxTileSize, prevX, argb);

                    // Gather accumulated histogram data.
                    for (int y = tileYOffset; y < allYMax; y++)
                    {
                        int ix = (y * width) + tileXOffset;
                        int ixEnd = ix + allXMax - tileXOffset;

                        for (; ix < ixEnd; ix++)
                        {
                            uint pix = argb[ix];
                            if (ix >= 2 && pix == argb[ix - 2] && pix == argb[ix - 1])
                            {
                                continue;  // Repeated pixels are handled by backward references.
                            }

                            if (ix >= width + 2 && argb[ix - 2] == argb[ix - width - 2] && argb[ix - 1] == argb[ix - width - 1] && pix == argb[ix - width])
                            {
                                continue;  // Repeated pixels are handled by backward references.
                            }

                            accumulatedRedHisto[(pix >> 16) & 0xff]++;
                            accumulatedBlueHisto[(pix >> 0) & 0xff]++;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns best predictor and updates the accumulated histogram.
        /// If maxQuantization > 1, assumes that near lossless processing will be
        /// applied, quantizing residuals to multiples of quantization levels up to
        /// maxQuantization (the actual quantization level depends on smoothness near
        /// the given pixel).
        /// </summary>
        /// <returns>Best predictor.</returns>
        private static int GetBestPredictorForTile(
            int width,
            int height,
            int tileX,
            int tileY,
            int bits,
            int[][] accumulated,
            Span<uint> argbScratch,
            Span<uint> argb,
            int maxQuantization,
            bool exact,
            bool usedSubtractGreen,
            Span<uint> modes)
        {
            const int numPredModes = 14;
            int startX = tileX << bits;
            int startY = tileY << bits;
            int tileSize = 1 << bits;
            int maxY = GetMin(tileSize, height - startY);
            int maxX = GetMin(tileSize, width - startX);

            // Whether there exist columns just outside the tile.
            int haveLeft = (startX > 0) ? 1 : 0;

            // Position and size of the strip covering the tile and adjacent columns if they exist.
            int contextStartX = startX - haveLeft;
            int contextWidth = maxX + haveLeft + (maxX < width ? 1 : 0) - startX;
            int tilesPerRow = LosslessUtils.SubSampleSize(width, bits);

            // Prediction modes of the left and above neighbor tiles.
            int leftMode = (int)((tileX > 0) ? (modes[(tileY * tilesPerRow) + tileX - 1] >> 8) & 0xff : 0xff);
            int aboveMode = (int)((tileY > 0) ? (modes[((tileY - 1) * tilesPerRow) + tileX] >> 8) & 0xff : 0xff);

            // The width of upper_row and current_row is one pixel larger than image width
            // to allow the top right pixel to point to the leftmost pixel of the next row
            // when at the right edge.
            Span<uint> upperRow = argbScratch;
            Span<uint> currentRow = upperRow.Slice(width + 1);
            Span<byte> maxDiffs = MemoryMarshal.Cast<uint, byte>(currentRow.Slice(width + 1));
            float bestDiff = MaxDiffCost;
            int bestMode = 0;
            uint[] residuals = new uint[1 << WebPConstants.MaxTransformBits];
            int[][] histoArgb = new int[4][];
            int[][] bestHisto = new int[4][];
            for (int i = 0; i < 4; i++)
            {
                histoArgb[i] = new int[256];
                bestHisto[i] = new int[256];
            }

            for (int mode = 0; mode < numPredModes; mode++)
            {
                float curDiff;
                for (int i = 0; i < 4; i++)
                {
                    histoArgb[i].AsSpan().Fill(0);
                }

                if (startY > 0)
                {
                    // Read the row above the tile which will become the first upper_row.
                    // Include a pixel to the left if it exists; include a pixel to the right
                    // in all cases (wrapping to the leftmost pixel of the next row if it does
                    // not exist).
                    Span<uint> src = argb.Slice(((startY - 1) * width) + contextStartX, maxX + haveLeft + 1);
                    Span<uint> dst = currentRow.Slice(contextStartX);
                    src.CopyTo(dst);
                }

                for (int relativeY = 0; relativeY < maxY; relativeY++)
                {
                    int y = startY + relativeY;
                    Span<uint> tmp = upperRow;
                    upperRow = currentRow;
                    currentRow = tmp;

                    // Read currentRow. Include a pixel to the left if it exists; include a
                    // pixel to the right in all cases except at the bottom right corner of
                    // the image (wrapping to the leftmost pixel of the next row if it does
                    // not exist in the currentRow).
                    Span<uint> src = argb.Slice((y * width) + contextStartX, maxX + haveLeft + ((y + 1) < height ? 1 : 0));
                    Span<uint> dst = currentRow.Slice(contextStartX);
                    src.CopyTo(dst);

                    // TODO: Source wraps this in conditional
                    // WEBP_NEAR_LOSSLESS == 1
                    if (maxQuantization > 1 && y >= 1 && y + 1 < height)
                    {
                        MaxDiffsForRow(contextWidth, width, argb.Slice((y * width) + contextStartX), maxDiffs.Slice(contextStartX), usedSubtractGreen);
                    }

                    GetResidual(width, height, upperRow, currentRow, maxDiffs, mode, startX, startX + maxX, y, maxQuantization, exact, usedSubtractGreen, residuals);
                    for (int relativeX = 0; relativeX < maxX; ++relativeX)
                    {
                        UpdateHisto(histoArgb, residuals[relativeX]);
                    }
                }

                curDiff = PredictionCostSpatialHistogram(accumulated, histoArgb);

                // Favor keeping the areas locally similar.
                if (mode == leftMode)
                {
                    curDiff -= SpatialPredictorBias;
                }

                if (mode == aboveMode)
                {
                    curDiff -= SpatialPredictorBias;
                }

                if (curDiff < bestDiff)
                {
                    // TODO: Consider swapping references
                    for (int i = 0; i < 4; i++)
                    {
                        histoArgb[i].AsSpan().CopyTo(bestHisto[i]);
                    }

                    bestDiff = curDiff;
                    bestMode = mode;
                }
            }

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 256; j++)
                {
                    accumulated[i][j] += bestHisto[i][j];
                }
            }

            return bestMode;
        }

        /// <summary>
        /// Stores the difference between the pixel and its prediction in "output".
        /// In case of a lossy encoding, updates the source image to avoid propagating
        /// the deviation further to pixels which depend on the current pixel for their
        /// predictions.
        /// </summary>
        private static void GetResidual(
            int width,
            int height,
            Span<uint> upperRowSpan,
            Span<uint> currentRowSpan,
            Span<byte> maxDiffs,
            int mode,
            int xStart,
            int xEnd,
            int y,
            int maxQuantization,
            bool exact,
            bool usedSubtractGreen,
            Span<uint> output)
        {
            if (exact)
            {
                PredictBatch(mode, xStart, y, xEnd - xStart, currentRowSpan, upperRowSpan, output);
            }
            else
            {
                fixed (uint* currentRow = currentRowSpan)
                fixed (uint* upperRow = upperRowSpan)
                {
                    for (int x = xStart; x < xEnd; ++x)
                    {
                        uint predict = 0;
                        uint residual;
                        if (y == 0)
                        {
                            predict = (x == 0) ? WebPConstants.ArgbBlack : currentRow[x - 1];  // Left.
                        }
                        else if (x == 0)
                        {
                            predict = upperRow[x];  // Top.
                        }
                        else
                        {
                            switch (mode)
                            {
                                case 0:
                                    predict = WebPConstants.ArgbBlack;
                                    break;
                                case 1:
                                    predict = currentRow[x - 1];
                                    break;
                                case 2:
                                    predict = LosslessUtils.Predictor2(currentRow[x - 1], upperRow + x);
                                    break;
                                case 3:
                                    predict = LosslessUtils.Predictor3(currentRow[x - 1], upperRow + x);
                                    break;
                                case 4:
                                    predict = LosslessUtils.Predictor4(currentRow[x - 1], upperRow + x);
                                    break;
                                case 5:
                                    predict = LosslessUtils.Predictor5(currentRow[x - 1], upperRow + x);
                                    break;
                                case 6:
                                    predict = LosslessUtils.Predictor6(currentRow[x - 1], upperRow + x);
                                    break;
                                case 7:
                                    predict = LosslessUtils.Predictor7(currentRow[x - 1], upperRow + x);
                                    break;
                                case 8:
                                    predict = LosslessUtils.Predictor8(currentRow[x - 1], upperRow + x);
                                    break;
                                case 9:
                                    predict = LosslessUtils.Predictor9(currentRow[x - 1], upperRow + x);
                                    break;
                                case 10:
                                    predict = LosslessUtils.Predictor10(currentRow[x - 1], upperRow + x);
                                    break;
                                case 11:
                                    predict = LosslessUtils.Predictor11(currentRow[x - 1], upperRow + x);
                                    break;
                                case 12:
                                    predict = LosslessUtils.Predictor12(currentRow[x - 1], upperRow + x);
                                    break;
                                case 13:
                                    predict = LosslessUtils.Predictor13(currentRow[x - 1], upperRow + x);
                                    break;
                            }
                        }

                        if (maxQuantization == 1 || mode == 0 || y == 0 || y == height - 1 || x == 0 || x == width - 1)
                        {
                            residual = LosslessUtils.SubPixels(currentRow[x], predict);
                        }
                        else
                        {
                            residual = NearLossless(currentRow[x], predict, maxQuantization, maxDiffs[x], usedSubtractGreen);

                            // Update the source image.
                            currentRow[x] = LosslessUtils.AddPixels(predict, residual);

                            // x is never 0 here so we do not need to update upperRow like below.
                        }

                        if ((currentRow[x] & MaskAlpha) == 0)
                        {
                            // If alpha is 0, cleanup RGB. We can choose the RGB values of the
                            // residual for best compression. The prediction of alpha itself can be
                            // non-zero and must be kept though. We choose RGB of the residual to be
                            // 0.
                            residual &= MaskAlpha;

                            // Update the source image.
                            currentRow[x] = predict & ~MaskAlpha;

                            // The prediction for the rightmost pixel in a row uses the leftmost
                            // pixel
                            // in that row as its top-right context pixel. Hence if we change the
                            // leftmost pixel of current_row, the corresponding change must be
                            // applied
                            // to upperRow as well where top-right context is being read from.
                            if (x == 0 && y != 0)
                            {
                                upperRow[width] = currentRow[0];
                            }
                        }

                        output[x - xStart] = residual;
                    }
                }
            }
        }

        /// <summary>
        /// Quantize every component of the difference between the actual pixel value and
        /// its prediction to a multiple of a quantization (a power of 2, not larger than
        /// maxQuantization which is a power of 2, smaller than maxDiff). Take care if
        /// value and predict have undergone subtract green, which means that red and
        /// blue are represented as offsets from green.
        /// </summary>
        private static uint NearLossless(uint value, uint predict, int maxQuantization, int maxDiff, bool usedSubtractGreen)
        {
            int quantization;
            byte newGreen = 0;
            byte greenDiff = 0;
            byte a, r, g, b;
            if (maxDiff <= 2)
            {
                return LosslessUtils.SubPixels(value, predict);
            }

            quantization = maxQuantization;
            while (quantization >= maxDiff)
            {
                quantization >>= 1;
            }

            if ((value >> 24) == 0 || (value >> 24) == 0xff)
            {
                // Preserve transparency of fully transparent or fully opaque pixels.
                a = NearLosslessDiff((byte)((value >> 24) & 0xff), (byte)((predict >> 24) & 0xff));
            }
            else
            {
                a = NearLosslessComponent((byte)(value >> 24), (byte)(predict >> 24), 0xff, quantization);
            }

            g = NearLosslessComponent((byte)((value >> 8) & 0xff), (byte)((predict >> 8) & 0xff), 0xff, quantization);

            if (usedSubtractGreen)
            {
                // The green offset will be added to red and blue components during decoding
                // to obtain the actual red and blue values.
                newGreen = (byte)(((predict >> 8) + g) & 0xff);

                // The amount by which green has been adjusted during quantization. It is
                // subtracted from red and blue for compensation, to avoid accumulating two
                // quantization errors in them.
                greenDiff = NearLosslessDiff(newGreen, (byte)((value >> 8) & 0xff));
            }

            r = NearLosslessComponent(NearLosslessDiff((byte)((value >> 16) & 0xff), greenDiff), (byte)((predict >> 16) & 0xff), (byte)(0xff - newGreen), quantization);
            b = NearLosslessComponent(NearLosslessDiff((byte)(value & 0xff), greenDiff), (byte)(predict & 0xff), (byte)(0xff - newGreen), quantization);

            return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
        }

        /// <summary>
        /// Quantize the difference between the actual component value and its prediction
        /// to a multiple of quantization, working modulo 256, taking care not to cross
        /// a boundary (inclusive upper limit).
        /// </summary>
        private static byte NearLosslessComponent(byte value, byte predict, byte boundary, int quantization)
        {
            int residual = (value - predict) & 0xff;
            int boundaryResidual = (boundary - predict) & 0xff;
            int lower = residual & ~(quantization - 1);
            int upper = lower + quantization;

            // Resolve ties towards a value closer to the prediction (i.e. towards lower
            // if value comes after prediction and towards upper otherwise).
            int bias = ((boundary - value) & 0xff) < boundaryResidual ? 1 : 0;

            if (residual - lower < upper - residual + bias)
            {
                // lower is closer to residual than upper.
                if (residual > boundaryResidual && lower <= boundaryResidual)
                {
                    // Halve quantization step to avoid crossing boundary. This midpoint is
                    // on the same side of boundary as residual because midpoint >= residual
                    // (since lower is closer than upper) and residual is above the boundary.
                    return (byte)(lower + (quantization >> 1));
                }

                return (byte)lower;
            }
            else
            {
                // upper is closer to residual than lower.
                if (residual <= boundaryResidual && upper > boundaryResidual)
                {
                    // Halve quantization step to avoid crossing boundary. This midpoint is
                    // on the same side of boundary as residual because midpoint <= residual
                    // (since upper is closer than lower) and residual is below the boundary.
                    return (byte)(lower + (quantization >> 1));
                }

                return (byte)(upper & 0xff);
            }
        }

        /// <summary>
        /// Converts pixels of the image to residuals with respect to predictions.
        /// If max_quantization > 1, applies near lossless processing, quantizing
        /// residuals to multiples of quantization levels up to max_quantization
        /// (the actual quantization level depends on smoothness near the given pixel).
        /// </summary>
        private static void CopyImageWithPrediction(int width, int height, int bits, Span<uint> modes, Span<uint> argbScratch, Span<uint> argb, int maxQuantization, bool exact, bool usedSubtractGreen)
        {
            int tilesPerRow = LosslessUtils.SubSampleSize(width, bits);

            // The width of upper_row and current_row is one pixel larger than image width
            // to allow the top right pixel to point to the leftmost pixel of the next row
            // when at the right edge.
            Span<uint> upperRow = argbScratch;
            Span<uint> currentRow = upperRow.Slice(width + 1);
            Span<byte> currentMaxDiffs = MemoryMarshal.Cast<uint, byte>(currentRow.Slice(width + 1));

            // TODO: This should be wrapped in a condition?
            Span<byte> lowerMaxDiffs = currentMaxDiffs.Slice(width);
            for (int y = 0; y < height; ++y)
            {
                Span<uint> tmp32 = upperRow;
                upperRow = currentRow;
                currentRow = tmp32;
                Span<uint> src = argb.Slice(y * width, width + ((y + 1) < height ? 1 : 0));
                src.CopyTo(currentRow);

                // TODO: Near lossless conditional?
                if (maxQuantization > 1)
                {
                    // Compute max_diffs for the lower row now, because that needs the
                    // contents of argb for the current row, which we will overwrite with
                    // residuals before proceeding with the next row.
                    Span<byte> tmp8 = currentMaxDiffs;
                    currentMaxDiffs = lowerMaxDiffs;
                    lowerMaxDiffs = tmp8;
                    if (y + 2 < height)
                    {
                        MaxDiffsForRow(width, width, argb.Slice((y + 1) * width), lowerMaxDiffs, usedSubtractGreen);
                    }
                }

                for (int x = 0; x < width;)
                {
                    int mode = (int)((modes[((y >> bits) * tilesPerRow) + (x >> bits)] >> 8) & 0xff);
                    int xEnd = x + (1 << bits);
                    if (xEnd > width)
                    {
                        xEnd = width;
                    }

                    GetResidual(
                        width,
                        height,
                        upperRow,
                        currentRow,
                        currentMaxDiffs,
                        mode,
                        x,
                        xEnd,
                        y,
                        maxQuantization,
                        exact,
                        usedSubtractGreen,
                        argb.Slice((y * width) + x));

                    x = xEnd;
                }
            }
        }

        private static void PredictBatch(
            int mode,
            int xStart,
            int y,
            int numPixels,
            Span<uint> currentSpan,
            Span<uint> upperSpan,
            Span<uint> outputSpan)
        {
            fixed (uint* current = currentSpan)
            fixed (uint* upper = upperSpan)
            fixed (uint* outputFixed = outputSpan)
            {
                uint* output = outputFixed;
                if (xStart == 0)
                {
                    if (y == 0)
                    {
                        // ARGB_BLACK.
                        LosslessUtils.PredictorSub0(current, 1, output);
                    }
                    else
                    {
                        // Top one.
                        LosslessUtils.PredictorSub2(current, upper, 1, output);
                    }

                    ++xStart;
                    ++output;
                    --numPixels;
                }

                if (y == 0)
                {
                    // Left one.
                    LosslessUtils.PredictorSub1(current + xStart, numPixels, output);
                }
                else
                {
                    switch (mode)
                    {
                        case 0:
                            LosslessUtils.PredictorSub0(current, numPixels, output);
                            break;
                        case 1:
                            LosslessUtils.PredictorSub1(current + xStart, numPixels, output);
                            break;
                        case 2:
                            LosslessUtils.PredictorSub2(current + xStart, upper + xStart, numPixels, output);
                            break;
                        case 3:
                            LosslessUtils.PredictorSub3(current + xStart, upper + xStart, numPixels, output);
                            break;
                        case 4:
                            LosslessUtils.PredictorSub4(current + xStart, upper + xStart, numPixels, output);
                            break;
                        case 5:
                            LosslessUtils.PredictorSub5(current + xStart, upper + xStart, numPixels, output);
                            break;
                        case 6:
                            LosslessUtils.PredictorSub6(current + xStart, upper + xStart, numPixels, output);
                            break;
                        case 7:
                            LosslessUtils.PredictorSub7(current + xStart, upper + xStart, numPixels, output);
                            break;
                        case 8:
                            LosslessUtils.PredictorSub8(current + xStart, upper + xStart, numPixels, output);
                            break;
                        case 9:
                            LosslessUtils.PredictorSub9(current + xStart, upper + xStart, numPixels, output);
                            break;
                        case 10:
                            LosslessUtils.PredictorSub10(current + xStart, upper + xStart, numPixels, output);
                            break;
                        case 11:
                            LosslessUtils.PredictorSub11(current + xStart, upper + xStart, numPixels, output);
                            break;
                        case 12:
                            LosslessUtils.PredictorSub12(current + xStart, upper + xStart, numPixels, output);
                            break;
                        case 13:
                            LosslessUtils.PredictorSub13(current + xStart, upper + xStart, numPixels, output);
                            break;
                    }
                }
            }
        }

        private static void MaxDiffsForRow(int width, int stride, Span<uint> argb, Span<byte> maxDiffs, bool usedSubtractGreen)
        {
            if (width <= 2)
            {
                return;
            }

            uint current = argb[0];
            uint right = argb[1];
            if (usedSubtractGreen)
            {
                current = AddGreenToBlueAndRed(current);
                right = AddGreenToBlueAndRed(right);
            }

            for (int x = 1; x < width - 1; ++x)
            {
                uint up = argb[-stride + x];
                uint down = argb[stride + x];
                uint left = current;
                current = right;
                right = argb[x + 1];
                if (usedSubtractGreen)
                {
                    up = AddGreenToBlueAndRed(up);
                    down = AddGreenToBlueAndRed(down);
                    right = AddGreenToBlueAndRed(right);
                }

                maxDiffs[x] = (byte)MaxDiffAroundPixel(current, up, down, left, right);
            }
        }

        private static int MaxDiffBetweenPixels(uint p1, uint p2)
        {
            int diffA = Math.Abs((int)(p1 >> 24) - (int)(p2 >> 24));
            int diffR = Math.Abs((int)((p1 >> 16) & 0xff) - (int)((p2 >> 16) & 0xff));
            int diffG = Math.Abs((int)((p1 >> 8) & 0xff) - (int)((p2 >> 8) & 0xff));
            int diffB = Math.Abs((int)(p1 & 0xff) - (int)(p2 & 0xff));
            return GetMax(GetMax(diffA, diffR), GetMax(diffG, diffB));
        }

        private static int MaxDiffAroundPixel(uint current, uint up, uint down, uint left, uint right)
        {
            int diffUp = MaxDiffBetweenPixels(current, up);
            int diffDown = MaxDiffBetweenPixels(current, down);
            int diffLeft = MaxDiffBetweenPixels(current, left);
            int diffRight = MaxDiffBetweenPixels(current, right);
            return GetMax(GetMax(diffUp, diffDown), GetMax(diffLeft, diffRight));
        }

        private static void UpdateHisto(int[][] histoArgb, uint argb)
        {
            ++histoArgb[0][argb >> 24];
            ++histoArgb[1][(argb >> 16) & 0xff];
            ++histoArgb[2][(argb >> 8) & 0xff];
            ++histoArgb[3][argb & 0xff];
        }

        private static uint AddGreenToBlueAndRed(uint argb)
        {
            uint green = (argb >> 8) & 0xff;
            uint redBlue = argb & 0x00ff00ffu;
            redBlue += (green << 16) | green;
            redBlue &= 0x00ff00ffu;
            return (argb & 0xff00ff00u) | redBlue;
        }

        private static void CopyTileWithColorTransform(int xSize, int ySize, int tileX, int tileY, int maxTileSize, Vp8LMultipliers colorTransform, Span<uint> argb)
        {
            int xScan = GetMin(maxTileSize, xSize - tileX);
            int yScan = GetMin(maxTileSize, ySize - tileY);
            argb = argb.Slice((tileY * xSize) + tileX);
            while (yScan-- > 0)
            {
                LosslessUtils.TransformColor(colorTransform, argb, xScan);

                if (argb.Length > xSize)
                {
                    argb = argb.Slice(xSize);
                }
            }
        }

        private static Vp8LMultipliers GetBestColorTransformForTile(int tile_x, int tile_y, int bits, Vp8LMultipliers prevX, Vp8LMultipliers prevY, int quality, int xSize, int ySize, int[] accumulatedRedHisto, int[] accumulatedBlueHisto, Span<uint> argb)
        {
            int maxTileSize = 1 << bits;
            int tileYOffset = tile_y * maxTileSize;
            int tileXOffset = tile_x * maxTileSize;
            int allXMax = GetMin(tileXOffset + maxTileSize, xSize);
            int allYMax = GetMin(tileYOffset + maxTileSize, ySize);
            int tileWidth = allXMax - tileXOffset;
            int tileHeight = allYMax - tileYOffset;
            Span<uint> tileArgb = argb.Slice((tileYOffset * xSize) + tileXOffset);

            var bestTx = default(Vp8LMultipliers);

            GetBestGreenToRed(tileArgb, xSize, tileWidth, tileHeight, prevX, prevY, quality, accumulatedRedHisto, ref bestTx);

            GetBestGreenRedToBlue(tileArgb, xSize, tileWidth, tileHeight, prevX, prevY, quality, accumulatedBlueHisto, ref bestTx);

            return bestTx;
        }

        private static void GetBestGreenToRed(Span<uint> argb, int stride, int tileWidth, int tileHeight, Vp8LMultipliers prevX, Vp8LMultipliers prevY, int quality, int[] accumulatedRedHisto, ref Vp8LMultipliers bestTx)
        {
            int maxIters = 4 + ((7 * quality) >> 8);  // in range [4..6]
            int greenToRedBest = 0;
            float bestDiff = GetPredictionCostCrossColorRed(argb, stride, tileWidth, tileHeight, prevX, prevY, greenToRedBest, accumulatedRedHisto);
            for (int iter = 0; iter < maxIters; iter++)
            {
                // ColorTransformDelta is a 3.5 bit fixed point, so 32 is equal to
                // one in color computation. Having initial delta here as 1 is sufficient
                // to explore the range of (-2, 2).
                int delta = 32 >> iter;

                // Try a negative and a positive delta from the best known value.
                for (int offset = -delta; offset <= delta; offset += 2 * delta)
                {
                    int greenToRedCur = offset + greenToRedBest;
                    float curDiff = GetPredictionCostCrossColorRed(argb, stride, tileWidth, tileHeight, prevX, prevY, greenToRedCur, accumulatedRedHisto);
                    if (curDiff < bestDiff)
                    {
                        bestDiff = curDiff;
                        greenToRedBest = greenToRedCur;
                    }
                }
            }

            bestTx.GreenToRed = (byte)(greenToRedBest & 0xff);
        }

        private static void GetBestGreenRedToBlue(Span<uint> argb, int stride, int tileWidth, int tileHeight, Vp8LMultipliers prevX, Vp8LMultipliers prevY, int quality, int[] accumulatedBlueHisto, ref Vp8LMultipliers bestTx)
        {
            int iters = (quality < 25) ? 1 : (quality > 50) ? GreenRedToBlueMaxIters : 4;
            int greenToBlueBest = 0;
            int redToBlueBest = 0;
            sbyte[][] offset = { new sbyte[] { 0, -1 }, new sbyte[] { 0, 1 }, new sbyte[] { -1, 0 }, new sbyte[] { 1, 0 }, new sbyte[] { -1, -1 }, new sbyte[] { -1, 1 }, new sbyte[] { 1, -1 }, new sbyte[] { 1, 1 } };
            sbyte[] deltaLut = { 16, 16, 8, 4, 2, 2, 2 };

            // Initial value at origin:
            float bestDiff = GetPredictionCostCrossColorBlue(argb, stride, tileWidth, tileHeight, prevX, prevY, greenToBlueBest, redToBlueBest, accumulatedBlueHisto);
            for (int iter = 0; iter < iters; iter++)
            {
                int delta = deltaLut[iter];
                for (int axis = 0; axis < GreenRedToBlueNumAxis; axis++)
                {
                    int greenToBlueCur = (offset[axis][0] * delta) + greenToBlueBest;
                    int redToBlueCur = (offset[axis][1] * delta) + redToBlueBest;
                    float curDiff = GetPredictionCostCrossColorBlue(argb, stride, tileWidth, tileHeight, prevX, prevY, greenToBlueCur, redToBlueCur, accumulatedBlueHisto);
                    if (curDiff < bestDiff)
                    {
                        bestDiff = curDiff;
                        greenToBlueBest = greenToBlueCur;
                        redToBlueBest = redToBlueCur;
                    }

                    if (quality < 25 && iter == 4)
                    {
                        // Only axis aligned diffs for lower quality.
                        break;  // next iter.
                    }
                }

                if (delta == 2 && greenToBlueBest == 0 && redToBlueBest == 0)
                {
                    // Further iterations would not help.
                    break;  // out of iter-loop.
                }
            }

            bestTx.GreenToBlue = (byte)(greenToBlueBest & 0xff);
            bestTx.RedToBlue = (byte)(redToBlueBest & 0xff);
        }

        private static float GetPredictionCostCrossColorRed(Span<uint> argb, int stride, int tileWidth, int tileHeight, Vp8LMultipliers prevX, Vp8LMultipliers prevY, int greenToRed, int[] accumulatedRedHisto)
        {
            int[] histo = new int[256];

            CollectColorRedTransforms(argb, stride, tileWidth, tileHeight, greenToRed, histo);
            float curDiff = PredictionCostCrossColor(accumulatedRedHisto, histo);

            if ((byte)greenToRed == prevX.GreenToRed)
            {
                curDiff -= 3;  // Favor keeping the areas locally similar.
            }

            if ((byte)greenToRed == prevY.GreenToRed)
            {
                curDiff -= 3;  // Favor keeping the areas locally similar.
            }

            if (greenToRed == 0)
            {
                curDiff -= 3;
            }

            return curDiff;
        }

        private static float GetPredictionCostCrossColorBlue(Span<uint> argb, int stride, int tileWidth, int tileHeight, Vp8LMultipliers prevX, Vp8LMultipliers prevY, int greenToBlue, int redToBlue, int[] accumulatedBlueHisto)
        {
            int[] histo = new int[256];

            CollectColorBlueTransforms(argb, stride, tileWidth, tileHeight, greenToBlue, redToBlue, histo);
            float curDiff = PredictionCostCrossColor(accumulatedBlueHisto, histo);
            if ((byte)greenToBlue == prevX.GreenToBlue)
            {
                curDiff -= 3;  // Favor keeping the areas locally similar.
            }

            if ((byte)greenToBlue == prevY.GreenToBlue)
            {
                curDiff -= 3;  // Favor keeping the areas locally similar.
            }

            if ((byte)redToBlue == prevX.RedToBlue)
            {
                curDiff -= 3;  // Favor keeping the areas locally similar.
            }

            if ((byte)redToBlue == prevY.RedToBlue)
            {
                curDiff -= 3;  // Favor keeping the areas locally similar.
            }

            if (greenToBlue == 0)
            {
                curDiff -= 3;
            }

            if (redToBlue == 0)
            {
                curDiff -= 3;
            }

            return curDiff;
        }

        private static void CollectColorRedTransforms(Span<uint> argb, int stride, int tileWidth, int tileHeight, int greenToRed, int[] histo)
        {
            int startIdx = 0;
            while (tileHeight-- > 0)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    int idx = LosslessUtils.TransformColorRed((sbyte)greenToRed, argb[startIdx + x]);
                    ++histo[idx];
                }

                startIdx += stride;
            }
        }

        private static void CollectColorBlueTransforms(Span<uint> argb, int stride, int tileWidth, int tileHeight, int greenToBlue, int redToBlue, int[] histo)
        {
            int pos = 0;
            while (tileHeight-- > 0)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    int idx = LosslessUtils.TransformColorBlue((sbyte)greenToBlue, (sbyte)redToBlue, argb[pos + x]);
                    ++histo[idx];
                }

                pos += stride;
            }
        }

        private static float PredictionCostSpatialHistogram(int[][] accumulated, int[][] tile)
        {
            double retVal = 0.0d;
            for (int i = 0; i < 4; ++i)
            {
                double kExpValue = 0.94;
                retVal += PredictionCostSpatial(tile[i], 1, kExpValue);
                retVal += LosslessUtils.CombinedShannonEntropy(tile[i], accumulated[i]);
            }

            return (float)retVal;
        }

        private static float PredictionCostCrossColor(int[] accumulated, int[] counts)
        {
            // Favor low entropy, locally and globally.
            // Favor small absolute values for PredictionCostSpatial.
            const double expValue = 2.4d;
            return LosslessUtils.CombinedShannonEntropy(counts, accumulated) + PredictionCostSpatial(counts, 3, expValue);
        }

        private static float PredictionCostSpatial(int[] counts, int weight0, double expVal)
        {
            int significantSymbols = 256 >> 4;
            double expDecayFactor = 0.6;
            double bits = weight0 * counts[0];
            for (int i = 1; i < significantSymbols; i++)
            {
                bits += expVal * (counts[i] + counts[256 - i]);
                expVal *= expDecayFactor;
            }

            return (float)(-0.1 * bits);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static byte NearLosslessDiff(byte a, byte b)
        {
            return (byte)((a - b) & 0xff);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static uint MultipliersToColorCode(Vp8LMultipliers m)
        {
            return 0xff000000u | ((uint)m.RedToBlue << 16) | ((uint)m.GreenToBlue << 8) | m.GreenToRed;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetMin(int a, int b)
        {
            return (a > b) ? b : a;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetMax(int a, int b)
        {
            return (a < b) ? b : a;
        }
    }
}
