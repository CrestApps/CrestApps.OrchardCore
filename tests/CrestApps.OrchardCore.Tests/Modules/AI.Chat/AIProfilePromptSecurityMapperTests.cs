using System.Linq.Expressions;
using CrestApps.Core.AI.Security;
using CrestApps.OrchardCore.AI.Chat.Drivers;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

public sealed class AIProfilePromptSecurityMapperTests
{
    [Fact]
    public void PopulateSiteDefaults_CopiesSiteLevelValues()
    {
        var options = new PromptSecurityOptions
        {
            MaxMessagesPerWindow = 20,
            RateLimitWindow = TimeSpan.FromSeconds(60),
            MaxAnonymousSessionsPerWindow = 5,
            AnonymousSessionRateLimitWindow = TimeSpan.FromMinutes(10),
        };
        var model = new AIProfilePromptSecurityViewModel();

        AIProfilePromptSecurityMapper.PopulateSiteDefaults(model, options);

        Assert.Equal(20, model.SiteMaxMessagesPerWindow);
        Assert.Equal(60, model.SiteRateLimitWindowSeconds);
        Assert.Equal(5, model.SiteMaxAnonymousSessionsPerWindow);
        Assert.Equal(600, model.SiteAnonymousSessionRateLimitWindowSeconds);
        Assert.Equal("5, 00:00:30; 30, 00:05:00; 150, 01:00:00; 500, 1.00:00:00", model.SiteAnonymousMessageRateLimitTiers);
        Assert.Equal("5, 00:00:30; 10, 00:05:00; 150, 01:00:00; 500, 1.00:00:00", model.SiteAnonymousSessionStartRateLimitTiers);
    }

    [Fact]
    public void PopulateOverrides_MapsTierListsToTextAndOptOut()
    {
        var settings = new PromptSecurityProfileSettings
        {
            AnonymousMessageRateLimitTiers =
            [
                new() { Limit = 3, Window = TimeSpan.FromSeconds(45) },
            ],
            AnonymousSessionStartRateLimitTiers = [],
        };
        var model = new AIProfilePromptSecurityViewModel();

        AIProfilePromptSecurityMapper.PopulateOverrides(model, settings);

        Assert.Equal("3, 00:00:45", model.AnonymousMessageRateLimitTiers);
        Assert.False(model.DisableAnonymousMessageRateLimitTiers);
        Assert.Empty(model.AnonymousSessionStartRateLimitTiers);
        Assert.True(model.DisableAnonymousSessionStartRateLimitTiers);
    }

    [Fact]
    public void PopulateOverrides_WhenTiersAreNull_LeavesTextBlankAndOptOutUnchecked()
    {
        var model = new AIProfilePromptSecurityViewModel();

        AIProfilePromptSecurityMapper.PopulateOverrides(model, new PromptSecurityProfileSettings());

        Assert.Empty(model.AnonymousMessageRateLimitTiers);
        Assert.False(model.DisableAnonymousMessageRateLimitTiers);
        Assert.Empty(model.AnonymousSessionStartRateLimitTiers);
        Assert.False(model.DisableAnonymousSessionStartRateLimitTiers);
    }

    [Fact]
    public void ApplyOverrides_WhenTiersProvided_StoresParsedTiers()
    {
        var model = new AIProfilePromptSecurityViewModel
        {
            AnonymousMessageRateLimitTiers = "3, 00:00:45",
            AnonymousSessionStartRateLimitTiers = "2, 00:10:00",
        };
        var settings = new PromptSecurityProfileSettings();

        AIProfilePromptSecurityMapper.ApplyOverrides(model, settings);

        var messageTier = Assert.Single(settings.AnonymousMessageRateLimitTiers);
        Assert.Equal(3, messageTier.Limit);
        Assert.Equal(TimeSpan.FromSeconds(45), messageTier.Window);

        var sessionTier = Assert.Single(settings.AnonymousSessionStartRateLimitTiers);
        Assert.Equal(2, sessionTier.Limit);
        Assert.Equal(TimeSpan.FromMinutes(10), sessionTier.Window);
    }

