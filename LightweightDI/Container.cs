using System.Collections.Concurrent;
using System.Reflection;

namespace LightweightDI;

/// <summary>
/// A lightweight dependency injection container supporting constructor injection, singleton and transient lifetimes, 
/// and registration of abstractions to concrete implementations (for example, IEmailSender -> AzureEmailSender). 
/// Tracks disposable instances it creates and validates against captive dependencies (a Singleton capturing
/// a Transient dependency).
/// </summary>
public class Container : IDisposable, IAsyncDisposable
{
    private readonly ConcurrentDictionary<Type, ServiceRegistration> _registrations = new();
    private readonly ConcurrentStack<object> _disposables = new();
    private readonly ConcurrentDictionary<Type, ConstructorInfo> _constructorCache = new();
    private readonly ConcurrentStack<HashSet<Type>> _resolutionChainPool = new();
    private readonly SemaphoreSlim _disposalLock = new(1, 1);
    private int _disposed;

    /// <summary>
    /// Registers a mapping from <typeparamref name="TService"/> to <typeparamref name="TImplementation"/>
    /// with the given lifetime. Re-registering a service type overwrites the previous registration.
    /// </summary>
    public Container Register<TService, TImplementation>(Lifetime lifetime = Lifetime.Transient)
        where TImplementation : TService
    {
        return Register(typeof(TService), typeof(TImplementation), lifetime);
    }

    /// <summary>
    /// Registers a concrete type as its own service type (useful for classes with no
    /// dedicated abstraction), with the given lifetime.
    /// </summary>
    public Container Register<TImplementation>(Lifetime lifetime = Lifetime.Transient)
    {
        return Register(typeof(TImplementation), typeof(TImplementation), lifetime);
    }

    /// <summary>
    /// Registers a mapping from <paramref name="serviceType"/> to <paramref name="implementationType"/>.
    /// </summary>
    public Container Register(Type serviceType, Type implementationType, Lifetime lifetime = Lifetime.Transient)
    {
        if (serviceType is null) throw new ArgumentNullException(nameof(serviceType));
        if (implementationType is null) throw new ArgumentNullException(nameof(implementationType));

        if (!serviceType.IsAssignableFrom(implementationType))
        {
            throw new ArgumentException(
                $"Type '{implementationType.FullName}' does not implement or derive from '{serviceType.FullName}'.");
        }

        if (implementationType.IsAbstract || implementationType.IsInterface)
        {
            throw new ArgumentException(
                $"Implementation type '{implementationType.FullName}' must be a concrete, instantiable class.");
        }

        _registrations[serviceType] = new ServiceRegistration(serviceType, implementationType, lifetime);
        return this;
    }

    /// <summary>
    /// Registers an already-created singleton instance for the given service type.
    /// </summary>
    public Container RegisterInstance<TService>(TService instance) where TService : notnull
    {
        if (instance is null) throw new ArgumentNullException(nameof(instance));

        var registration = new ServiceRegistration(typeof(TService), instance.GetType(), Lifetime.Singleton)
        {
            SingletonInstance = instance
        };
        _registrations[typeof(TService)] = registration;
        return this;
    }

    /// <summary>
    /// Resolves an instance of <typeparamref name="TService"/>, constructing any nested
    /// dependencies recursively via constructor injection.
    /// </summary>
    public TService Resolve<TService>()
    {
        return (TService)Resolve(typeof(TService));
    }

    /// <summary>
    /// Resolves an instance of <paramref name="serviceType"/>, constructing any nested
    /// dependencies recursively via constructor injection.
    /// </summary>
    public object Resolve(Type serviceType)
    {
        if (serviceType is null) throw new ArgumentNullException(nameof(serviceType));
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (_registrations.TryGetValue(serviceType, out var registration) &&
            registration.Lifetime == Lifetime.Singleton &&
            registration.SingletonInstance is { } singletonInstance)
        {
            return singletonInstance;
        }

        if (!_resolutionChainPool.TryPop(out var resolutionChain))
        {
            resolutionChain = new HashSet<Type>();
        }

        try
        {
            return Resolve(serviceType, resolutionChain, isWithinSingletonGraph: false);
        }
        finally
        {
            resolutionChain.Clear();
            _resolutionChainPool.Push(resolutionChain);
        }
    }

    private object Resolve(Type serviceType, HashSet<Type> resolutionChain, bool isWithinSingletonGraph)
    {
        if (!_registrations.TryGetValue(serviceType, out var registration))
        {
            throw new ResolutionException(
                $"No registration found for type '{serviceType.FullName}'. Register it before resolving.");
        }

        if (registration.Lifetime == Lifetime.Transient && isWithinSingletonGraph)
        {
            throw new ResolutionException(
                $"Captive dependency detected: a Singleton-lifetime service depends (directly or transitively) " +
                $"on '{serviceType.FullName}', which is registered as Transient. A longer-lived service must not " +
                "capture a shorter-lived dependency.");
        }

