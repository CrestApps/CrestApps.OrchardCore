using System.Reflection;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.DialPad.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Asserts that telephony capabilities are backed by separately implementable contracts, so a provider only
/// implements the operations it actually offers and an advertised capability it cannot execute fails closed.
/// </summary>
public sealed class TelephonyProviderCapabilityContractTests
{
    private static readonly string[] _operations =
    [
        "Hold",
        "Resume",
        "Mute",
        "Unmute",
        "Transfer",
        "Merge",
        "SendDigits",
        "Answer",
        "Reject",
        "SendToVoicemail",
    ];

    [Theory]
    [InlineData("Dial")]
    [InlineData("Hangup")]
    public async Task CallControlOnlyProvider_ExecutesTheOperationsItImplements(string operation)
    {
        // Arrange
        var provider = new CallControlOnlyTelephonyProvider();
        var service = CreateService(provider);

        // Act
        var result = await InvokeAsync(service, operation);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(operation, provider.LastOperation);
    }

    [Theory]
    [InlineData("Hold")]
    [InlineData("Resume")]
    [InlineData("Mute")]
    [InlineData("Unmute")]
    [InlineData("Transfer")]
    [InlineData("Merge")]
    [InlineData("SendDigits")]
    [InlineData("Answer")]
    [InlineData("Reject")]
    [InlineData("SendToVoicemail")]
    public async Task ProviderAdvertisingCapabilityItDoesNotImplement_FailsClosed(string operation)
    {
        // Arrange: the provider claims every capability but only implements call control.
        var provider = new CallControlOnlyTelephonyProvider
        {
            Capabilities = TelephonyCapabilities.Dial
                | TelephonyCapabilities.Hangup
                | TelephonyCapabilities.Hold
                | TelephonyCapabilities.Resume
                | TelephonyCapabilities.Mute
                | TelephonyCapabilities.Transfer
                | TelephonyCapabilities.AttendedTransfer
                | TelephonyCapabilities.Merge
                | TelephonyCapabilities.SendDigits
                | TelephonyCapabilities.ReceiveCalls
                | TelephonyCapabilities.Voicemail
                | TelephonyCapabilities.Directory,
        };
        var service = CreateService(provider);

        // Act
        var result = await InvokeAsync(service, operation);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrEmpty(result.Error));
        Assert.Null(provider.LastOperation);
    }

    [Fact]
    public async Task ProviderAdvertisingSoftPhoneCredentialsItDoesNotImplement_ReturnsNoCredentials()
    {
        // Arrange
        var provider = new CallControlOnlyTelephonyProvider
        {
            Capabilities = TelephonyCapabilities.Dial | TelephonyCapabilities.Hangup,
        };
        var service = CreateService(provider);

        // Act
        var credentials = await service.GetClientCredentialsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(credentials);
    }

    [Fact]
    public async Task ProviderImplementingContractWithoutAdvertisingIt_FailsClosed()
    {
        // Arrange: the provider implements every contract but advertises none of them.
        var provider = new RecordingTelephonyProvider
        {
            Capabilities = TelephonyCapabilities.None,
        };
        var service = CreateService(provider);

        // Act
        var results = new List<TelephonyResult>();

        foreach (var operation in _operations)
        {
            results.Add(await InvokeAsync(service, operation));
        }

        // Assert
        Assert.All(results, result => Assert.False(result.Succeeded));
        Assert.Null(provider.LastOperation);
    }

    [Fact]
    public async Task WarmTransfer_RoutesToTheAttendedTransferContract()
    {
        // Arrange
        var provider = new RecordingTelephonyProvider
        {
            Capabilities = TelephonyCapabilities.Transfer | TelephonyCapabilities.AttendedTransfer,
        };
        var service = CreateService(provider);

        // Act
        var result = await service.TransferAsync(
            new TransferRequest
            {
                CallId = "call-1",
                To = "+15551234567",
                Mode = TransferMode.Warm,
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("AttendedTransfer", provider.LastOperation);
    }

    [Fact]
    public async Task BlindTransfer_RoutesToTheBlindTransferContract()
    {
        // Arrange
        var provider = new RecordingTelephonyProvider
        {
            Capabilities = TelephonyCapabilities.Transfer | TelephonyCapabilities.AttendedTransfer,
        };
        var service = CreateService(provider);

        // Act
        var result = await service.TransferAsync(
            new TransferRequest
            {
                CallId = "call-1",
                To = "+15551234567",
                Mode = TransferMode.Blind,
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("Transfer", provider.LastOperation);
    }

    [Fact]
    public async Task WarmTransfer_WhenProviderOnlyAdvertisesBlindTransfer_FailsClosed()
    {
        // Arrange: a provider that can release a call but cannot consult the destination first.
        var provider = new RecordingTelephonyProvider
        {
            Capabilities = TelephonyCapabilities.Transfer,
        };
        var service = CreateService(provider);

        // Act
        var result = await service.TransferAsync(
            new TransferRequest
            {
                CallId = "call-1",
                To = "+15551234567",
                Mode = TransferMode.Warm,
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(provider.LastOperation);
    }

    [Fact]
    public void EveryCapabilityFlag_DeclaresARequiredContract()
    {
        // Arrange
        var flags = Enum.GetValues<TelephonyCapabilities>()
            .Where(flag => flag != TelephonyCapabilities.None);

        // Act
        var uncovered = flags
            .Where(flag => TelephonyCapabilityContracts.GetContract(flag) is null)
            .ToArray();

        // Assert
        Assert.Empty(uncovered);
        Assert.NotEmpty(TelephonyCapabilityContracts.ContractsByCapability);
    }

    [Fact]
    public void EveryDeclaredContract_IsAnInterfaceInTheTelephonyAbstractions()
    {
        // Arrange
        var abstractionsAssembly = typeof(ITelephonyProvider).Assembly;

        // Act & Assert
        Assert.All(TelephonyCapabilityContracts.ContractsByCapability.Values, contract =>
        {
            Assert.True(contract.IsInterface, $"'{contract.Name}' must be an interface.");
            Assert.Same(abstractionsAssembly, contract.Assembly);
        });
    }

    [Fact]
    public void TelephonyProvider_DeclaresNoCallOperations()
    {
        // Arrange: re-fattening the provider interface would silently undo the split, because every provider
        // would again be forced to supply operations it cannot perform.
        var declaredMembers = typeof(ITelephonyProvider)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(member => member.Name)
            .Where(name => !name.StartsWith("get_", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.Equal(["Capabilities", "Name"], declaredMembers);
    }

    [Theory]
    [InlineData(typeof(DialPadTelephonyProvider))]
    [InlineData(typeof(AsteriskTelephonyProviderBase))]
    public void ShippedProviders_ImplementTheContractsForEveryCapabilityTheyCanAdvertise(Type providerType)
    {
        // Arrange: a shipped provider must never be able to advertise a capability whose contract it lacks,
        // otherwise the fail-closed path would be reachable in production instead of only for third parties.
        var advertisable = Enum.GetValues<TelephonyCapabilities>()
            .Where(flag => flag != TelephonyCapabilities.None)
            .Where(flag => AdvertisableCapabilities(providerType).HasFlag(flag));

        // Act
        var missing = advertisable
            .Select(TelephonyCapabilityContracts.GetContract)
            .Distinct()
            .Where(contract => !contract.IsAssignableFrom(providerType))
            .Select(contract => contract.Name)
            .ToArray();

        // Assert
        Assert.Empty(missing);
    }

    private static TelephonyCapabilities AdvertisableCapabilities(Type providerType)
    {
        // The widest set is read from the provider's own capability source rather than restated here, so a
        // provider that starts advertising a new capability is checked against its contracts automatically.
        if (providerType == typeof(AsteriskTelephonyProviderBase))
        {
            var getCapabilities = typeof(AsteriskTelephonyProviderBase).GetMethod(
                "GetCapabilities",
                BindingFlags.NonPublic | BindingFlags.Static,
                [typeof(string), typeof(bool)]);

            Assert.NotNull(getCapabilities);

            var widest = TelephonyCapabilities.None;

            foreach (var endpointTemplate in new[] { null, string.Empty, "PJSIP/agent", "Local/agent@context" })
            {
                foreach (var hasVoicemail in new[] { false, true })
                {
                    widest |= (TelephonyCapabilities)getCapabilities.Invoke(null, [endpointTemplate, hasVoicemail]);
                }
            }

            return widest;
        }

        // The DialPad capability set is a constant expression, so an uninitialized instance reports it
        // faithfully without standing up the provider's dependencies.
        var instance = (ITelephonyProvider)RuntimeHelpers.GetUninitializedObject(providerType);

        return instance.Capabilities;
    }

    private static DefaultTelephonyService CreateService(ITelephonyProvider provider)
        => new(
            new StubTelephonyProviderResolver(provider),
            new PassThroughStringLocalizer<DefaultTelephonyService>());

    private static Task<TelephonyResult> InvokeAsync(DefaultTelephonyService service, string operation)
    {
        var call = new CallReference { CallId = "call-1" };
        var token = TestContext.Current.CancellationToken;

        return operation switch
        {
            "Dial" => service.DialAsync(new DialRequest { To = "+15551234567" }, token),
            "Hangup" => service.HangupAsync(call, token),
            "Hold" => service.HoldAsync(call, token),
            "Resume" => service.ResumeAsync(call, token),
            "Mute" => service.MuteAsync(call, token),
            "Unmute" => service.UnmuteAsync(call, token),
            "Transfer" => service.TransferAsync(new TransferRequest { CallId = "call-1", To = "+15551234567" }, token),
            "Merge" => service.MergeAsync(new MergeRequest { CallIds = ["call-1", "call-2"] }, token),
            "SendDigits" => service.SendDigitsAsync(new SendDigitsRequest { CallId = "call-1", Digits = "1" }, token),
            "Answer" => service.AnswerAsync(call, token),
            "Reject" => service.RejectAsync(call, token),
            "SendToVoicemail" => service.SendToVoicemailAsync(call, token),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown operation."),
        };
    }
}
