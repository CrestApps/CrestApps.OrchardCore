using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Transactions.AI;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Transactions.AI;

/// <summary>
/// <c>setPartyCollectionSettings</c> refuses a caller who is not signed in or lacks Manage transactions, sets an
/// account's or a customer's collection settings as the card on its page does, keeps the setting it is not given (the
/// service saves both), stores the party's kind as the page does, refuses a party that does not exist or has no card (a
/// user, a guest), saves nothing when nothing changes, and asks for no approval.
/// </summary>
public sealed class SetPartyCollectionSettingsToolTests
{
    private static readonly PartyRef _northwind = new(PartyKinds.Account, "account-7");

    private readonly TransactionActionToolContext _context = new();

    [Fact]
    public async Task SetPartyCollectionSettings_RefusesACallerWhoIsNotSignedIn_OrWithoutManageTransactions()
    {
        // Arrange
        var services = _context.Build(authorize: (asked, _) => asked.Name != TransactionsPermissions.ManageTransactions.Name);

        // Act
        var anonymous = await TransactionActionToolContext.InvokeAsync(new SetPartyCollectionSettingsTool(), _context.Build(signedIn: false), Arguments("Account:account-7", remindersPaused: true));
        var refused = await TransactionActionToolContext.InvokeAsync(new SetPartyCollectionSettingsTool(), services, Arguments("Account:account-7", remindersPaused: true));

        // Assert
        Assert.Equal(TransactionsAIToolBase.NotAllowedMessage, anonymous);
        Assert.Equal(TransactionsAIToolBase.NotAllowedMessage, refused);
        Assert.Equal(TransactionsPermissions.ManageTransactions.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_context.CollectionSettings.Invocations);
    }

    [Fact]
    public async Task SetPartyCollectionSettings_KeepsTheSettingItIsNotGiven_AndStoresTheKindAsThePageDoes()
    {
        // Arrange
        _context.CollectionSettings
            .Setup(settings => settings.GetAsync(_northwind, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartyCollectionSettings { ItemId = "settings-1", Party = _northwind, LateFeesExempt = true });

        // Act
        var answer = AgentToolTestContext.ReadJson(await TransactionActionToolContext.InvokeAsync(new SetPartyCollectionSettingsTool(), _context.Build(), Arguments("account:account-7", remindersPaused: true)));

        // Assert
        _context.CollectionSettings.Verify(settings => settings.SaveAsync(_northwind, true, true, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("Account:account-7", answer.GetProperty("party").GetString());
        Assert.Equal("Northwind Traders", answer.GetProperty("name").GetString());
        Assert.True(answer.GetProperty("remindersPaused").GetBoolean());
        Assert.True(answer.GetProperty("lateFeesExempt").GetBoolean());
        Assert.False(answer.GetProperty("before").GetProperty("remindersPaused").GetBoolean());
        Assert.Contains("reminders sent by hand still go", answer.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetPartyCollectionSettings_SavesNothing_WhenNothingChanges()
    {
        // Act
        var answer = AgentToolTestContext.ReadJson(await TransactionActionToolContext.InvokeAsync(new SetPartyCollectionSettingsTool(), _context.Build(), Arguments("Account:account-7", lateFeesExempt: false)));

        // Assert
        Assert.False(answer.GetProperty("changed").GetBoolean());
        _context.CollectionSettings.Verify(settings => settings.SaveAsync(It.IsAny<PartyRef>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Account:nobody", true, "no account has the id 'nobody'")]
    [InlineData("User:user-9", true, "only a business account")]
    [InlineData("Guest:guest-1", true, "only a business account")]
    [InlineData("account-7", true, "Kind:Id")]
    [InlineData("Account:account-7", null, "give remindersPaused, lateFeesExempt or both")]
    public async Task SetPartyCollectionSettings_RefusesWhatThePageWouldNotSave(string party, bool? remindersPaused, string expected)
    {
        // Act
        var answer = await TransactionActionToolContext.InvokeAsync(new SetPartyCollectionSettingsTool(), _context.Build(), Arguments(party, remindersPaused));

        // Assert
        Assert.StartsWith("Nothing was changed", answer, StringComparison.Ordinal);
        Assert.Contains(expected, answer, StringComparison.Ordinal);
        _context.CollectionSettings.Verify(settings => settings.SaveAsync(It.IsAny<PartyRef>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void SetPartyCollectionSettings_DoesNotAskForApproval()
    {
        // Assert
        Assert.False(TransactionActionToolContext.RequiresApproval(SetPartyCollectionSettingsTool.TheName));
    }

    private static Dictionary<string, object> Arguments(string party, bool? remindersPaused = null, bool? lateFeesExempt = null)
    {
        var arguments = new Dictionary<string, object> { ["party"] = party };

        if (remindersPaused is { } paused)
        {
            arguments["remindersPaused"] = paused;
        }

        if (lateFeesExempt is { } exempt)
        {
            arguments["lateFeesExempt"] = exempt;
        }

        return arguments;
    }
}
