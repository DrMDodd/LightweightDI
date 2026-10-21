namespace LightweightDI;

/// <summary>
/// Internal registration record describing how a service type should be created.
/// </summary>
internal sealed class ServiceRegistration
{
    public Type ServiceType { get; }
    public Type ImplementationType { get; }
    public Lifetime Lifetime { get; }

    /// <summary>Cached singleton instance, populated lazily on first resolution.</summary>
    public object? SingletonInstance { get; set; }

    /// <summary>Guards lazy singleton creation against concurrent resolutions.</summary>
    public readonly object SingletonLock = new();

    public ServiceRegistration(Type serviceType, Type implementationType, Lifetime lifetime)
    {
        ServiceType = serviceType;
        ImplementationType = implementationType;
        Lifetime = lifetime;
    }
}
