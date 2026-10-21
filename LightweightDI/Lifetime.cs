namespace LightweightDI;

/// <summary>
/// Defines how instances resolved from the container are lifetime-managed.
/// </summary>
public enum Lifetime
{
    /// <summary>A single shared instance is created and reused for all resolutions.</summary>
    Singleton,

    /// <summary>A new instance is created every time the service is resolved.</summary>
    Transient
}
