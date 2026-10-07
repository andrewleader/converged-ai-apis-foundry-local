using System.Numerics;

namespace Microsoft.AI.Local;

/// <summary>A point in pixel coordinates.</summary>
/// <param name="X">The horizontal coordinate.</param>
/// <param name="Y">The vertical coordinate.</param>
public readonly record struct ImagePoint(int X, int Y);

/// <summary>An axis-aligned rectangle in pixel coordinates.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
public readonly record struct ImageRect(int X, int Y, int Width, int Height);

/// <summary>
/// A (possibly rotated) quadrilateral in pixel coordinates, for example around a line of recognized text.
/// </summary>
/// <param name="TopLeft">The top-left corner.</param>
/// <param name="TopRight">The top-right corner.</param>
/// <param name="BottomRight">The bottom-right corner.</param>
/// <param name="BottomLeft">The bottom-left corner.</param>
public readonly record struct ImageQuad(Vector2 TopLeft, Vector2 TopRight, Vector2 BottomRight, Vector2 BottomLeft)
{
    /// <summary>Gets the smallest axis-aligned rectangle that contains the quadrilateral.</summary>
    public ImageRect Bounds
    {
        get
        {
            var minX = MathF.Min(MathF.Min(TopLeft.X, TopRight.X), MathF.Min(BottomRight.X, BottomLeft.X));
            var minY = MathF.Min(MathF.Min(TopLeft.Y, TopRight.Y), MathF.Min(BottomRight.Y, BottomLeft.Y));
            var maxX = MathF.Max(MathF.Max(TopLeft.X, TopRight.X), MathF.Max(BottomRight.X, BottomLeft.X));
            var maxY = MathF.Max(MathF.Max(TopLeft.Y, TopRight.Y), MathF.Max(BottomRight.Y, BottomLeft.Y));
            var x = (int)MathF.Floor(minX);
            var y = (int)MathF.Floor(minY);
            return new ImageRect(x, y, (int)MathF.Ceiling(maxX) - x, (int)MathF.Ceiling(maxY) - y);
        }
    }
}
