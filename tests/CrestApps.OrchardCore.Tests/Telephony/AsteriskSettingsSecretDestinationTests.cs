using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Deployments;
using CrestApps.OrchardCore.Asterisk.Drivers;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Recipes;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Asterisk.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Entities;
using OrchardCore.Environment.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// F10 (fix verification, round 7): the Asterisk settings kept the stored ARI password when the box came back blank,
/// whatever ARI address they now named, so a user who may change the telephony settings, but never saw the password,
/// could send it to a server of their own with the next ARI call. The settings import is held to the same rule.
/// </summary>
public sealed class AsteriskSettingsSecretDestinationTests
{
    private const string StoredBaseUrl = "https://pbx.example.test:8089/ari";

    private readonly EphemeralDataProtectionProvider _protection = new();

    [Theory]
    [InlineData("https://pbx.attacker.test:8089/ari", false)]
    [InlineData("https://pbx.example.test:8443/ari", false)]
    [InlineData("https://PBX.example.test:8089/ari/", true)]
    public async Task Editor_KeepsTheStoredPassword_OnlyForTheSameOrigin(string baseUrl, bool kept)
    {
        var settings = StoredSettings();
        var stored = settings.Password;
        var context = PostedFormUpdateModel.CreateContext(new AsteriskSettingsViewModel
        {
            IsEnabled = true,
            BaseUrl = baseUrl,
            UserName = "ari",
            ApplicationName = "crestapps",
        });

        await Driver().UpdateAsync(new SiteSettings(), settings, context);

        Assert.Equal(kept, settings.Password == stored);
        Assert.Equal(kept, !context.Updater.ModelState.Any(entry => entry.Key.EndsWith(nameof(AsteriskSettingsViewModel.Password), StringComparison.Ordinal) && entry.Value.Errors.Count > 0));
    }

    [Theory]
    [InlineData("https://pbx.attacker.test:8089/ari", null, false)]
    [InlineData("https://pbx.attacker.test:8089/ari", "typed-again", true)]
    [InlineData("https://pbx.example.test:8089/ari/v2", null, true)]
    public async Task Import_KeepsTheStoredPassword_OnlyForTheSameOrigin(string baseUrl, string password, bool hasPassword)
    {
        var site = new SiteSettings();
        var settings = StoredSettings();
        var stored = settings.Password;
        site.Put(settings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.LoadSiteSettingsAsync()).ReturnsAsync(site);

        var data = new JsonObject
        {
            [nameof(AsteriskSettings.IsEnabled)] = true,
            [nameof(AsteriskSettings.BaseUrl)] = baseUrl,
            [nameof(AsteriskSettings.UserName)] = "ari",
            [nameof(AsteriskSettings.ApplicationName)] = "crestapps",
        };

        if (password is not null)
        {
            data[nameof(AsteriskSettings.Password)] = password;
        }

        var step = new AsteriskSettingsStep(
            siteService.Object,
            _protection,
            Mock.Of<IOptionsUpdateNotifier>(),
            [],
            Mock.Of<IStringLocalizer<AsteriskSettingsStep>>());
        var context = new RecipeExecutionContext
        {
            Name = AsteriskDeploymentSteps.Settings,
            Step = new JsonObject { ["name"] = AsteriskDeploymentSteps.Settings, ["Settings"] = data },
        };

        await step.ExecuteAsync(context);

        var imported = site.As<AsteriskSettings>();

        Assert.Empty(context.Errors);
        Assert.Equal(hasPassword, !string.IsNullOrEmpty(imported.Password));
        Assert.Equal(password is null && hasPassword, imported.Password == stored);
    }

    private AsteriskSettings StoredSettings()
        => new()
        {
            IsEnabled = true,
            BaseUrl = StoredBaseUrl,
            UserName = "ari",
            ApplicationName = "crestapps",
            Password = _protection.CreateProtector(AsteriskConstants.ProtectorName).Protect("ari-password"),
        };

    private AsteriskSettingsDisplayDriver Driver()
    {
        var authorization = new Mock<IAuthorizationService>();

        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        return new AsteriskSettingsDisplayDriver(
            Mock.Of<IOptionsUpdateNotifier>(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("Test")) } },
            authorization.Object,
            _protection,
            Mock.Of<INotifier>(),
            Array.Empty<IAsteriskChannelTenantBindingStore>(),
            Mock.Of<IHtmlLocalizer<AsteriskSettingsDisplayDriver>>(),
            new PassThroughStringLocalizer());
    }

    private sealed class PassThroughStringLocalizer : IStringLocalizer<AsteriskSettingsDisplayDriver>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
