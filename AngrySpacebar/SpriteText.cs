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
    private readonly Dictionary<char, Glyph> _glyphs = new();
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

    public void Draw(SpriteBatch sb, string text, Vector2 position, float pixelSize,
                    Color color, float rotation = 0f, float originXRatio = 0f,
                    float originYRatio = 0f)
    {
        float scale = pixelSize / BaseSize;
        float adv = _cellWidth * scale;
        float total = Measure(text, pixelSize);
        float ox = total * originXRatio;
        float oy = _lineHeight * scale * originYRatio;

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
