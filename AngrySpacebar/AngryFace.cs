using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AngrySpacebar;

/// <summary>
/// Draws the escalating angry smiley face(s) — the visual anchor of the scene.
/// Uses <see cref="PrimitiveBatch"/> helpers for its circles, ellipses and
/// thick rotated lines (eyebrows, mouth, steam puffs).
/// </summary>
public class AngryFace : DrawableBase
{
    private readonly PrimitiveBatch _prims;

    public AngryFace(Game1 game, PrimitiveBatch prims) : base(game)
    {
        _prims = prims;
        DrawOrder = 100; // faces sit on top of everything else
    }

    public override void Draw(in SceneInfo scene)
    {
        float anger = scene.Anger;
        float pressFlash = scene.PressFlash;
        var sb = Batch;

        // the central face is the anchor all wording shares
        float faceX = LogicWidth / 2f;
        float cy = LogicHeight / 2f;

        DrawFace(sb, new Vector2(faceX, cy + 190), scale: 1f + anger * 0.6f, anger, pressFlash);

        // extra mini-faces at high anger
        if (anger > 0.5f)
        {
            DrawFace(sb, new Vector2(faceX - 330, cy + 210), 0.6f, anger, pressFlash);
            DrawFace(sb, new Vector2(faceX + 330, cy + 210), 0.6f, anger, pressFlash);
        }
    }

    private void DrawFace(SpriteBatch sb, Vector2 pos, float scale, float anger, float pressFlash)
    {
        float s = 90 * scale;
        Color skin = Color.Lerp(new Color(255, 214, 165), new Color(220, 40, 40), anger);
        Color feature = Color.Black;

        // head circle
        _prims.DrawCircleFilled(sb, pos, s, skin);

        // eyebrows slant inward when angry
        float browAngle = MathHelper.Lerp(-0.15f, 0.6f, anger); // radians tilt
        float browY = pos.Y - s * 0.35f;
        float browLen = s * 0.42f;
        _prims.DrawThickLine(sb, new Vector2(pos.X - s * 0.55f, browY), browLen, browAngle, s * 0.12f, feature);
        _prims.DrawThickLine(sb, new Vector2(pos.X + s * 0.15f, browY), browLen, -browAngle, s * 0.12f, feature);

        // eyes: shrink with anger
        float eyeR = MathHelper.Lerp(s * 0.18f, s * 0.08f, anger);
        _prims.DrawCircleFilled(sb, new Vector2(pos.X - s * 0.35f, pos.Y - s * 0.05f), eyeR, feature);
        _prims.DrawCircleFilled(sb, new Vector2(pos.X + s * 0.35f, pos.Y - s * 0.05f), eyeR, feature);

        // mouth: smile -> flat line -> screaming open oval
        if (anger < 0.35f)
        {
            // gentle smile: three segments
            _prims.DrawThickLine(sb, new Vector2(pos.X - s * 0.4f, pos.Y + s * 0.38f), s * 0.28f, 0.35f, s * 0.09f, feature);
            _prims.DrawThickLine(sb, new Vector2(pos.X - s * 0.14f, pos.Y + s * 0.47f), s * 0.28f, 0f, s * 0.09f, feature);
            _prims.DrawThickLine(sb, new Vector2(pos.X + s * 0.12f, pos.Y + s * 0.38f), s * 0.28f, -0.35f, s * 0.09f, feature);
        }
        else if (anger < 0.5f)
        {
            _prims.DrawThickLine(sb, new Vector2(pos.X - s * 0.4f, pos.Y + s * 0.45f), s * 0.8f,
                          0f, s * 0.1f, feature);
        }
        else
        {
            // scream mouth
            float openness = MathHelper.Lerp(0.2f, 0.6f, (anger - 0.5f) * 2f) * (1f + pressFlash * 0.5f);
            _prims.DrawEllipseFilled(sb, new Vector2(pos.X, pos.Y + s * 0.5f), s * 0.35f, s * openness * 0.5f, feature);
        }

        // steam puffs at high anger
        if (anger > 0.6f)
        {
            float puff = (float)(Math.Sin(State.GameTime * 10) * 0.5 + 0.5);
            _prims.DrawCircleFilled(sb, new Vector2(pos.X - s * 0.9f, pos.Y - s * (1.1f + puff * 0.2f)), s * 0.15f,
                             Color.White * (0.3f + puff * 0.4f));
            _prims.DrawCircleFilled(sb, new Vector2(pos.X + s * 0.9f, pos.Y - s * (1.1f + (1 - puff) * 0.2f)), s * 0.15f,
                             Color.White * (0.3f + (1 - puff) * 0.4f));
        }
    }
}
