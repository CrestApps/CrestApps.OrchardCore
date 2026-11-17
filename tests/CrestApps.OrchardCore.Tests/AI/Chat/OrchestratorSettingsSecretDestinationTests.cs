using System.Runtime.CompilerServices;
using CrestApps.Core.AI.Claude.Models;
using CrestApps.Core.AI.Claude.Services;
using CrestApps.Core.AI.Copilot.Models;
using CrestApps.OrchardCore.AI.Chat.Claude.Drivers;
using CrestApps.OrchardCore.AI.Chat.Claude.ViewModels;
using CrestApps.OrchardCore.AI.Chat.Copilot.Drivers;
using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using CrestApps.OrchardCore.AI.Chat.Copilot.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Environment.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.AI.Chat;

/// <summary>
/// F10 (fix verification, round 7): the Claude settings and the Copilot settings in its own-key mode kept the stored API
/// key when the box came back blank, whatever base URL they now named, so a user who may change the settings, but never
/// saw the key, could send it to a host of their own with the next chat.
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
        var settings = new ClaudeSettings
        {
            AuthenticationType = ClaudeAuthenticationType.ApiKey,
            BaseUrl = "https://api.anthropic.com",
            ProtectedApiKey = stored,
        };
        var context = PostedFormUpdateModel.CreateContext(new ClaudeSettingsViewModel
        {
            AuthenticationType = ClaudeAuthenticationType.ApiKey,
            BaseUrl = baseUrl,
        });
        var driver = new ClaudeSettingsDisplayDriver(
            new HttpContextAccessor(),
            Mock.Of<IAuthorizationService>(),
            _protection,
            (ClaudeClientService)RuntimeHelpers.GetUninitializedObject(typeof(ClaudeClientService)),
            new PassThroughStringLocalizer<ClaudeSettingsDisplayDriver>());

        await driver.UpdateAsync(new SiteSettings(), settings, context);

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
        var settings = new CopilotSettings
        {
            AuthenticationType = CopilotAuthenticationType.ApiKey,
            ProviderType = "openai",
            BaseUrl = "https://models.vendor.example/v1",
            DefaultModel = "model",
            ProtectedApiKey = stored,
        };
        var context = PostedFormUpdateModel.CreateContext(new CopilotSettingsViewModel
        {
            AuthenticationType = CopilotAuthenticationType.ApiKey,
            ProviderType = "openai",
            BaseUrl = baseUrl,
            DefaultModel = "model",
        });
        var driver = new CopilotSettingsDisplayDriver(
            new HttpContextAccessor(),
            Mock.Of<IAuthorizationService>(),
            _protection,
            (CopilotCallbackUrlProvider)RuntimeHelpers.GetUninitializedObject(typeof(CopilotCallbackUrlProvider)),
            Mock.Of<IOptionsUpdateNotifier>(),
            Mock.Of<IHtmlLocalizer<CopilotSettingsDisplayDriver>>(),
            new PassThroughStringLocalizer<CopilotSettingsDisplayDriver>());

        await driver.UpdateAsync(new SiteSettings(), settings, context);

        Assert.Equal(kept, settings.ProtectedApiKey == stored);
    }

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
