using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Verifies that the activation-time log warning mirrors the readiness gate: a production host logs a critical
/// entry when the base-voice path is unverified, a non-production host only warns, and an acknowledged
/// deployment is silent — and that it never throws or blocks activation.
/// </summary>
public sealed class BaseVoiceVerificationStartupCheckTests
{
    [Fact]
    public async Task ActivatedAsync_WhenUnacknowledgedInProduction_LogsCritical()
    {
        // Arrange
        var logger = new CapturingLogger();
        var check = CreateCheck(acknowledged: false, environmentName: Environments.Production, logger);

        // Act
        await check.ActivatedAsync();

        // Assert
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Critical);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task ActivatedAsync_WhenUnacknowledgedOutsideProduction_LogsWarning()
    {
        // Arrange
        var logger = new CapturingLogger();
        var check = CreateCheck(acknowledged: false, environmentName: Environments.Development, logger);

        // Act
        await check.ActivatedAsync();

        // Assert
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task ActivatedAsync_WhenAcknowledged_LogsNothing()
    {
        // Arrange
        var logger = new CapturingLogger();
        var check = CreateCheck(acknowledged: true, environmentName: Environments.Production, logger);

        // Act
        await check.ActivatedAsync();

        // Assert
        Assert.Empty(logger.Entries);
    }

    private static BaseVoiceVerificationStartupCheck CreateCheck(
        bool acknowledged,
        string environmentName,
        ILogger<BaseVoiceVerificationStartupCheck> logger)
    {
        var options = Options.Create(new BaseVoiceVerificationOptions
        {
            AudioVerificationAcknowledged = acknowledged,
            AudioVerificationEvidenceReference = acknowledged ? "https://evidence/base-voice-proof" : null,
        });

        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(environment => environment.EnvironmentName).Returns(environmentName);

        return new BaseVoiceVerificationStartupCheck(
            options,
            hostEnvironment.Object,
            new ShellSettings { Name = "Default" },
            logger);
    }

    private sealed class CapturingLogger : ILogger<BaseVoiceVerificationStartupCheck>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
