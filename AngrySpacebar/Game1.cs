using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AngrySpacebar;

/// <summary>
/// "DO NOT PRESS THE SPACEBAR" — a game where the only button you can use is
/// the spacebar, the instructions beg you not to press it, and every press
/// makes the screen angrier: harsher messages, redder background, shaking,
/// distorted angry faces, and escalating sound.
/// </summary>
public class Game1 : Game
{
    private const int LogicWidth = 1280;
    private const int LogicHeight = 720;

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _sb = null!;
    private SpriteText _text = null!;
    private Texture2D _pixel = null!;

    // ---- audio ----
    private readonly SoundEffectInstance[] _boops = new SoundEffectInstance[5];
    private SoundEffectInstance? _scream;
    private SoundEffectInstance? _hum;

    // ---- game state ----
    private static readonly string[] Messages =
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

    private KeyboardState _prevKeys;
    private int _pressCount = 0;
    private double _gameTime = 0;      // seconds since LoadContent
    private double _lastPressAt = -99; // for flash / shake envelope
    private string _currentMessage = Messages[0];

    // jitter that changes once per frame (re-seeded at the top of Draw)
    private readonly Random _jitterRng = new();
    private float _jx1, _jy1, _jr1, _jx2, _jy2, _jr2, _jx3, _jy3, _jr3;
    private void RerollJitter()
    {
        _jx1 = Jitter(); _jy1 = Jitter(); _jr1 = Jitter();
        _jx2 = Jitter(); _jy2 = Jitter(); _jr2 = Jitter();
        _jx3 = Jitter(); _jy3 = Jitter(); _jr3 = Jitter();
    }
    private float Jitter() => (float)(_jitterRng.NextDouble() * 2 - 1);

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            IsFullScreen = false
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
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
        RerollJitter(); // fresh jitter values each frame

        float anger = Math.Min(1f, _pressCount / 15f);           // 0..1 over 15 presses
        // 1 right after a press, decays to 0 over ~0.6s (drives flash + scream mouth)
        float pressFlash = (float)Math.Clamp(1 - (_gameTime - _lastPressAt) / 0.6, 0, 1);
        float shakeMag = anger * 14f * pressFlash + (anger > 0.8f ? 3f : 0f);
        Vector2 shake = new Vector2(
            (float)(Math.Sin(_gameTime * 61) * Math.Cos(_gameTime * 47)),
            (float)(Math.Cos(_gameTime * 53) * Math.Sin(_gameTime * 71))) * shakeMag;

        // background: calm blue -> pure rage red
        Color bg = Color.Lerp(new Color(28, 42, 74), new Color(150, 10, 10), anger);
        // flash brighter right after a press
        bg = Color.Lerp(bg, new Color(255, 60, 60), pressFlash * 0.35f);
        GraphicsDevice.Clear(bg);