    [Fact]
    public void ApplyOverrides_WhenTiersDisabled_StoresEmptyListToOptOut()
    {
        var model = new AIProfilePromptSecurityViewModel
        {
            DisableAnonymousMessageRateLimitTiers = true,
            DisableAnonymousSessionStartRateLimitTiers = true,
        };
        var settings = new PromptSecurityProfileSettings();

        AIProfilePromptSecurityMapper.ApplyOverrides(model, settings);

        Assert.Empty(settings.AnonymousMessageRateLimitTiers);
        Assert.Empty(settings.AnonymousSessionStartRateLimitTiers);
    }

    [Fact]
    public void ApplyOverrides_WhenTiersBlank_ClearsStoredOptOutBackToInherit()
    {
        var settings = new PromptSecurityProfileSettings
        {
            AnonymousMessageRateLimitTiers = [],
            AnonymousSessionStartRateLimitTiers = [],
        };

        AIProfilePromptSecurityMapper.ApplyOverrides(new AIProfilePromptSecurityViewModel(), settings);

        Assert.Null(settings.AnonymousMessageRateLimitTiers);
        Assert.Null(settings.AnonymousSessionStartRateLimitTiers);
    }

    [Fact]
    public void Validate_WhenTierTextIsInvalid_AddsModelError()
    {
        var model = new AIProfilePromptSecurityViewModel
        {
            AnonymousMessageRateLimitTiers = "not a tier",
        };
        var updater = new TestUpdateModel();

        AIProfilePromptSecurityMapper.Validate(model, updater, "Prefix", NullStringLocalizer.Instance);

        Assert.False(updater.ModelState.IsValid);
    }

    [Fact]
    public void Validate_WhenTiersAreBothTypedAndDisabled_AddsModelError()
    {
        var model = new AIProfilePromptSecurityViewModel
        {
            AnonymousSessionStartRateLimitTiers = "5, 00:00:30",
            DisableAnonymousSessionStartRateLimitTiers = true,
        };
        var updater = new TestUpdateModel();

        AIProfilePromptSecurityMapper.Validate(model, updater, "Prefix", NullStringLocalizer.Instance);

        Assert.False(updater.ModelState.IsValid);
    }

    [Fact]
    public void Validate_WhenTiersAreDisabledAndBlank_AddsNoErrors()
    {
        var model = new AIProfilePromptSecurityViewModel
        {
            DisableAnonymousMessageRateLimitTiers = true,
            DisableAnonymousSessionStartRateLimitTiers = true,
        };
        var updater = new TestUpdateModel();

        AIProfilePromptSecurityMapper.Validate(model, updater, "Prefix", NullStringLocalizer.Instance);

        Assert.True(updater.ModelState.IsValid);
    }

    [Fact]
    public void ApplyOverrides_WhenValuesProvided_SetsThrottleOverrides()
    {
        var model = new AIProfilePromptSecurityViewModel
        {
            MaxMessagesPerWindow = 3,
            RateLimitWindowSeconds = 45,
            MaxAnonymousSessionsPerWindow = 1,
            AnonymousSessionRateLimitWindowSeconds = 120,
        };
        var settings = new PromptSecurityProfileSettings();

        AIProfilePromptSecurityMapper.ApplyOverrides(model, settings);

        Assert.Equal(3, settings.MaxMessagesPerWindow);
        Assert.Equal(TimeSpan.FromSeconds(45), settings.RateLimitWindow);
        Assert.Equal(1, settings.MaxAnonymousSessionsPerWindow);
        Assert.Equal(TimeSpan.FromSeconds(120), settings.AnonymousSessionRateLimitWindow);
    }

    [Fact]
    public void ApplyOverrides_WhenAllBlank_LeavesEverythingNullToInheritSiteDefaults()
    {
        var model = new AIProfilePromptSecurityViewModel();
        var settings = new PromptSecurityProfileSettings();

        AIProfilePromptSecurityMapper.ApplyOverrides(model, settings);

        Assert.Null(settings.MaxMessagesPerWindow);
        Assert.Null(settings.RateLimitWindow);
        Assert.Null(settings.MaxAnonymousSessionsPerWindow);
        Assert.Null(settings.AnonymousSessionRateLimitWindow);
    }

