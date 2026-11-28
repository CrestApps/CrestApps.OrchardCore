using System.Text.Json;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// <c>pauseOrResumeRecurringPlan</c> pauses and resumes a recurring plan through the plan service, as the plan's buttons
/// do: a resumed plan runs again from today on, a plan that is not active is not paused, and a caller without the
/// permission changes nothing.
/// </summary>
public sealed class PauseOrResumeRecurringPlanToolTests
{
    private static readonly DateOnly _today = DateOnly.FromDateTime(InvoicingTestHarness.Now);

    private readonly InvoicingCreditToolContext _context = new();

    [Fact]
    public async Task Pause_IsRefused_WithoutThePermission()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var plan = await CreatePlanAsync(harness, activate: true);

        // Act
        var answer = await _context.InvokeAsync(harness, new PauseOrResumeRecurringPlanTool(), new() { ["planId"] = plan.ItemId, ["action"] = "pause" }, InvoicingCreditToolContext.AllBut(InvoicingPermissions.ManageRecurringInvoices));

        // Assert
        Assert.Contains("not allowed", answer, StringComparison.Ordinal);
        Assert.Equal(RecurringPlanStatus.Active, (await FindAsync(harness, plan.ItemId)).Status);
    }

    [Fact]
    public async Task PauseThenResume_StopsThePlan_AndRunsItAgainFromToday()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var plan = await CreatePlanAsync(harness, activate: true);

        // Act
        var paused = await _context.InvokeAsync(harness, new PauseOrResumeRecurringPlanTool(), new() { ["planId"] = plan.ItemId, ["action"] = "pause" });
        var whilePaused = (await FindAsync(harness, plan.ItemId)).Status;
        var resumed = await _context.InvokeAsync(harness, new PauseOrResumeRecurringPlanTool(), new() { ["planId"] = plan.ItemId, ["action"] = "resume" });

        // Assert
        using var pausedJson = JsonDocument.Parse(paused);
        using var resumedJson = JsonDocument.Parse(resumed);
        Assert.Equal("paused", pausedJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(RecurringPlanStatus.Paused, whilePaused);
        Assert.Equal(("active", "issue"), (resumedJson.RootElement.GetProperty("status").GetString(), resumedJson.RootElement.GetProperty("mode").GetString()));

        var stored = await FindAsync(harness, plan.ItemId);
        Assert.Equal(RecurringPlanStatus.Active, stored.Status);
        Assert.True(stored.NextRunDate >= _today);
    }

    [Fact]
    public async Task Pause_OfADraftPlan_IsRefusedByThePlanService()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var plan = await CreatePlanAsync(harness, activate: false);

        // Act
        var answer = await _context.InvokeAsync(harness, new PauseOrResumeRecurringPlanTool(), new() { ["planId"] = plan.ItemId, ["action"] = "pause" });

        // Assert
        Assert.Contains("Only an active plan can be paused.", answer, StringComparison.Ordinal);
        Assert.Equal(RecurringPlanStatus.Draft, (await FindAsync(harness, plan.ItemId)).Status);
    }

    private static async Task<RecurringPlan> CreatePlanAsync(InvoicingTestHarness harness, bool activate)
    {
        var plan = await harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IRecurringPlanManager>();
            var draft = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

            draft.Name = "Hosting";
            draft.Rule = "FREQ=MONTHLY;BYMONTHDAY=1";
            draft.StartDate = _today.AddDays(1);
            draft.Mode = RecurringPlanMode.Issue;
            draft.Currency = "USD";
            draft.BillTo = new DocumentParty { Contact = new ContactInfo { Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" } };
            draft.Lines.Add(new DocumentLine { Id = "line-1", Name = "Hosting", Quantity = 1m, UnitPrice = 100m });

            await manager.CreateAsync(draft, TestContext.Current.CancellationToken);

            return draft;
        });

        if (!activate)
        {
            return plan;
        }

        var activated = await harness.InScopeAsync(services => services.GetRequiredService<IRecurringPlanService>().ActivateAsync(plan.ItemId, TestContext.Current.CancellationToken));

        Assert.True(activated.Succeeded, string.Join(" ", activated.Errors));

        return activated.Plan;
    }

    private static Task<RecurringPlan> FindAsync(InvoicingTestHarness harness, string id)
        => harness.InScopeAsync(services => services.GetRequiredService<IRecurringPlanStore>().FindByIdAsync(id, TestContext.Current.CancellationToken).AsTask());
}
