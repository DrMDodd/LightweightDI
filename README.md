# LightweightDI

A lightweight, reflection-based dependency injection container for .NET 9, built from scratch to demonstrate the core mechanics of an IoC container: constructor injection, lifetime management, and safeguards against common DI pitfalls.

## Features

- **Constructor injection** — dependencies are resolved recursively based on constructor parameters.
- **Fluent registration API** — `Register<TService, TImplementation>()` calls can be chained.
- **Lifetimes** — `Transient` (new instance per resolution) and `Singleton` (single shared instance, created lazily and thread-safely).
- **Instance registration** — register an already-constructed, externally-owned instance via `RegisterInstance`.
- **Circular dependency detection** — throws a `ResolutionException` instead of causing a stack overflow when a dependency graph cycles back on itself (including direct self-references).
- **Ambiguous constructor detection** — throws when a type has multiple public constructors tied for the most parameters, instead of silently picking one.
- **Captive dependency validation** — throws if a `Singleton` would capture a `Transient` dependency (directly or transitively), preventing a well-known DI lifetime bug.
- **Disposal tracking** — the container implements `IDisposable` and `IAsyncDisposable`, tracking instances it creates that implement `IDisposable`/`IAsyncDisposable` and disposing them in reverse (LIFO) creation order. Instances supplied via `RegisterInstance` are considered caller-owned and are never disposed by the container.

## Project Structure

| Project | Description |
|---|---|
| `LightweightDI` | The core container implementation (`Container`, `ServiceRegistration`, `Lifetime`, `ResolutionException`). |
| `LightweightDI.Tests` | xUnit test suite covering mapping, lifetimes, nested injection, error handling, instance registration, mocking, and disposal. |
| `LightweightDI.Tests` | NUnit test suite covering mapping, lifetimes, nested injection, error handling, instance registration, mocking, disposal, and concurrency. |
| `BenchmarkSuite1` | BenchmarkDotNet benchmarks measuring container resolution performance (e.g. singleton vs. transient dependency graphs). |

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)

### Build

```powershell
dotnet build
```

### Run Tests

```powershell
dotnet test
```

## Usage

```csharp
using LightweightDI;

var container = new Container();

container.Register<IEmailSender, SmtpEmailSender>();
container.Register<ILogger, ConsoleLogger>(Lifetime.Singleton);
container.Register<NotificationService>();

var notificationService = container.Resolve<NotificationService>();
```

### Registering an existing instance

```csharp
var logger = new ConsoleLogger();
container.RegisterInstance<ILogger>(logger);
```

### Disposing the container

```csharp
using var container = new Container();
container.Register<MyDisposableService>();

var service = container.Resolve<MyDisposableService>();
// service is disposed automatically when the container is disposed
```

Or asynchronously:

```csharp
await using var container = new Container();
```

## Error Handling

All resolution and registration failures surface as `ResolutionException` (for resolution-time issues) or `ArgumentException`/`ArgumentNullException` (for invalid registration arguments), including:

- Missing registrations
- Types with no public constructor
- Ambiguous (tied) public constructors
- Circular dependencies
- Captive dependencies (Singleton depending on Transient)
- Invalid or incompatible service/implementation type pairs

## Testing

The test suite uses [xUnit](https://xunit.net/), [FluentAssertions](https://fluentassertions.com/), and [Moq](https://github.com/devlooped/moq), organized into category-specific files:
The test suite uses [NUnit](https://nunit.org/), [FluentAssertions](https://fluentassertions.com/), and [Moq](https://github.com/devlooped/moq), organized into category-specific files:

- `MappingTests` — abstraction-to-implementation resolution
- `LifetimeTests` — transient/singleton behavior and thread safety
- `NestedInjectionTests` — multi-level constructor injection and constructor selection
- `ErrorHandlingTests` — failure paths and validation
- `InstanceRegistrationTests` — `RegisterInstance` behavior
- `MockingTests` — Moq-based mocked dependency injection
- `DisposalTests` — disposal tracking and ordering
- `DisposalTests` — disposal tracking/ordering and concurrent resolve-vs-dispose safety

## Benchmarks

`BenchmarkSuite1` contains [BenchmarkDotNet](https://benchmarkdotnet.org/) benchmarks for container resolution:

```powershell
dotnet run -c Release --project BenchmarkSuite1
```
