using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AngrySpacebar;

/// <summary>
/// Minimal sprite-sheet text renderer. The font atlas (font.png) and its
/// metrics (font.dat) are pre-generated offline, so no content pipeline /
/// MGCB is required at build or run time.
/// </summary>
public class SpriteText
{
    private struct Glyph
    {
        public Rectangle Bounds; // source rect in the atlas
        public int Width;        // visible advance width (no padding)
    }

    private readonly Texture2D _atlas;
    private readonly Dictionary<char, Glyph> _glyphs = [];
    private readonly int _lineHeight;
    private readonly int _cellWidth;

    /// <summary>The pixel size the atlas was rendered at (used for scaling).</summary>
    public int BaseSize { get; }

    public SpriteText(GraphicsDevice device, string contentDir)
    {
        _atlas = LoadPng(device, Path.Combine(contentDir, "font.png"));

        using var fs = File.OpenRead(Path.Combine(contentDir, "font.dat"));
        using var br = new BinaryReader(fs);
        _cellWidth = br.ReadInt32();
        _lineHeight = br.ReadInt32();
        int count = br.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            int code = br.ReadInt32();
            int x = br.ReadInt32();
            int y = br.ReadInt32();
            int w = br.ReadInt32();
            br.ReadInt32(); // unused top offset
            _glyphs[(char)code] = new Glyph
            {
                Bounds = new Rectangle(x, y, _cellWidth, _lineHeight),
                Width = w,
            };
        }

        BaseSize = 48; // must match the generator script
    }

    private static Texture2D LoadPng(GraphicsDevice device, string path)
    {
        using var stream = File.OpenRead(path);
        return Texture2D.FromStream(device, stream);
    }

    /// <summary>Total pixel width of a string at the given cap height.</summary>
    public float Measure(string text, float pixelSize)
    {
        float scale = pixelSize / BaseSize;
        float adv = _cellWidth * scale;
        float lastVisible = 0f;
        bool any = false;
        foreach (char c in text)
        {
            if (_glyphs.TryGetValue(c, out var g))
            {
                lastVisible = g.Width * scale;
                any = true;
            }
            else
            {
                lastVisible = adv; // unknown chars fall back to full cell
                any = true;
            }
        }
        return any ? adv + lastVisible : 0f;
    }

    /// <summary>
    /// The largest pixel size &lt;= <paramref name="maxPixelSize"/> for which the
    /// string fits within <paramref name="maxWidth"/> pixels. Used to keep text
    /// from being clipped at the screen edges.
    /// </summary>
    public float FitSize(string text, float maxWidth, float maxPixelSize)
    {
        if (string.IsNullOrEmpty(text)) return maxPixelSize;
        float w = Measure(text, maxPixelSize);
        if (w <= maxWidth || w <= 0f) return maxPixelSize;
        // scale down proportionally so the measured width equals maxWidth
        float size = maxPixelSize * maxWidth / w;
        return MathF.Max(size, 1f);
    }

    /// <summary>
    /// Greedy word wrap that also breaks words longer than <paramref name="maxWidth"/>
    /// so no line can ever exceed the maximum width (prevents clipping).
    /// </summary>
    public List<string> Wrap(string text, float maxWidth, float pixelSize)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;

        float adv = _cellWidth * (pixelSize / BaseSize);
        var curWords = new List<string>();
        float curWidth = 0f;

        void Flush()
        {
            if (curWords.Count > 0)
            {
                lines.Add(string.Join(" ", curWords));
                curWords.Clear();
                curWidth = 0f;
            }
        }

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            float wordW = Measure(word, pixelSize);

            // word alone is too wide: break it into chunks that fit
            if (wordW > maxWidth)
            {
                Flush();
                var chunk = new System.Text.StringBuilder();
                float chunkW = 0f;
                foreach (char ch in word)
                {
                    float cw = _glyphs.TryGetValue(ch, out var g)
                        ? g.Width * (pixelSize / BaseSize)
                        : adv;
                    if (chunk.Length > 0 && chunkW + adv + cw > maxWidth)
                    {
                        lines.Add(chunk.ToString());
                        chunk.Clear();
                        chunkW = 0f;
                    }
                    if (chunk.Length > 0) chunkW += adv;
                    chunk.Append(ch);
                    chunkW += cw;
                }
                if (chunk.Length > 0) lines.Add(chunk.ToString());
                continue;
            }

            float addW = curWords.Count == 0 ? wordW : adv + wordW;
            if (curWords.Count > 0 && curWidth + addW > maxWidth)
                Flush();

            curWords.Add(word);
            curWidth = curWords.Count == 1 ? wordW : curWidth + adv + wordW;
        }
        Flush();
        return lines;
    }

    public void Draw(SpriteBatch sb, string text, Vector2 position, float pixelSize,
                    Color color, float rotation = 0f, float originXRatio = 0f,
                    float originYRatio = 0f)
    {
        float scale = pixelSize / BaseSize;
        float adv = _cellWidth * scale;
        // the text block spans from position.X to position.X + (len-1)*adv,
        // so its true geometric center is half of that extent. (Measure()
        // includes a trailing advance for every char, which would shift the
        // "center" left by half an advance and make text look off-center.)
        int len = text?.Length ?? 0;
        float total = len > 0 ? (len - 1) * adv : 0f;
        float ox = total * originXRatio;
        float oy = _lineHeight * scale * originYRatio;

        // guard against a null string so the loop below never dereferences null
        if (text is null) return;

        float x = 0f;
        foreach (char c in text)
        {
            if (_glyphs.TryGetValue(c, out var g))
            {
                sb.Draw(_atlas,
                        position + new Vector2(x - ox, -oy),
                        g.Bounds,
                        color,
                        rotation,
                        Vector2.Zero,
                        scale,
                        SpriteEffects.None,
                        0f);
            }
            x += adv;
        }
    }

    public void DrawCentered(SpriteBatch sb, string text, Vector2 center, float pixelSize,
                             Color color, float rotation = 0f)
    {
        Draw(sb, text, center, pixelSize, color, rotation, 0.5f, 0.5f);
    }
}
