using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Controllers;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins the dialer profile Clone action: the copy is stored through the real manager and handler, carries every setting
/// of its source under a new identity, and starts with none of the source's history.
/// </summary>
public sealed class DialerProfileCloneTests
{
    private const string SourceId = "source-profile";

    private static readonly DateTime _now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    // Members that belong to the stored record rather than to its settings, and the name the copy is given.
    private static readonly HashSet<string> _identityMembers = new(StringComparer.Ordinal)
    {
        nameof(DialerProfile.ItemId),
        nameof(DialerProfile.Name),
        nameof(DialerProfile.CreatedUtc),
        nameof(DialerProfile.ModifiedUtc),
    };

    [Fact]
    public async Task Clone_CopiesEverySettingIncludingBagParts_UnderANewIdentity()
    {
        // Arrange
        var source = CreateConfiguredProfile();
        var harness = Harness.Create(source);

        // Act
        var result = await harness.Controller.Clone(SourceId);

        // Assert
        var clone = Assert.Single(harness.Created);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Edit", redirect.ActionName);
        Assert.Equal(clone.ItemId, redirect.RouteValues["id"]);

        Assert.False(string.IsNullOrEmpty(clone.ItemId));
        Assert.NotEqual(SourceId, clone.ItemId);
        Assert.Equal("Outbound sales (copy)", clone.Name);
        Assert.Equal(_now, clone.CreatedUtc);
        Assert.Null(clone.ModifiedUtc);

        // Every other public setting, including any added later, matches the source.
        foreach (var property in GetSettingProperties())
        {
            Assert.True(
                JsonSerializer.Serialize(property.GetValue(source)) == JsonSerializer.Serialize(property.GetValue(clone)),
                $"The clone did not keep '{property.Name}'.");
        }

        var bagPart = Assert.IsType<JsonObject>(clone.Properties["OutboundExtras"]);
        Assert.Equal("weekday", bagPart["Schedule"]?.GetValue<string>());
        Assert.Equal("cal-ca", clone.RegionalCallingCalendarIds["CA"]);

        // The copy is a separate object: nothing done to it reaches the profile it came from.
        bagPart["Schedule"] = "weekend";
        clone.RegionalCallingCalendarIds["CA"] = "changed";
        Assert.Equal("weekday", ((JsonObject)source.Properties["OutboundExtras"])["Schedule"]?.GetValue<string>());
        Assert.Equal("cal-ca", source.RegionalCallingCalendarIds["CA"]);
        Assert.Equal("Outbound sales", source.Name);
        Assert.Equal(new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc), source.CreatedUtc);

