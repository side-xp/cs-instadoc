namespace Sample.Implicit;

// No `using System.Threading;` here on purpose: CancellationToken only resolves when the implicit global
// usings are injected, mirroring how an ImplicitUsings-enabled project is written.
public class UsesImplicit
{
    public CancellationToken Token { get; set; }
}
