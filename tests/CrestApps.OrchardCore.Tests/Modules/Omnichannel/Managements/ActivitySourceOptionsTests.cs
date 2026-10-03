using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements;
using CrestApps.OrchardCore.Omnichannel.Managements.Reports;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Framework.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// The Source filter of Manage Activities and the report filters listed eleven hard-coded sources, of which only a
/// handful are ever written onto an activity: Workflow and API are never produced, Predictive profiles cannot be
/// saved, and a dialer load never stores "Dialer" because each activity gets its profile's mode. Picking one of
/// the missing sources returned nothing, and picking "Dialer" missed every dialer activity. The options now come
/// from what the enabled features register, and one option matches every stored value it stands for.
/// </summary>
public sealed class ActivitySourceOptionsTests
{
    [Fact]
    public void GetStoredValues_WhenTheSourceIsRegistered_ReturnsEveryValueItMatches()
    {
        // Arrange
        var options = new ActivitySourceOptions();
        options.AddSource(ActivitySources.Dialer, entry => entry.Matches(ActivitySources.PreviewDial, ActivitySources.PowerDial));

        // Act
        var storedValues = options.GetStoredValues("dialer");

        // Assert
        Assert.Equal([ActivitySources.Dialer, ActivitySources.PreviewDial, ActivitySources.PowerDial], storedValues);
    }

