namespace WoWBoxer.Layouts.Core.Tests;

public class WowConfigAndPngTests
{
    [Fact]
    public void Reads_set_lines_from_config_wtf()
    {
        const string text = "SET RenderScale \"1\"\r\nSET gxWindowedResolution \"870x696\"\r\nSET gxMaximize \"0\"\r\nnot a set line\r\n";

        var values = WowConfig.Parse(text);

        Assert.Equal("870x696", values["gxwindowedresolution"]);
        Assert.Equal("0", values["gxMaximize"]);
        Assert.Equal((870, 696), WowConfig.ParseSize(values["gxWindowedResolution"]));
        Assert.Null(WowConfig.ParseSize("auto"));
    }

    [Fact]
    public void Png_has_signature_header_and_end()
    {
        var pixels = new byte[3 * 2 * 4];
        pixels[2] = 255;   // first pixel red (BGRA)
        using var stream = new MemoryStream();

        PngWriter.Write(stream, 3, 2, pixels);
        var png = stream.ToArray();

        Assert.Equal([0x89, 0x50, 0x4E, 0x47], png[..4]);
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal("IEND", System.Text.Encoding.ASCII.GetString(png, png.Length - 8, 4));
        // CRC of an empty IEND chunk is a well-known constant.
        Assert.Equal([0xAE, 0x42, 0x60, 0x82], png[^4..]);
    }
}
