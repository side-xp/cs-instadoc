namespace Sample.Invalid;

// Deliberately malformed C#: Roslyn must still produce a syntax tree (with diagnostics), never throw.
public class Broken
{
    public void Oops(
}