        harness.Store.Verify(store => store.UpdateAsync(It.IsAny<DialerProfile>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal([NotifyType.Success], harness.Messages.Select(message => message.Type));
    }

    [Fact]
    public async Task Clone_IsNamedSoNoOtherProfileHoldsTheName()
    {
        // Arrange
        var source = CreateConfiguredProfile();
        var harness = Harness.Create(
            source,
            others:
            [
                new DialerProfile { ItemId = "other-1", Name = "Outbound sales (copy)" },
                new DialerProfile { ItemId = "other-2", Name = "OUTBOUND SALES (COPY 2)" },
            ]);

        // Act
        await harness.Controller.Clone(SourceId);

        // Assert
        var clone = Assert.Single(harness.Created);
        Assert.Equal("Outbound sales (copy 3)", clone.Name);
    }

    [Fact]
    public async Task Clone_OfAProfileWithTheLongestName_StillFitsTheStoredName()
    {
        // Arrange
        var source = CreateConfiguredProfile();
        source.Name = new string('a', 255);
        var harness = Harness.Create(source);

        // Act
        await harness.Controller.Clone(SourceId);

        // Assert
        var clone = Assert.Single(harness.Created);
        Assert.True(clone.Name.Length <= 255);
        Assert.EndsWith(" (copy)", clone.Name, StringComparison.Ordinal);
    }

    // The connect wait is only allowed once the profile's own connected calls prove it; the copy has none, so it starts
    // without one rather than being refused, and the operator is told why.
    [Fact]
    public async Task Clone_OfAPredictiveProfile_KeepsItsPacingButStartsWithoutAConnectWait()
    {
        // Arrange
        var source = CreateConfiguredProfile();
        source.Mode = DialerMode.Predictive;
        source.ConnectWaitMilliseconds = 800;
        var harness = Harness.Create(source);

        // Act
        var result = await harness.Controller.Clone(SourceId);

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        var clone = Assert.Single(harness.Created);
        Assert.Equal(0, clone.ConnectWaitMilliseconds);
        Assert.Equal(800, source.ConnectWaitMilliseconds);
        Assert.Equal(DialerMode.Predictive, clone.Mode);
        Assert.Equal(PredictivePacingModel.OverDial, clone.PredictivePacingModel);
        Assert.Equal(1.5, clone.TargetAbandonmentRatePercent);
        Assert.Equal(3.5, clone.MaxLinesPerAgent);
        Assert.Equal(250, clone.MaxCallsInFlight);
        Assert.True(clone.CreditAgentsFreeingUp);
        Assert.Equal(40, clone.FreeUpCreditPercent);
        Assert.Equal([NotifyType.Success, NotifyType.Information], harness.Messages.Select(message => message.Type));
        Assert.Contains("connect wait", harness.Messages[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clone_TheRulesRefuse_IsNotStoredAndTheReasonIsShown()
    {
        // Arrange
        var source = CreateConfiguredProfile();
        var harness = Harness.Create(source, automatedDialerEnabled: false);

        // Act
        var result = await harness.Controller.Clone(SourceId);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Empty(harness.Created);
        var (type, message) = Assert.Single(harness.Messages);
        Assert.Equal(NotifyType.Error, type);
        Assert.Contains("Paced Dialing", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clone_OfAProfileThatNoLongerExists_IsNotFound()
    {
        // Arrange
        var harness = Harness.Create(CreateConfiguredProfile());

        // Act
        var result = await harness.Controller.Clone("missing");

        // Assert
        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(harness.Created);
    }

    [Fact]
    public async Task Clone_WithoutPermission_IsForbidden()
    {
        // Arrange
        var harness = Harness.Create(CreateConfiguredProfile(), authorized: false);

        // Act
        var result = await harness.Controller.Clone(SourceId);

        // Assert
        Assert.IsType<ForbidResult>(result);
        Assert.Empty(harness.Created);
    }

    [Fact]
    public void Clone_IsAPostOnly()
    {
        var method = typeof(DialerProfilesController).GetMethod(nameof(DialerProfilesController.Clone));

        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
    }

    private static IEnumerable<PropertyInfo> GetSettingProperties()
        => typeof(DialerProfile)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead
                && property.CanWrite
                && property.GetIndexParameters().Length == 0
                && property.GetCustomAttribute<JsonIgnoreAttribute>() is null
                && !_identityMembers.Contains(property.Name));

    // Every setting differs from its default, so a member the copy dropped would show up as a mismatch.
    private static DialerProfile CreateConfiguredProfile()
    {
        var profile = new DialerProfile
        {
            ItemId = SourceId,
            Name = "Outbound sales",
            Description = "Weekday outbound sales",
            Mode = DialerMode.Power,
            ProviderName = "Contoso",
            CallsPerAgent = 2,
            MaxAttempts = 5,
            RetryDelayMinutes = 90,
            AnsweringMachineDetection = DialerAnsweringMachineDetection.Premium,
            RingTimeoutSeconds = 25,
            PreviewExtensionSeconds = 45,
            MaxPreviewExtensions = 4,
            CallerId = "+17025550100",
            AlwaysUseCallerId = true,
            DefaultRegionCode = "US",
            RespectDoNotCall = false,
            EnforceCallingWindow = true,
            CallingCalendarId = "cal-us",
            RegionalCallingCalendarIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CA"] = "cal-ca",
            },
            EnforceAbandonmentCap = true,
            MaxAbandonmentRatePercent = 2.5,
            AbandonmentSampleFloor = 40,
            SafeHarborEnabled = true,
            SafeHarborMessage = "Sorry we missed you.",
            PredictivePacingModel = PredictivePacingModel.OverDial,
            TargetAbandonmentRatePercent = 1.5,
            MaxLinesPerAgent = 3.5,
            MaxCallsInFlight = 250,
            AnswerRateSampleFloor = 75,
            AnswerRateWindowMinutes = 30,
            CreditAgentsFreeingUp = true,
            FreeUpCreditPercent = 40,
            ConnectWaitMilliseconds = 0,
            AbandonedRetryRequiresAgent = false,
            Enabled = false,
            CreatedUtc = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            ModifiedUtc = new DateTime(2025, 6, 7, 8, 9, 10, DateTimeKind.Utc),
        };

        profile.Properties["OutboundExtras"] = new JsonObject
        {
            ["Schedule"] = "weekday",
        };

        return profile;
    }

    private sealed class Harness
    {
        public Mock<IDialerProfileStore> Store { get; } = new();

        public Mock<INotifier> Notifier { get; } = new();

        public List<DialerProfile> Created { get; } = [];

        public List<(NotifyType Type, string Message)> Messages { get; } = [];

        public DialerProfilesController Controller { get; private set; }

        public static Harness Create(DialerProfile source, bool automatedDialerEnabled = true, bool authorized = true, DialerProfile[] others = null)
        {
            var harness = new Harness();
            var all = new List<DialerProfile> { source };
            all.AddRange(others ?? []);

            harness.Store
                .Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => all.FirstOrDefault(profile => profile.ItemId == id));
            harness.Store
                .Setup(store => store.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => all.ToArray());
            harness.Store
                .Setup(store => store.CreateAsync(It.IsAny<DialerProfile>(), It.IsAny<CancellationToken>()))
                .Callback((DialerProfile profile, CancellationToken _) => harness.Created.Add(profile))
                .Returns(ValueTask.CompletedTask);

            // The notifier extensions reach both overloads, so both record what the operator would see.
            harness.Notifier
                .Setup(notifier => notifier.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>(), It.IsAny<NotifyContext>()))
                .Callback((NotifyType type, LocalizedHtmlString message, NotifyContext _) => harness.Record(type, message))
                .Returns(ValueTask.CompletedTask);
            harness.Notifier
                .Setup(notifier => notifier.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>()))
                .Callback((NotifyType type, LocalizedHtmlString message) => harness.Record(type, message))
                .Returns(ValueTask.CompletedTask);

            var manager = new DialerProfileManager(
                harness.Store.Object,
                [CreateHandler(automatedDialerEnabled)],
                NullLogger<CatalogManager<DialerProfile>>.Instance);

            var authorization = new Mock<IAuthorizationService>();
            authorization
                .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());

            var updater = new Mock<IUpdateModel>();
            updater.SetupGet(value => value.ModelState).Returns(new ModelStateDictionary());

            harness.Controller = new DialerProfilesController(
                manager,
                authorization.Object,
                Mock.Of<IUpdateModelAccessor>(accessor => accessor.ModelUpdater == updater.Object),
                Mock.Of<IDisplayManager<DialerProfile>>(),
                harness.Notifier.Object,
                new TestHtmlLocalizer<DialerProfilesController>(),
                new TestStringLocalizer<DialerProfilesController>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")),
                    },
                },
            };

