namespace Sample.Inheritance;

/// <summary>
/// A base widget, with documentation that derived types inherit via <c>&lt;inheritdoc/&gt;</c>.
/// </summary>
public abstract class WidgetBase
{
    /// <summary>Refreshes the widget's state from its source.</summary>
    /// <param name="force">When <see langword="true"/>, refresh even if nothing changed.</param>
    /// <returns>The number of fields that were updated.</returns>
    public abstract int Refresh(bool force);

    /// <summary>Gets or sets the human-readable display name.</summary>
    public abstract string Name { get; set; }
}

/// <summary>A concrete widget whose members inherit their documentation.</summary>
public sealed class Widget : WidgetBase
{
    /// <inheritdoc/>
    public override int Refresh(bool force) => 0;

    /// <inheritdoc/>
    public override string Name { get; set; } = string.Empty;

    /// <summary>The widget's identifier.</summary>
    public int Id { get; init; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>
/// A gauge contract, documented so implementers can inherit it.
/// </summary>
public interface IGauge
{
    /// <summary>Gets the current reading, in bars.</summary>
    double Reading { get; }

    /// <summary>Resets the gauge back to zero.</summary>
    void Reset();
}

/// <summary>A pressure gauge whose members inherit the interface documentation.</summary>
public sealed class PressureGauge : IGauge
{
    /// <inheritdoc/>
    public double Reading => 0;

    /// <inheritdoc/>
    public void Reset() { }
}

/// <summary>
/// A base command, documented in full so derived types can inherit parts of its documentation while replacing others.
/// </summary>
public abstract class CommandBase
{
    /// <summary>Runs the command against the given target.</summary>
    /// <param name="target">The path the command operates on.</param>
    /// <param name="retries">How many times to retry on failure.</param>
    /// <returns>The process exit code.</returns>
    public abstract int Run(string target, int retries);
}

/// <summary>
/// Re-documents a single parameter while inheriting the summary, the other parameter and the return value. This is the
/// "replace the text of one parameter" case — the merge must work per-parameter, not all-or-nothing.
/// </summary>
public sealed class RewriteCommand : CommandBase
{
    /// <inheritdoc/>
    /// <param name="target">The absolute path this command rewrites in place.</param>
    public override int Run(string target, int retries) => 0;
}

/// <summary>
/// Overrides only the summary, inheriting both parameters and the return value.
/// </summary>
public sealed class QuietCommand : CommandBase
{
    /// <inheritdoc/>
    /// <summary>Runs the command without writing progress to the console.</summary>
    public override int Run(string target, int retries) => 0;
}

/// <summary>
/// Overrides only the return value, inheriting the summary and both parameters.
/// </summary>
public sealed class CountingCommand : CommandBase
{
    /// <inheritdoc/>
    /// <returns>The number of files that were changed.</returns>
    public override int Run(string target, int retries) => 0;
}