    [Fact]
    public void ApplyOverrides_WhenPartiallyProvided_OnlyOverridesProvidedFields()
    {
        var model = new AIProfilePromptSecurityViewModel
        {
            MaxMessagesPerWindow = 10,
            RateLimitWindowSeconds = null,
        };
        var settings = new PromptSecurityProfileSettings();

        AIProfilePromptSecurityMapper.ApplyOverrides(model, settings);

        Assert.Equal(10, settings.MaxMessagesPerWindow);
        Assert.Null(settings.RateLimitWindow);
        Assert.Null(settings.MaxAnonymousSessionsPerWindow);
        Assert.Null(settings.AnonymousSessionRateLimitWindow);
    }

    [Fact]
    public void PopulateOverrides_RoundTripsStoredSettings()
    {
        var settings = new PromptSecurityProfileSettings
        {
            MaxMessagesPerWindow = 8,
            RateLimitWindow = TimeSpan.FromSeconds(90),
            MaxAnonymousSessionsPerWindow = 2,
            AnonymousSessionRateLimitWindow = TimeSpan.FromSeconds(300),
        };
        var model = new AIProfilePromptSecurityViewModel();

        AIProfilePromptSecurityMapper.PopulateOverrides(model, settings);

        Assert.Equal(8, model.MaxMessagesPerWindow);
        Assert.Equal(90, model.RateLimitWindowSeconds);
        Assert.Equal(2, model.MaxAnonymousSessionsPerWindow);
        Assert.Equal(300, model.AnonymousSessionRateLimitWindowSeconds);
    }

    [Fact]
    public void Validate_WhenValuesInRangeOrBlank_AddsNoErrors()
    {
        var model = new AIProfilePromptSecurityViewModel
        {
            MaxMessagesPerWindow = 0,
            RateLimitWindowSeconds = null,
            MaxAnonymousSessionsPerWindow = 1000,
            AnonymousSessionRateLimitWindowSeconds = 86_400,
        };
        var updater = new TestUpdateModel();

        AIProfilePromptSecurityMapper.Validate(model, updater, "Prefix", NullStringLocalizer.Instance);

        Assert.True(updater.ModelState.IsValid);
    }

    [Theory]
    [InlineData(-1, null, null, null)]
    [InlineData(1001, null, null, null)]
    [InlineData(null, 0, null, null)]
    [InlineData(null, 86_401, null, null)]
    [InlineData(null, null, -1, null)]
    [InlineData(null, null, null, 0)]
    public void Validate_WhenValueOutOfRange_AddsModelError(
        int? maxMessages,
        int? rateWindow,
        int? maxAnonymous,
        int? anonymousWindow)
    {
        var model = new AIProfilePromptSecurityViewModel
        {
            MaxMessagesPerWindow = maxMessages,
            RateLimitWindowSeconds = rateWindow,
            MaxAnonymousSessionsPerWindow = maxAnonymous,
            AnonymousSessionRateLimitWindowSeconds = anonymousWindow,
        };
        var updater = new TestUpdateModel();

        AIProfilePromptSecurityMapper.Validate(model, updater, "Prefix", NullStringLocalizer.Instance);

        Assert.False(updater.ModelState.IsValid);
    }

    private sealed class TestUpdateModel : IUpdateModel
    {
        public ModelStateDictionary ModelState { get; } = new();

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model)
            where TModel : class
            => Task.FromResult(true);

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix)
            where TModel : class
            => Task.FromResult(true);

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix, params Expression<Func<TModel, object>>[] includeExpressions)
            where TModel : class
            => Task.FromResult(true);

        public bool TryValidateModel(object model)
            => true;

        public bool TryValidateModel(object model, string prefix)
            => true;
    }

    private sealed class NullStringLocalizer : IStringLocalizer
    {
        public static readonly NullStringLocalizer Instance = new();

        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
