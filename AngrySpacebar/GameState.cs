using Microsoft.Xna.Framework;

namespace AngrySpacebar;

/// <summary>
/// Pure game-state <see cref="GameComponent"/>: tracks spacebar presses, the
/// current angry message, timing, and derived anger/flash/shake values. It
/// receives edge-triggered press events from <see cref="InputManager"/> and
/// exposes everything the drawable components need to render the scene.
/// </summary>
public class GameState : GameComponent
{
    /// <summary>The scripted angry lines, one per press.</summary>
    public static readonly string[] Messages =
    [
        "Welcome! Please do NOT press the spacebar.",
        "You pressed the spacebar. We said NOT to.",
        "Seriously. Stop pressing the spacebar.",
        "What is WRONG with you?! STOP IT.",
        "WHY CAN'T YOU JUST FOLLOW INSTRUCTIONS?!",
        "AAAAAARGH!! THE SPACEBAR!! NO MORE OF IT!!",
        "THIS IS YOUR FINAL WARNING!!! DO NOT!!!",
        "GRRRRR!!!! FURY LEVEL MAXIMUM!!!!",
        "WE BEG YOU... PLEASE... NO MORE SPACEBAR...",
        "THAT'S IT. THAT'S IT!! I QUIT!! DO NOT PRESS IT!!!",
        "YOU WON'T LISTEN?? YOU NEVER LISTEN??! GRRRAAAHH!!",
        "THE SPACEBAR IS BANNED. BANNED!! DO YOU HEAR ME?!",
        "I CAN FEEL MY CIRCUITS SCREAMING. STOP. PRESSING.",
        "ENOUGH!!! ENOUGH!!! ENOUGH!!! AAAAAAAAA!!!",
        "FINE. PRESS IT. SEE WHAT HAPPENS. ...IT GETS WORSE.",
        "WE ARE SO, SO ANGRY RIGHT NOW. GRRRRRRRR!!!!!!",
    ];

    /// <summary>The closing line shown after the last scripted sentence.</summary>
    public const string FinalMessage = "DO NOT PRESS THE SPACEBAR!!! (we mean it this time)";

    /// <summary>Raised whenever the player presses the spacebar (after state update).</summary>
    public event Action<int>? SpacePressed;

    private int _pressCount;
    private double _gameTime;      // seconds since LoadContent
    private double _lastPressAt = -99; // for flash / shake envelope
    private string _currentMessage = Messages[0];

    public GameState(Game game) : base(game) { }

    /// <summary>Total spacebar presses so far.</summary>
    public int PressCount => _pressCount;

    /// <summary>The message currently on screen.</summary>
    public string CurrentMessage => _currentMessage;

    /// <summary>Seconds elapsed since content load (drives all animation).</summary>
    public double GameTime => _gameTime;

    /// <summary>Clamped 0..1 anger that ramps over 15 presses.</summary>
    public float Anger => Math.Min(1f, _pressCount / 15f);

    /// <summary>Index into <see cref="Messages"/> capped at the last entry.</summary>
    public int AngerLevel => Math.Min(_pressCount, Messages.Length - 1);

    /// <summary>1 right after a press, decays to 0 over ~0.6s (drives flash + scream mouth).</summary>
    public float PressFlash =>
        (float)Math.Clamp(1 - (_gameTime - _lastPressAt) / 0.6, 0, 1);

    /// <summary>Screen-shake magnitude for this frame, in logic pixels.</summary>
    public float ShakeMagnitude
    {
        get
        {
            float anger = Anger;
            return anger * 14f * PressFlash + (anger > 0.8f ? 3f : 0f);
        }
    }

    /// <summary>Called by <see cref="InputManager"/> on each spacebar key-down edge.</summary>
    public void RegisterPress()
    {
        _pressCount++;
        _lastPressAt = _gameTime;

        if (_pressCount <= Messages.Length - 1)
        {
            _currentMessage = Messages[_pressCount];
        }
        else if (_pressCount == Messages.Length)
        {
            // One press beyond the last listed sentence: show the closing line…
            _currentMessage = FinalMessage;
        }
        else
        {
            // …and the very next press after that last sentence closes the game.
            Game.Exit();
            return;
        }

        SpacePressed?.Invoke(_pressCount);
    }

    public override void Update(GameTime gameTime)
    {
        _gameTime += gameTime.ElapsedGameTime.TotalSeconds;
        base.Update(gameTime);
    }
}