            return harness;
        }

        private void Record(NotifyType type, LocalizedHtmlString message)
        {
            using var writer = new StringWriter();
            message.WriteTo(writer, HtmlEncoder.Default);
            Messages.Add((type, writer.ToString()));
        }

        private static DialerProfileHandler CreateHandler(bool automatedDialerEnabled)
        {
            var features = new List<IFeatureInfo>();

            if (automatedDialerEnabled)
            {
                var feature = new Mock<IFeatureInfo>();
                feature.SetupGet(value => value.Id).Returns(ContactCenterConstants.Feature.DialerPaced);
                features.Add(feature.Object);
            }

            var featuresManager = new Mock<IShellFeaturesManager>();
            featuresManager
                .Setup(value => value.GetEnabledFeaturesAsync())
                .ReturnsAsync(features);

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            // No profile measured here has any connected calls, as a new copy would not.
            var statistics = new Mock<IDialerPacingStatisticsProvider>();
            statistics
                .Setup(value => value.GetStatisticsAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DialerPacingStatistics)null);

            return new DialerProfileHandler(
                clock.Object,
                featuresManager.Object,
                new DefaultPhoneNumberService(),
                [statistics.Object],
                new TestStringLocalizer<DialerProfileHandler>());
        }
    }

    private sealed class TestHtmlLocalizer<T> : IHtmlLocalizer<T>
    {
        public LocalizedHtmlString this[string name] => new(name, name);

        public LocalizedHtmlString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public LocalizedString GetString(string name) => new(name, name);

        public LocalizedString GetString(string name, params object[] arguments) => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class TestStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
