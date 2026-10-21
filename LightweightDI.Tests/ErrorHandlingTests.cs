using FluentAssertions;
using LightweightDI.Tests.Fixtures;

namespace LightweightDI.Tests;

[TestFixture]
[Category("ErrorHandling")]
public class ErrorHandlingTests
{
    [Test]
    public void Resolve_WithoutRegistration_ThrowsResolutionException()
    {
        var container = new Container();

        var act = () => container.Resolve<IEmailSender>();

        act.Should().Throw<ResolutionException>();
    }

    [Test]
    public void Resolve_TypeWithNoPublicConstructor_ThrowsResolutionException()
    {
        var container = new Container();
        container.Register<NoPublicConstructorService>();

        var act = () => container.Resolve<NoPublicConstructorService>();

        act.Should().Throw<ResolutionException>();
    }

    [Test]
    public void Resolve_CircularDependency_ThrowsResolutionExceptionInsteadOfStackOverflow()
    {
        var container = new Container();
        container.Register<ICircularA, CircularA>();
        container.Register<ICircularB, CircularB>();

        var act = () => container.Resolve<ICircularA>();

        act.Should().Throw<ResolutionException>();
    }

    [Test]
    public void Resolve_DirectSelfReferencingDependency_ThrowsResolutionException()
    {
        var container = new Container();
        container.Register<ISelfReferencing, SelfReferencing>();

        var act = () => container.Resolve<ISelfReferencing>();

        act.Should().Throw<ResolutionException>();
    }

    [Test]
    public void Resolve_NullServiceType_ThrowsArgumentNullException()
    {
        var container = new Container();

        var act = () => container.Resolve(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Register_ImplementationNotAssignableToService_ThrowsArgumentException()
    {
        var container = new Container();

        var act = () => container.Register(typeof(IEmailSender), typeof(ConsoleLogger));

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Register_NullServiceType_ThrowsArgumentNullException()
    {
        var container = new Container();

        var act = () => container.Register(null!, typeof(AzureEmailSender));

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Register_NullImplementationType_ThrowsArgumentNullException()
    {
        var container = new Container();

        var act = () => container.Register(typeof(IEmailSender), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Register_AbstractImplementationType_ThrowsArgumentException()
    {
        var container = new Container();

        var act = () => container.Register<IEmailSender, AbstractEmailSender>();

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void RegisterInstance_NullInstance_ThrowsArgumentNullException()
    {
        var container = new Container();

        var act = () => container.RegisterInstance<IEmailSender>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Resolve_AmbiguousConstructors_ThrowsResolutionException()
    {
        var container = new Container();
        container.Register<IEmailSender, AzureEmailSender>();
        container.Register<ILogger, ConsoleLogger>();
        container.Register<INotificationService, NotificationService>();
        container.Register<AmbiguousConstructorService>();

        var act = () => container.Resolve<AmbiguousConstructorService>();

        act.Should().Throw<ResolutionException>();
    }

    [Test]
    public void Resolve_SingletonDependsDirectlyOnTransient_ThrowsResolutionException()
    {
        var container = new Container();
        container.Register<ITransientLeaf, TransientLeaf>(Lifetime.Transient);
        container.Register<ISingletonConsumer, SingletonConsumer>(Lifetime.Singleton);

        var act = () => container.Resolve<ISingletonConsumer>();

        act.Should().Throw<ResolutionException>();
    }

    [Test]
    public void Resolve_SingletonDependsTransitivelyOnTransient_ThrowsResolutionException()
    {
        var container = new Container();
        container.Register<ITransientLeaf, TransientLeaf>(Lifetime.Transient);
        container.Register<ISingletonConsumer, SingletonConsumer>(Lifetime.Singleton);
        container.Register<IIntermediateSingleton, IntermediateSingleton>(Lifetime.Singleton);

        var act = () => container.Resolve<IIntermediateSingleton>();

        act.Should().Throw<ResolutionException>();
    }

    [Test]
    public void Resolve_SingletonDependsOnSingleton_DoesNotThrow()
    {
        var container = new Container();
        container.Register<ITransientLeaf, TransientLeaf>(Lifetime.Singleton);
        container.Register<ISingletonConsumer, SingletonConsumer>(Lifetime.Singleton);

        var act = () => container.Resolve<ISingletonConsumer>();

        act.Should().NotThrow();
    }
}
