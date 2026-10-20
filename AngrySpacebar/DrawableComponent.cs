using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AngrySpacebar;

/// <summary>
/// Base class for game components that render into the fixed 1280x720 logic
/// space. The shared <see cref="SpriteBatch"/> is already begun by
/// <see cref="Game1.Draw"/> with the scene transform applied (letterboxing +
/// screen shake), so drawables only need to issue draws in logic coordinates.
/// </summary>
public abstract class DrawableBase : DrawableGameComponent
{
    /// <summary>Width of the virtual logic space every sentence must fit inside.</summary>
    public const int LogicWidth = 1280;

    /// <summary>Height of the virtual logic space every sentence must fit inside.</summary>
    public const int LogicHeight = 720;

    protected DrawableBase(Game1 game) : base(game) { }

    /// <summary>The owning game, typed for convenience.</summary>
    protected Game1 AngryGame => (Game1)Game;

    /// <summary>Shared batch, already begun with the scene transform.</summary>
    protected SpriteBatch Batch => AngryGame.Sprites;

    /// <summary>Loaded sprite-sheet font, shared by all text drawables.</summary>
    protected SpriteText Font => AngryGame.Text;

    /// <summary>Live anger / press state of the game.</summary>
    protected GameState State => AngryGame.State;

    /// <summary>Draw this component inside the owner's single batch pass.</summary>
    public abstract void Draw(in SceneInfo scene);

    // The framework-driven component draw loop is not used (the owner batches
    // everything itself), so satisfy the abstract member with a no-op.
    public override void Draw(GameTime gameTime) { }
}

/// <summary>Per-frame values every drawable needs (computed once in Game1.Draw).</summary>
public readonly record struct SceneInfo(float Anger, float PressFlash, double GameTime);
