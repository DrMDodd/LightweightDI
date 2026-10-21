using BenchmarkDotNet.Attributes;
using LightweightDI;
using Microsoft.VSDiagnostics;

namespace LightweightDI.Benchmarks;
public interface ILeafService
{
}

public class LeafService : ILeafService
{
}

public interface IMiddleService
{
}

public class MiddleService : IMiddleService
{
    public MiddleService(ILeafService leaf)
    {
    }
}

public interface IRootService
{
}

public class RootService : IRootService
{
    public RootService(IMiddleService middle, ILeafService leaf)
    {
    }
}

[CPUUsageDiagnoser]
public class ContainerResolveBenchmarks
{
    private Container _transientContainer = null!;
    private Container _singletonContainer = null!;
    [GlobalSetup]
    public void Setup()
    {
        _transientContainer = new Container().Register<ILeafService, LeafService>().Register<IMiddleService, MiddleService>().Register<IRootService, RootService>();
        _singletonContainer = new Container().Register<ILeafService, LeafService>(Lifetime.Singleton).Register<IMiddleService, MiddleService>(Lifetime.Singleton).Register<IRootService, RootService>(Lifetime.Singleton);
    }

    [Benchmark]
    public IRootService ResolveTransientGraph()
    {
        return _transientContainer.Resolve<IRootService>();
    }

    [Benchmark]
    public IRootService ResolveSingletonGraph()
    {
        return _singletonContainer.Resolve<IRootService>();
    }
}