using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AngrySpacebar;

/// <summary>
/// A minimal 1x1 white-texture drawing toolkit exposed as a MonoGame
/// <see cref="DrawableGameComponent"/>. Provides the rectangles, circles,
/// ellipses and thick rotated lines that the face and vignette drawables use.
/// </summary>
public class PrimitiveBatch : DrawableGameComponent
{
    private Texture2D _pixel = null!;

    public PrimitiveBatch(Game game) : base(game) { }

    /// <summary>The shared white pixel texture (loaded in LoadContent).</summary>
    public Texture2D Pixel => _pixel;

    public override void Initialize()
    {
        // Never auto-drawn by the framework's component loop: the owner begins
        // the SpriteBatch once per frame and calls our helpers inside it.
        DrawOrder = 0;
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        base.LoadContent();
    }

    public void DrawRect(SpriteBatch sb, Rectangle r, Color c) => sb.Draw(_pixel, r, c);

    public void DrawCircleFilled(SpriteBatch sb, Vector2 center, float radius, Color c)
    {
        // approximate with many thin vertical strips
        int steps = Math.Max(8, (int)(radius / 2f));
        for (int i = 0; i <= steps; i++)
        {
            float dx = -radius + 2f * radius * i / steps;
            float h = (float)Math.Sqrt(Math.Max(0f, radius * radius - dx * dx));
            sb.Draw(_pixel, new Rectangle((int)(center.X + dx), (int)(center.Y - h), 2, (int)(h * 2)), c);
        }
    }

    public void DrawEllipseFilled(SpriteBatch sb, Vector2 center, float rx, float ry, Color c)
    {
        int steps = Math.Max(8, (int)(rx / 2f));
        for (int i = 0; i <= steps; i++)
        {
            float dx = -rx + 2f * rx * i / steps;
            float h = (float)Math.Sqrt(Math.Max(0f, 1 - (dx * dx) / (rx * rx))) * ry;
            sb.Draw(_pixel, new Rectangle((int)(center.X + dx), (int)(center.Y - h), 2, (int)(h * 2)), c);
        }
    }

    public void DrawThickLine(SpriteBatch sb, Vector2 start, float length, float angleRad,
                              float thickness, Color c)
    {
        // Draws a rotated bar. Origin is centered on the thickness axis so the
        // line grows from `start` along `angleRad`.
        sb.Draw(_pixel,
                position: start,
                sourceRectangle: _pixel.Bounds,
                color: c,
                rotation: angleRad,
                origin: new Vector2(0f, 0.5f), // middle of thickness
                scale: new Vector2(length, thickness),
                effects: SpriteEffects.None,
                layerDepth: 0f);
    }

    // Not used: this component only provides helper draws issued by other
    // components inside the owner's single SpriteBatch pass.
    public override void Draw(GameTime gameTime) { }
}
