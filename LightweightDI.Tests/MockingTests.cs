using FluentAssertions;
using LightweightDI.Tests.Fixtures;
using Moq;
using Serilog;
using Serilog.Events;
using ILogger = LightweightDI.Tests.Fixtures.ILogger;

namespace LightweightDI.Tests;

[TestFixture]
[Category("Mocking")]
public class MockingTests
{
    [Test]
    public void RegisterInstance_WithMockedDependency_ResolvesMockedInstance()
    {
        var container = new Container();
        var mockSender = new Mock<IEmailSender>();
        mockSender.Setup(s => s.Send(It.IsAny<string>())).Returns("mocked");
        mockSender.SetupGet(s => s.InstanceId).Returns(Guid.NewGuid());

        container.RegisterInstance<IEmailSender>(mockSender.Object);

        var resolved = container.Resolve<IEmailSender>();

        resolved.Should().BeSameAs(mockSender.Object);
        resolved.Send("hello").Should().Be("mocked");
    }

    [Test]
    public void Resolve_InjectsMockedDependency_IntoDependentService()
    {
        var container = new Container();
        var mockSender = new Mock<IEmailSender>();
        mockSender.Setup(s => s.Send(It.IsAny<string>())).Returns("mocked-send");

        container.RegisterInstance<IEmailSender>(mockSender.Object);
        container.Register<INotificationService, NotificationService>();

        var notifications = container.Resolve<INotificationService>();

        notifications.EmailSender.Should().BeSameAs(mockSender.Object);
        notifications.EmailSender.Send("test").Should().Be("mocked-send");
        mockSender.Verify(s => s.Send("test"), Times.Once);
    }

    [Test]
    public void Resolve_SerilogLogger_IsInjectedAndLogsThroughContainer()
    {
        var events = new List<LogEvent>();
        var serilog = (Serilog.Core.Logger)new LoggerConfiguration()
            .WriteTo.Sink(new DelegatingSink(events.Add))
            .CreateLogger();

        var container = new Container();
        container.RegisterInstance<ILogger>(new SerilogLogger(serilog));

        var logger = container.Resolve<ILogger>();
        logger.Log("hello from container");

        events.Should().ContainSingle();
        events[0].RenderMessage().Should().Be("hello from container");
    }

    [Test]
    public void Resolve_SerilogLogger_InjectedIntoDependentService_LogsExpectedMessage()
    {
        var events = new List<LogEvent>();
        var serilog = (Serilog.Core.Logger)new LoggerConfiguration()
            .WriteTo.Sink(new DelegatingSink(events.Add))
            .CreateLogger();

        var container = new Container();
        var mockSender = new Mock<IEmailSender>();
        container.RegisterInstance<IEmailSender>(mockSender.Object);
        container.RegisterInstance<ILogger>(new SerilogLogger(serilog));
        container.Register<INotificationService, NotificationService>();
        container.Register<IOrderProcessor, OrderProcessor>();

        var processor = container.Resolve<IOrderProcessor>();
        processor.Logger.Log("order processed");

        events.Should().ContainSingle();
        events[0].RenderMessage().Should().Be("order processed");
    }

    private sealed class DelegatingSink : Serilog.Core.ILogEventSink
    {
        private readonly Action<LogEvent> _onEmit;

        public DelegatingSink(Action<LogEvent> onEmit) => _onEmit = onEmit;

        public void Emit(LogEvent logEvent) => _onEmit(logEvent);
    }
}
