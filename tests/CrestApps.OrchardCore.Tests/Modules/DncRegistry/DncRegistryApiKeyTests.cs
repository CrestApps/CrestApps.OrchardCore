using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.DncRegistry.Drivers;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.Services;
using CrestApps.OrchardCore.DncRegistry.ViewModels;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.DncRegistry;

/// <summary>
/// Pins how the national do-not-call registries read the API key the settings editor saved. The registries used
/// to unprotect the key with a purpose the settings editor never protected it with, so a key saved through the
/// admin could never be decrypted and every screening call crashed with a <see cref="CryptographicException"/>.
/// </summary>
public sealed class DncRegistryApiKeyTests
{
    private const string FakeFtcApiKey = "fake-ftc-api-key";
    private const string FakeDnclApiKey = "fake-dncl-api-key";

    private static readonly PhoneNumber _usNumber = PhoneNumber.FromE164("+14255551212");

    [Fact]
    public async Task UsaFtcDncRegistry_DecryptsKeySavedBySettingsDriver()
    {
        // Arrange
        // The driver and the registry share one tenant key ring, as they do at runtime. A real (purpose-sensitive)
        // protector is used so a purpose mismatch between the two fails here instead of passing silently.
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var settings = new UsaFtcDncRegistrySettings();
        var context = PostedFormUpdateModel.CreateContext(new UsaFtcDncRegistrySettingsViewModel
        {
            ApiKey = FakeFtcApiKey,
            OrganizationId = "org-1",
            BaseUrl = "https://ftc.example.test/api",
        });

        var driver = new UsaFtcDncRegistrySettingsDisplayDriver(
            CreateHttpContextAccessor(),
            CreateAuthorizationService(),
            dataProtectionProvider,
            CreateLocalizer<UsaFtcDncRegistrySettingsDisplayDriver>());

        await driver.UpdateAsync(Mock.Of<ISite>(), settings, context);

        Assert.True(context.Updater.ModelState.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(settings.ProtectedApiKey));
        Assert.NotEqual(FakeFtcApiKey, settings.ProtectedApiKey);

        var handler = new RecordingHttpMessageHandler("""{ "IsOnDnc": true }""");
        var registry = CreateUsaFtcRegistry(settings, handler, dataProtectionProvider, new RecordingLogger<UsaFtcDncRegistry>());

        // Act
        var registered = await registry.GetRegisteredNumbersAsync([_usNumber], TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(handler.Requests);
        var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
        Assert.Equal(FakeFtcApiKey, query["api_key"].ToString());
        Assert.Equal("4255551212", query["PhoneNumber"].ToString());
        Assert.Contains(_usNumber, registered);
    }

    [Fact]
    public async Task UsaFtcDncRegistry_DecryptsKeyProtectedWithSharedPurpose()
    {
        // Arrange
        // The purpose constant is the contract between writer and reader; a key protected with it must be readable.
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var settings = new UsaFtcDncRegistrySettings
        {
            ProtectedApiKey = dataProtectionProvider
                .CreateProtector(DncRegistryConstants.DataProtectionPurposes.UsaFtcApiKey)
                .Protect(FakeFtcApiKey),
            OrganizationId = "org-1",
            BaseUrl = "https://ftc.example.test/api",
        };

        var handler = new RecordingHttpMessageHandler("""{ "IsOnDnc": false }""");
        var registry = CreateUsaFtcRegistry(settings, handler, dataProtectionProvider, new RecordingLogger<UsaFtcDncRegistry>());

        // Act
        var registered = await registry.GetRegisteredNumbersAsync([_usNumber], TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.Equal(FakeFtcApiKey, QueryHelpers.ParseQuery(request.RequestUri.Query)["api_key"].ToString());
        Assert.Empty(registered);
    }

    [Fact]
    public async Task CanadaDnclRegistry_DecryptsKeySavedBySettingsDriver()
    {
        // Arrange
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var settings = new CanadaDnclRegistrySettings();
        var context = PostedFormUpdateModel.CreateContext(new CanadaDnclRegistrySettingsViewModel
        {
            ApiKey = FakeDnclApiKey,
            AccountNumber = "account-1",
            BaseUrl = "https://dncl.example.test/api",
        });

        var driver = new CanadaDnclRegistrySettingsDisplayDriver(
            CreateHttpContextAccessor(),
            CreateAuthorizationService(),
            dataProtectionProvider,
            CreateLocalizer<CanadaDnclRegistrySettingsDisplayDriver>());

        await driver.UpdateAsync(Mock.Of<ISite>(), settings, context);

        Assert.True(context.Updater.ModelState.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(settings.ProtectedApiKey));
        Assert.NotEqual(FakeDnclApiKey, settings.ProtectedApiKey);

        var handler = new RecordingHttpMessageHandler("""{ "IsRegistered": true }""");
        var registry = CreateCanadaDnclRegistry(settings, handler, dataProtectionProvider, new RecordingLogger<CanadaDnclRegistry>());

        // Act
        var registered = await registry.GetRegisteredNumbersAsync([_usNumber], TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.True(request.Headers.TryGetValues("x-api-key", out var apiKeyValues));
        Assert.Equal(FakeDnclApiKey, Assert.Single(apiKeyValues));
        Assert.EndsWith("/DNCLNumbers/4255551212", request.RequestUri.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains(_usNumber, registered);
    }

    [Fact]
    public async Task CanadaDnclRegistry_DecryptsKeyProtectedWithSharedPurpose()
    {
        // Arrange
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var settings = new CanadaDnclRegistrySettings
        {
            ProtectedApiKey = dataProtectionProvider
                .CreateProtector(DncRegistryConstants.DataProtectionPurposes.CanadaDnclApiKey)
                .Protect(FakeDnclApiKey),
            AccountNumber = "account-1",
            BaseUrl = "https://dncl.example.test/api",
        };

        var handler = new RecordingHttpMessageHandler("""{ "IsRegistered": false }""");
        var registry = CreateCanadaDnclRegistry(settings, handler, dataProtectionProvider, new RecordingLogger<CanadaDnclRegistry>());

        // Act
        var registered = await registry.GetRegisteredNumbersAsync([_usNumber], TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.True(request.Headers.TryGetValues("x-api-key", out var apiKeyValues));
        Assert.Equal(FakeDnclApiKey, Assert.Single(apiKeyValues));
        Assert.Empty(registered);
    }

    [Fact]
    public async Task UsaFtcDncRegistry_UnreadableKey_ThrowsScreeningExceptionAndLogsWarning()
    {
        // Arrange
        // A key protected under a different key ring (for example after the tenant's keys were replaced) cannot be
        // read. That used to escape screening as a CryptographicException; it must be reported as the registry
        // being unavailable so callers treat the number as unscreened.
        var settings = new UsaFtcDncRegistrySettings
        {
            ProtectedApiKey = ProtectWithAnotherKeyRing(DncRegistryConstants.DataProtectionPurposes.UsaFtcApiKey, FakeFtcApiKey),
            OrganizationId = "org-1",
            BaseUrl = "https://ftc.example.test/api",
        };

        var handler = new RecordingHttpMessageHandler("""{ "IsOnDnc": false }""");
        var logger = new RecordingLogger<UsaFtcDncRegistry>();
        var registry = CreateUsaFtcRegistry(settings, handler, new EphemeralDataProtectionProvider(), logger);

        // Act
        var exception = await Assert.ThrowsAsync<DoNotCallScreeningException>(
            () => registry.GetRegisteredNumbersAsync([_usNumber], TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(registry.Key, exception.RegistryKey);
        Assert.IsAssignableFrom<CryptographicException>(exception.InnerException);
        Assert.Equal(1, logger.Count(LogLevel.Warning));
        Assert.Equal(0, logger.Count(LogLevel.Error));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CanadaDnclRegistry_UnreadableKey_ThrowsScreeningExceptionAndLogsWarning()
    {
        // Arrange
        var settings = new CanadaDnclRegistrySettings
        {
            ProtectedApiKey = ProtectWithAnotherKeyRing(DncRegistryConstants.DataProtectionPurposes.CanadaDnclApiKey, FakeDnclApiKey),
            AccountNumber = "account-1",
            BaseUrl = "https://dncl.example.test/api",
        };

        var handler = new RecordingHttpMessageHandler("""{ "IsRegistered": false }""");
        var logger = new RecordingLogger<CanadaDnclRegistry>();
        var registry = CreateCanadaDnclRegistry(settings, handler, new EphemeralDataProtectionProvider(), logger);

        // Act
        var exception = await Assert.ThrowsAsync<DoNotCallScreeningException>(
            () => registry.GetRegisteredNumbersAsync([_usNumber], TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(registry.Key, exception.RegistryKey);
        Assert.IsAssignableFrom<CryptographicException>(exception.InnerException);
        Assert.Equal(1, logger.Count(LogLevel.Warning));
        Assert.Equal(0, logger.Count(LogLevel.Error));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UsaFtcDncRegistry_NoAddressableNumbers_DoesNotReadKey()
    {
        // Arrange
        // A batch the registry cannot answer for (no North American numbers) must not depend on the saved key being
        // readable: the key is unreadable here, yet the batch comes back empty without any attempt to unprotect it.
        var settings = new UsaFtcDncRegistrySettings
        {
            ProtectedApiKey = ProtectWithAnotherKeyRing(DncRegistryConstants.DataProtectionPurposes.UsaFtcApiKey, FakeFtcApiKey),
            OrganizationId = "org-1",
            BaseUrl = "https://ftc.example.test/api",
        };

        var dataProtectionProvider = new Mock<IDataProtectionProvider>();
        var handler = new RecordingHttpMessageHandler("""{ "IsOnDnc": true }""");
        var logger = new RecordingLogger<UsaFtcDncRegistry>();
        var registry = CreateUsaFtcRegistry(settings, handler, dataProtectionProvider.Object, logger);

        // Act
        var registered = await registry.GetRegisteredNumbersAsync(CreateUnaddressableBatch(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(registered);
        Assert.Empty(handler.Requests);
        dataProtectionProvider.Verify(provider => provider.CreateProtector(It.IsAny<string>()), Times.Never);
        Assert.Equal(0, logger.Count(LogLevel.Warning));
    }

    [Fact]
    public async Task CanadaDnclRegistry_NoAddressableNumbers_DoesNotReadKey()
    {
        // Arrange
        var settings = new CanadaDnclRegistrySettings
        {
            ProtectedApiKey = ProtectWithAnotherKeyRing(DncRegistryConstants.DataProtectionPurposes.CanadaDnclApiKey, FakeDnclApiKey),
            AccountNumber = "account-1",
            BaseUrl = "https://dncl.example.test/api",
        };

        var dataProtectionProvider = new Mock<IDataProtectionProvider>();
        var handler = new RecordingHttpMessageHandler("""{ "IsRegistered": true }""");
        var logger = new RecordingLogger<CanadaDnclRegistry>();
        var registry = CreateCanadaDnclRegistry(settings, handler, dataProtectionProvider.Object, logger);

        // Act
        var registered = await registry.GetRegisteredNumbersAsync(CreateUnaddressableBatch(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(registered);
        Assert.Empty(handler.Requests);
        dataProtectionProvider.Verify(provider => provider.CreateProtector(It.IsAny<string>()), Times.Never);
        Assert.Equal(0, logger.Count(LogLevel.Warning));
    }

    private static PhoneNumber[] CreateUnaddressableBatch()
        => [PhoneNumber.FromE164("+442071838750"), PhoneNumber.FromE164("+33142685300"), default];

    private static string ProtectWithAnotherKeyRing(string purpose, string value)
        => new EphemeralDataProtectionProvider().CreateProtector(purpose).Protect(value);

    private static UsaFtcDncRegistry CreateUsaFtcRegistry(
        UsaFtcDncRegistrySettings settings,
        HttpMessageHandler handler,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<UsaFtcDncRegistry> logger)
        => new(
            CreateHttpClientFactory(nameof(UsaFtcDncRegistry), handler),
            CreateSiteService(settings),
            dataProtectionProvider,
            CreateLocalizer<UsaFtcDncRegistry>(),
            logger);

    private static CanadaDnclRegistry CreateCanadaDnclRegistry(
        CanadaDnclRegistrySettings settings,
        HttpMessageHandler handler,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<CanadaDnclRegistry> logger)
        => new(
            CreateHttpClientFactory(nameof(CanadaDnclRegistry), handler),
            CreateSiteService(settings),
            dataProtectionProvider,
            CreateLocalizer<CanadaDnclRegistry>(),
            logger);

    private static IHttpClientFactory CreateHttpClientFactory(string clientName, HttpMessageHandler handler)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(clientName))
            .Returns(new HttpClient(handler));

        return httpClientFactory.Object;
    }

    private static ISiteService CreateSiteService<TSettings>(TSettings settings)
        where TSettings : class, new()
    {
        var site = new Mock<ISite>();
        site.Setup(site => site.GetOrCreate<TSettings>())
            .Returns(settings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync())
            .ReturnsAsync(site.Object);

        return siteService.Object;
    }

    private static HttpContextAccessor CreateHttpContextAccessor()
        => new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], "test")),
            },
        };

    private static IAuthorizationService CreateAuthorizationService()
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        authorizationService
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        return authorizationService.Object;
    }

    private static IStringLocalizer<T> CreateLocalizer<T>()
    {
        var localizer = new Mock<IStringLocalizer<T>>();

        localizer
            .Setup(localizer => localizer[It.IsAny<string>()])
            .Returns((string name) => new LocalizedString(name, name));

        return localizer.Object;
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _payload;

        public RecordingHttpMessageHandler(string payload)
        {
            _payload = payload;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_payload, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly List<LogLevel> _levels = [];

        public int Count(LogLevel level)
            => _levels.Count(logged => logged == level);

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            _levels.Add(logLevel);
        }
    }
}
