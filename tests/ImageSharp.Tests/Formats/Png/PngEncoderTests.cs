// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

// ReSharper disable InconsistentNaming
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Png;

[Trait("Format", "Png")]
public partial class PngEncoderTests
{
    private static PngEncoder PngEncoder => new();

    public static readonly TheoryData<string, PngBitDepth> PngBitDepthFiles =
    new()
    {
        { TestImages.Png.Rgb48Bpp, PngBitDepth.Bit16 },
        { TestImages.Png.Bpp1, PngBitDepth.Bit1 }
    };

    public static readonly TheoryData<string, PngBitDepth, PngColorType> PngTrnsFiles =
    new()
    {
        { TestImages.Png.Gray1BitTrans, PngBitDepth.Bit1, PngColorType.Grayscale },
        { TestImages.Png.Gray2BitTrans, PngBitDepth.Bit2, PngColorType.Grayscale },
        { TestImages.Png.Gray4BitTrans, PngBitDepth.Bit4, PngColorType.Grayscale },
        { TestImages.Png.L8BitTrans, PngBitDepth.Bit8, PngColorType.Grayscale },
        { TestImages.Png.GrayTrns16BitInterlaced, PngBitDepth.Bit16, PngColorType.Grayscale },
        { TestImages.Png.Rgb24BppTrans, PngBitDepth.Bit8, PngColorType.Rgb },
        { TestImages.Png.Rgb48BppTrans, PngBitDepth.Bit16, PngColorType.Rgb }
    };

    /// <summary>
    /// All types except Palette
    /// </summary>
    public static readonly TheoryData<PngColorType> PngColorTypes = new()
    {
        PngColorType.RgbWithAlpha,
        PngColorType.Rgb,
        PngColorType.Grayscale,
        PngColorType.GrayscaleWithAlpha,
    };

    public static readonly TheoryData<PngFilterMethod> PngFilterMethods = new()
    {
        PngFilterMethod.None,
        PngFilterMethod.Sub,
        PngFilterMethod.Up,
        PngFilterMethod.Average,
        PngFilterMethod.Paeth,
        PngFilterMethod.Adaptive
    };

    /// <summary>
    /// All types except Palette
    /// </summary>
    public static readonly TheoryData<PngCompressionLevel> CompressionLevels
    = new()
    {
        PngCompressionLevel.Level0,
        PngCompressionLevel.Level1,
        PngCompressionLevel.Level2,
        PngCompressionLevel.Level3,
        PngCompressionLevel.Level4,
        PngCompressionLevel.Level5,
        PngCompressionLevel.Level6,
        PngCompressionLevel.Level7,
        PngCompressionLevel.Level8,
        PngCompressionLevel.Level9,
    };

    public static readonly TheoryData<int> PaletteSizes = new()
    {
        30, 55, 100, 201, 255
    };

    public static readonly TheoryData<int> PaletteLargeOnly = new()
    {
        80, 100, 120, 230
    };

    public static readonly PngInterlaceMode[] InterlaceMode =
    [
        PngInterlaceMode.None,
        PngInterlaceMode.Adam7
    ];

    public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
    new()
    {
        { TestImages.Png.Splash, 11810, 11810, PixelResolutionUnit.PixelsPerMeter },
        { TestImages.Png.Ratio1x4, 1, 4, PixelResolutionUnit.AspectRatio },
        { TestImages.Png.Ratio4x1, 4, 1, PixelResolutionUnit.AspectRatio }
    };

    [Fact]
    public void PngEncoderDefaultInstanceHasNullQuantizer() => Assert.Null(PngEncoder.Quantizer);

