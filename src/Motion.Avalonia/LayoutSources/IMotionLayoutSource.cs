using Avalonia.Controls.Templates;

namespace Motion.Avalonia;

/// <summary>
/// Provides one live layout template consumed by a motion panel host.
/// </summary>
/// <remarks>
/// States and keyframes intentionally remain distinct concepts. This interface lets custom
/// motions expose additional live layout templates without merging their evaluation semantics.
/// </remarks>
public interface IMotionLayoutSource
{
    /// <summary>
    /// Gets the live layout template represented by this source.
    /// </summary>
    IDataTemplate? Template { get; }

    /// <summary>
    /// Gets a concise description used by validation diagnostics.
    /// </summary>
    string Description { get; }
}