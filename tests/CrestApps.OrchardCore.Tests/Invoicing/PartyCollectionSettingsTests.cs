using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// A party can be kept out of payment reminders and late fees: the setting is kept once for the party, shows in its
/// billing profile, and is read when the reminder and late-fee sweeps run, for every debt and invoice of the party.
/// </summary>
public sealed class PartyCollectionSettingsTests
{
    private static readonly DateOnly _today = DateOnly.FromDateTime(InvoicingTestHarness.Now);
    private static readonly PartyRef _exempt = new(PartyKinds.Account, "exempt");
    private static readonly PartyRef _other = new(PartyKinds.Account, "other");

    [Fact]
    public async Task AnExemptParty_GetsNoLateFee_AndItsBillingProfileSaysSo()
    {
        // Arrange: $25 once an invoice is late; two overdue invoices, one billed to the exempt account.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        await SaveAsync(harness, _exempt, remindersPaused: false, lateFeesExempt: true);
        await harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<ILateFeePolicyManager>();
            var policy = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
            policy.Name = "Late fee";
            policy.Kind = LateFeeKind.Flat;
            policy.Amount = 25m;
            await manager.CreateAsync(policy, TestContext.Current.CancellationToken);

            return policy;
        });
        var exemptInvoice = await IssueOverdueAsync(harness, _exempt);
        var otherInvoice = await IssueOverdueAsync(harness, _other);

        // Act
        var made = await harness.InScopeAsync(services => services.GetRequiredService<ILateFeeService>().AssessDueAsync(_today, TestContext.Current.CancellationToken));
        var profile = new PartyBillingProfile { Party = _exempt };
        await harness.InScopeAsync(async services =>
        {
            await new PartyCollectionSettingsBillingProfileSource(services.GetRequiredService<IPartyCollectionSettingsService>()).ContributeAsync(_exempt, profile, TestContext.Current.CancellationToken);

            return true;
        });

        // Assert
        Assert.Equal(1, made);
        Assert.Empty(await DebitNotesAsync(harness, exemptInvoice.ItemId));
        Assert.Single(await DebitNotesAsync(harness, otherInvoice.ItemId));
        Assert.Equal((false, true), (profile.RemindersPaused, profile.LateFeesExempt));
        Assert.False(exemptInvoice.LateFeesExempt);
    }

    [Fact]
    public async Task APartysPausedReminders_CoverTheDebtsItOwesOrPays()
    {
        // Arrange: reminders paused for the user who owns one debt and for the account that pays another.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        await SaveAsync(harness, new PartyRef(PartyKinds.User, "user-paused"), remindersPaused: true, lateFeesExempt: false);
        await SaveAsync(harness, _exempt, remindersPaused: true, lateFeesExempt: false);
        var owned = new Transaction { OwnerId = "user-paused" };
        var paid = new Transaction { OwnerId = "user-2" };
        paid.SetPayer(_exempt);
        var other = new Transaction { OwnerId = "user-3" };
        other.SetPayer(_other);

        // Act
        var exclusions = await harness.InScopeAsync(services => services.GetRequiredService<IPartyCollectionSettingsService>().GetExclusionsAsync(TestContext.Current.CancellationToken));
        var cleared = await harness.InScopeAsync(async services =>
        {
            var settings = services.GetRequiredService<IPartyCollectionSettingsService>();
            await settings.SaveAsync(_exempt, remindersPaused: false, lateFeesExempt: false, TestContext.Current.CancellationToken);

            return await settings.GetExclusionsAsync(TestContext.Current.CancellationToken);
        });

        // Assert
        Assert.True(exclusions.AreRemindersPaused(owned));
        Assert.True(exclusions.AreRemindersPaused(paid));
        Assert.False(exclusions.AreRemindersPaused(other));
        Assert.False(cleared.AreRemindersPaused(paid));
        Assert.True(cleared.AreRemindersPaused(owned));
    }

    private static Task<bool> SaveAsync(InvoicingTestHarness harness, PartyRef party, bool remindersPaused, bool lateFeesExempt)
        => harness.InScopeAsync(async services =>
        {
            await services.GetRequiredService<IPartyCollectionSettingsService>().SaveAsync(party, remindersPaused, lateFeesExempt, TestContext.Current.CancellationToken);

            return true;
        });

    private static Task<IReadOnlyList<Invoice>> DebitNotesAsync(InvoicingTestHarness harness, string invoiceId)
        => harness.InScopeAsync(services => services.GetRequiredService<ILateFeeService>().ListDebitNotesAsync(invoiceId, TestContext.Current.CancellationToken));

    private static async Task<Invoice> IssueOverdueAsync(InvoicingTestHarness harness, PartyRef party)
    {
        var draft = await harness.CreateDraftAsync(invoice =>
        {
            invoice.BillTo.Party = party;
            invoice.IssueDate = _today.AddDays(-40);
            invoice.DueDate = _today.AddDays(-10);
        });
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }
}
