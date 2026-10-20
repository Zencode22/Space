using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AngrySpacebar;

/// <summary>
/// Draws every sentence in the scene — the title, the instruction line, the
/// current angry message and the press counter. Each string is passed through
/// <see cref="SentenceLayout"/> so it always fits fully inside the 1280x720
/// window (wrapped and/or shrunk as needed, never clipped at an edge).
/// </summary>
public class SentenceRenderer : DrawableBase
{
    public const string TitleStr = "DO NOT PRESS THE SPACEBAR";
    public const string InstrStr = "The only button you can use is [SPACE]. Don't.";

    // jitter that changes once per frame (re-seeded at the top of Draw)
    private readonly Random _jitterRng = new();
    private float _jx1, _jy1, _jr1, _jx2, _jy2, _jr2, _jx3, _jy3, _jr3;

    public SentenceRenderer(Game1 game) : base(game) { }

    private void RerollJitter()
    {
        _jx1 = Jitter(); _jy1 = Jitter(); _jr1 = Jitter();
        _jx2 = Jitter(); _jy2 = Jitter(); _jr2 = Jitter();
        _jx3 = Jitter(); _jy3 = Jitter(); _jr3 = Jitter();
    }

    private float Jitter() => (float)(_jitterRng.NextDouble() * 2 - 1);

    public override void Draw(in SceneInfo scene)
    {
        RerollJitter(); // fresh jitter values each frame

        float anger = scene.Anger;
        var sb = Batch;
        var font = Font;

        // The central smiley face is the visual anchor of the scene, so its
        // draw position (faceX) is THE center point used for all wording.
        float faceX = LogicWidth / 2f;   // X where AngryFace draws below
        float cy = LogicHeight / 2f;

        // title — occupies the band from the top edge down to the instruction
        // line; wrapped/shrunk so it always fits horizontally AND vertically.
        float titleBandH = 140f - SentenceLayout.TopSafe; // room between top edge and instr line
        var titleBlock = SentenceLayout.FitSentence(font, TitleStr, 40f, titleBandH);
        Color titleColor = Color.Lerp(Color.White, new Color(255, 80, 80), anger);
        float titleRot = anger * 0.06f * (float)Math.Sin(scene.GameTime * 8);
        if (titleBlock.Lines.Count == 1)
        {
            font.DrawCentered(sb, TitleStr,
                new Vector2(faceX, SentenceLayout.TopSafe + titleBlock.Size * 0.7f),
                titleBlock.Size, titleColor, titleRot);
        }
        else
        {
            // multi-line title: stack the lines downward from the top margin
            float lineH = titleBlock.LineHeight;
            for (int i = 0; i < titleBlock.Lines.Count; i++)
                font.DrawCentered(sb, titleBlock.Lines[i],
                    new Vector2(faceX, SentenceLayout.TopSafe + titleBlock.Size * 0.7f + i * lineH),
                    titleBlock.Size, titleColor, titleRot);
        }

        // instruction line — same guarantee: shrinks/wraps so the full
        // sentence stays inside the window.
        float instrSize = font.FitSize(InstrStr, SentenceLayout.TextMaxWidth, 20f);
        var instrLines = font.Wrap(InstrStr, SentenceLayout.TextMaxWidth, instrSize);
        {
            float lineH = instrSize * 1.4f;
            float y = 160f - (instrLines.Count - 1) * lineH / 2f;
            foreach (var ln in instrLines)
            {
                font.DrawCentered(sb, ln, new Vector2(faceX, y), instrSize,
                    Color.Lerp(Color.LightGray, Color.OrangeRed, anger));
                y += lineH;
            }
        }

        // current message (the angry voice): wrapped to the safe width and
        // shrunk as far as needed so ALL lines fit vertically in the band
        // between the instruction line and the face — every sentence, even
        // the longest rant, is fully on screen.
        float msgTopLimit = 185f;                       // start below instruction line
        float msgBottomLimit = cy + 100f;               // stop above the face
        float msgAvailH = msgBottomLimit - msgTopLimit;
        var msgBlock = SentenceLayout.FitSentence(font, State.CurrentMessage, 24f, msgAvailH);
        float msgCenterY = Math.Min(cy - 60f, msgTopLimit + msgBlock.BlockHeight / 2f);
        Color msgColor = Color.Lerp(new Color(220, 220, 255), Color.Red, anger);
        DrawWrapped(sb, msgBlock, new Vector2(faceX, msgCenterY), msgColor,
                    jitter: anger * 6f, rotBase: anger * 0.03f);

        // press counter — pinned just above the bottom safe edge, shrunk if
        // the number of digits ever makes it too wide for the window.
        string pressText = $"Spacebar presses: {State.PressCount}";
        float pressSize = font.FitSize(pressText, SentenceLayout.TextMaxWidth, 18f);
        font.DrawCentered(sb, pressText,
            new Vector2(faceX, SentenceLayout.BottomSafe - pressSize * 0.6f), pressSize,
            Color.Lerp(Color.Gray, Color.Yellow, anger));
    }

    private void DrawWrapped(SpriteBatch sb, SentenceLayout.FittedBlock block,
                             Vector2 center, Color color, float jitter, float rotBase)
    {
        // per-line jitter values (rolled once per frame in Draw)
        ReadOnlySpan<float> jxs = [_jx1, _jx2, _jx3];
        ReadOnlySpan<float> jys = [_jy1, _jy2, _jy3];
        ReadOnlySpan<float> jrs = [_jr1, _jr2, _jr3];

        var lines = block.Lines;
        float lineH = block.LineHeight;
        float y = center.Y - (lines.Count - 1) * lineH / 2f;
        for (int i = 0; i < lines.Count; i++)
        {
            int k = Math.Min(i, 2);
            float jx = jxs[k] * jitter;
            float jy = jys[k] * jitter;
            float rot = jrs[k] * rotBase;
            Font.DrawCentered(sb, lines[i], new Vector2(center.X + jx, y + jy), block.Size, color, rot);
            y += lineH;
        }
    }
}
