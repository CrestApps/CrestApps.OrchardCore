using CrestApps.Core.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.Documents;
using OrchardCore.Email;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Core.Omnichannel.Services;

/// <summary>
/// Omnichannel addresses replaced channel endpoints that had one channel each. A number used for calls and texts was two
/// records, inbound traffic matched whichever was found first, and its settings were split between them. An address
/// is now one record per number with a capability per channel, and the records an upgrade merged stay reachable by the
/// identifiers history still carries.
/// </summary>
public sealed class OmnichannelAddressTests
{
    private const string Number = "+14155552671";

    [Theory]
    [InlineData(OmnichannelConstants.Channels.Phone, OmnichannelAddressTypes.PhoneNumber)]
    [InlineData(OmnichannelConstants.Channels.Sms, OmnichannelAddressTypes.PhoneNumber)]
    [InlineData(OmnichannelConstants.Channels.Email, OmnichannelAddressTypes.EmailAddress)]
    public void ARecordSavedWithOneChannel_ReadsAsAnAddressWithThatCapability(string channel, string addressType)
    {
        // Arrange
        var address = new OmnichannelChannelEndpoint { Channel = channel, Value = Number };

        // Act & Assert
        Assert.Equal(addressType, address.GetAddressType());
        Assert.Equal([channel], address.GetCapabilities());
        Assert.True(address.HasCapability(channel.ToLowerInvariant()));
    }

    [Fact]
    public void AnAddressWithCapabilities_IgnoresTheChannelItWasSavedWith()
    {
        // Arrange
        var address = new OmnichannelChannelEndpoint
        {
            Channel = OmnichannelConstants.Channels.Sms,
            AddressType = OmnichannelAddressTypes.PhoneNumber,
            Capabilities = [OmnichannelConstants.Channels.Phone],
        };

        // Act & Assert
        Assert.True(address.HasCapability(OmnichannelConstants.Channels.Phone));
        Assert.False(address.HasCapability(OmnichannelConstants.Channels.Sms));
    }

    [Fact]
    public void AnAddress_IsKnownByItsOwnIdAndTheIdsMergedIntoIt()
    {
        // Arrange
        var address = new OmnichannelChannelEndpoint { ItemId = "phone", MergedItemIds = ["sms"] };

        // Act & Assert
        Assert.True(address.IsKnownAs("phone"));
        Assert.True(address.IsKnownAs("sms"));
        Assert.False(address.IsKnownAs("other"));
        Assert.False(address.IsKnownAs(null));
        Assert.Equal(["phone", "sms"], address.GetKnownIds());
    }

    [Fact]
    public void Clone_KeepsTheTypeCapabilitiesAndMergedIds()
    {
        // Arrange
        var address = new OmnichannelChannelEndpoint
        {
            ItemId = "phone",
            AddressType = OmnichannelAddressTypes.PhoneNumber,
            Capabilities = [OmnichannelConstants.Channels.Phone, OmnichannelConstants.Channels.Sms],
            MergedItemIds = ["sms"],
        };

        // Act
        var clone = address.Clone();
        address.Capabilities.Clear();

        // Assert
        Assert.Equal(OmnichannelAddressTypes.PhoneNumber, clone.AddressType);
        Assert.Equal([OmnichannelConstants.Channels.Phone, OmnichannelConstants.Channels.Sms], clone.Capabilities);
        Assert.Equal(["sms"], clone.MergedItemIds);
    }

