// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp;

/// <content>
/// Contains the definition of <see cref="WernerPalette"/>.
/// </content>
public partial struct Color
{
    private static readonly Lazy<Color[]> WernerPaletteLazy = new(CreateWernerPalette, true);

    /// <summary>
    /// Gets a collection of colors as defined in the original second edition of Werner’s Nomenclature of Colours 1821.
    /// The hex codes were collected and defined by Nicholas Rougeux <see href="https://www.c82.net/werner"/>.
    /// </summary>
    public static ReadOnlyMemory<Color> WernerPalette => WernerPaletteLazy.Value;

    private static Color[] CreateWernerPalette() =>
    [
        ParseHex("#f1e9cd"),
        ParseHex("#f2e7cf"),
        ParseHex("#ece6d0"),
        ParseHex("#f2eacc"),
        ParseHex("#f3e9ca"),
        ParseHex("#f2ebcd"),
        ParseHex("#e6e1c9"),
        ParseHex("#e2ddc6"),
        ParseHex("#cbc8b7"),
        ParseHex("#bfbbb0"),
        ParseHex("#bebeb3"),
        ParseHex("#b7b5ac"),
        ParseHex("#bab191"),
        ParseHex("#9c9d9a"),
        ParseHex("#8a8d84"),
        ParseHex("#5b5c61"),
        ParseHex("#555152"),
        ParseHex("#413f44"),
        ParseHex("#454445"),
        ParseHex("#423937"),
        ParseHex("#433635"),
        ParseHex("#252024"),
        ParseHex("#241f20"),
        ParseHex("#281f3f"),
        ParseHex("#1c1949"),
        ParseHex("#4f638d"),
        ParseHex("#383867"),
        ParseHex("#5c6b8f"),
        ParseHex("#657abb"),
        ParseHex("#6f88af"),
        ParseHex("#7994b5"),
        ParseHex("#6fb5a8"),
        ParseHex("#719ba2"),
        ParseHex("#8aa1a6"),
        ParseHex("#d0d5d3"),
        ParseHex("#8590ae"),
        ParseHex("#3a2f52"),
        ParseHex("#39334a"),
        ParseHex("#6c6d94"),
        ParseHex("#584c77"),
        ParseHex("#533552"),
        ParseHex("#463759"),
        ParseHex("#bfbac0"),
        ParseHex("#77747f"),
        ParseHex("#4a475c"),
        ParseHex("#b8bfaf"),
        ParseHex("#b2b599"),
        ParseHex("#979c84"),
        ParseHex("#5d6161"),
        ParseHex("#61ac86"),
        ParseHex("#a4b6a7"),
        ParseHex("#adba98"),
        ParseHex("#93b778"),
        ParseHex("#7d8c55"),
        ParseHex("#33431e"),
        ParseHex("#7c8635"),
        ParseHex("#8e9849"),
        ParseHex("#c2c190"),
        ParseHex("#67765b"),
        ParseHex("#ab924b"),
        ParseHex("#c8c76f"),
        ParseHex("#ccc050"),
        ParseHex("#ebdd99"),
        ParseHex("#ab9649"),
        ParseHex("#dbc364"),
        ParseHex("#e6d058"),
        ParseHex("#ead665"),
        ParseHex("#d09b2c"),
        ParseHex("#a36629"),
        ParseHex("#a77d35"),
        ParseHex("#f0d696"),
        ParseHex("#d7c485"),
        ParseHex("#f1d28c"),
        ParseHex("#efcc83"),
        ParseHex("#f3daa7"),
        ParseHex("#dfa837"),
        ParseHex("#ebbc71"),
        ParseHex("#d17c3f"),
        ParseHex("#92462f"),
        ParseHex("#be7249"),
        ParseHex("#bb603c"),
        ParseHex("#c76b4a"),
        ParseHex("#a75536"),
        ParseHex("#b63e36"),
        ParseHex("#b5493a"),
        ParseHex("#cd6d57"),
        ParseHex("#711518"),
        ParseHex("#e9c49d"),
        ParseHex("#eedac3"),
        ParseHex("#eecfbf"),
        ParseHex("#ce536b"),
        ParseHex("#b74a70"),
        ParseHex("#b7757c"),
        ParseHex("#612741"),
        ParseHex("#7a4848"),
        ParseHex("#3f3033"),
        ParseHex("#8d746f"),
        ParseHex("#4d3635"),
        ParseHex("#6e3b31"),
        ParseHex("#864735"),
        ParseHex("#553d3a"),
        ParseHex("#613936"),
        ParseHex("#7a4b3a"),
        ParseHex("#946943"),
        ParseHex("#c39e6d"),
        ParseHex("#513e32"),
        ParseHex("#8b7859"),
        ParseHex("#9b856b"),
        ParseHex("#766051"),
        ParseHex("#453b32")
    ];
}
