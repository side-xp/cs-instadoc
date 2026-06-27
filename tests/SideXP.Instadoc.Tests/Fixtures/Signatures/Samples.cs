namespace Sample.Signatures;

/// <summary>A record, used to check the compiler-synthesized IEquatable is not shown in the signature.</summary>
public record Point(int X, int Y);

/// <summary>A type with a value-type parameter that defaults to <c>default</c>.</summary>
public class Worker
{
    /// <summary>Runs the work, optionally cancellable.</summary>
    public void Run(System.Threading.CancellationToken cancellationToken = default) { }
}
