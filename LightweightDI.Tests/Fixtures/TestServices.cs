using Serilog;

namespace LightweightDI.Tests.Fixtures;

public interface IEmailSender
{
    string Send(string message);
    Guid InstanceId { get; }
}

public class AzureEmailSender : IEmailSender
{
    public Guid InstanceId { get; } = Guid.NewGuid();

    public string Send(string message) => $"Azure sent: {message}";
}

public class SmtpEmailSender : IEmailSender
{
    public Guid InstanceId { get; } = Guid.NewGuid();

    public string Send(string message) => $"Smtp sent: {message}";
}

public interface ILogger
{
    Guid InstanceId { get; }
    void Log(string message);
}

public class ConsoleLogger : ILogger
{
    public Guid InstanceId { get; } = Guid.NewGuid();

    public void Log(string message) => Console.WriteLine(message);
}

/// <summary>
/// A production-style <see cref="ILogger"/> implementation backed by Serilog, demonstrating
/// how a real logging library can be wired into the container via constructor injection.
/// </summary>
public class SerilogLogger : ILogger, IDisposable
{
    private readonly Serilog.Core.Logger _serilog;
    private readonly bool _ownsLogger;

    public Guid InstanceId { get; } = Guid.NewGuid();

    public SerilogLogger()
        : this((Serilog.Core.Logger)new LoggerConfiguration().WriteTo.Console().CreateLogger(), ownsLogger: true)
    {
    }

    /// <summary>
    /// Allows tests to supply a custom-configured Serilog logger (for example, one writing
    /// to an in-memory sink) so log output can be asserted without touching the console.
    /// </summary>
    public SerilogLogger(Serilog.Core.Logger serilog)
        : this(serilog, ownsLogger: false)
    {
    }

    private SerilogLogger(Serilog.Core.Logger serilog, bool ownsLogger)
    {
        _serilog = serilog;
        _ownsLogger = ownsLogger;
    }

    public void Log(string message) => _serilog.Information(message);

    public void Dispose()
    {
        if (_ownsLogger)
        {
            _serilog.Dispose();
        }
    }
}


/// <summary>Depends directly on IEmailSender - one level of nesting.</summary>
public interface INotificationService
{
    IEmailSender EmailSender { get; }
}

public class NotificationService : INotificationService
{
    public IEmailSender EmailSender { get; }

    public NotificationService(IEmailSender emailSender)
    {
        EmailSender = emailSender;
    }
}

/// <summary>Depends on INotificationService, which itself depends on IEmailSender and ILogger - two levels of nesting.</summary>
public interface IOrderProcessor
{
    INotificationService Notifications { get; }
    ILogger Logger { get; }
}

public class OrderProcessor : IOrderProcessor
{
    public INotificationService Notifications { get; }
    public ILogger Logger { get; }

    public OrderProcessor(INotificationService notifications, ILogger logger)
    {
        Notifications = notifications;
        Logger = logger;
    }
}

public class NoPublicConstructorService
{
    private NoPublicConstructorService()
    {
    }
}

public class ServiceWithMultipleConstructors
{
    public ILogger? Logger { get; }
    public string Marker { get; }

    public ServiceWithMultipleConstructors()
    {
        Marker = "default";
    }

    public ServiceWithMultipleConstructors(ILogger logger)
    {
        Logger = logger;
        Marker = "with-logger";
    }
}

/// <summary>Used to prove circular dependencies are detected rather than causing a stack overflow.</summary>
public interface ICircularA
{
}

public interface ICircularB
{
}

public class CircularA : ICircularA
{
    public CircularA(ICircularB b)
    {
    }
}

public class CircularB : ICircularB
{
    public CircularB(ICircularA a)
    {
    }
}

/// <summary>Abstract implementation type - cannot be instantiated, used to test registration guard.</summary>
public abstract class AbstractEmailSender : IEmailSender
{
    public abstract Guid InstanceId { get; }
    public abstract string Send(string message);
}

/// <summary>Depends directly on its own service type - simplest possible circular dependency.</summary>
public interface ISelfReferencing
{
}

public class SelfReferencing : ISelfReferencing
{
    public SelfReferencing(ISelfReferencing self)
    {
    }
}

/// <summary>Has two public constructors tied for the most parameters - ambiguous for greedy selection.</summary>
public class AmbiguousConstructorService
{
    public AmbiguousConstructorService(IEmailSender sender, ILogger logger)
    {
    }

    public AmbiguousConstructorService(IEmailSender sender, INotificationService notifications)
    {
    }
}

/// <summary>Depends on a transient-registered leaf service - used to test captive dependency detection
/// when this type itself is registered as a Singleton.</summary>
public interface ITransientLeaf
{
    Guid InstanceId { get; }
}

public class TransientLeaf : ITransientLeaf
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public interface ISingletonConsumer
{
    ITransientLeaf Leaf { get; }
}

public class SingletonConsumer : ISingletonConsumer
{
    public ITransientLeaf Leaf { get; }

    public SingletonConsumer(ITransientLeaf leaf)
    {
        Leaf = leaf;
    }
}

/// <summary>Captures the transient dependency indirectly through another singleton, to prove
/// captive dependency detection walks the full graph rather than just direct parameters.</summary>
public interface IIntermediateSingleton
{
    ISingletonConsumer Consumer { get; }
}

public class IntermediateSingleton : IIntermediateSingleton
{
    public ISingletonConsumer Consumer { get; }

    public IntermediateSingleton(ISingletonConsumer consumer)
    {
        Consumer = consumer;
    }
}

/// <summary>Shared ordering list used by disposal tests to assert LIFO teardown order.</summary>
public class DisposalLog
{
    public List<string> DisposedNames { get; } = new();
}

public interface IDisposableService
{
}

public class DisposableService : IDisposableService, IDisposable
{
    private readonly DisposalLog _log;
    private readonly string _name;

    public DisposableService(DisposalLog log)
    {
        _log = log;
        _name = nameof(DisposableService);
    }

    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
        _log.DisposedNames.Add(_name);
    }
}

public interface IAsyncDisposableService
{
}

public class AsyncDisposableService : IAsyncDisposableService, IAsyncDisposable
{
    private readonly DisposalLog _log;

    public AsyncDisposableService(DisposalLog log)
    {
        _log = log;
    }

    public bool IsDisposed { get; private set; }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        _log.DisposedNames.Add(nameof(AsyncDisposableService));
        return ValueTask.CompletedTask;
    }
}

/// <summary>Depends on a disposable leaf, so disposing the container must tear down nested
/// container-created instances too.</summary>
public interface IDisposableConsumer
{
}

public class DisposableConsumer : IDisposableConsumer, IDisposable
{
    private readonly DisposalLog _log;

    public DisposableConsumer(DisposableService inner, DisposalLog log)
    {
        _log = log;
    }

    public void Dispose()
    {
        _log.DisposedNames.Add(nameof(DisposableConsumer));
    }
}
