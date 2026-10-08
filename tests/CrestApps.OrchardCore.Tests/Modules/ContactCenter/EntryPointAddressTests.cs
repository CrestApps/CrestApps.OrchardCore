using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Inbound routing for a number used to be set in up to four places: numbers typed on entry points, the entry point a
/// phone number picked on its own screen, the number a queue mapped, and subject flows. Entry points now pick their
/// numbers from the address list, one entry point per number per channel, and the upgrade moves the old places onto
/// them.
/// </summary>
public sealed class EntryPointAddressTests
{
    private const string Number = "+15551234567";

    [Fact]
    public async Task Validating_RefusesANumberNotUsedForTheEntryPointsChannel()
    {
        // Arrange
        var textsOnly = Address("texts", OmnichannelConstants.Channels.Sms);
        var handler = CreateHandler([textsOnly], []);
        var context = new ValidatingContext<ContactCenterEntryPoint>(CallEntryPoint("new", "texts"));

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(nameof(ContactCenterEntryPoint.AddressIds)));
    }

    [Fact]
    public async Task Validating_RefusesANumberAnotherEnabledEntryPointAnswersOnTheSameChannel()
    {
        // Arrange
        var line = Address("line", OmnichannelConstants.Channels.Phone, OmnichannelConstants.Channels.Sms);
        var existing = CallEntryPoint("main", "line");
        existing.Name = "Main line";
        var handler = CreateHandler([line], [existing]);
        var context = new ValidatingContext<ContactCenterEntryPoint>(CallEntryPoint("second", "line"));

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(context.Result.Errors, error => error.ErrorMessage.Contains("Main line", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Validating_AcceptsTheSameNumberOnAnotherChannelOrADisabledEntryPoint()
    {
        // Arrange
        var line = Address("line", OmnichannelConstants.Channels.Phone, OmnichannelConstants.Channels.Sms);
        var texts = new ContactCenterEntryPoint { ItemId = "texts", Name = "Texts", Channel = OmnichannelConstants.Channels.Sms, AddressIds = ["line"], Enabled = true };
        var disabled = CallEntryPoint("old", "line");
        disabled.Enabled = false;
        var handler = CreateHandler([line], [texts, disabled]);
        var context = new ValidatingContext<ContactCenterEntryPoint>(CallEntryPoint("calls", "line"));

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Fact]
    public async Task Migration_MovesTypedNumbersOntoAddressesAndCreatesTheMissingOnes()
    {
        // Arrange
        var smsOnly = Address("sms", OmnichannelConstants.Channels.Sms);
        smsOnly.Value = Number;
        var entryPoint = new ContactCenterEntryPoint { ItemId = "main", Name = "Main", Enabled = true, DialedNumbers = ["(555) 123-4567", "+15550000001"] };
        var harness = new MigrationHarness([smsOnly], [entryPoint], []);

        // Act
        await harness.Migration.CreateAsync();

        // Assert
        Assert.Equal(OmnichannelConstants.Channels.Phone, entryPoint.Channel);
        Assert.Empty(entryPoint.DialedNumbers);

        // The typed number in another form found its address, which is now used for calls too.
        Assert.Contains("sms", entryPoint.AddressIds);
        Assert.True(smsOnly.HasCapability(OmnichannelConstants.Channels.Phone));

        // A typed number with no address got one, used for calls.
        var created = Assert.Single(harness.Addresses, address => address.Value == "+15550000001");
        Assert.Equal([OmnichannelConstants.Channels.Phone], created.Capabilities);
        Assert.Contains(created.ItemId, entryPoint.AddressIds);
    }

    [Fact]
    public async Task Migration_GivesTheNumberToTheEntryPointThePhoneNumberPicked()
    {
        // Arrange
        var line = Address("line", OmnichannelConstants.Channels.Phone);
        line.Value = Number;
        line.Properties = new Dictionary<string, object>
        {
            ["PhoneEndpointRoutingSettings"] = JsonSerializer.SerializeToElement(new { EntryPointId = "picked" }),
        };

        var typed = new ContactCenterEntryPoint { ItemId = "typed", Name = "Typed", Enabled = true, DialedNumbers = [Number] };
        var picked = new ContactCenterEntryPoint { ItemId = "picked", Name = "Picked", Enabled = true };
        var harness = new MigrationHarness([line], [typed, picked], []);

        // Act
        await harness.Migration.CreateAsync();

        // Assert
        Assert.Equal(["line"], picked.AddressIds);
        Assert.Empty(typed.AddressIds);
        Assert.False(line.Properties.ContainsKey("PhoneEndpointRoutingSettings"));
    }

    [Fact]
    public async Task Migration_TurnsAQueuesNumberIntoAnEntryPointForThatQueue()
    {
        // Arrange
        var line = Address("line", OmnichannelConstants.Channels.Phone);
        var queue = new ActivityQueue { ItemId = "support", Name = "Support", Enabled = true, InboundChannelEndpointId = "line" };
        var harness = new MigrationHarness([line], [], [queue]);

        // Act
        await harness.Migration.CreateAsync();

        // Assert
        var entryPoint = Assert.Single(harness.EntryPoints);
        Assert.Equal("Support", entryPoint.Name);
        Assert.Equal(OmnichannelConstants.Channels.Phone, entryPoint.Channel);
        Assert.Equal(["line"], entryPoint.AddressIds);
        Assert.Equal(EntryPointTargetType.Queue, entryPoint.TargetType);
        Assert.Equal("support", entryPoint.TargetQueueId);
        Assert.Null(queue.InboundChannelEndpointId);
    }

    [Fact]
    public async Task Migration_LeavesAQueuesNumberThatAnEntryPointAlreadyAnswers()
    {
        // Arrange
        var line = Address("line", OmnichannelConstants.Channels.Phone);
        var entryPoint = CallEntryPoint("main", "line");
        var queue = new ActivityQueue { ItemId = "support", Name = "Support", Enabled = true, InboundChannelEndpointId = "line" };
        var harness = new MigrationHarness([line], [entryPoint], [queue]);

        // Act
        await harness.Migration.CreateAsync();

        // Assert
        Assert.Single(harness.EntryPoints);
        Assert.Null(queue.InboundChannelEndpointId);
    }

    private static OmnichannelChannelEndpoint Address(string id, params string[] capabilities)
        => new()
        {
            ItemId = id,
            DisplayText = id,
            AddressType = OmnichannelAddressTypes.PhoneNumber,
            Capabilities = [.. capabilities],
            Value = "+1555000" + id.Length.ToString("0000", System.Globalization.CultureInfo.InvariantCulture),
        };

    private static ContactCenterEntryPoint CallEntryPoint(string id, params string[] addressIds)
        => new()
        {
            ItemId = id,
            Name = id,
            Channel = OmnichannelConstants.Channels.Phone,
            AddressIds = [.. addressIds],
            TargetType = EntryPointTargetType.Queue,
            TargetQueueId = "queue",
            Enabled = true,
        };

    private static ContactCenterEntryPointHandler CreateHandler(OmnichannelChannelEndpoint[] addresses, ContactCenterEntryPoint[] entryPoints)
    {
        var addressStore = new Mock<IOmnichannelChannelEndpointStore>();
        addressStore.Setup(store => store.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(addresses);

        var entryPointStore = new Mock<IContactCenterEntryPointStore>();
        entryPointStore.Setup(store => store.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(entryPoints);

        return new ContactCenterEntryPointHandler(
            new Mock<IClock>().Object,
            addressStore.Object,
            entryPointStore.Object,
            Microsoft.Extensions.Options.Options.Create(new EntryPointAIAgentOptions()),
            new PassThroughStringLocalizer<ContactCenterEntryPointHandler>());
    }

    private sealed class MigrationHarness
    {
        public MigrationHarness(OmnichannelChannelEndpoint[] addresses, ContactCenterEntryPoint[] entryPoints, ActivityQueue[] queues)
        {
            Addresses = [.. addresses];
            EntryPoints = [.. entryPoints];

            var addressManager = new Mock<IOmnichannelChannelEndpointManager>();
            addressManager.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Addresses.ToArray());
            addressManager
                .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new OmnichannelChannelEndpoint { ItemId = "created-" + Addresses.Count });
            addressManager
                .Setup(manager => manager.CreateAsync(It.IsAny<OmnichannelChannelEndpoint>(), It.IsAny<CancellationToken>()))
                .Returns((OmnichannelChannelEndpoint address, CancellationToken _) =>
                {
                    Addresses.Add(address);

                    return ValueTask.CompletedTask;
                });

            var entryPointManager = new Mock<IContactCenterEntryPointManager>();
            entryPointManager.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => EntryPoints.ToArray());
            entryPointManager
                .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ContactCenterEntryPoint { ItemId = "entry-" + EntryPoints.Count });
            entryPointManager
                .Setup(manager => manager.CreateAsync(It.IsAny<ContactCenterEntryPoint>(), It.IsAny<CancellationToken>()))
                .Returns((ContactCenterEntryPoint entryPoint, CancellationToken _) =>
                {
                    EntryPoints.Add(entryPoint);

                    return ValueTask.CompletedTask;
                });

            var queueManager = new Mock<IActivityQueueManager>();
            queueManager.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(queues);

            var phoneNumbers = new Mock<IPhoneNumberService>();
            phoneNumbers
                .Setup(service => service.TryFormatToE164(It.IsAny<string>(), It.IsAny<string>(), out It.Ref<string>.IsAny))
                .Returns(new TryFormatToE164Callback((string raw, string region, out string e164) =>
                {
                    e164 = raw == "(555) 123-4567" ? Number : raw;

                    return raw.StartsWith('+') || raw == "(555) 123-4567";
                }));

            Migration = new EntryPointAddressMigrations(
                entryPointManager.Object,
                addressManager.Object,
                [queueManager.Object],
                phoneNumbers.Object,
                NullLogger<EntryPointAddressMigrations>.Instance);
        }

        public List<OmnichannelChannelEndpoint> Addresses { get; }

        public List<ContactCenterEntryPoint> EntryPoints { get; }

        public EntryPointAddressMigrations Migration { get; }
    }

    private delegate bool TryFormatToE164Callback(string rawNumber, string regionCode, out string e164Number);
}
