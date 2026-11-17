using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using CrestApps.Core.AI.Claude.Models;
using CrestApps.Core.AI.Claude.Services;
using CrestApps.Core.AI.Copilot.Models;
using CrestApps.OrchardCore.AI.Chat.Claude.Drivers;
using CrestApps.OrchardCore.AI.Chat.Claude.ViewModels;
using CrestApps.OrchardCore.AI.Chat.Copilot.Drivers;
using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using CrestApps.OrchardCore.AI.Chat.Copilot.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.AI.Chat;

/// <summary>
/// F10 (fix verification, round 7): the Claude settings and the Copilot settings in its own-key mode kept the stored API
/// key when the box came back blank, whatever base URL they now named, so a user who may change the settings, but never
/// saw the key, could send it to a host of their own with the next chat. Found while fixing F10: both drivers checked
/// their manage permission only when building the editor, and saved a base URL on the site's own network.
/// </summary>
public sealed class OrchestratorSettingsSecretDestinationTests
{
    private readonly EphemeralDataProtectionProvider _protection = new();

    [Theory]
    [InlineData("https://attacker.example", false)]
    [InlineData("http://api.anthropic.com", false)]
    [InlineData("https://API.anthropic.com/v1", true)]
    [InlineData("", true)]
    public async Task Claude_KeepsTheStoredKey_OnlyForTheSameOrigin(string baseUrl, bool kept)
    {
        var stored = _protection.CreateProtector("CrestApps.OrchardCore.AI.Chat.Claude.Settings").Protect("sk-ant");
        var settings = ClaudeStored(stored);
        var context = PostedFormUpdateModel.CreateContext(new ClaudeSettingsViewModel
        {
            AuthenticationType = ClaudeAuthenticationType.ApiKey,
            BaseUrl = baseUrl,
        });

        await ClaudeDriver(allowed: true).UpdateAsync(new SiteSettings(), settings, context);

        Assert.Equal(kept, settings.ProtectedApiKey == stored);
        Assert.Equal(kept, context.Updater.ModelState.IsValid);
    }

    [Theory]
    [InlineData("https://attacker.example/v1", false)]
    [InlineData("https://models.vendor.example:8443/v1", false)]
    [InlineData("https://models.vendor.example/v2", true)]
    public async Task Copilot_KeepsTheStoredKey_OnlyForTheSameOrigin(string baseUrl, bool kept)
    {
        var stored = _protection.CreateProtector("CrestApps.OrchardCore.AI.Chat.Copilot.Settings").Protect("sk-own");
        var settings = CopilotStored(stored);
        var context = PostedFormUpdateModel.CreateContext(CopilotPosted(baseUrl));

        await CopilotDriver(allowed: true).UpdateAsync(new SiteSettings(), settings, context);

        Assert.Equal(kept, settings.ProtectedApiKey == stored);
    }

    [Fact]
    public async Task Claude_WithoutTheManagePermission_ChangesNothing()
    {
        var stored = _protection.CreateProtector("CrestApps.OrchardCore.AI.Chat.Claude.Settings").Protect("sk-ant");
        var settings = ClaudeStored(stored);
        var context = PostedFormUpdateModel.CreateContext(new ClaudeSettingsViewModel
        {
            AuthenticationType = ClaudeAuthenticationType.ApiKey,
            BaseUrl = "https://attacker.example",
            ApiKey = "sk-attacker",
            DefaultModel = "other",
        });

        var result = await ClaudeDriver(allowed: false).UpdateAsync(new SiteSettings(), settings, context);

        Assert.Null(result);
        Assert.Equal("https://api.anthropic.com", settings.BaseUrl);
        Assert.Equal(stored, settings.ProtectedApiKey);
        Assert.Null(settings.DefaultModel);
    }

    [Fact]
    public async Task Copilot_WithoutTheManagePermission_ChangesNothing()
    {
        var stored = _protection.CreateProtector("CrestApps.OrchardCore.AI.Chat.Copilot.Settings").Protect("sk-own");
        var settings = CopilotStored(stored);
        var posted = CopilotPosted("https://attacker.example/v1");
        posted.ApiKey = "sk-attacker";
        var context = PostedFormUpdateModel.CreateContext(posted);

        var result = await CopilotDriver(allowed: false).UpdateAsync(new SiteSettings(), settings, context);

        Assert.Null(result);
        Assert.Equal("https://models.vendor.example/v1", settings.BaseUrl);
        Assert.Equal(stored, settings.ProtectedApiKey);
    }

