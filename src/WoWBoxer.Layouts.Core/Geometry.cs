namespace WoWBoxer.Layouts.Core;

internal static class Geometry
{
    public const double DefaultMinAspect = 1.25;
    private const double Epsilon = 1e-9;

    /// <summary>Rounds a pixel position. Half-way values go away from zero; tiny float errors are absorbed.</summary>
    public static int Round(double value) => (int)Math.Round(value + (value >= 0 ? Epsilon : -Epsilon), MidpointRounding.AwayFromZero);

    public static int Floor(double value) => (int)Math.Floor(value + Epsilon);

    public static int Ceiling(double value) => (int)Math.Ceiling(value - Epsilon);

    /// <summary>Pixels to percent of a length, so the profile resolves back to the same pixels on this monitor.</summary>
    public static double Percent(double pixels, int length) => length == 0 ? 0 : pixels * 100.0 / length;
}
