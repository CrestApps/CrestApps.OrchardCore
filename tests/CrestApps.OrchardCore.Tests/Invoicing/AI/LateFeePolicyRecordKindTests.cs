using System.Text.Json;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// The Invoicing Collections feature exposes the late-fee policies to the generic record tools as the
/// <c>lateFeePolicy</c> kind, with the Late fees page's permission and editor, an allow-listed projection, and a rule
/// the page does not need: a save from the chat that turns a policy on or makes it charge more is refused, because the
/// record tools cannot ask for approval and an enabled policy charges every overdue customer on the next run.
/// </summary>
public sealed class LateFeePolicyRecordKindTests
{
    [Fact]
    public void TheCollectionsStartup_RegistersTheLateFeePolicyKind_WithTheSettingsPermission()
    {
        // Arrange
        var kind = GetKind();

        // Assert
        Assert.Equal(InvoicingPermissions.ManageInvoicingSettings.Name, kind.Permission.Name);
        Assert.Equal(InvoicingPermissions.ManageInvoicingSettings.Name, kind.EffectiveReadPermission.Name);
        Assert.True(kind.CanWrite);
        Assert.False(string.IsNullOrWhiteSpace(kind.Description));
        Assert.NotEqual(kind.Name, kind.Title);
    }

    [Fact]
    public void TheProjection_IsAnAllowList()
    {
        // Arrange
        var kind = GetKind();
        var policy = new LateFeePolicy { ItemId = "p1", Name = "Monthly", Kind = LateFeeKind.Percent, Percent = 1.5m, GraceDays = 5, RepeatEveryDays = 30, MaxFees = 3 };

        // Act
        using var projected = JsonDocument.Parse(JsonSerializer.Serialize(kind.Projection(policy)));

        // Assert
        Assert.Equal(
            new[] { "capAmount", "description", "enabled", "graceDays", "id", "kind", "maxFees", "minimumBalance", "name", "percent", "repeatEveryDays", "sendDebitNote", "taxable", "amount" }.Order(StringComparer.Ordinal),
            projected.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("percent", projected.RootElement.GetProperty("kind").GetString());
    }

    [Theory]
    [InlineData(nameof(LateFeePolicy.Enabled), false, true)]
    [InlineData(nameof(LateFeePolicy.Amount), 25, true)]
    [InlineData(nameof(LateFeePolicy.Amount), 5, false)]
    [InlineData(nameof(LateFeePolicy.GraceDays), 2, true)]
    [InlineData(nameof(LateFeePolicy.GraceDays), 30, false)]
    [InlineData(nameof(LateFeePolicy.RepeatEveryDays), 15, true)]
    [InlineData(nameof(LateFeePolicy.RepeatEveryDays), 60, false)]
    [InlineData(nameof(LateFeePolicy.MaxFees), 5, true)]
    [InlineData(nameof(LateFeePolicy.MaxFees), 2, false)]
    [InlineData(nameof(LateFeePolicy.CapAmount), 200, true)]
    [InlineData(nameof(LateFeePolicy.CapAmount), 50, false)]
    [InlineData(nameof(LateFeePolicy.SendDebitNote), true, true)]
    [InlineData(nameof(LateFeePolicy.Taxable), true, true)]
    [InlineData(nameof(LateFeePolicy.Name), "Renamed", false)]
    public void GetFeeIncreases_FindsWhatChargesMore(string field, object value, bool raises)
    {
        // Arrange: an enabled 10 USD fee after 10 days, every 30 days, at most 3 fees, capped at 100.
        var before = Enabled();
        var after = Enabled();

        switch (field)
        {
            case nameof(LateFeePolicy.Enabled): before.Enabled = (bool)value; break;
            case nameof(LateFeePolicy.Amount): after.Amount = Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture); break;
            case nameof(LateFeePolicy.GraceDays): after.GraceDays = (int)value; break;
            case nameof(LateFeePolicy.RepeatEveryDays): after.RepeatEveryDays = (int)value; break;
            case nameof(LateFeePolicy.MaxFees): after.MaxFees = (int)value; break;
            case nameof(LateFeePolicy.CapAmount): after.CapAmount = Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture); break;
            case nameof(LateFeePolicy.SendDebitNote): after.SendDebitNote = (bool)value; break;
            case nameof(LateFeePolicy.Taxable): after.Taxable = (bool)value; break;
            case nameof(LateFeePolicy.Name): after.Name = (string)value; break;
        }

