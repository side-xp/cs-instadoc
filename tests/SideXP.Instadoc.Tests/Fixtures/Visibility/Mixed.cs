namespace Sample.Visibility;

/// <summary>
/// A public type whose members span every visibility level, so extraction can prove the filter.
/// </summary>
public class Visible
{
    /// <summary>An explicit public constructor (the implicit one would be filtered out as compiler-generated).</summary>
    public Visible() { }

    public int PublicProperty { get; set; }

    protected int ProtectedProperty { get; set; }

    internal int InternalProperty { get; set; }

    private int _privateField;

    // No doc comment on purpose: it must still be selected, since enumeration works from symbols, not XML docs.
    public void PublicMethod() { }

    private void PrivateMethod() { }
}

/// <summary>
/// An internal type: it must be excluded when only public/protected are requested.
/// </summary>
internal class Hidden
{
}

/// <summary>
/// A public type with a public nested type, to prove nested types become their own pages (not members of the parent).
/// </summary>
public class Outer
{
    /// <summary>A public nested type.</summary>
    public class Inner
    {
    }
}