    [Theory]
    [WithFile(TestImages.Png.Palette8Bpp, nameof(PngColorTypes), PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(PngColorTypes), 48, 24, PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(PngColorTypes), 47, 8, PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(PngColorTypes), 49, 7, PixelTypes.Rgba32)]
    [WithSolidFilledImages(nameof(PngColorTypes), 1, 1, 255, 100, 50, 255, PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(PngColorTypes), 7, 5, PixelTypes.Rgba32)]
    public void WorksWithDifferentSizes<TPixel>(TestImageProvider<TPixel> provider, PngColorType pngColorType)
        where TPixel : unmanaged, IPixel<TPixel>
        => TestPngEncoderCore(
            provider,
            pngColorType,
            PngFilterMethod.Adaptive,
            PngBitDepth.Bit8,
            PngInterlaceMode.None,
            appendPngColorType: true);

    [Theory]
    [WithTestPatternImages(nameof(PngColorTypes), 24, 24, PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.Rgb24)]
    public void IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, PngColorType pngColorType)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        foreach (PngInterlaceMode interlaceMode in InterlaceMode)
        {
            TestPngEncoderCore(
                provider,
                pngColorType,
                PngFilterMethod.Adaptive,
                PngBitDepth.Bit8,
                interlaceMode,
                appendPngColorType: true,
                appendPixelType: true);
        }
    }

    [Theory]
    [WithTestPatternImages(nameof(PngFilterMethods), 24, 24, PixelTypes.Rgba32)]
    public void WorksWithAllFilterMethods<TPixel>(TestImageProvider<TPixel> provider, PngFilterMethod pngFilterMethod)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        foreach (PngInterlaceMode interlaceMode in InterlaceMode)
        {
            TestPngEncoderCore(
            provider,
            PngColorType.RgbWithAlpha,
            pngFilterMethod,
            PngBitDepth.Bit8,
            interlaceMode,
            appendPngFilterMethod: true);
        }
    }

    [Theory]
    [WithTestPatternImages(nameof(CompressionLevels), 24, 24, PixelTypes.Rgba32)]
    public void WorksWithAllCompressionLevels<TPixel>(TestImageProvider<TPixel> provider, PngCompressionLevel compressionLevel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        foreach (PngInterlaceMode interlaceMode in InterlaceMode)
        {
            TestPngEncoderCore(
            provider,
            PngColorType.RgbWithAlpha,
            PngFilterMethod.Adaptive,
            PngBitDepth.Bit8,
            interlaceMode,
            compressionLevel,
            appendCompressionLevel: true);
        }
    }

    [Theory]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba32, PngColorType.Rgb, PngBitDepth.Bit8)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba64, PngColorType.Rgb, PngBitDepth.Bit16)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba32, PngColorType.RgbWithAlpha, PngBitDepth.Bit8)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba64, PngColorType.RgbWithAlpha, PngBitDepth.Bit16)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba32, PngColorType.Palette, PngBitDepth.Bit1)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba32, PngColorType.Palette, PngBitDepth.Bit2)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba32, PngColorType.Palette, PngBitDepth.Bit4)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba32, PngColorType.Palette, PngBitDepth.Bit8)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgb24, PngColorType.Grayscale, PngBitDepth.Bit1)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgb24, PngColorType.Grayscale, PngBitDepth.Bit2)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgb24, PngColorType.Grayscale, PngBitDepth.Bit4)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgb24, PngColorType.Grayscale, PngBitDepth.Bit8)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgb48, PngColorType.Grayscale, PngBitDepth.Bit16)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba32, PngColorType.GrayscaleWithAlpha, PngBitDepth.Bit8)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba64, PngColorType.GrayscaleWithAlpha, PngBitDepth.Bit16)]
    public void WorksWithAllBitDepths<TPixel>(TestImageProvider<TPixel> provider, PngColorType pngColorType, PngBitDepth pngBitDepth)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // TODO: Investigate WuQuantizer to see if we can reduce memory pressure.
        if (TestEnvironment.RunsOnCI && !TestEnvironment.Is64BitProcess)
        {
            return;
        }

        foreach (object[] filterMethod in PngFilterMethods)
        {
            foreach (PngInterlaceMode interlaceMode in InterlaceMode)
            {
                TestPngEncoderCore(
                    provider,
                    pngColorType,
                    (PngFilterMethod)filterMethod[0],
                    pngBitDepth,
                    interlaceMode,
                    appendPngColorType: true,
                    appendPixelType: true,
                    appendPngBitDepth: true);
            }
        }
    }

    [Theory]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba32, PngColorType.Rgb, PngBitDepth.Bit8)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba64, PngColorType.Rgb, PngBitDepth.Bit16)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba32, PngColorType.RgbWithAlpha, PngBitDepth.Bit8)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba64, PngColorType.RgbWithAlpha, PngBitDepth.Bit16)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba32, PngColorType.Palette, PngBitDepth.Bit1)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba32, PngColorType.Palette, PngBitDepth.Bit2)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba32, PngColorType.Palette, PngBitDepth.Bit4)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba32, PngColorType.Palette, PngBitDepth.Bit8)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgb24, PngColorType.Grayscale, PngBitDepth.Bit1)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgb24, PngColorType.Grayscale, PngBitDepth.Bit2)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgb24, PngColorType.Grayscale, PngBitDepth.Bit4)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgb24, PngColorType.Grayscale, PngBitDepth.Bit8)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgb48, PngColorType.Grayscale, PngBitDepth.Bit16)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba32, PngColorType.GrayscaleWithAlpha, PngBitDepth.Bit8)]
    [WithTestPatternImages(24, 24, PixelTypes.Rgba64, PngColorType.GrayscaleWithAlpha, PngBitDepth.Bit16)]
    public void WorksWithAllBitDepthsAndExcludeAllFilter<TPixel>(TestImageProvider<TPixel> provider, PngColorType pngColorType, PngBitDepth pngBitDepth)
      where TPixel : unmanaged, IPixel<TPixel>
    {
        foreach (object[] filterMethod in PngFilterMethods)
        {
            foreach (PngInterlaceMode interlaceMode in InterlaceMode)
            {
                TestPngEncoderCore(
                provider,
                pngColorType,
                (PngFilterMethod)filterMethod[0],
                pngBitDepth,
                interlaceMode,
                appendPngColorType: true,
                appendPixelType: true,
                appendPngBitDepth: true,
                optimizeMethod: PngChunkFilter.ExcludeAll);
            }
        }
    }

    [Theory]
    [WithFile(TestImages.Png.Palette8Bpp, nameof(PaletteLargeOnly), PixelTypes.Rgba32)]
    public void PaletteColorType_WuQuantizer<TPixel>(TestImageProvider<TPixel> provider, int paletteSize)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // TODO: Investigate WuQuantizer to see if we can reduce memory pressure.
        if (TestEnvironment.RunsOnCI && !TestEnvironment.Is64BitProcess)
        {
            return;
        }

        foreach (PngInterlaceMode interlaceMode in InterlaceMode)
        {
            TestPngEncoderCore(
                provider,
                PngColorType.Palette,
                PngFilterMethod.Adaptive,
                PngBitDepth.Bit8,
                interlaceMode,
                paletteSize: paletteSize,
                appendPaletteSize: true);
        }
    }

    [Theory]
    [WithBlankImages(1, 1, PixelTypes.Rgba32)]
    public void WritesFileMarker<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        using MemoryStream ms = new();
        image.Save(ms, PngEncoder);

        byte[] data = ms.ToArray().Take(8).ToArray();
        byte[] expected =
        [
            0x89, // Set the high bit.
                0x50, // P
                0x4E, // N
                0x47, // G
                0x0D, // Line ending CRLF
                0x0A, // Line ending CRLF
                0x1A, // EOF
                0x0A // LF
        ];

        Assert.Equal(expected, data);
    }

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public void Encode_PreserveRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();
        input.Save(memStream, PngEncoder);

        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        ImageMetadata meta = output.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Theory]
    [MemberData(nameof(PngBitDepthFiles))]
    public void Encode_PreserveBits(string imagePath, PngBitDepth pngBitDepth)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();
        input.Save(memStream, PngEncoder);

        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        PngMetadata meta = output.Metadata.GetPngMetadata();

        Assert.Equal(pngBitDepth, meta.BitDepth);
    }

    [Theory]
    [InlineData(PngColorType.Palette)]
    [InlineData(PngColorType.RgbWithAlpha)]
    [InlineData(PngColorType.GrayscaleWithAlpha)]
    public void Encode_WithTransparentColorBehaviorClear_Works(PngColorType colorType)
    {
        // arrange
        using Image<Rgba32> image = new(50, 50);
        PngEncoder encoder = new()
        {
            TransparentColorMode = TransparentColorMode.Clear,
            ColorType = colorType
        };
        Rgba32 rgba32 = Color.Blue.ToPixel<Rgba32>();
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < image.Height; y++)
            {
                Span<Rgba32> rowSpan = accessor.GetRowSpan(y);

                // Half of the test image should be transparent.
                if (y > 25)
                {
                    rgba32.A = 0;
                }

                for (int x = 0; x < image.Width; x++)
                {
                    rowSpan[x] = Rgba32.FromRgba32(rgba32);
                }
            }
        });

        // act
        using MemoryStream memStream = new();
        image.Save(memStream, encoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> actual = Image.Load<Rgba32>(memStream);
        Rgba32 expectedColor = Color.Blue.ToPixel<Rgba32>();
        if (colorType is PngColorType.Grayscale or PngColorType.GrayscaleWithAlpha)
        {
            byte luminance = ColorNumerics.Get8BitBT709Luminance(expectedColor.R, expectedColor.G, expectedColor.B);
            expectedColor = new Rgba32(luminance, luminance, luminance);
        }

        actual.ProcessPixelRows(accessor =>
        {
            Rgba32 transparent = Color.Transparent.ToPixel<Rgba32>();
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgba32> rowSpan = accessor.GetRowSpan(y);

                if (y > 25)
                {
                    expectedColor = transparent;
                }

                for (int x = 0; x < accessor.Width; x++)
                {
                    Assert.Equal(expectedColor, rowSpan[x]);
                }
            }
        });
    }

    [Theory]
    [WithFile(TestImages.Png.APng, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.DefaultNotAnimated, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.FrameOffset, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Issue2882, PixelTypes.Rgba32)]
    public void Encode_APng<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        using MemoryStream memStream = new();
        image.Save(memStream, PngEncoder);
        memStream.Position = 0;

        image.DebugSave(provider: provider, encoder: PngEncoder, null, false);

        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);

        // Some loss from original, due to palette matching accuracy.
        ImageComparer.TolerantPercentage(0.172F).VerifySimilarity(output, image);

        Assert.Equal(image.Frames.Count, output.Frames.Count);

        PngMetadata originalMetadata = image.Metadata.GetPngMetadata();
        PngMetadata outputMetadata = output.Metadata.GetPngMetadata();

        Assert.Equal(originalMetadata.RepeatCount, outputMetadata.RepeatCount);
        Assert.Equal(originalMetadata.AnimateRootFrame, outputMetadata.AnimateRootFrame);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            PngFrameMetadata originalFrameMetadata = image.Frames[i].Metadata.GetPngMetadata();
            PngFrameMetadata outputFrameMetadata = output.Frames[i].Metadata.GetPngMetadata();

            Assert.Equal(originalFrameMetadata.FrameDelay, outputFrameMetadata.FrameDelay);
            Assert.Equal(originalFrameMetadata.BlendMode, outputFrameMetadata.BlendMode);
            Assert.Equal(originalFrameMetadata.DisposalMode, outputFrameMetadata.DisposalMode);
        }
    }

    [Theory]
    [WithFile(TestImages.Gif.Leo, PixelTypes.Rgba32)]
    [WithFile(TestImages.Gif.Issues.Issue2866, PixelTypes.Rgba32)]
    public void Encode_AnimatedFormatTransform_FromGif<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (TestEnvironment.RunsOnCI && !TestEnvironment.IsWindows)
        {
            return;
        }

        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance);

        // Save the image for visual inspection.
        provider.Utility.SaveTestOutputFile(image, "png", PngEncoder, "animated");

        // Now compare the debug output with the reference output.
        // We do this because the transcoding encoding is lossy and encoding will lead to differences.
        // From the unencoded image, we can see that the image is visually the same.
        static bool Predicate(int i, int _) => i % 8 == 0; // Image has many frames, only compare a selection of them.
        image.CompareDebugOutputToReferenceOutputMultiFrame(provider, ImageComparer.Exact, extension: "png", encoder: PngEncoder, predicate: Predicate);

        // Now save the image and load it again to compare the metadata.
        using MemoryStream memStream = new();
        image.Save(memStream, PngEncoder);
        memStream.Position = 0;

        using Image<TPixel> encoded = Image.Load<TPixel>(memStream);
        GifMetadata gif = image.Metadata.GetGifMetadata();
        PngMetadata png = encoded.Metadata.GetPngMetadata();

        Assert.Equal(gif.RepeatCount, png.RepeatCount);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            GifFrameMetadata gifF = image.Frames[i].Metadata.GetGifMetadata();
            PngFrameMetadata pngF = encoded.Frames[i].Metadata.GetPngMetadata();

            Assert.Equal(gifF.FrameDelay, (int)(pngF.FrameDelay.ToDouble() * 100));

            switch (gifF.DisposalMode)
            {
                case FrameDisposalMode.RestoreToBackground:
                    Assert.Equal(FrameDisposalMode.RestoreToBackground, pngF.DisposalMode);
                    break;
                case FrameDisposalMode.RestoreToPrevious:
                    Assert.Equal(FrameDisposalMode.RestoreToPrevious, pngF.DisposalMode);
                    break;
                case FrameDisposalMode.Unspecified:
                case FrameDisposalMode.DoNotDispose:
                default:
                    Assert.Equal(FrameDisposalMode.DoNotDispose, pngF.DisposalMode);
                    break;
            }
        }
    }

    [Theory]
    [WithFile(TestImages.Webp.Lossless.Animated, PixelTypes.Rgba32)]
    public void Encode_AnimatedFormatTransform_FromWebp<TPixel>(TestImageProvider<TPixel> provider)
    where TPixel : unmanaged, IPixel<TPixel>
    {
        if (TestEnvironment.RunsOnCI && !TestEnvironment.IsWindows)
        {
            return;
        }

        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);

        using MemoryStream memStream = new();
        image.Save(memStream, PngEncoder);
        memStream.Position = 0;

        using Image<TPixel> output = Image.Load<TPixel>(memStream);
        ImageComparer.Exact.VerifySimilarity(output, image);

        WebpMetadata webp = image.Metadata.GetWebpMetadata();
        PngMetadata png = output.Metadata.GetPngMetadata();

        Assert.Equal(webp.RepeatCount, png.RepeatCount);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            WebpFrameMetadata webpF = image.Frames[i].Metadata.GetWebpMetadata();
            PngFrameMetadata pngF = output.Frames[i].Metadata.GetPngMetadata();

            Assert.Equal(webpF.FrameDelay, (uint)(pngF.FrameDelay.ToDouble() * 1000));

            switch (webpF.BlendMode)
            {
                case FrameBlendMode.Source:
                    Assert.Equal(FrameBlendMode.Source, pngF.BlendMode);
                    break;
                case FrameBlendMode.Over:
                default:
                    Assert.Equal(FrameBlendMode.Over, pngF.BlendMode);
                    break;
            }

            switch (webpF.DisposalMode)
            {
                case FrameDisposalMode.RestoreToBackground:
                    Assert.Equal(FrameDisposalMode.RestoreToBackground, pngF.DisposalMode);
                    break;
                case FrameDisposalMode.DoNotDispose:
                default:
                    Assert.Equal(FrameDisposalMode.DoNotDispose, pngF.DisposalMode);
                    break;
            }
        }
    }

    [Theory]
    [MemberData(nameof(PngTrnsFiles))]
    public void Encode_PreserveTrns(string imagePath, PngBitDepth pngBitDepth, PngColorType pngColorType)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        PngMetadata inMeta = input.Metadata.GetPngMetadata();
        Assert.True(inMeta.TransparentColor.HasValue);

        using MemoryStream memStream = new();
        input.Save(memStream, PngEncoder);
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        PngMetadata outMeta = output.Metadata.GetPngMetadata();
        Assert.True(outMeta.TransparentColor.HasValue);
        Assert.Equal(inMeta.TransparentColor, outMeta.TransparentColor);
        Assert.Equal(pngBitDepth, outMeta.BitDepth);
        Assert.Equal(pngColorType, outMeta.ColorType);
    }

    [Theory]
    [WithTestPatternImages(587, 821, PixelTypes.Rgba32)]
    [WithTestPatternImages(677, 683, PixelTypes.Rgba32)]
    public void Encode_WorksWithDiscontiguousBuffers<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.LimitAllocatorBufferCapacity().InPixelsSqrt(200);
        foreach (PngInterlaceMode interlaceMode in InterlaceMode)
        {
            TestPngEncoderCore(
                provider,
                PngColorType.Rgb,
                PngFilterMethod.Adaptive,
                PngBitDepth.Bit8,
                interlaceMode,
                appendPngColorType: true,
                appendPixelType: true);
        }
    }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
    public void EncodeWorksWithoutSsse3Intrinsics(TestImageProvider<Rgba32> provider)
    {
        static void RunTest(string serialized)
        {
            TestImageProvider<Rgba32> provider =
                FeatureTestRunner.DeserializeForXunit<TestImageProvider<Rgba32>>(serialized);

            foreach (PngInterlaceMode interlaceMode in InterlaceMode)
            {
                TestPngEncoderCore(
                    provider,
                    PngColorType.Rgb,
                    PngFilterMethod.Adaptive,
                    PngBitDepth.Bit8,
                    interlaceMode,
                    appendPngColorType: true,
                    appendPixelType: true);
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.DisableSSSE3,
            provider);
    }

    [Fact]
    public void EncodeFixesInvalidOptions()
    {
        // https://github.com/SixLabors/ImageSharp/issues/935
        using MemoryStream ms = new();
        TestFile testFile = TestFile.Create(TestImages.Png.Issue935);
        using Image<Rgba32> image = testFile.CreateRgba32Image(PngDecoder.Instance);

        image.Save(ms, new PngEncoder { ColorType = PngColorType.RgbWithAlpha });
    }

    // https://github.com/SixLabors/ImageSharp/issues/2469
    [Theory]
    [WithFile(TestImages.Png.Issue2469, PixelTypes.Rgba32)]
    public void Issue2469_Quantized_Encode_Artifacts<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        PngEncoder encoder = new() { BitDepth = PngBitDepth.Bit8, ColorType = PngColorType.Palette };

        string actualOutputFile = provider.Utility.SaveTestOutputFile(image, "png", encoder);
        using Image<Rgba32> encoded = Image.Load<Rgba32>(actualOutputFile);
        encoded.CompareToReferenceOutput(ImageComparer.Exact, provider);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2668
    [Theory]
    [WithFile(TestImages.Png.Issue2668, PixelTypes.Rgba32)]
    public void Issue2668_Quantized_Encode_Alpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.Mutate(x => x.Resize(100, 100));

        PngEncoder encoder = new() { BitDepth = PngBitDepth.Bit8, ColorType = PngColorType.Palette };

        string actualOutputFile = provider.Utility.SaveTestOutputFile(image, "png", encoder);
        using Image<Rgba32> encoded = Image.Load<Rgba32>(actualOutputFile);
        encoded.CompareToReferenceOutput(ImageComparer.Exact, provider);
    }

    [Fact]
    public void Issue_2862()
    {
        // Create a grayscale palette (or any other palette with colors that are very close to each other):
        Rgba32[] palette = [.. Enumerable.Range(0, 256).Select(i => new Rgba32((byte)i, (byte)i, (byte)i))];

        using Image<Rgba32> image = new(254, 4);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[x, y] = palette[x];
            }
        }

        PaletteQuantizer quantizer = new(
            palette.Select(Color.FromPixel).ToArray(),
            new QuantizerOptions { ColorMatchingMode = ColorMatchingMode.Hybrid });

        using MemoryStream ms = new();
        image.Save(ms, new PngEncoder
        {
            ColorType = PngColorType.Palette,
            BitDepth = PngBitDepth.Bit8,
            Quantizer = quantizer
        });

        ms.Position = 0;

        using Image<Rgba32> encoded = Image.Load<Rgba32>(ms);
        ImageComparer.Exact.VerifySimilarity(image, encoded);
    }

    private static void TestPngEncoderCore<TPixel>(
        TestImageProvider<TPixel> provider,
        PngColorType pngColorType,
        PngFilterMethod pngFilterMethod,
        PngBitDepth bitDepth,
        PngInterlaceMode interlaceMode,
        PngCompressionLevel compressionLevel = PngCompressionLevel.DefaultCompression,
        int paletteSize = 255,
        bool appendPngColorType = false,
        bool appendPngFilterMethod = false,
        bool appendPixelType = false,
        bool appendCompressionLevel = false,
        bool appendPaletteSize = false,
        bool appendPngBitDepth = false,
        PngChunkFilter optimizeMethod = PngChunkFilter.None)
            where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        PngEncoder encoder = new()
        {
            ColorType = pngColorType,
            FilterMethod = pngFilterMethod,
            CompressionLevel = compressionLevel,
            BitDepth = bitDepth,
            Quantizer = new WuQuantizer(new QuantizerOptions { MaxColors = paletteSize }),
            InterlaceMethod = interlaceMode,
            ChunkFilter = optimizeMethod,
        };

        string pngColorTypeInfo = appendPngColorType ? pngColorType.ToString() : string.Empty;
        string pngFilterMethodInfo = appendPngFilterMethod ? pngFilterMethod.ToString() : string.Empty;
        string compressionLevelInfo = appendCompressionLevel ? $"_C{compressionLevel}" : string.Empty;
        string paletteSizeInfo = appendPaletteSize ? $"_PaletteSize-{paletteSize}" : string.Empty;
        string pngBitDepthInfo = appendPngBitDepth ? bitDepth.ToString() : string.Empty;
        string pngInterlaceModeInfo = interlaceMode != PngInterlaceMode.None ? $"_{interlaceMode}" : string.Empty;

        string debugInfo = pngColorTypeInfo + pngFilterMethodInfo + compressionLevelInfo + paletteSizeInfo + pngBitDepthInfo + pngInterlaceModeInfo;

        string actualOutputFile = provider.Utility.SaveTestOutputFile(image, "png", encoder, debugInfo, appendPixelType);

        // Compare to the Magick reference decoder.
        IImageDecoder referenceDecoder = TestEnvironment.GetReferenceDecoder(actualOutputFile);

        // We compare using both our decoder and the reference decoder as pixel transformation
        // occurs within the encoder itself leaving the input image unaffected.
        // This means we are benefiting from testing our decoder also.
        using FileStream fileStream = File.OpenRead(actualOutputFile);
        using Image<TPixel> imageSharpImage = PngDecoder.Instance.Decode<TPixel>(DecoderOptions.Default, fileStream);

        fileStream.Position = 0;

        using Image<TPixel> referenceImage = referenceDecoder.Decode<TPixel>(DecoderOptions.Default, fileStream);
        ImageComparer.Exact.VerifySimilarity(referenceImage, imageSharpImage);
    }
}
