using CrestApps.OrchardCore.Checks.Core;
using CrestApps.OrchardCore.Checks.Services;
using CrestApps.OrchardCore.Numbering;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Checks;

public sealed class ChecksRegistrationTests
{
    [Fact]
    public void AddChecksCore_RegistersTheNumberSequenceTheCheckServiceNeeds()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddChecksCore();

        // Assert
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(INumberSequence));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICheckService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICheckPrinter));
    }
}
