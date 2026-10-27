namespace WoWBoxer.Layouts.Core;

/// <summary>A rectangle in physical pixels (virtual-screen coordinates, Per-Monitor-V2).</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public double Aspect => Height == 0 ? 0 : (double)Width / Height;

    public bool Contains(PixelRect other) =>
        other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;

    public bool Overlaps(PixelRect other) =>
        X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    public override string ToString() => $"{X},{Y} {Width}x{Height}";
}
