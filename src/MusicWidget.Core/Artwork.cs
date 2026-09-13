using System.Buffers.Binary;
using System.IO.Compression;

namespace MusicWidget;

// Tiny PNGs avoid theme-dependent padding in Adaptive Card progress-bar containers.
// Everything is generated locally; no external asset requests or drawing dependencies.
public static class Artwork
{
    public static readonly string Placeholder = Png(240, 240, (x, y) => {
        var dx = x - 120; var dy = y - 120;
        var r = Math.Sqrt(dx * dx + dy * dy);
        if (r < 77 && r > 70) return (220, 95, 97, 255);
        if (x >= 105 && x <= 146 && Math.Abs(y - 120) < (146 - x) * 0.66) return (238, 223, 220, 255);
        return (38 + y / 12, 36 + x / 22, 44 + y / 16, 255);
    });
    internal static string Png(int width, int height, Func<int, int, (int R, int G, int B, int A)> pixel)
    {
        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8; header[9] = 6;
        Chunk(png, "IHDR", header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true)) {
            for (var y = 0; y < height; y++) {
                zlib.WriteByte(0);
                for (var x = 0; x < width; x++) { var c = pixel(x, y); zlib.Write([(byte)c.R, (byte)c.G, (byte)c.B, (byte)c.A]); }
            }
        }
        Chunk(png, "IDAT", compressed.ToArray()); Chunk(png, "IEND", []);
        return "data:image/png;base64," + Convert.ToBase64String(png.ToArray());
    }
    static void Chunk(Stream output, string type, byte[] data)
    {
        Span<byte> value = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(value, data.Length); output.Write(value);
        var name = System.Text.Encoding.ASCII.GetBytes(type); output.Write(name); output.Write(data);
        uint crc = 0xffffffff;
        foreach (var bytes in new[] { name, data }) foreach (var b in bytes) {
            crc ^= b;
            for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? 0xedb88320 ^ (crc >> 1) : crc >> 1;
        }
        BinaryPrimitives.WriteUInt32BigEndian(value, crc ^ 0xffffffff); output.Write(value);
    }
}

