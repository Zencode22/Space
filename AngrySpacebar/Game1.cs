using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AngrySpacebar;

/// <summary>
/// "DO NOT PRESS THE SPACEBAR" — a game where the only button you can use is
/// the spacebar, the instructions beg you not to press it, and every press
/// makes the screen angrier: harsher messages, redder background, shaking,
/// distorted angry faces, and escalating sound.
///
/// The game is composed entirely of MonoGame <see cref="GameComponent"/>s /
/// <see cref="DrawableGameComponent"/>s wired together here:
///   - <see cref="InputManager"/>    : keyboard polling + edge detection events
///   - <see cref="GameState"/>       : presses, current sentence, anger/flash/shake
///   - <see cref="AudioManager"/>    : procedural boops / scream / hum escalation
///   - <see cref="PrimitiveBatch"/>  : shared white-pixel shape drawing toolkit
///   - <see cref="BackgroundLayer"/> : mood-coloured clear + vignette
///   - <see cref="SentenceRenderer"/>: every sentence, always fitted to the window
///   - <see cref="AngryFace"/>       : the escalating smiley face(s)
///
/// The first press after the final sentence closes the game (handled in
/// <see cref="GameState.RegisterPress"/>).
/// </summary>
public class Game1 : Game
{
    private const int LogicWidth = 1280;
    private const int LogicHeight = 720;

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _sb = null!;
    private SpriteText _text = null!;

    /// <summary>The single shared sprite batch (begun by Draw each frame).</summary>
    public SpriteBatch Sprites => _sb;

    /// <summary>The loaded sprite-sheet font, shared by all text drawables.</summary>
    public SpriteText Text => _text;

    // ---- components ----
    public InputManager Input { get; private set; } = null!;
    public GameState State { get; private set; } = null!;
    public AudioManager Audio { get; private set; } = null!;
    public PrimitiveBatch Prims { get; private set; } = null!;
    public BackgroundLayer Background { get; private set; } = null!;
    public SentenceRenderer Sentences { get; private set; } = null!;
    public AngryFace Face { get; private set; } = null!;

    private DrawableBase[] _drawables = [];

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = LogicWidth,
            PreferredBackBufferHeight = LogicHeight,
            // Plain fixed-size window; there is no fullscreen mode.
            IsFullScreen = false
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // Create and register all components up front so Initialize/LoadContent
        // run through the standard MonoGame component lifecycle.
        Input = new InputManager(this);
        State = new GameState(this);
        Audio = new AudioManager(this);
        Prims = new PrimitiveBatch(this);
        Background = new BackgroundLayer(this, Prims);
        Sentences = new SentenceRenderer(this);
        Face = new AngryFace(this, Prims);

        Components.Add(Input);
        Components.Add(State);
        Components.Add(Audio);
        Components.Add(Prims);
        Components.Add(Background);
        Components.Add(Sentences);
        Components.Add(Face);

        _drawables = [Background, Sentences, Face];
    }

    protected override void Initialize()
    {
        // Run at a fixed 60fps regardless of vsync/display refresh rate.
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromTicks(166667);

        // Plain fixed-size window only — no fullscreen of any kind. The back
        // buffer matches the 1280x720 virtual logic space, so rendering is
        // 1:1 and every sentence fits the window by construction.
        _graphics.PreferredBackBufferWidth = LogicWidth;
        _graphics.PreferredBackBufferHeight = LogicHeight;
        _graphics.IsFullScreen = false;
        _graphics.ApplyChanges();

        // Input -> state wiring: a spacebar press advances the story, and the
        // first press after the last sentence fully closes the game (Exit is
        // called inside GameState.RegisterPress for that case).
        Input.SpacePressed += State.RegisterPress;
        Input.ExitRequested += Exit;

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _sb = new SpriteBatch(GraphicsDevice);

        // Prefer the app-base directory so the game works no matter what the
        // current working directory happens to be.
        string contentDir = Path.Combine(AppContext.BaseDirectory, Content.RootDirectory);
        if (!Directory.Exists(contentDir))
            contentDir = Content.RootDirectory; // fallback: relative to CWD

        _text = new SpriteText(GraphicsDevice, contentDir);

        base.LoadContent();
    }

    protected override void Draw(GameTime gameTime)
    {
        // Per-frame scene values computed once from the state component.
        float shakeMag = State.ShakeMagnitude;
        Vector2 shake = new Vector2(
            (float)(Math.Sin(State.GameTime * 61) * Math.Cos(State.GameTime * 47)),
            (float)(Math.Cos(State.GameTime * 53) * Math.Sin(State.GameTime * 71))) * shakeMag;

        // Scale the 1280x720 logic area to fit the actual back buffer so the
        // whole game is always visible, no matter the window/display size.
        int bufW = GraphicsDevice.PresentationParameters.BackBufferWidth;
        int bufH = GraphicsDevice.PresentationParameters.BackBufferHeight;
        float scaleX = bufW / (float)LogicWidth;
        float scaleY = bufH / (float)LogicHeight;
        float fitScale = Math.Min(scaleX, scaleY);
        float offX = (bufW - LogicWidth * fitScale) / 2f;
        float offY = (bufH - LogicHeight * fitScale) / 2f;
        Matrix transform = Matrix.CreateTranslation(offX, offY, 0) *
                           Matrix.CreateScale(fitScale, fitScale, 1) *
                           Matrix.CreateTranslation(shake.X, shake.Y, 0);

        SceneInfo scene = new(State.Anger, State.PressFlash, State.GameTime);

        // Single batch pass shared by all drawables (sorted by DrawOrder):
        // background/vignette -> sentences -> faces.
        _sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                  SamplerState.PointClamp, transformMatrix: transform);

        foreach (var d in _drawables.OrderBy(d => d.DrawOrder))
            d.Draw(in scene);

        _sb.End();

        // Note: the framework's own drawable-component loop is intentionally
        // skipped here — every visual is painted inside the single batch above.
    }
}
