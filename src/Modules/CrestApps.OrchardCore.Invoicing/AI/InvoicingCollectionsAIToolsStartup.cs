using System.ComponentModel.DataAnnotations;
using CrestApps.Core.AI;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// Registers the AI tools and the record kind of the Invoicing Collections feature when the AI feature is on: the
/// <c>sendInvoiceStatement</c> tool, and the late-fee policies as the <c>lateFeePolicy</c> kind of the generic record
/// tools, written through the Late fees page's own editor (<c>LateFeePolicyDisplayDriver</c>) with its permission.
/// </summary>
[Feature(InvoicingConstants.Features.Collections)]
[RequireFeatures(AIConstants.Feature.Area)]
public sealed class InvoicingCollectionsAIToolsStartup : StartupBase
{
    /// <summary>
    /// The name of the late-fee policy record kind.
    /// </summary>
    public const string LateFeePolicyKind = "lateFeePolicy";

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvoicingCollectionsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InvoicingCollectionsAIToolsStartup(IStringLocalizer<InvoicingCollectionsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<InvoicingToolScope>();

        services.AddCoreAITool<SendInvoiceStatementTool>(SendInvoiceStatementTool.TheName)
            .WithTitle(S["Send an invoice statement"])
            .WithDescription(S["E-mails a customer their statement with its PDF, to their billing address on file: open items (what they still owe and how late) or activity over a period."])
            .WithCategory(S["Invoicing"])
            .RequireApproval()
            .Selectable();

        services.AddAIRecordKind<LateFeePolicy>(LateFeePolicyKind, kind => kind
            .WithTitle(S["Late-fee policy"])
            .WithDescription(S["a late-fee policy that charges overdue invoices a fixed or percent fee as linked debit notes, after grace days, repeating up to a limit or cap; turning one on or raising its fees is done on the Late fees page"])
            .WithPermission(InvoicingPermissions.ManageInvoicingSettings)
            .UseCatalogManager<ILateFeePolicyManager>()
            .WithProjection(policy => new
            {
                id = policy.ItemId,
                name = policy.Name,
                description = policy.Description,
                enabled = policy.Enabled,
                kind = policy.Kind == LateFeeKind.Percent ? "percent" : "flat",

                // A flat fee is charged in each invoice's own currency.
                amount = policy.Kind == LateFeeKind.Flat ? policy.Amount : (decimal?)null,
                percent = policy.Kind == LateFeeKind.Percent ? policy.Percent : (decimal?)null,
                graceDays = policy.GraceDays,
                repeatEveryDays = policy.RepeatEveryDays > 0 ? policy.RepeatEveryDays : (int?)null,
                maxFees = policy.MaxFees,
                capAmount = policy.CapAmount,
                minimumBalance = policy.MinimumBalance,
                taxable = policy.Taxable,
                sendDebitNote = policy.SendDebitNote,
            })
            .ValidateUsing(ValidateLateFeePolicyAsync)
            .UseEditorReplay());
    }

    /// <summary>
    /// Checks a late-fee policy as the Late fees page does before saving (the manager's rules), and refuses a save that
    /// leaves the policy charging more: the generic record tools cannot ask for approval, and an enabled policy charges
    /// every overdue customer on the next hourly run, so turning one on or raising its fees stays on the page.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="policy">The policy, with the posted values bound.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The outcome of the checks.</returns>
    internal static async ValueTask<ValidationResultDetails> ValidateLateFeePolicyAsync(IServiceProvider services, LateFeePolicy policy, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<ILateFeePolicyManager>();
        var result = await manager.ValidateAsync(policy, cancellationToken) ?? new ValidationResultDetails();
        var stored = string.IsNullOrEmpty(policy.ItemId) ? null : await manager.FindByIdAsync(policy.ItemId, cancellationToken);
        var increases = GetFeeIncreases(stored, policy);

        if (increases.Count > 0)
        {
            result.Fail(new ValidationResult(
                $"A late-fee policy is not turned on or raised from the chat ({string.Join("; ", increases)}): an enabled policy charges every overdue customer on the next hourly run, and this save cannot ask anyone to confirm it. A person makes this change on Commerce › Late fees; turning a policy off, making it gentler, renaming it and creating a disabled policy are allowed here.",
                [nameof(LateFeePolicy.Enabled)]));
        }

        return result;
    }

    /// <summary>
    /// Lists how a save makes a policy charge more, sooner or more often while it is enabled. A disabled policy charges
    /// nothing, so any change to it is allowed.
    /// </summary>
    /// <param name="before">The stored policy, or <see langword="null"/> for a new one.</param>
    /// <param name="after">The policy being saved.</param>
    /// <returns>What is raised; empty when nothing is.</returns>
    internal static List<string> GetFeeIncreases(LateFeePolicy before, LateFeePolicy after)
    {
        var increases = new List<string>();

        if (!after.Enabled)
        {
            return increases;
        }

        if (before is null)
        {
            increases.Add("a new policy that is turned on");

            return increases;
        }

        if (!before.Enabled)
        {
            increases.Add("turning the policy on");
        }

        if (before.Kind != after.Kind)
        {
            increases.Add("a different kind of fee");
        }
        else if (after.Kind == LateFeeKind.Flat && after.Amount > before.Amount)
        {
            increases.Add("a higher flat fee");
        }
        else if (after.Kind == LateFeeKind.Percent && after.Percent > before.Percent)
        {
            increases.Add("a higher percent");
        }

        if (before.CapAmount is { } cap && (after.CapAmount is null || after.CapAmount > cap))
        {
            increases.Add("a higher or removed cap");
        }

        if (after.GraceDays < before.GraceDays)
        {
            increases.Add("fewer grace days");
        }

        if (after.MinimumBalance < before.MinimumBalance)
        {
            increases.Add("a lower minimum balance");
        }

        if (after.RepeatEveryDays > 0)
        {
            if (before.RepeatEveryDays <= 0)
            {
                increases.Add("repeating fees");
            }
            else
            {
                if (after.RepeatEveryDays < before.RepeatEveryDays)
                {
                    increases.Add("more frequent repeats");
                }

                if (before.MaxFees is { } most && (after.MaxFees is null || after.MaxFees > most))
                {
                    increases.Add("more repeated fees");
                }
            }
        }

        if (after.Taxable && !before.Taxable)
        {
            increases.Add("tax on the fee");
        }

        if (after.SendDebitNote && !before.SendDebitNote)
        {
            increases.Add("e-mailing the debit notes to customers");
        }

        return increases;
    }
}
