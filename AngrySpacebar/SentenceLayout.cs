using Microsoft.Xna.Framework;

namespace AngrySpacebar;

/// <summary>
/// Text layout helpers shared by the sentence drawables. Every sentence in the
/// game is guaranteed to fit fully inside the fixed 1280x720 window: strings
/// are measured with the real font metrics, word-wrapped across lines when too
/// wide, and auto-shrunk until the whole multi-line block also fits the
/// available vertical band — for ANY sentence, no matter how long.
/// </summary>
public static class SentenceLayout
{
    /// <summary>Safe-area padding from the window edges, in logic pixels.</summary>
    public const float Margin = 30f;

    /// <summary>Headroom for per-frame line jitter so shaking text stays on screen.</summary>
    public const float JitterPad = 12f;

    /// <summary>Guaranteed on-screen maximum text width.</summary>
    public const float TextMaxWidth = DrawableBase.LogicWidth - 2 * (Margin + JitterPad);

    /// <summary>First usable Y inside the safe area.</summary>
    public const float TopSafe = Margin;

    /// <summary>Last usable Y inside the safe area.</summary>
    public const float BottomSafe = DrawableBase.LogicHeight - Margin;

    /// <summary>A fitted multi-line text block plus the size it was fitted at.</summary>
    public readonly record struct FittedBlock(List<string> Lines, float Size)
    {
        public float LineHeight => Size * 1.4f;
        public float BlockHeight => Lines.Count * LineHeight;
    }

    /// <summary>
    /// Wrap a sentence into lines that all fit the safe width, then shrink the
    /// text size step by step until the entire multi-line block also fits
    /// <paramref name="availHeight"/>. Guarantees the WHOLE sentence is visible
    /// inside the window.
    /// </summary>
    public static FittedBlock FitSentence(SpriteText font, string text,
                                          float maxPixelSize, float availHeight)
    {
        float size = font.EnsureFit(text, TextMaxWidth, maxPixelSize);
        var lines = font.Wrap(text, TextMaxWidth, size);
        while (lines.Count * font.LineHeight(size) * 1.4f > availHeight && size > 8f)
        {
            size -= 2f;
            lines = font.Wrap(text, TextMaxWidth, size);
        }
        return new FittedBlock(font.Wrap(text, TextMaxWidth, size), size);
    }
}
