using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// The tenant's recording disclosure on an inbound call, end to end over the real store: said word for word ahead of
/// the entry point's own message, and recorded as the caller's consent once the provider reports the speech ended.
/// <para>
/// Nothing told callers they were recorded unless someone wrote it into each entry point's welcome message, and
/// nothing ever captured consent, so a tenant that required it was never recorded at all.
/// </para>
/// </summary>
public sealed class IvrRecordingDisclosureIntegrationTests
{
    private const string Disclosure = "This call may be recorded for quality assurance and training purposes.";
    private const string Welcome = "Thanks for calling the main line.";

    [Fact]
    public async Task TheDisclosure_IsSaidAheadOfTheWelcome_AsOneMessage()
    {
        // Arrange
        await using var fixture = await DisclosureFixtureAsync(Welcome);

        // Act
        await fixture.AnnounceAsync();

        // Assert
        Assert.Equal($"{Disclosure} {Welcome}", fixture.Menus.Announcements.Single().Text);
        Assert.Empty(fixture.Menus.Prompts);
    }

    [Fact]
    public async Task ALineWithNoWelcome_SaysTheDisclosureAlone()
    {
        // Arrange
        await using var fixture = await DisclosureFixtureAsync(welcome: null);

        // Act
        await fixture.AnnounceAsync();

        // Assert
        Assert.Equal(Disclosure, fixture.Menus.Announcements.Single().Text);
    }

    [Fact]
    public async Task TheEndOfTheDisclosure_RecordsTheCallerAsTold_CapturesConsent_AndStartsTheMenu()
    {
        // Arrange
        await using var fixture = await DisclosureFixtureAsync(Welcome);
        await fixture.AnnounceAsync();

        // Act
        var handled = await fixture.EndSpeechAsync("speak-ended-1");

        // Assert
        Assert.True(handled);
        Assert.Single(fixture.Menus.Prompts);

        var interaction = await fixture.FindInteractionAsync();
        Assert.NotNull(interaction.RecordingDisclosedUtc);
        Assert.Equal(interaction.RecordingDisclosedUtc, interaction.RecordingConsentCapturedUtc);

        var disclosed = Assert.Single(fixture.Events, e => e.EventType == ContactCenterConstants.Events.RecordingDisclosed);
        var data = disclosed.GetData<RecordingDisclosedEventData>();
        Assert.Equal(ContactCenterConstants.RecordingDisclosureMethod.Announcement, data.Method);
        Assert.Equal(Disclosure, data.Text);
    }

    [Fact]
    public async Task ARepeatedEndOfTheDisclosure_RecordsItOnce()
    {
        // Arrange
        await using var fixture = await DisclosureFixtureAsync(Welcome);
        await fixture.AnnounceAsync();
        await fixture.EndSpeechAsync("speak-ended-1");
        var told = (await fixture.FindInteractionAsync()).RecordingDisclosedUtc;

        // Act
        await fixture.EndSpeechAsync("speak-ended-1");

        // Assert
        Assert.Single(fixture.Events, e => e.EventType == ContactCenterConstants.Events.RecordingDisclosed);
        Assert.Equal(told, (await fixture.FindInteractionAsync()).RecordingDisclosedUtc);
    }

    [Fact]
    public async Task ADisclosureTurnedOffAfterTheCallArrived_IsNotSaid_OrRecorded()
    {
        // Arrange
        await using var fixture = await DisclosureFixtureAsync(Welcome);
        fixture.EnableRecording(new ContactCenterRecordingSettings
        {
            EnableRecordingDisclosure = false,
            RecordingDisclosureText = Disclosure,
        });

        // Act
        await fixture.AnnounceAsync();
        await fixture.EndSpeechAsync("speak-ended-1");

        // Assert
        Assert.Equal(Welcome, fixture.Menus.Announcements.Single().Text);
        Assert.Null((await fixture.FindInteractionAsync()).RecordingDisclosedUtc);
        Assert.DoesNotContain(fixture.Events, e => e.EventType == ContactCenterConstants.Events.RecordingDisclosed);
    }

    [Fact]
    public async Task ADisclosureThatCouldNotBeSaid_IsNotRecorded()
    {
        // Arrange
        // The caller goes on without it, and their agent is asked to give it instead.
        await using var fixture = await DisclosureFixtureAsync(Welcome);
        fixture.Menus.CanAnnounce = false;

        // Act
        await fixture.AnnounceAsync();

        // Assert
        Assert.Equal(EntryPointAnnouncement.Failed, EntryPointAnnouncement.Read(await fixture.FindInteractionAsync()).Status);
        Assert.Null((await fixture.FindInteractionAsync()).RecordingDisclosedUtc);
    }

    [Fact]
    public async Task WithoutCallRecording_OnlyTheWelcomeIsSaid()
    {
        // Arrange
        // A call scheduled with the disclosure, on a tenant that has since turned call recording off.
        await using var fixture = await DisclosureFixtureAsync(Welcome);
        fixture.DisclosureServices.Clear();

        // Act
        await fixture.AnnounceAsync();
        await fixture.EndSpeechAsync("speak-ended-1");

        // Assert
        Assert.Equal(Welcome, fixture.Menus.Announcements.Single().Text);
        Assert.Null((await fixture.FindInteractionAsync()).RecordingDisclosedUtc);
    }

    private static async Task<IvrIntegrationFixture> DisclosureFixtureAsync(string welcome)
    {
        var fixture = await IvrIntegrationFixture.CreateAsync();
        fixture.EntryPoint.WelcomeMessage = welcome;
        fixture.EnableRecording(new ContactCenterRecordingSettings
        {
            EnableRecordingDisclosure = true,
            RecordingDisclosureText = "  " + Disclosure + " ",
        });
        await fixture.ScheduleAnnouncementAsync(EntryPointAnnouncement.Welcome, EntryPointAnnouncement.NextMenu, includesDisclosure: true);

        return fixture;
    }
}
