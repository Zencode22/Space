using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SpriteFontTool
{
    /// <summary>
    /// Bakes a MonoGame sprite font .xnb from a PIL-generated atlas PNG + metrics JSON.
    /// Usage: dotnet run -- <metrics.json> <atlas.png> <outDir>
    /// metrics.json: { "lineSpacing": f, "glyphs": [ {"c":"A","x":..,"y":..,"w":..,"h":..,"xa":..,"xo":..,"yo":..}, ... ] }
    /// </summary>
    internal static class Program
    {
        private struct Glyph
        {
            public string c; public int x, y, w, h; public float xa, xo, yo;
            public int[] kern; // 4 zeros normally
        }

        private static int Main(string[] args)
        {
            if (args.Length < 3) { Console.Error.WriteLine("usage: tool <metrics.json> <atlas.png> <outDir>"); return 1; }

            using var doc = JsonDocument.Parse(File.ReadAllText(args[0]));
            float lineSpacing = doc.RootElement.GetProperty("lineSpacing").GetSingle();
            int texW = doc.RootElement.GetProperty("textureWidth").GetInt32();
            int texH = doc.RootElement.GetProperty("textureHeight").GetInt32();
            var root = doc.RootElement.GetProperty("glyphs");
            int n = root.GetArrayLength();

            var list = new List<(string c, int x, int y, int w, int h, float xa, float xo, float yo)>();
            foreach (var g in root.EnumerateArray())
                list.Add((g.GetProperty("c").GetString() ?? "", g.GetProperty("x").GetInt32(),
                          g.GetProperty("y").GetInt32(), g.GetProperty("w").GetInt32(),
                          g.GetProperty("h").GetInt32(), g.GetProperty("xa").GetSingle(),
                          g.GetProperty("xo").GetSingle(), g.GetProperty("yo").GetSingle()));

            Directory.CreateDirectory(args[2]);

            using (var fs = File.Create(Path.Combine(args[2], "AngryFont.xnb")))
            using (var w = new BinaryWriter(fs))
            {
                w.Write(new byte[] { (byte)'X', (byte)'N', (byte)'B', 5, 0, 0, 0, 0 });
                WriteString(w, "Microsoft.Xna.Framework.Content.SpriteFontReader");
                WriteString(w, "");                    // LzxKey
                w.Write(n);                             // character count
                foreach (var g in list)
                {
                    w.Write(g.x); w.Write(g.y); w.Write(g.w); w.Write(g.h);
                    w.Write(g.xa); w.Write(g.xo); w.Write(g.yo);
                    for (int k = 0; k < 4; k++) w.Write(0f); // Keringing (4 floats)
                }
                w.Write(lineSpacing);
                w.Write(texW); w.Write(texH);
                WriteExternal(w, 0);                    // Texture external reference
            }

            // Copy the pre-baked texture xnb produced by the python stage.
            string texDir = Path.GetDirectoryName(args[1]) ?? ".";
            string srcTex = Path.Combine(texDir, "AngryFont_0.xnb");
            if (File.Exists(srcTex))
                File.Copy(srcTex, Path.Combine(args[2], "AngryFont_0.xnb"), true);

            Console.WriteLine($"OK: {n} glyphs -> {args[2]}");
            return 0;
        }

        private static void WriteString(BinaryWriter w, string s)
        {
            w.Write((7u << 24) | (uint)s.Length);
            w.Write(Encoding.UTF8.GetBytes(s));
        }

        private static void WriteExternal(BinaryWriter w, int index)
        {
            w.Write((7u << 24) | (uint)index);
        }
    }
}
