"""Renders the State Your Name radar icon and writes it as a multi-size .ico (PNG-compressed entries)."""
import math, struct, sys, zlib

def render(size):
    ss = 4  # supersampling
    n = size * ss
    c = (n - 1) / 2
    px = [[(0.0, 0.0, 0.0, 0.0)] * n for _ in range(n)]
    green, pale = (0.0, 1.0, 0.255), (0.55, 1.0, 0.55)

    def put(x, y, col, a):
        if 0 <= x < n and 0 <= y < n and a > 0:
            r, g, b, pa = px[y][x]
            na = a + pa * (1 - a)
            mix = lambda s, d: (s * a + d * pa * (1 - a)) / na if na else 0
            px[y][x] = (mix(col[0], r), mix(col[1], g), mix(col[2], b), na)

    for y in range(n):
        for x in range(n):
            dx, dy = (x - c) / c, (y - c) / c
            d = math.hypot(dx, dy)
            # rounded-square black tile
            q = max(abs(dx), abs(dy))
            corner = math.hypot(max(abs(dx) - 0.72, 0), max(abs(dy) - 0.72, 0))
            if q <= 1 and corner <= 0.26:
                put(x, y, (0.0, 0.03, 0.01), 1.0)
            else:
                continue
            # star glow + outline + dark fill
            put(x, y, green, max(0.0, 0.35 * (1 - d / 0.42)) ** 1.3)
            if d < 0.2: put(x, y, (0.0, 0.10, 0.02), 1.0)
            ring = lambda r, w, a: put(x, y, green, a * max(0.0, 1 - abs(d - r) / w))
            ring(0.2, 0.045, 1.0)
            ring(0.46, 0.022, 0.55)   # orbit
            ring(0.7, 0.03, 0.35)     # outer orbit (asteroid belt feel)
    # ship on the inner orbit, a planet on the outer one
    for (ang, r, rad, col) in ((-40, 0.46, 0.075, pale), (130, 0.7, 0.1, green)):
        a = math.radians(ang)
        sx, sy = c + math.cos(a) * r * c, c + math.sin(a) * r * c
        for y in range(int(sy - rad * c) - 2, int(sy + rad * c) + 3):
            for x in range(int(sx - rad * c) - 2, int(sx + rad * c) + 3):
                dd = math.hypot(x - sx, y - sy) / c
                put(x, y, col, max(0.0, min(1.0, (rad - dd) * c / 1.5)))
    # downsample
    out = bytearray()
    for y in range(size):
        out.append(0)
        for x in range(size):
            acc = [0.0] * 4
            for j in range(ss):
                for i in range(ss):
                    r, g, b, a = px[y * ss + j][x * ss + i]
                    acc[0] += r * a; acc[1] += g * a; acc[2] += b * a; acc[3] += a
            a = acc[3] / (ss * ss)
            rgb = [(acc[k] / acc[3]) if acc[3] else 0 for k in range(3)]
            out += bytes(int(round(min(1, v) * 255)) for v in (*rgb, a))
    return png(size, bytes(out))

def png(size, raw):
    chunk = lambda t, d: struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")

sizes = [256, 64, 48, 32, 16]
images = [render(s) for s in sizes]
offset = 6 + 16 * len(sizes)
header = struct.pack("<HHH", 0, 1, len(sizes))
entries = b""
for s, img in zip(sizes, images):
    entries += struct.pack("<BBBBHHII", s % 256, s % 256, 0, 0, 1, 32, len(img), offset)
    offset += len(img)
open(sys.argv[1], "wb").write(header + entries + b"".join(images))
open(sys.argv[2], "wb").write(images[0])
