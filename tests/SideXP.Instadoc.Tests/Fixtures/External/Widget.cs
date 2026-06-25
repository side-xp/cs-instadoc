namespace Sample.External;

/// <summary>
/// References a type from an assembly that is not on the reference path (no Unity here), so its <c>Target</c> property
/// type cannot be resolved and must come back as an error symbol.
/// </summary>
public sealed class Widget
{
    /// <summary>A reference to an external, unresolvable type.</summary>
    public UnityEngine.GameObject Target { get; set; }
}