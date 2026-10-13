using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace DontPressSpace;

/// <summary>
/// Tiny dependency-free sound engine. Synthesizes short "angry buzz" PCM clips
/// and plays them through ALSA (via a direct P/Invoke of libasound.so.2) when
/// available. If no audio device exists, everything degrades to silence and
/// the game simply shouts at you wordlessly — which is arguably worse.
/// </summary>
public sealed class Sound : IDisposable
{
    private static readonly ConcurrentQueue<byte[]> Queue = new();
    private static Thread? _worker;
    private static volatile bool _audioBroken;

    public void PlayBuzz(double frequencyHz, double durationSeconds, double volume)
    {
        if (_audioBroken) return;

        lock (typeof(Sound))
        {
            if (_worker is null or { ThreadState: ThreadState.Unstarted })
                (_worker = new Thread(Pump) { IsBackground = true, Name = "angry-buzz" }).Start();
        }

        Queue.Enqueue(Synthesize(frequencyHz, durationSeconds, Math.Clamp(volume, 0, 1)));
    }

    // ---- synthesis ----------------------------------------------------------
    private static byte[] Synthesize(double freq, double dur, double vol)
    {
        const int rate = 44100;
        int samples = (int)(rate * Math.Clamp(dur, 0.02, 1.5));
        var bytes = new byte[samples * 2]; // 16-bit mono little-endian

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / rate;
            double phase = 2 * Math.PI * freq * t;
            // Square-ish angry buzz + crunchy harmonic + a little grit.
            double wave = Math.Sign(Math.Sin(phase)) * 0.6
                        + Math.Sin(phase * 2.5) * 0.3
                        + (Random.Shared.NextDouble() * 2 - 1) * 0.1;
            double env = Math.Min(1.0, 40 * t) * Math.Max(0.0, 1.0 - t / dur);
            short s = (short)(Math.Clamp(wave * env * vol, -1, 1) * short.MaxValue);
            bytes[i * 2] = (byte)(s & 0xFF);
            bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }
        return bytes;
    }

    // ---- ALSA via raw P/Invoke (no dev headers needed) -----------------------
    private static class Alsa
    {
        private const string Lib = "libasound.so.2";

        [DllImport(Lib)] private static extern IntPtr snd_pcm_open(
            out IntPtr pcm, [MarshalAs(UnmanagedType.LPStr)] string name, int mode, int stream);
        [DllImport(Lib)] private static extern int snd_pcm_set_params(
            IntPtr pcm, int format, int access, int channels, int rate, int soft_resample, uint latency);
        [DllImport(Lib)] private static extern long snd_pcm_writei(
            IntPtr pcm, byte[] buffer, long size);
        [DllImport(Lib)] private static extern int snd_pcm_drain(IntPtr pcm);
        [DllImport(Lib)] private static extern int snd_pcm_close(IntPtr pcm);

        // SND_PCM_STREAM_PLAYBACK = 0, SND_PCM_FORMAT_S16_LE = 2,
        // SND_PCM_ACCESS_RW_INTERLEAVED = 3
        public static IntPtr Open()
        {
            if (snd_pcm_open(out var pcm, "default", 0, 0) < 0) return IntPtr.Zero;
            if (snd_pcm_set_params(pcm, 2, 3, 1, 44100, 1, 100000) < 0)
            {
                snd_pcm_close(pcm);
                return IntPtr.Zero;
            }
            return pcm;
        }

        public static bool Write(IntPtr pcm, byte[] data)
        {
            long frames = data.Length / 2;
            long done = 0;
            while (done < frames)
            {
                long r = snd_pcm_writei(pcm, data, frames - done);
                if (r == -11 /*EAGAIN*/) { Thread.Sleep(5); continue; }
                if (r < 0) return false;  // device hiccup; caller decides
                done += r;
            }
            return true;
        }

        public static void Close(IntPtr pcm) { snd_pcm_drain(pcm); snd_pcm_close(pcm); }
    }

    private static void Pump()
    {
        IntPtr pcm = IntPtr.Zero;
        try { pcm = Alsa.Open(); }
        catch { _audioBroken = true; return; }   // libasound missing → silence

        if (pcm == IntPtr.Zero) { _audioBroken = true; return; }

        try
        {
            while (true)
            {
                if (Queue.TryDequeue(out var chunk))
                {
                    if (!Alsa.Write(pcm, chunk)) break;
                }
                else Thread.Sleep(10);
            }
        }
        catch { /* stay silent */ }
        finally
        {
            _audioBroken = true;
            try { Alsa.Close(pcm); } catch { }
        }
    }

    public void Dispose() { /* background thread dies with the app */ }
}
