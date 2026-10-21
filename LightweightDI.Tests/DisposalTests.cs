using System.Collections.Concurrent;
using FluentAssertions;
using LightweightDI.Tests.Fixtures;

namespace LightweightDI.Tests;

[TestFixture]
[Category("Disposal")]
public class DisposalTests
{
    [Test]
    public void Dispose_TransientInstance_DisposesTrackedInstance()
    {
        var log = new DisposalLog();
        var container = new Container();
        container.RegisterInstance(log);
        container.Register<DisposableService>();

        var service = container.Resolve<DisposableService>();
        container.Dispose();

        service.IsDisposed.Should().BeTrue();
    }

    [Test]
    public void Dispose_SingletonInstance_DisposesTrackedInstance()
    {
        var log = new DisposalLog();
        var container = new Container();
        container.RegisterInstance(log);
        container.Register<DisposableService>(Lifetime.Singleton);

        var service = container.Resolve<DisposableService>();
        container.Dispose();

        service.IsDisposed.Should().BeTrue();
    }

    [Test]
    public void Dispose_NestedDisposableDependency_DisposesInReverseCreationOrder()
    {
        var log = new DisposalLog();
        var container = new Container();
        container.RegisterInstance(log);
        container.Register<DisposableService>();
        container.Register<DisposableConsumer>();

        container.Resolve<DisposableConsumer>();
        container.Dispose();

        log.DisposedNames.Should().Equal(nameof(DisposableConsumer), "DisposableService");
    }

    [Test]
    public void Dispose_RegisterInstanceSuppliedInstance_IsNotDisposed()
    {
        var log = new DisposalLog();
        var instance = new DisposableService(log);
        var container = new Container();
        container.RegisterInstance<IDisposableService>(instance);

        container.Dispose();

        instance.IsDisposed.Should().BeFalse();
    }

    [Test]
    public async Task DisposeAsync_AsyncDisposableInstance_DisposesTrackedInstance()
    {
        var log = new DisposalLog();
        var container = new Container();
        container.RegisterInstance(log);
        container.Register<AsyncDisposableService>();

        var service = container.Resolve<AsyncDisposableService>();
        await container.DisposeAsync();

        service.IsDisposed.Should().BeTrue();
    }

    [Test]
    public void Dispose_CalledMultipleTimes_DoesNotThrowOrDisposeTwice()
    {
        var log = new DisposalLog();
        var container = new Container();
        container.RegisterInstance(log);
        container.Register<DisposableService>();

        container.Resolve<DisposableService>();
        container.Dispose();
        var act = () => container.Dispose();

        act.Should().NotThrow();
        log.DisposedNames.Should().ContainSingle();
    }

    [Test]
    [Category("Concurrency")]
    public void Dispose_ConcurrentWithResolve_DoesNotLeakInstancesCreatedDuringDisposal()
    {
        // Regression test for the thread-safety gap identified in review: Resolve() rejects new resolutions once disposal has started, and CreateInstance() synchronizes the
        // push-vs-immediate-dispose decision with Dispose()'s drain loop, so an instance created concurrently with disposal is either pushed (and later drained) or
        // disposed immediately instead of being leaked.
        var log = new DisposalLog();
        var container = new Container();
        container.RegisterInstance(log);
        container.Register<DisposableService>();

        var resolvedInstances = new ConcurrentBag<DisposableService>();
        using var disposeNow = new ManualResetEventSlim(initialState: false);
        using var startedResolving = new ManualResetEventSlim(initialState: false);

        var resolverTasks = Enumerable.Range(0, Environment.ProcessorCount * 2).Select(_ => Task.Run(() =>
        {
            try
            {
                while (!disposeNow.IsSet)
                {
                    resolvedInstances.Add(container.Resolve<DisposableService>());
                    startedResolving.Set();
                }

                // Keep racing briefly after the dispose signal so some resolves land concurrently with (or just after) the drain loop in Dispose().
                for (var i = 0; i < 20_000; i++)
                {
                    resolvedInstances.Add(container.Resolve<DisposableService>());
                }
            }
            catch (ObjectDisposedException)
            {
                // Expected once Dispose() has completed: Resolve() now rejects further calls.
            }
        })).ToArray();

        startedResolving.Wait(TimeSpan.FromSeconds(5));
        var disposeTask = Task.Run(() =>
        {
            disposeNow.Set();
            container.Dispose();
        });

        Task.WaitAll(resolverTasks.Append(disposeTask).ToArray(), TimeSpan.FromSeconds(30));

        var undisposedCount = resolvedInstances.Count(instance => !instance.IsDisposed);

        undisposedCount.Should().Be(0,
            "every instance the container creates should eventually be disposed, even if Resolve() " +
            "races with Dispose(); a non-zero count reproduces the leak described in the thread-safety review");
    }
}
