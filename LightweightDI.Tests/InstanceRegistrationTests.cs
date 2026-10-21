using FluentAssertions;
using LightweightDI.Tests.Fixtures;
using Moq;

namespace LightweightDI.Tests;

[TestFixture]
[Category("InstanceRegistration")]
public class InstanceRegistrationTests
{
    [Test]
    public void RegisterInstance_AlwaysReturnsTheSameProvidedInstance()
    {
        var container = new Container();
        var mockSender = new Mock<IEmailSender>();
        container.RegisterInstance<IEmailSender>(mockSender.Object);

        var resolved = container.Resolve<IEmailSender>();

        resolved.Should().BeSameAs(mockSender.Object);
    }

    [Test]
    public void IsRegistered_ReflectsRegistrationState()
    {
        var container = new Container();

        container.IsRegistered<IEmailSender>().Should().BeFalse();

        container.RegisterInstance<IEmailSender>(new Mock<IEmailSender>().Object);

        container.IsRegistered<IEmailSender>().Should().BeTrue();
    }

    [Test]
    public void IsRegistered_TypeOverload_ReflectsRegistrationState()
    {
        var container = new Container();

        container.IsRegistered(typeof(IEmailSender)).Should().BeFalse();

        container.RegisterInstance<IEmailSender>(new Mock<IEmailSender>().Object);

        container.IsRegistered(typeof(IEmailSender)).Should().BeTrue();
    }
}
