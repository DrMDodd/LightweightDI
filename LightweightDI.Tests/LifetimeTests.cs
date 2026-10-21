using FluentAssertions;
using LightweightDI.Tests.Fixtures;
using Moq;
using System.Threading.Channels;

namespace LightweightDI.Tests;

[TestFixture]
[Category("Lifetime")]
public class LifetimeTests
{
    [Test]
    public void Resolve_Transient_ReturnsNewInstanceEachTime()
    {
        var container = new Container();
        container.Register<IEmailSender, AzureEmailSender>(Lifetime.Transient);

        var first = container.Resolve<IEmailSender>();
        var second = container.Resolve<IEmailSender>();

        second.Should().NotBeSameAs(first);
        second.InstanceId.Should().NotBe(first.InstanceId);
    }

    [Test]
    public void Resolve_Singleton_ReturnsSameInstanceEachTime()
    {
        var container = new Container();
        container.Register<IEmailSender, AzureEmailSender>(Lifetime.Singleton);

        var first = container.Resolve<IEmailSender>();
        var second = container.Resolve<IEmailSender>();

        second.Should().BeSameAs(first);
        second.InstanceId.Should().Be(first.InstanceId);
    }

    [Test]
    public async Task Resolve_Singleton_IsThreadSafeAndCreatedOnce()
    {
        var container = new Container();
        container.Register<IEmailSender, AzureEmailSender>(Lifetime.Singleton);

        var channel = Channel.CreateUnbounded<Guid>();

        // Resolve() is synchronous CPU-bound work, so Parallel.For is used to fan it out across
        // the thread pool instead of queuing individual Task.Run work items.
        Parallel.For(0, 50, _ =>
        {
            channel.Writer.TryWrite(container.Resolve<IEmailSender>().InstanceId);
        });
        channel.Writer.Complete();

        var ids = new List<Guid>();
        await foreach (var id in channel.Reader.ReadAllAsync())
        {
            ids.Add(id);
        }

        ids.Distinct().Should().ContainSingle();
    }

    [Test]
    public void Resolve_SingletonDependency_IsSharedAcrossMultipleConsumers()
    {
        var container = new Container();
        var mockSender = new Mock<IEmailSender>();
        container.RegisterInstance<IEmailSender>(mockSender.Object);
        container.Register<INotificationService, NotificationService>(Lifetime.Transient);

        var first = container.Resolve<INotificationService>();
        var second = container.Resolve<INotificationService>();

        second.Should().NotBeSameAs(first);
        second.EmailSender.Should().BeSameAs(first.EmailSender);
        second.EmailSender.Should().BeSameAs(mockSender.Object);
    }

    [Test]
    public void Resolve_TransientDependency_IsNotSharedAcrossMultipleConsumers()
    {
        var container = new Container();
        container.Register<IEmailSender, AzureEmailSender>(Lifetime.Transient);
        container.Register<INotificationService, NotificationService>(Lifetime.Transient);

        var first = container.Resolve<INotificationService>();
        var second = container.Resolve<INotificationService>();

        second.EmailSender.Should().NotBeSameAs(first.EmailSender);
    }
}
