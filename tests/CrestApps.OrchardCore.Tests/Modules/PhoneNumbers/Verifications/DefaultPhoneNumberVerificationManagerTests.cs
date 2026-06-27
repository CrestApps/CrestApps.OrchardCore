using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.PhoneNumbers.Verifications;

public sealed class DefaultPhoneNumberVerificationManagerTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task VerifyAsync_WithExplicitKey_FillsDefaultsFromManager()
    {
        // Arrange
        var provider = new FakeVerificationProvider(new PhoneNumberVerificationResult
        {
            Status = PhoneNumberVerificationStatus.Verified,
        });

        var manager = CreateManager([("Fake", provider)]);

        // Act
        var result = await manager.VerifyAsync("+17024993350", "Fake", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("+17024993350", result.PhoneNumber);
        Assert.Equal("+17024993350", result.NormalizedPhoneNumber);
        Assert.Equal("Fake", result.VerificationProvider);
        Assert.Equal(_now, result.VerificationDateUtc);
        Assert.Equal(PhoneNumberVerificationStatus.Verified, result.Status);
    }

    [Fact]
    public async Task VerifyAsync_NormalizesPhoneNumberToE164BeforeCallingProvider()
    {
        // Arrange
        var provider = new FakeVerificationProvider(new PhoneNumberVerificationResult
        {
            Status = PhoneNumberVerificationStatus.Verified,
        });

        var manager = CreateManager([("Fake", provider)]);

        // Act
        var result = await manager.VerifyAsync("+1 (702) 499-3350", "Fake", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("+17024993350", provider.LastPhoneNumber);
        Assert.Equal("+17024993350", result.NormalizedPhoneNumber);
    }

    [Fact]
    public async Task VerifyAsync_WhenProviderThrows_ReturnsFailedResult()
    {
        // Arrange
        var provider = new FakeVerificationProvider(exception: new InvalidOperationException("boom"));

        var manager = CreateManager([("Fake", provider)]);

        // Act
        var result = await manager.VerifyAsync("+17024993350", "Fake", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PhoneNumberVerificationStatus.Failed, result.Status);
        Assert.Equal("Fake", result.VerificationProvider);
        Assert.Equal(PhoneNumberLineType.Unknown, result.LineType);
    }

    [Fact]
    public async Task VerifyAsync_WhenProviderReturnsNull_ReturnsFailedResult()
    {
        // Arrange
        var provider = new FakeVerificationProvider(result: null);

        var manager = CreateManager([("Fake", provider)]);

        // Act
        var result = await manager.VerifyAsync("+17024993350", "Fake", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PhoneNumberVerificationStatus.Failed, result.Status);
    }

    [Fact]
    public async Task VerifyAsync_WithUnregisteredKey_Throws()
    {
        // Arrange
        var manager = CreateManager([("Fake", new FakeVerificationProvider())]);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.VerifyAsync("+17024993350", "Missing", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VerifyAsync_WithBlankPhoneNumber_Throws()
    {
        // Arrange
        var manager = CreateManager([("Fake", new FakeVerificationProvider())]);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => manager.VerifyAsync("  ", "Fake", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VerifyAsync_InvokesHandlersAroundVerification()
    {
        // Arrange
        var handler = new SpyHandler();
        var provider = new FakeVerificationProvider(new PhoneNumberVerificationResult
        {
            Status = PhoneNumberVerificationStatus.Verified,
        });

        var manager = CreateManager([("Fake", provider)], handlers: [handler]);

        // Act
        await manager.VerifyAsync("+17024993350", "Fake", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handler.VerifyingCalled);
        Assert.True(handler.VerifiedCalled);
        Assert.NotNull(handler.VerifiedResult);
    }

    [Fact]
    public async Task GetDefaultProviderKeyAsync_HonorsSelectedProvider()
    {
        // Arrange
        var manager = CreateManager(
            [("First", new FakeVerificationProvider()), ("Second", new FakeVerificationProvider())],
            selectedProvider: "Second");

        // Act
        var key = await manager.GetDefaultProviderKeyAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Second", key);
    }

    [Fact]
    public async Task GetDefaultProviderKeyAsync_FallsBackToFirstProvider()
    {
        // Arrange
        var manager = CreateManager(
            [("First", new FakeVerificationProvider()), ("Second", new FakeVerificationProvider())],
            selectedProvider: null);

        // Act
        var key = await manager.GetDefaultProviderKeyAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("First", key);
    }

    [Fact]
    public async Task GetDefaultProviderKeyAsync_WhenNoProviders_ReturnsNull()
    {
        // Arrange
        var manager = CreateManager([], selectedProvider: null);

        // Act
        var key = await manager.GetDefaultProviderKeyAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(key);
    }

    private static DefaultPhoneNumberVerificationManager CreateManager(
        (string Key, IPhoneNumberVerificationProvider Provider)[] providers,
        IEnumerable<IPhoneNumberVerificationHandler> handlers = null,
        string selectedProvider = null)
    {
        var services = new ServiceCollection();
        var options = new PhoneNumberVerificationProviderOptions();

        foreach (var (key, provider) in providers)
        {
            services.AddKeyedSingleton(key, provider);
            options.Providers[key] = new PhoneNumberVerificationProviderDescriptor(key);
        }

        var serviceProvider = services.BuildServiceProvider();

        var settings = new PhoneNumberVerificationsSettings
        {
            SelectedProvider = selectedProvider,
        };

        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<PhoneNumberVerificationsSettings>())
            .Returns(settings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync())
            .ReturnsAsync(site.Object);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new DefaultPhoneNumberVerificationManager(
            serviceProvider,
            Options.Create(options),
            siteService.Object,
            handlers ?? [],
            new DefaultPhoneNumberService(),
            clock.Object,
            NullLogger<DefaultPhoneNumberVerificationManager>.Instance);
    }

    private sealed class FakeVerificationProvider : IPhoneNumberVerificationProvider
    {
        private readonly PhoneNumberVerificationResult _result;
        private readonly Exception _exception;

        public FakeVerificationProvider(
            PhoneNumberVerificationResult result = null,
            Exception exception = null)
        {
            _result = result;
            _exception = exception;
        }

        public string LastPhoneNumber { get; private set; }

        public Task<PhoneNumberVerificationResult> VerifyAsync(
            string phoneNumber,
            CancellationToken cancellationToken = default)
        {
            LastPhoneNumber = phoneNumber;

            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_result);
        }
    }

    private sealed class SpyHandler : PhoneNumberVerificationHandlerBase
    {
        public bool VerifyingCalled { get; private set; }

        public bool VerifiedCalled { get; private set; }

        public PhoneNumberVerificationResult VerifiedResult { get; private set; }

        public override Task VerifyingAsync(
            PhoneNumberVerificationContext context,
            CancellationToken cancellationToken = default)
        {
            VerifyingCalled = true;

            return Task.CompletedTask;
        }

        public override Task VerifiedAsync(
            PhoneNumberVerificationContext context,
            CancellationToken cancellationToken = default)
        {
            VerifiedCalled = true;
            VerifiedResult = context.Result;

            return Task.CompletedTask;
        }
    }
}