        _sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                  SamplerState.PointClamp, transformMatrix: Matrix.CreateTranslation(shake.X, shake.Y, 0));

        // subtle vignette of darkening at high anger
        if (anger > 0.4f)
        {
            float a = (anger - 0.4f) * 0.9f;
            DrawRect(_sb, new Rectangle(-50, -50, GraphicsDevice.PresentationParameters.BackBufferWidth + 100, 60), new Color(0, 0, 0) { A = (byte)(a * 255) });
            DrawRect(_sb, new Rectangle(-50, GraphicsDevice.PresentationParameters.BackBufferHeight - 10, 1380, 60), new Color(0, 0, 0) { A = (byte)(a * 255) });
        }

        float cx = LogicWidth / 2f;
        float cy = LogicHeight / 2f;

        // title
        float titleSize = 64 + anger * 20 + pressFlash * 10;
        Color titleColor = Color.Lerp(Color.White, new Color(255, 80, 80), anger);
        float titleRot = anger * 0.06f * (float)Math.Sin(_gameTime * 8);
        _text.DrawCentered(_sb, "DO NOT PRESS THE SPACEBAR",
            new Vector2(cx, 90), titleSize, titleColor, titleRot);

        // instruction line
        _text.DrawCentered(_sb, "The only button you can use is [SPACE]. Don't.",
            new Vector2(cx, 170), 30, Color.Lerp(Color.LightGray, Color.OrangeRed, anger));

        // current message (the angry voice), wrapped
        float msgSize = 44 + anger * 18;
        Color msgColor = Color.Lerp(new Color(220, 220, 255), Color.Red, anger);
        DrawWrapped(_sb, _currentMessage, new Vector2(cx, cy - 60), msgSize, msgColor, maxWidth: 1100,
                    jitter: anger * 6f, rotBase: anger * 0.03f);

        // press counter
        _text.DrawCentered(_sb, $"Spacebar presses: {_pressCount}",
            new Vector2(cx, LogicHeight - 60), 34, Color.Lerp(Color.Gray, Color.Yellow, anger));

        // angry face(s)
        DrawFace(_sb, new Vector2(cx, cy + 190), scale: 1f + anger * 0.6f, anger, pressFlash);

        // extra mini-faces at high anger
        if (anger > 0.5f)
        {
            DrawFace(_sb, new Vector2(cx - 330, cy + 210), 0.6f, anger, pressFlash);
            DrawFace(_sb, new Vector2(cx + 330, cy + 210), 0.6f, anger, pressFlash);
        }

        _sb.End();
        base.Draw(gameTime);
    }

    private void DrawWrapped(SpriteBatch sb, string text, Vector2 centerTop, float size,
                             Color color, float maxWidth, float jitter, float rotBase)
    {
        // naive word wrap
        var words = text.Split(' ');
        var lines = new List<string>();
        string cur = "";
        foreach (var w in words)
        {
            string test = cur.Length == 0 ? w : cur + " " + w;
            if (_text.Measure(test, size) > maxWidth)
            {
                lines.Add(cur);
                cur = w;
            }
            else cur = test;
        }
        if (cur.Length > 0) lines.Add(cur);

        // per-line jitter values (rolled once per frame in Draw)
        ReadOnlySpan<float> jxs = [_jx1, _jx2, _jx3];
        ReadOnlySpan<float> jys = [_jy1, _jy2, _jy3];
        ReadOnlySpan<float> jrs = [_jr1, _jr2, _jr3];

        float lineH = size * 1.4f;
        float y = centerTop.Y - (lines.Count - 1) * lineH / 2f;
        for (int i = 0; i < lines.Count; i++)
        {
            int k = Math.Min(i, 2);
            float jx = jxs[k] * jitter;
            float jy = jys[k] * jitter;
            float rot = jrs[k] * rotBase;
            _text.DrawCentered(sb, lines[i], new Vector2(centerTop.X + jx, y + jy), size, color, rot);
            y += lineH;
        }
    }

    private void DrawFace(SpriteBatch sb, Vector2 pos, float scale, float anger, float pressFlash)
    {
        float s = 90 * scale;
        Color skin = Color.Lerp(new Color(255, 214, 165), new Color(220, 40, 40), anger);
        Color feature = Color.Black;

        // head circle
        DrawCircleFilled(sb, pos, s, skin);

        // eyebrows slant inward when angry
        float browAngle = MathHelper.Lerp(-0.15f, 0.6f, anger); // radians tilt
        float browY = pos.Y - s * 0.35f;
        float browLen = s * 0.42f;
        DrawThickLine(sb, new Vector2(pos.X - s * 0.55f, browY), browLen, browAngle, s * 0.12f, feature);
        DrawThickLine(sb, new Vector2(pos.X + s * 0.15f, browY), browLen, -browAngle, s * 0.12f, feature);

        // eyes: shrink with anger
        float eyeR = MathHelper.Lerp(s * 0.18f, s * 0.08f, anger);
        DrawCircleFilled(sb, new Vector2(pos.X - s * 0.35f, pos.Y - s * 0.05f), eyeR, feature);
        DrawCircleFilled(sb, new Vector2(pos.X + s * 0.35f, pos.Y - s * 0.05f), eyeR, feature);

        // mouth: smile -> flat line -> screaming open oval
        if (anger < 0.35f)
        {
            // gentle smile: three segments
            DrawThickLine(sb, new Vector2(pos.X - s * 0.4f, pos.Y + s * 0.38f), s * 0.28f, 0.35f, s * 0.09f, feature);
            DrawThickLine(sb, new Vector2(pos.X - s * 0.14f, pos.Y + s * 0.47f), s * 0.28f, 0f, s * 0.09f, feature);
            DrawThickLine(sb, new Vector2(pos.X + s * 0.12f, pos.Y + s * 0.38f), s * 0.28f, -0.35f, s * 0.09f, feature);
        }
        else if (anger < 0.5f)
        {
            DrawThickLine(sb, new Vector2(pos.X - s * 0.4f, pos.Y + s * 0.45f), s * 0.8f,
                          0f, s * 0.1f, feature);
        }
        else
        {
            // scream mouth
            float openness = MathHelper.Lerp(0.2f, 0.6f, (anger - 0.5f) * 2f) * (1f + pressFlash * 0.5f);
            DrawEllipseFilled(sb, new Vector2(pos.X, pos.Y + s * 0.5f), s * 0.35f, s * openness * 0.5f, feature);
        }

        // steam puffs at high anger
        if (anger > 0.6f)
        {
            float puff = (float)(Math.Sin(_gameTime * 10) * 0.5 + 0.5);
            DrawCircleFilled(sb, new Vector2(pos.X - s * 0.9f, pos.Y - s * (1.1f + puff * 0.2f)), s * 0.15f,
                             Color.White * (0.3f + puff * 0.4f));
            DrawCircleFilled(sb, new Vector2(pos.X + s * 0.9f, pos.Y - s * (1.1f + (1 - puff) * 0.2f)), s * 0.15f,
                             Color.White * (0.3f + (1 - puff) * 0.4f));
        }
    }

    // --- primitive drawing helpers using the white pixel texture ---

    private void DrawRect(SpriteBatch sb, Rectangle r, Color c)
    {
        sb.Draw(_pixel, r, c);
    }

    private void DrawCircleFilled(SpriteBatch sb, Vector2 center, float radius, Color c)
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

    private void DrawEllipseFilled(SpriteBatch sb, Vector2 center, float rx, float ry, Color c)
    {
        int steps = Math.Max(8, (int)(rx / 2f));
        for (int i = 0; i <= steps; i++)
        {
            float dx = -rx + 2f * rx * i / steps;
            float h = (float)Math.Sqrt(Math.Max(0f, 1 - (dx * dx) / (rx * rx))) * ry;
            sb.Draw(_pixel, new Rectangle((int)(center.X + dx), (int)(center.Y - h), 2, (int)(h * 2)), c);
        }
    }

    private void DrawThickLine(SpriteBatch sb, Vector2 start, float length, float angleRad, float thickness, Color c)
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
}