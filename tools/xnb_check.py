#!/usr/bin/env python3
"""Strict validator for the SpriteFont/Texture2D XNB files produced by
make_font.py. Mirrors MonoGame's ContentManager/SpriteFontReader parsing
step-by-step and fails loudly on any mismatch.

Usage: python3 tools/xnb_check.py [ContentDir]
"""
import struct
import sys
import os


def read7(d, o):
    val = 0
    shift = 0
    while True:
        b = d[o]
        o += 1
        val |= (b & 0x7F) << shift
        if not (b & 0x80):
            return val, o
        shift += 7


def write7(n):
    out = bytearray()
    while True:
        b = n & 0x7F
        n >>= 7
        if n:
            out.append(b | 0x80)
        else:
            out.append(b)
            return bytes(out)


def read_str(d, o):
    ln, o = read7(d, o)
    s = d[o:o + ln].decode("ascii")
    return s, o + ln


def check_header(d):
    """Validate the XNB v5 container header exactly like MonoGame does.

    Layout: 'X','N','B' | version(1) | flags(1) | fileSize(4 LE) | stream...
    Returns the offset where the serialized object stream begins (9).
    """
    assert d[:3] == b"XNB", f"bad magic: {d[:3]!r}"
    assert d[3] == 5, f"format version must be 5, got {d[3]}"
    assert d[4] == 0, f"flags must be 0 (uncompressed/cross-platform), got {d[4]}"
    size_field = struct.unpack("<I", d[5:9])[0]
    assert size_field == len(d), f"header size {size_field} != file size {len(d)}"
    return 9


def check_font(path):
    d = open(path, "rb").read()
    o = check_header(d)
    type_name, o = read_str(d, o)
    assert type_name == "Microsoft.Xna.Framework.Content.SpriteFontReader"
    kl, o = read7(d, o)
    o += kl  # lzx key
    cnt, o = read7(d, o)
    glyphs_end = o + cnt * 44
    x, y, w, h = struct.unpack_from("<iiii", d, o)
    adv, ox, oy = struct.unpack_from("<fff", d, o + 16)
    code = struct.unpack_from("<i", d, o + 32)[0]
    kern_count, _ = read7(d, o + 36)
    assert kern_count == 0
    ls, tex_w, tex_h = struct.unpack_from("<fii", d, glyphs_end)
    ext_type, eo = read7(d, glyphs_end + 12)
    ext_str, eo2 = read_str(d, eo)
    assert eo2 == len(d), f"trailing/missing bytes: parsed to {eo2}, file is {len(d)}"
    print(f"OK font: {cnt} glyphs, atlas {tex_w}x{tex_h}, lineSpacing {ls}")
    print(f"   glyph0 rect {(x,y,w,h)} advance {adv} char {code!r}")
    print(f"   external ref type index {ext_type} ({readers.get(ext_type)}) -> '{ext_str}'")


def check_texture(path):
    d = open(path, "rb").read()
    assert d[:4] == b"XNB\x05"
    assert d[4] == 5, f"version byte must be 5, got {d[4]}"
    assert d[5] == 0
    size_field = struct.unpack("<I", d[6:10])[0]
    assert size_field == len(d), f"header size {size_field} != file size {len(d)}"
    o = 10
    type_name, o = read_str(d, o)
    assert type_name.endswith("Texture2DReader"), type_name
    kl, o = read7(d, o); o += kl
    nr, o = read7(d, o)
    for _ in range(nr):
        idx, o = read7(d, o)
        nm, o = read_str(d, o)
    nsr, o = read7(d, o)
    for _ in range(nsr):
        t, o = read7(d, o)
    mt, o = read7(d, o)
    fmt, w, h = struct.unpack_from("<Bii", d, o)
    mip, o2 = read7(d, o + 9)
    data = d[o2:]
    assert fmt == 4, f"format {fmt} != Color(4)"
    assert w * h * 4 == len(data), f"pixel data {len(data)} != {w}*{h}*4"
    print(f"OK texture: {w}x{h} format {fmt}, {len(data)} pixel bytes, exact EOF")


if __name__ == "__main__":
    cdir = sys.argv[1] if len(sys.argv) > 1 else "Content"
    check_font(os.path.join(cdir, "AngryFont.xnb"))
    check_texture(os.path.join(cdir, "AngryFont_0.xnb"))
