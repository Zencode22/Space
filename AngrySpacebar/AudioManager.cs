using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace AngrySpacebar;

/// <summary>
/// All procedural audio as a MonoGame <see cref="GameComponent"/>: five boop
/// tones that get progressively lower &amp; nastier, a scream that joins in at
/// higher anger, and a looping hum that swells with rage. Reacts to
/// <see cref="GameState.SpacePressed"/> events; no other class needs to know
/// sound generation exists.
/// </summary>
public class AudioManager : GameComponent
{
    private readonly SoundEffectInstance[] _boops = new SoundEffectInstance[5];
    private SoundEffectInstance? _scream;
    private SoundEffectInstance? _hum;

    private GameState _state = null!;
    private bool _audioReady;

    public AudioManager(Game game) : base(game) { }

    public override void Initialize()
    {
        _state = ((Game1)Game).State;
        _state.SpacePressed += OnSpacePressed;

        // NOTE: plain GameComponent has no LoadContent hook (that one only
        // exists on DrawableGameComponent), so the procedural sounds are
        // generated lazily on the first update instead.
        base.Initialize();
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
            var sfx = GenerateTone(freqs[i].f, freqs[i].dur, freqs[i].vol,
                                   wave: i >= 3 ? Wave.Square : Wave.Sine);
            _boops[i] = sfx.CreateInstance();
        }

        _scream = GenerateScream().CreateInstance();
        _hum = GenerateHum().CreateInstance();
        if (_hum != null) _hum.IsLooped = true;

        _hum?.Play();
        _audioReady = true;
    }

    private void OnSpacePressed(int pressCount)
    {
        if (!_audioReady) return;

        // sound escalation
        int tier = Math.Min(pressCount - 1, _boops.Length - 1);
        if (tier >= 0 && _boops[tier] != null)
        {
            _boops[tier].Stop();
            _boops[tier].Play();
        }
        if (pressCount >= 4 && _scream != null)
        {
            _scream.Stop();
            _scream.Volume = Math.Min(1f, 0.3f + pressCount * 0.05f);
            _scream.Play();
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (!_audioReady) BuildAudio();

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // hum swells with anger
        if (_hum != null)
        {
            float target = _state.AngerLevel >= 3 ? 0.05f + _state.AngerLevel * 0.03f : 0f;
            _hum.Volume = MathHelper.Lerp(_hum.Volume, target, dt * 2f);
        }

        base.Update(gameTime);
    }

    // ---- procedural sound generation ----

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
        var rng = _rngGlobal;
        for (int i = 0; i < samples; i++)
        {
            float p = i / (float)samples;
            float hz = startHz + (endHz - startHz) * p;
            float vibrato = 1f + 0.15f * MathF.Sin(2f * MathF.PI * 9f * (i / (float)sampleRate));
            float saw = 2f * (hz * (i / (float)sampleRate) % 1f) - 1f;
            float noise = ((float)(rng.NextDouble() * 2 - 1)) * 0.4f;
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
}