    [Fact]
    public void Consolidate_MergesTheRecordsOfOneNumberIntoTheOldest()
    {
        // Arrange
        var phone = new OmnichannelChannelEndpoint
        {
            ItemId = "phone",
            DisplayText = "Main line",
            Channel = OmnichannelConstants.Channels.Phone,
            Value = Number,
            CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Properties = new Dictionary<string, object> { ["OutboundLineSettings"] = "agents", ["Shared"] = "phone" },
        };
        var sms = new OmnichannelChannelEndpoint
        {
            ItemId = "sms",
            DisplayText = "Main line texts",
            Channel = OmnichannelConstants.Channels.Sms,
            // Written differently; the comparison is on the canonical number.
            Value = "(415) 555-2671",
            ProviderName = "Twilio",
            CreatedUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            Properties = new Dictionary<string, object> { ["MessagingEndpointRoutingSettings"] = "queue", ["Shared"] = "sms" },
        };
        var other = new OmnichannelChannelEndpoint
        {
            ItemId = "other",
            Channel = OmnichannelConstants.Channels.Sms,
            Value = "+17025550100",
        };
        var records = Records(phone, sms, other);

        // Act
        var retired = OmnichannelAddressConsolidator.Consolidate(records, Canonicalize);

        // Assert
        Assert.Equal(new Dictionary<string, string> { ["sms"] = "phone" }, retired);
        Assert.Equal(["other", "phone"], records.Keys.Order());

        var merged = records["phone"];
        Assert.Equal(OmnichannelAddressTypes.PhoneNumber, merged.AddressType);
        Assert.Equal([OmnichannelConstants.Channels.Phone, OmnichannelConstants.Channels.Sms], merged.Capabilities);
        Assert.Equal(["sms"], merged.MergedItemIds);
        Assert.Equal("Main line", merged.DisplayText);
        Assert.Equal("Twilio", merged.ProviderName);
        Assert.Equal("agents", merged.Properties["OutboundLineSettings"]);
        Assert.Equal("queue", merged.Properties["MessagingEndpointRoutingSettings"]);
        Assert.Equal("phone", merged.Properties["Shared"]);

        // A record left alone is still brought forward to a type and a capability.
        Assert.Equal(OmnichannelAddressTypes.PhoneNumber, records["other"].AddressType);
        Assert.Equal([OmnichannelConstants.Channels.Sms], records["other"].Capabilities);
    }

    [Fact]
    public void Consolidate_RunAgain_ChangesNothing()
    {
        // Arrange
        var records = Records(
            new OmnichannelChannelEndpoint { ItemId = "phone", Channel = OmnichannelConstants.Channels.Phone, Value = Number },
            new OmnichannelChannelEndpoint { ItemId = "sms", Channel = OmnichannelConstants.Channels.Sms, Value = Number });

        OmnichannelAddressConsolidator.Consolidate(records, Canonicalize);

        // Act
        var retired = OmnichannelAddressConsolidator.Consolidate(records, Canonicalize);

        // Assert
        Assert.Empty(retired);
        Assert.Single(records);
    }

