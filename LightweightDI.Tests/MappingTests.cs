using FluentAssertions;
using LightweightDI.Tests.Fixtures;

namespace LightweightDI.Tests;

[TestFixture]
[Category("Mapping")]
public class MappingTests
{
    [Test]
    public void Resolve_MapsAbstractionToRegisteredImplementation()
    {
        var container = new Container();
        container.Register<IEmailSender, AzureEmailSender>();

        var sender = container.Resolve<IEmailSender>();

        sender.Should().BeOfType<AzureEmailSender>();
    }

    [Test]
    public void Resolve_UsesLastRegistrationWhenReRegistered()
    {
        var container = new Container();
        container.Register<IEmailSender, AzureEmailSender>();
        container.Register<IEmailSender, SmtpEmailSender>();

        var sender = container.Resolve<IEmailSender>();

        sender.Should().BeOfType<SmtpEmailSender>();
    }

    [Test]
    public void Resolve_NonGenericTypeOverload_ReturnsRegisteredImplementation()
    {
        var container = new Container();
        container.Register<IEmailSender, AzureEmailSender>();

        var sender = container.Resolve(typeof(IEmailSender));

        sender.Should().BeOfType<AzureEmailSender>();
    }
}