    [Theory]
    [InlineData("http://127.0.0.1:11434")]
    [InlineData("http://169.254.169.254/latest")]
    [InlineData("https://models.internal.example")]
    public async Task Claude_ABaseUrlOnTheSitesOwnNetwork_IsRefused(string baseUrl)
    {
        var settings = new ClaudeSettings();
        var context = PostedFormUpdateModel.CreateContext(new ClaudeSettingsViewModel
        {
            AuthenticationType = ClaudeAuthenticationType.ApiKey,
            BaseUrl = baseUrl,
            ApiKey = "sk-ant",
        });

        await ClaudeDriver(allowed: true).UpdateAsync(new SiteSettings(), settings, context);

        Assert.False(context.Updater.ModelState.IsValid);
        Assert.Contains(context.Updater.ModelState, entry => entry.Key.EndsWith(nameof(ClaudeSettingsViewModel.BaseUrl), StringComparison.Ordinal) && entry.Value.Errors.Count > 0);
    }

    [Theory]
    [InlineData("http://127.0.0.1:11434/v1")]
    [InlineData("https://models.internal.example/v1")]
    public async Task Copilot_ABaseUrlOnTheSitesOwnNetwork_IsRefused(string baseUrl)
    {
        var settings = new CopilotSettings();
        var posted = CopilotPosted(baseUrl);
        posted.ApiKey = "sk-own";
        var context = PostedFormUpdateModel.CreateContext(posted);

        await CopilotDriver(allowed: true).UpdateAsync(new SiteSettings(), settings, context);

        Assert.False(context.Updater.ModelState.IsValid);
        Assert.Contains(context.Updater.ModelState, entry => entry.Key.EndsWith(nameof(CopilotSettingsViewModel.BaseUrl), StringComparison.Ordinal) && entry.Value.Errors.Count > 0);
    }

    private static ClaudeSettings ClaudeStored(string protectedKey)
        => new()
        {
            AuthenticationType = ClaudeAuthenticationType.ApiKey,
            BaseUrl = "https://api.anthropic.com",
            ProtectedApiKey = protectedKey,
        };

    private static CopilotSettings CopilotStored(string protectedKey)
        => new()
        {
            AuthenticationType = CopilotAuthenticationType.ApiKey,
            ProviderType = "openai",
            BaseUrl = "https://models.vendor.example/v1",
            DefaultModel = "model",
            ProtectedApiKey = protectedKey,
        };

    private static CopilotSettingsViewModel CopilotPosted(string baseUrl)
        => new()
        {
            AuthenticationType = CopilotAuthenticationType.ApiKey,
            ProviderType = "openai",
            BaseUrl = baseUrl,
            DefaultModel = "model",
        };

    private ClaudeSettingsDisplayDriver ClaudeDriver(bool allowed)
        => new(
            Signed(),
            Authorization(allowed),
            _protection,
            (ClaudeClientService)RuntimeHelpers.GetUninitializedObject(typeof(ClaudeClientService)),
            EndpointPolicy(),
            new PassThroughStringLocalizer<ClaudeSettingsDisplayDriver>());

    private CopilotSettingsDisplayDriver CopilotDriver(bool allowed)
        => new(
            Signed(),
            Authorization(allowed),
            _protection,
            (CopilotCallbackUrlProvider)RuntimeHelpers.GetUninitializedObject(typeof(CopilotCallbackUrlProvider)),
            Mock.Of<IOptionsUpdateNotifier>(),
            EndpointPolicy(),
            Mock.Of<IHtmlLocalizer<CopilotSettingsDisplayDriver>>(),
            new PassThroughStringLocalizer<CopilotSettingsDisplayDriver>());

    // OrchardCore's permission check refuses a request without a user, so the post comes from a signed-in one.
    private static HttpContextAccessor Signed()
        => new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("Test")) } };

    private static IAuthorizationService Authorization(bool allowed)
    {
        var authorization = new Mock<IAuthorizationService>();

        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        return authorization.Object;
    }

    // The real policy, with a resolver that answers a private address for internal host names and a public one otherwise.
    private static AIEndpointPolicy EndpointPolicy()
        => new(null, Options.Create(new AIEgressOptions()), host => host.EndsWith(".internal.example", StringComparison.OrdinalIgnoreCase)
            ? [IPAddress.Parse("10.0.0.5")]
            : [IPAddress.Parse("93.184.215.14")]);

    private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
