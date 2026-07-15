using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Tests.Framework.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentAvailabilityOptionsTests
{
    [Theory]
    [InlineData("HeartbeatTimeout")]
    [InlineData("MaximumWrapUpDuration")]
    public void AvailabilityStartup_WhenDurationIsZero_RejectsConfiguration(string optionName)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                [$"CrestApps_ContactCenter:Availability:{optionName}"] = "00:00:00",
            })
            .Build();
        var services = new ServiceCollection();
        new AvailabilityStartup(new TestShellConfiguration(configuration)).ConfigureServices(services);
        using var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<AgentAvailabilityOptions>>().Value);
    }
}
