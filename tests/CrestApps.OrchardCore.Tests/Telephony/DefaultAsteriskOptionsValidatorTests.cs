using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using Microsoft.Extensions.Hosting;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class DefaultAsteriskOptionsValidatorTests
{
    [Fact]
    public void Validate_InProduction_WhenPasswordIsCheckedInDevelopmentCredential_Fails()
    {
        // Arrange
        var validator = new DefaultAsteriskOptionsValidator(CreateEnvironment(Environments.Production));
        var options = CreateValidEnabledOptions();
        options.Password = "crestapps-dev";

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("Password", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_InProduction_WhenTurnSharedSecretIsCheckedInDevelopmentCredential_Fails()
    {
        // Arrange
        var validator = new DefaultAsteriskOptionsValidator(CreateEnvironment(Environments.Production));
        var options = CreateValidEnabledOptions();
        options.TurnSharedSecret = "crestapps-dev-turn-secret";

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("TurnSharedSecret", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_InProduction_WhenTurnSharedSecretIsUnsubstitutedPlaceholder_Fails()
    {
        // Arrange
        var validator = new DefaultAsteriskOptionsValidator(CreateEnvironment(Environments.Production));
        var options = CreateValidEnabledOptions();
        options.TurnSharedSecret = "<replace-with-tenant-protected-secret>";

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("TurnSharedSecret", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_InProduction_WhenCredentialsAreGenuine_Succeeds()
    {
        // Arrange
        var validator = new DefaultAsteriskOptionsValidator(CreateEnvironment(Environments.Production));
        var options = CreateValidEnabledOptions();

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_InDevelopment_WhenPasswordIsCheckedInDevelopmentCredential_Succeeds()
    {
        // Arrange
        var validator = new DefaultAsteriskOptionsValidator(CreateEnvironment(Environments.Development));
        var options = CreateValidEnabledOptions();
        options.Password = "crestapps-dev";

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    private static DefaultAsteriskOptions CreateValidEnabledOptions()
        => new()
        {
            IsEnabled = true,
            UserName = "production-user",
            Password = "a-genuinely-random-production-secret-6f2c1a",
            TurnSharedSecret = "a-genuinely-random-turn-secret-9f31",
            PjsipRealtimeConnectionString = "Host=db;Username=asterisk;Password=production-secret",
        };

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(instance => instance.EnvironmentName).Returns(environmentName);

        return environment.Object;
    }
}
