using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// A misconfigured automation pass does not announce itself: a zero batch size drains nothing while every run
/// reports success, and a zero attempt ceiling retries one poisoned activity forever. These prove the tenant is
/// stopped at startup with the offending key named instead.
/// </summary>
public sealed class OmnichannelAutomationOptionsValidatorTests
{
    [Fact]
    public void Validate_AcceptsTheShippedDefaults()
    {
        // Arrange
        var validator = new OmnichannelAutomationOptionsValidator();

        // Act
        var result = validator.Validate(Options.DefaultName, new OmnichannelAutomationOptions());

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Failures ?? []));
    }

    [Theory]
    [InlineData(nameof(OmnichannelAutomationOptions.ProcessorLeaseMilliseconds))]
    [InlineData(nameof(OmnichannelAutomationOptions.ProcessorBatchSize))]
    [InlineData(nameof(OmnichannelAutomationOptions.MaxActivitiesPerInvocation))]
    [InlineData(nameof(OmnichannelAutomationOptions.MaxProcessingAttempts))]
    [InlineData(nameof(OmnichannelAutomationOptions.RetryDelayMinutes))]
    public void Validate_RefusesANonPositiveValue_AndNamesTheKey(string propertyName)
    {
        // Arrange
        var validator = new OmnichannelAutomationOptionsValidator();
        var options = new OmnichannelAutomationOptions();
        typeof(OmnichannelAutomationOptions).GetProperty(propertyName).SetValue(options, 0);

        // Act
        var result = validator.Validate(Options.DefaultName, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains(propertyName, StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("CrestApps:Omnichannel:Automation", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RefusesABatchLargerThanTheInvocationCeiling()
    {
        // Arrange
        // One query would already return more than the pass is allowed to process, so the ceiling that exists to
        // keep a pass inside its lease would never be honoured.
        var validator = new OmnichannelAutomationOptionsValidator();
        var options = new OmnichannelAutomationOptions
        {
            ProcessorBatchSize = 500,
            MaxActivitiesPerInvocation = 100,
        };

        // Act
        var result = validator.Validate(Options.DefaultName, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains(nameof(OmnichannelAutomationOptions.ProcessorBatchSize), StringComparison.Ordinal));
    }
}
