using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AngrySpacebar;

/// <summary>
/// Clears the screen with the mood colour (calm blue -> pure rage red, plus a
/// bright flash right after each press) and draws the darkening vignette bars
/// that appear at high anger. First drawable in the pass so everything paints
/// on top of it.
/// </summary>
public class BackgroundLayer : DrawableBase
{
    private readonly PrimitiveBatch _prims;

    public BackgroundLayer(Game1 game, PrimitiveBatch prims) : base(game)
    {
        _prims = prims;
        DrawOrder = -100; // behind all other drawables
    }

    public override void Draw(in SceneInfo scene)
    {
        float anger = scene.Anger;
        float pressFlash = scene.PressFlash;

        // background: calm blue -> pure rage red
        Color bg = Color.Lerp(new Color(28, 42, 74), new Color(150, 10, 10), anger);
        // flash brighter right after a press
        bg = Color.Lerp(bg, new Color(255, 60, 60), pressFlash * 0.35f);
        GraphicsDevice.Clear(bg);

        // subtle vignette of darkening at high anger
        if (anger > 0.4f)
        {
            var sb = Batch;
            float a = (anger - 0.4f) * 0.9f;
            _prims.DrawRect(sb, new Rectangle(-50, -50, LogicWidth + 100, 60),
                            new Color(0, 0, 0) { A = (byte)(a * 255) });
            _prims.DrawRect(sb, new Rectangle(-50, LogicHeight - 10, LogicWidth + 100, 60),
                            new Color(0, 0, 0) { A = (byte)(a * 255) });
        }
    }
}