    [Fact]
    public async Task GetByServiceAddressAsync_FindsTheAddressOnlyOnAChannelItIsUsedFor()
    {
        // Arrange
        var store = CreateStore(new OmnichannelChannelEndpoint
        {
            ItemId = "line",
            AddressType = OmnichannelAddressTypes.PhoneNumber,
            Capabilities = [OmnichannelConstants.Channels.Phone, OmnichannelConstants.Channels.Sms],
            Value = Number,
        });

        // Act & Assert
        Assert.Equal("line", (await store.GetByServiceAddressAsync(OmnichannelConstants.Channels.Phone, Number, TestContext.Current.CancellationToken))?.ItemId);
        Assert.Equal("line", (await store.GetByServiceAddressAsync(OmnichannelConstants.Channels.Sms, Number, TestContext.Current.CancellationToken))?.ItemId);
        Assert.Null(await store.GetByServiceAddressAsync("WhatsApp", Number, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FindByIdAsync_ForAMergedAwayRecord_ReturnsTheAddressItBecame()
    {
        // Arrange
        var store = CreateStore(new OmnichannelChannelEndpoint
        {
            ItemId = "phone",
            AddressType = OmnichannelAddressTypes.PhoneNumber,
            Capabilities = [OmnichannelConstants.Channels.Phone, OmnichannelConstants.Channels.Sms],
            MergedItemIds = ["sms"],
            Value = Number,
        });

        // Act
        var address = await store.FindByIdAsync("sms", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("phone", address?.ItemId);
        Assert.Null(await store.FindByIdAsync("unknown", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ValidatingAsync_RefusesANumberAlreadyInTheList()
    {
        // Arrange
        var existing = new OmnichannelChannelEndpoint
        {
            ItemId = "line",
            DisplayText = "Main line",
            AddressType = OmnichannelAddressTypes.PhoneNumber,
            Capabilities = [OmnichannelConstants.Channels.Phone],
            Value = Number,
        };
        var handler = CreateHandler(existing);
        var context = new ValidatingContext<OmnichannelChannelEndpoint>(new OmnichannelChannelEndpoint
        {
            ItemId = "new",
            DisplayText = "Texts",
            AddressType = OmnichannelAddressTypes.PhoneNumber,
            Capabilities = [OmnichannelConstants.Channels.Sms],
            // A new address is validated before it is canonicalized, so this has to match the stored form.
            Value = "(415) 555-2671",
        });

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(context.Result.Errors, error => error.ErrorMessage.Contains("Main line", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidatingAsync_RequiresSomethingTheAddressIsUsedFor()
    {
        // Arrange
        var handler = CreateHandler();
        var context = new ValidatingContext<OmnichannelChannelEndpoint>(new OmnichannelChannelEndpoint
        {
            ItemId = "new",
            DisplayText = "Main line",
            AddressType = OmnichannelAddressTypes.PhoneNumber,
            Value = Number,
        });

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(nameof(OmnichannelChannelEndpoint.Capabilities)));
    }

    [Fact]
    public async Task ValidatingAsync_AcceptsTheSameAddressBeingSavedAgain()
    {
        // Arrange
        var existing = new OmnichannelChannelEndpoint
        {
            ItemId = "line",
            DisplayText = "Main line",
            AddressType = OmnichannelAddressTypes.PhoneNumber,
            Capabilities = [OmnichannelConstants.Channels.Phone],
            Value = Number,
        };
        var handler = CreateHandler(existing);
        var context = new ValidatingContext<OmnichannelChannelEndpoint>(existing.Clone());

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    private static string Canonicalize(string addressType, string value)
        => addressType == OmnichannelAddressTypes.PhoneNumber && value == "(415) 555-2671" ? Number : value;

    private static Dictionary<string, OmnichannelChannelEndpoint> Records(params OmnichannelChannelEndpoint[] records)
        => records.ToDictionary(record => record.ItemId, StringComparer.Ordinal);

    private static OmnichannelChannelEndpointStore CreateStore(params OmnichannelChannelEndpoint[] records)
    {
        var document = new DictionaryDocument<OmnichannelChannelEndpoint>();

        foreach (var record in records)
        {
            document.Records[record.ItemId] = record;
        }

        var documentManager = new Mock<IDocumentManager<DictionaryDocument<OmnichannelChannelEndpoint>>>();
        documentManager
            .Setup(manager => manager.GetOrCreateImmutableAsync(It.IsAny<Func<Task<DictionaryDocument<OmnichannelChannelEndpoint>>>>()))
            .ReturnsAsync(document);

        return new OmnichannelChannelEndpointStore(documentManager.Object);
    }

    private static OmnichannelChannelEndpointHandler CreateHandler(params OmnichannelChannelEndpoint[] existing)
    {
        var phoneNumberService = new Mock<IPhoneNumberService>();
        phoneNumberService
            .Setup(service => service.TryFormatToE164(It.IsAny<string>(), It.IsAny<string>(), out It.Ref<string>.IsAny))
            .Returns(new TryFormatToE164Callback((string rawNumber, string regionCode, out string e164Number) =>
            {
                e164Number = Number;

                return rawNumber is "(415) 555-2671" or Number;
            }));

        var store = new Mock<IOmnichannelChannelEndpointStore>();
        store
            .Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        return new OmnichannelChannelEndpointHandler(
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IClock>().Object,
            phoneNumberService.Object,
            new Mock<IEmailAddressValidator>().Object,
            [],
            store.Object,
            new PassThroughStringLocalizer<OmnichannelCampaignHandler>());
    }

    private delegate bool TryFormatToE164Callback(string rawNumber, string regionCode, out string e164Number);
}
