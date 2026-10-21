using FluentAssertions;
using LightweightDI.Tests.Fixtures;
using Moq;

namespace LightweightDI.Tests;

[TestFixture]
[Category("NestedInjection")]
public class NestedInjectionTests
{
    [Test]
    public void Resolve_InjectsNestedDependencies_OneLevel()
    {
        var container = new Container();
        var mockSender = new Mock<IEmailSender>();
        container.RegisterInstance<IEmailSender>(mockSender.Object);
        container.Register<INotificationService, NotificationService>();

        var notifications = container.Resolve<INotificationService>();

        notifications.EmailSender.Should().BeSameAs(mockSender.Object);
    }

    [Test]
    public void Resolve_InjectsNestedDependencies_MultipleLevels()
    {
        var container = new Container();
        var mockSender = new Mock<IEmailSender>();
        var mockLogger = new Mock<ILogger>();
        container.RegisterInstance<IEmailSender>(mockSender.Object);
        container.RegisterInstance<ILogger>(mockLogger.Object);
        container.Register<INotificationService, NotificationService>();
        container.Register<IOrderProcessor, OrderProcessor>();

        var processor = container.Resolve<IOrderProcessor>();

        processor.Should().BeOfType<OrderProcessor>();
        processor.Notifications.Should().BeOfType<NotificationService>();
        processor.Notifications.EmailSender.Should().BeSameAs(mockSender.Object);
        processor.Logger.Should().BeSameAs(mockLogger.Object);
    }

    [Test]
    public void Resolve_ImplementationWithNoDependencies_SucceedsWithoutRegisteringDependents()
    {
        var container = new Container();
        container.Register<ILogger, ConsoleLogger>();

        var logger = container.Resolve<ILogger>();

        logger.Should().BeOfType<ConsoleLogger>();
    }

    [Test]
    public void Resolve_PicksConstructorWithMostParameters()
    {
        var container = new Container();
        var mockLogger = new Mock<ILogger>();
        container.RegisterInstance<ILogger>(mockLogger.Object);
        container.Register<ServiceWithMultipleConstructors>();

        var service = container.Resolve<ServiceWithMultipleConstructors>();

        service.Marker.Should().Be("with-logger");
        service.Logger.Should().BeSameAs(mockLogger.Object);
    }
}
