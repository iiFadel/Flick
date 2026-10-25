// fork: the Edit editor's sliders (Phase 5): exposure, contrast, saturation and warmth from −100 to 100, sharpness from 0
// to 100; 0 changes nothing. What each does to the pixels is the image engine's (Magick/ImageEdits.Adjust), which the
// window's preview runs too. Pure, unit tested (AdjustmentsTests).

namespace Filee.Core.Editing;

public sealed record Adjustments
{
    public const int Min = -100;
    public const int Max = 100;

    public static readonly Adjustments None = new();

    public int Exposure { get; init; }
    public int Contrast { get; init; }
    public int Saturation { get; init; }
    public int Warmth { get; init; }

    /// <summary>0 to 100.</summary>
    public int Sharpness { get; init; }

    /// <summary>True when every slider is at 0.</summary>
    public bool IsNone => this == None;

    /// <summary>Every slider inside its range.</summary>
    public Adjustments Clamped() => new()
    {
        Exposure = Math.Clamp(Exposure, Min, Max),
        Contrast = Math.Clamp(Contrast, Min, Max),
        Saturation = Math.Clamp(Saturation, Min, Max),
        Warmth = Math.Clamp(Warmth, Min, Max),
        Sharpness = Math.Clamp(Sharpness, 0, Max),
    };

    /// <summary>How much brighter: 2 to the power of a stop per 50 of Exposure (two stops either way at the ends).</summary>
    public double ExposureFactor => Math.Pow(2, Math.Clamp(Exposure, Min, Max) / 50.0);
}