        // Act
        var increases = InvoicingCollectionsAIToolsStartup.GetFeeIncreases(before, after);

        // Assert
        Assert.Equal(raises, increases.Count > 0);
    }

    [Fact]
    public void GetFeeIncreases_AllowsADisabledPolicy_AndRefusesANewEnabledOne()
    {
        // Arrange
        var disabled = Enabled();
        disabled.Enabled = false;
        disabled.Amount = 500m;

        // Act
        var turnedOff = InvoicingCollectionsAIToolsStartup.GetFeeIncreases(Enabled(), disabled);
        var newDisabled = InvoicingCollectionsAIToolsStartup.GetFeeIncreases(null, disabled);
        var newEnabled = InvoicingCollectionsAIToolsStartup.GetFeeIncreases(null, Enabled());

        // Assert
        Assert.Empty(turnedOff);
        Assert.Empty(newDisabled);
        Assert.Equal(["a new policy that is turned on"], newEnabled);
    }

    [Fact]
    public async Task ValidateLateFeePolicy_RefusesTurningAStoredPolicyOn_AndAllowsLoweringIt()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var stored = await harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<ILateFeePolicyManager>();
            var policy = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
            Copy(Enabled(), policy);
            policy.Enabled = false;
            await manager.CreateAsync(policy, TestContext.Current.CancellationToken);

            return policy;
        });

        // Act
        var (turnedOn, lowered) = await harness.InScopeAsync(async services =>
        {
            var on = new LateFeePolicy { ItemId = stored.ItemId };
            Copy(stored, on);
            on.Enabled = true;

            var lower = new LateFeePolicy { ItemId = stored.ItemId };
            Copy(stored, lower);
            lower.Amount = 5m;

            return (
                await InvoicingCollectionsAIToolsStartup.ValidateLateFeePolicyAsync(services, on, TestContext.Current.CancellationToken),
                await InvoicingCollectionsAIToolsStartup.ValidateLateFeePolicyAsync(services, lower, TestContext.Current.CancellationToken));
        });

        // Assert
        Assert.False(turnedOn.Succeeded);
        Assert.Contains(turnedOn.Errors, error => error.ErrorMessage.Contains("turning the policy on", StringComparison.Ordinal));
        Assert.True(lowered.Succeeded, string.Join(" ", lowered.Errors.Select(error => error.ErrorMessage)));
    }

    [Fact]
    public async Task ValidateLateFeePolicy_KeepsThePagesOwnRules()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();

        // Act
        var result = await harness.InScopeAsync(services => InvoicingCollectionsAIToolsStartup.ValidateLateFeePolicyAsync(
            services,
            new LateFeePolicy { Name = "No amount", Enabled = false, Kind = LateFeeKind.Flat, Amount = 0m },
            TestContext.Current.CancellationToken).AsTask());

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("amount above zero", StringComparison.Ordinal));
    }

    private static LateFeePolicy Enabled()
        => new()
        {
            Name = "Late fee",
            Enabled = true,
            Kind = LateFeeKind.Flat,
            Amount = 10m,
            GraceDays = 10,
            RepeatEveryDays = 30,
            MaxFees = 3,
            CapAmount = 100m,
        };

    private static void Copy(LateFeePolicy from, LateFeePolicy to)
    {
        to.Name = from.Name;
        to.Description = from.Description;
        to.Enabled = from.Enabled;
        to.Kind = from.Kind;
        to.Amount = from.Amount;
        to.Percent = from.Percent;
        to.GraceDays = from.GraceDays;
        to.RepeatEveryDays = from.RepeatEveryDays;
        to.MaxFees = from.MaxFees;
        to.CapAmount = from.CapAmount;
        to.MinimumBalance = from.MinimumBalance;
        to.Taxable = from.Taxable;
        to.SendDebitNote = from.SendDebitNote;
    }

    private static AIRecordKind<LateFeePolicy> GetKind()
    {
        var services = new ServiceCollection();

        new InvoicingCollectionsAIToolsStartup(new PassThroughStringLocalizer<InvoicingCollectionsAIToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();
        var kinds = provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;

        return Assert.IsType<AIRecordKind<LateFeePolicy>>(kinds[InvoicingCollectionsAIToolsStartup.LateFeePolicyKind]);
    }
}
