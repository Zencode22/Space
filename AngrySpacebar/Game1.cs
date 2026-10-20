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
    private readonly GraphicsDeviceManager _graphics;

    /// <summary>The single shared sprite batch (begun by Draw each frame).</summary>
    public SpriteBatch Sprites { get; private set; } = null!;

    /// <summary>The loaded sprite-sheet font, shared by all text drawables.</summary>
    public SpriteText Text { get; private set; } = null!;

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
            PreferredBackBufferWidth = DrawableBase.LogicWidth,
            PreferredBackBufferHeight = DrawableBase.LogicHeight,
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

        // The game runs in a plain windowed mode only — no fullscreen of any
        // kind. The back buffer matches the 1280x720 virtual logic space, so
        // rendering is 1:1 and nothing needs scaling to a monitor resolution.
        _graphics.PreferredBackBufferWidth = DrawableBase.LogicWidth;
        _graphics.PreferredBackBufferHeight = DrawableBase.LogicHeight;
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
        Sprites = new SpriteBatch(GraphicsDevice);

        // Prefer the app-base directory so the game works no matter what the
        // current working directory happens to be.
        string contentDir = Path.Combine(AppContext.BaseDirectory, Content.RootDirectory);
        if (!Directory.Exists(contentDir))
            contentDir = Content.RootDirectory; // fallback: relative to CWD

        _text = new SpriteText(GraphicsDevice, contentDir);

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        BuildAudio();
        _hum?.Play();
    }

    private void BuildAudio()
    {
        // Five "boop" tones that get progressively lower & nastier.
        var freqs = new (float f, float dur, float vol)[]
        {
            (440f, 0.12f, 0.35f),
            (330f, 0.16f, 0.45f),
            (247f, 0.22f, 0.6f),
            (185f, 0.30f, 0.8f),
            (138f, 0.45f, 1.0f),
        };
        for (int i = 0; i < freqs.Length; i++)
        {
            var sfx = GenerateTone(freqs[i].f, freqs[i].dur, freqs[i].vol, wave: i >= 3 ? Wave.Square : Wave.Sine);
            var inst = sfx.CreateInstance();
            _boops[i] = inst;
        }

        _scream = GenerateScream().CreateInstance();
        _hum = GenerateHum().CreateInstance();
        if (_hum != null) _hum.IsLooped = true;
    }

    private enum Wave { Sine, Square }

    private static SoundEffect GenerateTone(float hz, float durationSec, float volume, Wave wave)
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * durationSec);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float raw = wave == Wave.Sine
                ? MathF.Sin(2f * MathF.PI * hz * t)
                : (MathF.Sign(MathF.Sin(2f * MathF.PI * hz * t)));
            // simple ADSR-ish envelope
            float env = MathF.Min(1f, i / (sampleRate * 0.01f));
            env *= MathF.Max(0f, 1f - (i / (float)samples));
            data[i] = raw * env * volume;
        }
        return ToSoundEffect(data, sampleRate);
    }

    private static SoundEffect GenerateScream()
    {
        int sampleRate = 44100;
        float dur = 0.9f;
        int samples = (int)(sampleRate * dur);
        var data = new float[samples];
        float startHz = 900f, endHz = 180f;
        for (int i = 0; i < samples; i++)
        {
            float p = i / (float)samples;
            float hz = startHz + (endHz - startHz) * p;
            float vibrato = 1f + 0.15f * MathF.Sin(2f * MathF.PI * 9f * (i / (float)sampleRate));
            float saw = 2f * (hz * (i / (float)sampleRate) % 1f) - 1f;
            float noise = ((float)(_rngGlobal.NextDouble() * 2 - 1)) * 0.4f;
            float env = MathF.Min(1f, i / (sampleRate * 0.02f));
            env *= MathF.Max(0f, 1f - p);
            data[i] = (saw * vibrato + noise) * env * 0.9f;
        }
        return ToSoundEffect(data, sampleRate);
    }

    private static SoundEffect GenerateHum()
    {
        int sampleRate = 44100;
        float dur = 2f;
        int samples = (int)(sampleRate * dur);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float v = MathF.Sin(2f * MathF.PI * 55f * t) * 0.5f
                    + MathF.Sin(2f * MathF.PI * 58f * t) * 0.3f;
            data[i] = v * 0.5f;
        }
        return ToSoundEffect(data, sampleRate);
    }

    private static readonly Random _rngGlobal = new(42);

    private static SoundEffect ToSoundEffect(float[] data, int sampleRate)
    {
        var pcm = new byte[data.Length * 2];
        for (int i = 0; i < data.Length; i++)
        {
            short s = (short)(Math.Clamp(data[i], -1f, 1f) * short.MaxValue);
            pcm[i * 2] = (byte)(s & 0xFF);
            pcm[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }
        return new SoundEffect(pcm, sampleRate, AudioChannels.Mono);
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _gameTime += dt;

        var keys = Keyboard.GetState();
        bool spaceNow = keys.IsKeyDown(Keys.Space);
        bool spaceWas = _prevKeys.IsKeyDown(Keys.Space);

        if (spaceNow && !spaceWas)
        {
            OnSpacePressed();
        }

        // hum swells with anger
        if (_hum != null)
        {
            float target = AngerLevel >= 3 ? 0.05f + AngerLevel * 0.03f : 0f;
            _hum.Volume = MathHelper.Lerp(_hum.Volume, target, dt * 2f);
        }

        _prevKeys = keys;
        base.Update(gameTime);
    }

    private int AngerLevel => Math.Min(_pressCount, Messages.Length - 1);

    private void OnSpacePressed()
    {
        _pressCount++;
        _lastPressAt = _gameTime;

        if (_pressCount <= Messages.Length - 1)
            _currentMessage = Messages[_pressCount];
        else
            _currentMessage = "DO NOT PRESS THE SPACEBAR!!! (we mean it this time)";

        // sound escalation
        int tier = Math.Min(_pressCount - 1, _boops.Length - 1);
        if (tier >= 0 && _boops[tier] != null)
        {
            _boops[tier].Stop();
            _boops[tier].Play();
        }
        if (_pressCount >= 4 && _scream != null)
        {
            _scream.Stop();
            _scream.Volume = Math.Min(1f, 0.3f + _pressCount * 0.05f);
            _scream.Play();
        }
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
        float scaleX = bufW / (float)DrawableBase.LogicWidth;
        float scaleY = bufH / (float)DrawableBase.LogicHeight;
        float fitScale = Math.Min(scaleX, scaleY);
        // center the scaled logic area in the window
        float offX = (bufW - DrawableBase.LogicWidth * fitScale) / 2f;
        float offY = (bufH - DrawableBase.LogicHeight * fitScale) / 2f;
        Matrix transform = Matrix.CreateTranslation(offX, offY, 0) *
                           Matrix.CreateScale(fitScale, fitScale, 1) *
                           Matrix.CreateTranslation(shake.X, shake.Y, 0);

        SceneInfo scene = new(State.Anger, State.PressFlash, State.GameTime);

        // Single batch pass shared by all drawables (sorted by DrawOrder):
        // background/vignette -> sentences -> faces.
        Sprites.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                      SamplerState.PointClamp, transformMatrix: transform);

        foreach (var d in _drawables.OrderBy(d => d.DrawOrder))
            d.Draw(in scene);

        Sprites.End();

        // Note: the framework's own drawable-component loop is intentionally
        // skipped here — every visual is painted inside the single batch above.
        // (Their Draw overrides are no-ops / not driven by base.Draw.)
    }
}
