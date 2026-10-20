using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace AngrySpacebar;

/// <summary>
/// Centralised keyboard input as a MonoGame <see cref="GameComponent"/>. Polls
/// the keyboard once per frame, performs edge detection, and raises semantic
/// events (<see cref="SpacePressed"/>, <see cref="ExitRequested"/>) so gameplay
/// components never touch raw <c>KeyboardState</c> themselves.
/// </summary>
public class InputManager : GameComponent
{
    /// <summary>Raised on each spacebar press (key-down edge).</summary>
    public event Action? SpacePressed;

    /// <summary>Raised when Escape is pressed — requests the game to exit.</summary>
    public event Action? ExitRequested;

    private KeyboardState _previous;

    /// <summary>True while Space is held this frame.</summary>
    public bool IsSpaceDown { get; private set; }

    public InputManager(Game game) : base(game)
    {
        // Run before any other component so presses are seen in the same frame.
        UpdateOrder = -1000;
    }

    public override void Initialize()
    {
        _previous = Keyboard.GetState();
        base.Initialize();
    }

    public override void Update(GameTime gameTime)
    {
        var current = Keyboard.GetState();

        // Edge detection: key down now but not last frame.
        if (current.IsKeyDown(Keys.Space) && _previous.IsKeyUp(Keys.Space))
            SpacePressed?.Invoke();

        if (current.IsKeyDown(Keys.Escape) && _previous.IsKeyUp(Keys.Escape))
            ExitRequested?.Invoke();

        IsSpaceDown = current.IsKeyDown(Keys.Space);
        _previous = current;

        base.Update(gameTime);
    }
}
