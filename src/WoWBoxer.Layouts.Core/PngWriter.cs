using System.Buffers.Binary;
using System.IO.Compression;

namespace WoWBoxer.Layouts.Core;

/// <summary>Minimal PNG encoder (8-bit RGB) for window snapshots, so we need no imaging package.</summary>
public static class PngWriter
{
    /// <param name="bgra">Top-down 32-bit BGRA pixels (Windows DIB order), <paramref name="width"/>*4 bytes per row.</param>
    public static void Write(Stream output, int width, int height, ReadOnlySpan<byte> bgra)
    {
        if (bgra.Length < width * height * 4) throw new ArgumentException("Pixel buffer too small.", nameof(bgra));
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8;   // bit depth
        header[9] = 2;   // colour type RGB
        WriteChunk(output, "IHDR", header);

        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Fastest, leaveOpen: true))
        {
            var row = new byte[1 + width * 3];   // filter byte 0 + RGB
            for (var y = 0; y < height; y++)
            {
                var src = bgra.Slice(y * width * 4, width * 4);
                for (int x = 0, o = 1; x < width; x++, o += 3)
                {
                    row[o] = src[x * 4 + 2];
                    row[o + 1] = src[x * 4 + 1];
                    row[o + 2] = src[x * 4];
                }
                z.Write(row);
            }
        }
        WriteChunk(output, "IDAT", raw.ToArray());
        WriteChunk(output, "IEND", []);
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        output.Write(buffer);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crc = Crc(Crc(0xFFFFFFFFu, typeBytes), data) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(buffer, crc);
        output.Write(buffer);
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    private static uint Crc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}