        var childIsWithinSingletonGraph = isWithinSingletonGraph || registration.Lifetime == Lifetime.Singleton;

        if (registration.Lifetime == Lifetime.Singleton)
        {
            if (registration.SingletonInstance is not null)
            {
                return registration.SingletonInstance;
            }

            lock (registration.SingletonLock)
            {
                registration.SingletonInstance ??= CreateInstance(registration.ImplementationType, resolutionChain, childIsWithinSingletonGraph);
                return registration.SingletonInstance;
            }
        }

        return CreateInstance(registration.ImplementationType, resolutionChain, childIsWithinSingletonGraph);
    }

    private object CreateInstance(Type implementationType, HashSet<Type> resolutionChain, bool isWithinSingletonGraph)
    {
        if (!resolutionChain.Add(implementationType))
        {
            var cycle = string.Join(" -> ", resolutionChain.Select(t => t.Name)) + $" -> {implementationType.Name}";
            throw new ResolutionException($"Circular dependency detected while resolving '{implementationType.FullName}': {cycle}");
        }

        try
        {
            var constructor = _constructorCache.GetOrAdd(implementationType, SelectConstructor);
            var parameters = constructor.GetParameters();

            object instance;
            if (parameters.Length == 0)
            {
                instance = Activator.CreateInstance(implementationType)!;
            }
            else
            {
                var arguments = new object[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    var parameterType = parameters[i].ParameterType;
                    try
                    {
                        arguments[i] = Resolve(parameterType, resolutionChain, isWithinSingletonGraph);
                    }
                    catch (ResolutionException ex)
                    {
                        throw new ResolutionException(
                            $"Failed to resolve parameter '{parameters[i].Name}' of type '{parameterType.FullName}' " +
                            $"while constructing '{implementationType.FullName}'.", ex);
                    }
                }

                instance = constructor.Invoke(arguments);
            }

            if (instance is IDisposable or IAsyncDisposable)
            {
                _disposalLock.Wait();
                try
                {
                    if (Volatile.Read(ref _disposed) != 0)
                    {
                        // The container finished (or is finishing) disposal concurrently with this
                        // resolution; dispose the instance immediately instead of leaking it.
                        DisposeInstanceSynchronously(instance);
                    }
                    else
                    {
                        _disposables.Push(instance);
                    }
                }
                finally
                {
                    _disposalLock.Release();
                }
            }

            return instance;
        }
        finally
        {
            resolutionChain.Remove(implementationType);
        }
    }

    /// <summary>
    /// Chooses the constructor to use for instantiation: the one with the most parameters,
    /// to favor full constructor injection when multiple constructors exist.
    /// </summary>
    private static ConstructorInfo SelectConstructor(Type implementationType)
    {
        var constructors = implementationType.GetConstructors();
        if (constructors.Length == 0)
        {
            throw new ResolutionException($"Type '{implementationType.FullName}' has no public constructors.");
        }

        var orderedByParameterCount = constructors
            .OrderByDescending(c => c.GetParameters().Length)
            .ToList();

        var maxParameterCount = orderedByParameterCount[0].GetParameters().Length;
        var tiedForMax = orderedByParameterCount.Count(c => c.GetParameters().Length == maxParameterCount);

        if (tiedForMax > 1)
        {
            throw new ResolutionException(
                $"Type '{implementationType.FullName}' has {tiedForMax} public constructors tied for the most " +
                $"parameters ({maxParameterCount}). Constructor selection is ambiguous; define a single constructor " +
                "with the most parameters.");
        }

        return orderedByParameterCount[0];
    }

    /// <summary>
    /// Returns true if a registration exists for the given service type.
    /// </summary>
    public bool IsRegistered<TService>() => _registrations.ContainsKey(typeof(TService));

    /// <summary>
    /// Returns true if a registration exists for the given service type.
    /// </summary>
    public bool IsRegistered(Type serviceType) => _registrations.ContainsKey(serviceType);

    /// <summary>
    /// Disposes all instances the container itself created (via constructor injection) that implement
    /// <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>, in the reverse order they were created.
    /// Instances supplied via <see cref="RegisterInstance{TService}"/> are not disposed, since the caller
    /// retains ownership of those instances.
    /// </summary>
    public void Dispose()
    {
        _disposalLock.Wait();
        try
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            while (_disposables.TryPop(out var instance))
            {
                DisposeInstanceSynchronously(instance);
            }
        }
        finally
        {
            _disposalLock.Release();
        }

        GC.SuppressFinalize(this);
    }

    private static void DisposeInstanceSynchronously(object instance)
    {
        if (instance is IDisposable disposable)
        {
            disposable.Dispose();
        }
        else if (instance is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Asynchronously disposes all instances the container itself created that implement
    /// <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>, in the reverse order they were created.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _disposalLock.WaitAsync();
        try
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            while (_disposables.TryPop(out var instance))
            {
                switch (instance)
                {
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync();
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
        }
        finally
        {
            _disposalLock.Release();
        }

        GC.SuppressFinalize(this);
    }
}