    [Fact]
    public void GetStoredValues_WhenTheSourceIsNotRegistered_MatchesOnlyThatValue()
    {
        // Arrange
        // An old link or a saved report filter can name a source that is no longer offered. It must keep
        // filtering on that value rather than widen to every activity.
        var options = new ActivitySourceOptions();
        options.AddSource(ActivitySources.Manual);

        // Act
        var storedValues = options.GetStoredValues($" {ActivitySources.Workflow} ");

        // Assert
        Assert.Equal([ActivitySources.Workflow], storedValues);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetStoredValues_WhenNoSourceIsSelected_ReturnsNothing(string source)
    {
        // Arrange
        var options = new ActivitySourceOptions();
        options.AddSource(ActivitySources.Manual);

        // Act
        var storedValues = options.GetStoredValues(source);

        // Assert
        Assert.Empty(storedValues);
    }

    [Fact]
    public void AddSource_WhenCalledAgainForTheSameSource_ExtendsTheEntryWithoutRepeatingValues()
    {
        // Arrange
        // Paced Dialing extends the Dialer entry that the Outbound Dialer feature registered.
        var options = new ActivitySourceOptions();
        options.AddSource(ActivitySources.Dialer, entry =>
        {
            entry.DisplayName = new LocalizedString("Dialer", "Dialer");
            entry.Matches(ActivitySources.PreviewDial);
        });

        // Act
        options.AddSource(ActivitySources.Dialer, entry => entry.Matches(ActivitySources.PreviewDial, ActivitySources.ProgressiveDial));

        // Assert
        var entry = Assert.Single(options.Sources.Values);
        Assert.Equal("Dialer", entry.DisplayName.Value);
        Assert.Equal([ActivitySources.Dialer, ActivitySources.PreviewDial, ActivitySources.ProgressiveDial], entry.StoredValues);
    }

    [Fact]
    public void AddSource_WhenNoDisplayNameIsGiven_UsesTheSourceValue()
    {
        // Arrange
        var options = new ActivitySourceOptions();

        // Act
        options.AddSource(ActivitySources.Callback);

        // Assert
        Assert.Equal(ActivitySources.Callback, options.Sources[ActivitySources.Callback].DisplayName.Value);
    }

    [Theory]
    [InlineData("Manual", true, "Manual")]
    [InlineData("automatic", true, "Automatic")]
    [InlineData("Dialer", false, null)]
    [InlineData("PreviewDial", false, null)]
    [InlineData("Workflow", false, null)]
    [InlineData("", false, null)]
    [InlineData(null, false, null)]
    public void TryGetManuallyAssignableSource_AcceptsOnlyRegisteredSourcesThatMayBeSetByHand(string requested, bool expected, string expectedSource)
    {
        // Arrange
        // The bulk Change source action used to store whatever value was posted. A dialer mode set by hand skips
        // the dialer profile, and an unregistered value leaves activities no source filter can find again.
        var options = CreateRegisteredOptions(includeDialer: true, includePacedDialing: false);

        // Act
        var accepted = options.TryGetManuallyAssignableSource(requested, out var entry);

        // Assert
        Assert.Equal(expected, accepted);
        Assert.Equal(expectedSource, entry?.Source);
    }

    [Fact]
    public void ActivitiesFeature_RegistersManualAutomaticAndInboundOnly()
    {
        // Arrange & Act
        var options = CreateRegisteredOptions(includeDialer: false, includePacedDialing: false);

        // Assert
        Assert.Equal(
            [ActivitySources.Automatic, ActivitySources.Inbound, ActivitySources.Manual],
            options.Sources.Keys.Order(StringComparer.Ordinal));
        Assert.True(options.Sources[ActivitySources.Manual].CanBeSetManually);
        Assert.True(options.Sources[ActivitySources.Automatic].CanBeSetManually);
        Assert.False(options.Sources[ActivitySources.Inbound].CanBeSetManually);
    }

    [Fact]
    public void DialerFeature_AddsDialerMatchingPreviewAndCallback()
    {
        // Arrange & Act
        var options = CreateRegisteredOptions(includeDialer: true, includePacedDialing: false);

        // Assert
        Assert.Equal(
            [ActivitySources.Automatic, ActivitySources.Callback, ActivitySources.Dialer, ActivitySources.Inbound, ActivitySources.Manual],
            options.Sources.Keys.Order(StringComparer.Ordinal));
        Assert.Equal([ActivitySources.Dialer, ActivitySources.PreviewDial], options.Sources[ActivitySources.Dialer].StoredValues);
        Assert.False(options.Sources[ActivitySources.Dialer].CanBeSetManually);
        Assert.False(options.Sources[ActivitySources.Callback].CanBeSetManually);
    }

    [Fact]
    public void PacedDialingFeature_AddsThePacedModesToTheDialerSource()
    {
        // Arrange & Act
        var options = CreateRegisteredOptions(includeDialer: true, includePacedDialing: true);

        // Assert
        Assert.DoesNotContain(ActivitySources.PowerDial, options.Sources.Keys);
        Assert.DoesNotContain(ActivitySources.PredictiveDial, options.Sources.Keys);
        Assert.Equal(
            [ActivitySources.Dialer, ActivitySources.PreviewDial, ActivitySources.PowerDial, ActivitySources.ProgressiveDial, ActivitySources.PredictiveDial],
            options.Sources[ActivitySources.Dialer].StoredValues);
        Assert.Equal("Dialer", options.Sources[ActivitySources.Dialer].DisplayName.Value);
    }

    [Fact]
    public void ActivitiesFeature_RegistersPhoneAndSmsChannelsOnly()
    {
        // Arrange
        var services = new ServiceCollection();
        new OmnichannelActivitiesStartup(new PassThroughStringLocalizer<OmnichannelActivitiesStartup>()).ConfigureServices(services);

        // Act
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ActivityChannelOptions>>().Value;

        // Assert
        Assert.Equal(
            [OmnichannelConstants.Channels.Phone, OmnichannelConstants.Channels.Sms],
            options.Channels.Keys.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(OmnichannelConstants.Channels.Email, options.Channels.Keys);
    }

    [Fact]
    public void BuildSourceItems_OrdersByDisplayNameAndKeepsAnUnregisteredSelection()
    {
        // Arrange
        var options = CreateRegisteredOptions(includeDialer: true, includePacedDialing: false);

        // Act
        var items = ActivityFilterSelectListBuilder.BuildSourceItems(options, ActivitySources.Workflow);

        // Assert
        Assert.Equal(
            ["Automatic", "Callback", "Dialer", "Inbound", "Manual", ActivitySources.Workflow],
            items.Select(item => item.Text));
        var selected = Assert.Single(items, item => item.Selected);
        Assert.Equal(ActivitySources.Workflow, selected.Value);
    }

    [Fact]
    public void BuildSourceItems_WhenTheSelectionIsRegistered_SelectsItWithoutAddingAnOption()
    {
        // Arrange
        var options = CreateRegisteredOptions(includeDialer: true, includePacedDialing: false);

        // Act
        var items = ActivityFilterSelectListBuilder.BuildSourceItems(options, "dialer");

        // Assert
        Assert.Equal(5, items.Count);
        Assert.Equal(ActivitySources.Dialer, Assert.Single(items, item => item.Selected).Value);
    }

    [Fact]
    public void BuildManuallyAssignableSourceItems_ListsOnlyManualAndAutomatic()
    {
        // Arrange
        var options = CreateRegisteredOptions(includeDialer: true, includePacedDialing: true);

        // Act
        var items = ActivityFilterSelectListBuilder.BuildManuallyAssignableSourceItems(options);

        // Assert
        Assert.Equal([ActivitySources.Automatic, ActivitySources.Manual], items.Select(item => item.Value));
        Assert.DoesNotContain(items, item => item.Selected);
    }

    [Fact]
    public void BuildChannelItems_KeepsAChannelSavedBeforeItStoppedBeingOffered()
    {
        // Arrange
        var options = new ActivityChannelOptions();
        options.AddChannel(OmnichannelConstants.Channels.Phone, entry => entry.DisplayName = new LocalizedString("Phone", "Phone"));
        options.AddChannel(OmnichannelConstants.Channels.Sms, entry => entry.DisplayName = new LocalizedString("SMS", "SMS"));

        // Act
        var items = ActivityFilterSelectListBuilder.BuildChannelItems(options, OmnichannelConstants.Channels.Email);

        // Assert
        Assert.Equal(["Phone", "SMS", OmnichannelConstants.Channels.Email], items.Select(item => item.Text));
        Assert.Equal(OmnichannelConstants.Channels.Email, Assert.Single(items, item => item.Selected).Value);
    }

    [Fact]
    public async Task OmnichannelReportFilter_WhenDialerIsSelected_MatchesEveryDialerMode()
    {
        // Arrange
        // A dialer load stores the profile's mode on each activity, so a report filtered on the stored value
        // "Dialer" found nothing at all.
        var options = CreateRegisteredOptions(includeDialer: true, includePacedDialing: true);
        var filter = new ReportFilter();
        filter.Set(OmnichannelReportFilter.Source, ActivitySources.Dialer);

        var preview = Activity(ActivitySources.PreviewDial);
        var power = Activity(ActivitySources.PowerDial);
        var progressive = Activity(ActivitySources.ProgressiveDial);
        var legacy = Activity(ActivitySources.Dialer);
        var manual = Activity(ActivitySources.Manual);
        var callback = Activity(ActivitySources.Callback);

        // Act
        var criteria = await OmnichannelReportFilter.GetCriteriaAsync(
            filter,
            Mock.Of<ICatalogManager<OmnichannelCampaign>>(),
            options,
            TestContext.Current.CancellationToken);
        var filtered = OmnichannelReportQuery.Filter([preview, power, progressive, legacy, manual, callback], criteria);

        // Assert
        Assert.Equal([preview, power, progressive, legacy], filtered);
    }

    [Fact]
    public async Task OmnichannelReportFilter_WhenAnUnregisteredSourceIsSaved_MatchesOnlyThatValue()
    {
        // Arrange
        var options = CreateRegisteredOptions(includeDialer: false, includePacedDialing: false);
        var filter = new ReportFilter();
        filter.Set(OmnichannelReportFilter.Source, ActivitySources.PreviewDial);
        var preview = Activity(ActivitySources.PreviewDial);

        // Act
        var criteria = await OmnichannelReportFilter.GetCriteriaAsync(
            filter,
            Mock.Of<ICatalogManager<OmnichannelCampaign>>(),
            options,
            TestContext.Current.CancellationToken);
        var filtered = OmnichannelReportQuery.Filter([preview, Activity(ActivitySources.PowerDial), Activity(ActivitySources.Manual)], criteria);

        // Assert
        Assert.Same(preview, Assert.Single(filtered));
    }

    internal static ActivitySourceOptions CreateRegisteredOptions(bool includeDialer, bool includePacedDialing)
    {
        var services = new ServiceCollection();

        new OmnichannelActivitiesStartup(new PassThroughStringLocalizer<OmnichannelActivitiesStartup>()).ConfigureServices(services);

        if (includeDialer)
        {
            new DialerStartup(
                new PassThroughStringLocalizer<DialerStartup>(),
                new TestShellConfiguration(new ConfigurationBuilder().Build())).ConfigureServices(services);
        }

        if (includePacedDialing)
        {
            new DialerPacedStartup(new PassThroughStringLocalizer<DialerPacedStartup>()).ConfigureServices(services);
        }

        return services.BuildServiceProvider().GetRequiredService<IOptions<ActivitySourceOptions>>().Value;
    }

    private static OmnichannelActivityIndex Activity(string source)
    {
        return new OmnichannelActivityIndex
        {
            Source = source,
            Status = ActivityStatus.Completed,
        };
    }

    private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
