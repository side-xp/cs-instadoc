namespace Sample.Shapes;

/// <summary>
/// A circle, defined by its radius.
/// </summary>
public sealed class Circle : IShape
{
    /// <summary>The radius of the circle.</summary>
    public double Radius { get; init; }

    /// <inheritdoc/>
    public double Area() => System.Math.PI * Radius * Radius;
}
