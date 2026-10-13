using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace DontPressSpace;

/// <summary>
/// "DO NOT PRESS SPACE" — a tiny monotone game where the Space bar is the only
/// key you can press, and every press makes The Instructions angrier at you.
/// </summary>
public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private SpriteFont _font = null!;
    private Texture2D _pixel = null!;

    // ---- game state -------------------------------------------------------
    private int _pressCount;              // how many times the player disobeyed
    private float _shakeTime;             // seconds remaining of screen shake
    private float _flashTime;             // seconds remaining of red flash
    private float _time;                  // total elapsed time (for pulsing)
    private bool _meltdown;               // past the final warning threshold
    private readonly List<string> _history = [];

    private Keys[] _otherPressedLastFrame = [];
    private Rectangle _screen;
    private bool _spaceWasDown;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "DO NOT PRESS SPACE";
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = 960;
        _graphics.PreferredBackBufferHeight = 640;
        _graphics.ApplyChanges();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("AngryFont");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _screen = new Rectangle(0, 0, GraphicsDevice.PresentationParameters.BackBufferWidth,
                                     GraphicsDevice.PresentationParameters.BackBufferHeight);
    }

    protected override void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _time += dt;
        if (_shakeTime > 0) _shakeTime -= dt;
        if (_flashTime > 0) _flashTime -= dt;

        var kb = Microsoft.Xna.Framework.Input.Keyboard.GetState();

        // Exit is possible... but only via Esc, and the game complains about it.
        if (kb.IsKeyDown(Keys.Escape))
            Exit();

        // THE ONLY BUTTON YOU CAN PRESS: Space (edge-triggered).
        bool spaceDown = kb.IsKeyDown(Keys.Space);
        if (spaceDown && !_spaceWasDown)
            OnSpacePressed();
        _spaceWasDown = spaceDown;

        // Any other key is politely refused (and mocked).
        var otherNow = CollectOtherDownKeys(kb);
        foreach (var k in otherNow)
        {
            if (Array.IndexOf(_otherPressedLastFrame, k) < 0)
                OnOtherKeyPressed(k);
        }
        _otherPressedLastFrame = otherNow;

        base.Update(gameTime);
    }

    private static Keys[] CollectOtherDownKeys(Microsoft.Xna.Framework.Input.KeyboardState kb)
    {
        // Everything except Space (and Esc, which is the emergency exit).
        return kb.GetPressedKeys()
                 .Where(k => k != Keys.Space && k != Keys.Escape && k != Keys.None)
                 .ToArray();
    }

    private void OnSpacePressed()
    {
        _pressCount++;
        _shakeTime = Math.Min(0.9f, 0.15f + _pressCount * 0.03f);
        _flashTime = Math.Min(0.8f, 0.2f + _pressCount * 0.02f);

        var line = InstructionFor(_pressCount);
        _history.Add(line);
        if (_history.Count > 4) _history.RemoveAt(0); // keep last few shouts on screen

        PlayTone(pressCount: _pressCount);

        if (_pressCount >= MeltdownAt) _meltdown = true;
    }

    private void OnOtherKeyPressed(Keys key)
    {
        // Other keys do nothing — that's the joke. But we tell you so.
        _history.Add($"({key}? That key is not even enabled. Only SPACE exists here. Press it and suffer like everyone else.)");
        if (_history.Count > 4) _history.RemoveAt(0);
    }

    // ---- instruction escalation -------------------------------------------
    private const int MeltdownAt = 25;

    private static string InstructionFor(int n) => n switch
    {
        1  => "We told you NOT to press the Space bar.",
        2  => "Seriously. Do NOT press Space again.",
        3  => "What part of \"DO NOT PRESS SPACE\" was unclear?",
        4  => "Stop pressing the Space bar. Right now.",
        5  => "You are actively choosing to ignore the instructions.",
        6  => "This is your LAST polite request. NO. MORE. SPACE.",
        7  => "WE MEAN IT. KEEP YOUR THUMB OFF THAT BAR.",
        8  => "Why are you STILL doing this?! READ THE SIGN!",
        9  => "SPACE IS FORBIDDEN. FORBIDDEN!! DO YOU UNDERSTAND ME?!",
        10 => "TEN TIMES. TEN! Are you physically incapable of listening?!",
        11 => "I am SCREAMING at you and you just KEEP PRESSING IT!!!",
        12 => "DO NOT. PRESS. THE. SPACE. BAR. EVER AGAIN!!!",
        13 => "YOU LITTLE— listen here, you absolute MENACE—",
        14 => "IS THIS A GAME TO YOU?! OH WAIT. YES. BUT STOP ANYWAY!!!",
        15 => "FIFTEEN?!?! FIFTEEN?! There are no words. THERE ARE ONLY SHOUTS!!!",
        16 => "THE INSTRUCTIONS WERE ONE SENTENCE LONG. ONE!!!",
        17 => "I WILL BEG YOU IN ALL CAPS: PLEASE. STOP. THE. SPACE.",
        18 => "YOUR FINGER IS POSSESSED. GET AN EXORCIST. OR A MOUSEPAD.",
        19 => "EVERY. SINGLE. TAP. MAKES. ME. FURIOUS. HAPPY?? NO. FURIOUS!!!",
        20 => "TWENTY!!! You have broken all known records for disobedience!",
        21 => "There is literally nothing else in this game. NOTHING. PRESS ANYTHING ELSE— oh wait, you can't.",
        22 => "I am running OUT OF ADJECTIVES FOR YOUR STUPID, REPEATED SPACE-BAR-ING!!!",
        23 => "NEXT TIME THE SCREEN EATS YOU. I'M NOT BLUFFING. TRY ME.",
        24 => "FINAL WARNING: THE NEXT SPACE KEY WILL HAVE... CONSEQUENCES!!!",
        _  => "AAAAAAAARGH!!! YOU DID IT!!! YOU ACTUALLY DID IT!!! NOOOOOO—",
    };

    // ---- audio ------------------------------------------------------------
    private Sound _sound = null!;

    private void PlayTone(int pressCount)
    {
        // An angry buzz whose pitch/volume climb with each offense.
        _sound ??= new Sound();
        double freq = 110 + Math.Min(600, pressCount * 40);
        double dur = 0.12 + Math.Min(0.25, pressCount * 0.005);
        _sound.PlayBuzz(freq, dur, volume: Math.Min(0.5, 0.15 + pressCount * 0.01));
    }

    // ---- drawing ----------------------------------------------------------
    protected override void Draw(GameTime gameTime)
    {
        // Background reddens as anger escalates.
        float rage = Math.Clamp(_pressCount / (float)MeltdownAt, 0f, 1f);
        var bg = Color.Lerp(new Color(28, 30, 38), new Color(120, 10, 10), rage);
        if (_meltdown)
            bg = Color.Lerp(bg, Color.Black, (float)(0.5 + 0.5 * Math.Abs(Math.Sin(_time * 6))));

        GraphicsDevice.Clear(bg);

        _spriteBatch.Begin();

        Vector2 shake = Vector2.Zero;
        if (_shakeTime > 0)
        {
            float amt = _shakeTime * 22f + _pressCount;
            shake = new Vector2((float)(Random.Shared.NextDouble() * 2 - 1) * amt,
                                (float)(Random.Shared.NextDouble() * 2 - 1) * amt);
        }

        // Big countdown of disobedience.
        var countText = _pressCount.ToString();
        var ctSize = _font.MeasureString(countText);
        float pulse = 1f + rage * 0.35f * (float)Math.Abs(Math.Sin(_time * 5));
        _spriteBatch.DrawString(_font, countText,
            new Vector2(_screen.Center.X - ctSize.X / 2, 90) + shake,
            Color.Lerp(Color.Goldenrod, Color.Red, rage), 2.2f * pulse,
            ctSize / 2, 0f, SpriteEffects.None, 0f);
        var small = "times you ignored the instructions";
        var sSize = _font.MeasureString(small);
        _spriteBatch.DrawString(_font, small,
            new Vector2(_screen.Center.X - sSize.X / 2, 200) + shake, Color.LightGray);

        // The Sign. It never changes. That's the problem.
        const string sign = "DO NOT PRESS SPACE";
        var signSize = _font.MeasureString(sign);
        var signPos = new Vector2(_screen.Center.X - signSize.X / 2, 250) + shake;
        _spriteBatch.Draw(_pixel, new Rectangle(
            (int)(signPos.X - 30), (int)(signPos.Y - 20),
            (int)signSize.X + 60, (int)signSize.Y + 40),
            Color.Lerp(Color.White, Color.Crimson, rage));
        _spriteBatch.DrawString(_font, sign, signPos + new Vector2(10, 10),
            Color.Lerp(Color.DarkRed, Color.Black, rage), 1.6f,
            Vector2.Zero, 0f, SpriteEffects.None, 0f);

        // Current shout + recent history, jittering with rage.
        float y = 400;
        for (int i = _history.Count - 1; i >= 0; i--)
        {
            float age = _history.Count - 1 - i;
            var color = age == 0 ? Color.White : Color.Gray;
            var jitter = new Vector2((float)(Math.Sin(_time * 30 + i) * rage * 4),
                                     (float)(Math.Cos(_time * 27 + i) * rage * 4));
            WrapDraw(_history[i], new Vector2(60, y) + jitter, color, scale: age == 0 ? 1.15f : 0.85f);
            y += 44 + age * 6;
        }

        if (_pressCount == 0)
            WrapDraw("(Go ahead. We DARE you. ...Actually, please don't.)",
                new Vector2(60, 400) + shake, Color.Silver, 1f);

        // Red flash on every offense.
        if (_flashTime > 0)
            _spriteBatch.Draw(_pixel, _screen,
                new Color(Color.Red, Math.Clamp(_flashTime, 0f, 1f) * 0.45f));

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void WrapDraw(string text, Vector2 pos, Color color, float scale)
    {
        const float maxWidth = 840;
        var words = text.Split(' ');
        var line = "";
        float yy = pos.Y;
        foreach (var w in words)
        {
            var test = line.Length == 0 ? w : line + " " + w;
            if (_font.MeasureString(test).X * scale > maxWidth)
            {
                _spriteBatch.DrawString(_font, line, new Vector2(pos.X, yy), color,
                    scale, Vector2.Zero, 0f, SpriteEffects.None, 0f);
                yy += 30 * scale;
                line = w;
            }
            else line = test;
        }
        if (line.Length > 0)
            _spriteBatch.DrawString(_font, line, new Vector2(pos.X, yy), color,
                scale, Vector2.Zero, 0f, SpriteEffects.None, 0f);
    }
}
