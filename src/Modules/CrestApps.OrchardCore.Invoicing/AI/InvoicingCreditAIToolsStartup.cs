using CrestApps.Core.AI;
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
/// Registers the credit note AI tools when Invoicing and the AI feature are on: <c>previewCreditNote</c> prepares a draft
/// credit note and returns the token <c>issueCreditNote</c> requires; <c>issueCreditNote</c> asks for approval, because it
/// moves money or credit and an issued credit note cannot be undone.
/// </summary>
[Feature(InvoicingConstants.Features.Area)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class CreditNoteAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreditNoteAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CreditNoteAIToolsStartup(IStringLocalizer<CreditNoteAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<InvoicingToolScope>();

        services.AddCoreAITool<PreviewCreditNoteTool>(PreviewCreditNoteTool.TheName)
            .WithTitle(S["Preview a credit note"])
            .WithDescription(S["Lists what is creditable on an invoice, or prepares a draft credit note and returns its total, effect and the token to issue it."])
            .WithCategory(S["Invoicing"])
            .Selectable();

        services.AddCoreAITool<IssueCreditNoteTool>(IssueCreditNoteTool.TheName)
            .WithTitle(S["Issue a credit note"])
            .WithDescription(S["Issues a previewed credit note: lowers what the invoice owes, refunds the payment or adds customer credit."])
            .WithCategory(S["Invoicing"])
            .RequireApproval()
            .Selectable();
    }
}

/// <summary>
/// Registers the estimate AI tools and the <c>estimate</c> record kind when Invoicing Estimates and the AI feature are on:
/// draft an estimate, send it (asks for approval: it e-mails the customer and locks the offer) and turn it into a draft
/// invoice. Estimates are read with the generic record tools, read-only, because their editor is a controller form.
/// </summary>
[Feature(InvoicingConstants.Features.Estimates)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class EstimateAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EstimateAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EstimateAIToolsStartup(IStringLocalizer<EstimateAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<DraftEstimateTool>(DraftEstimateTool.TheName)
            .WithTitle(S["Draft an estimate"])
            .WithDescription(S["Drafts an estimate (quote) for a customer from lines; staff check it and send it."])
            .WithCategory(S["Invoicing"])
            .Selectable();

        services.AddCoreAITool<SendEstimateTool>(SendEstimateTool.TheName)
            .WithTitle(S["Send an estimate"])
            .WithDescription(S["Numbers an estimate and e-mails it to the customer with the link to accept or decline it."])
            .WithCategory(S["Invoicing"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ConvertEstimateToInvoiceTool>(ConvertEstimateToInvoiceTool.TheName)
            .WithTitle(S["Convert an estimate to an invoice"])
            .WithDescription(S["Turns a sent or accepted estimate into a draft invoice: all of it, its deposit, or a percent of its lines."])
            .WithCategory(S["Invoicing"])
            .Selectable();

        services.AddAIRecordKind<Estimate>(InvoicingRecordKinds.EstimateKind, kind => kind
            .WithTitle(S["Estimate"])
            .WithDescription(S["a priced offer (quote) sent to a customer before the work, with its status (draft, sent, accepted, declined, expired, canceled, invoiced), lines, total, expiry and deposit; draft one with draftEstimate, send it with sendEstimate, invoice it with convertEstimateToInvoice; the text filter matches the number, customer or reference, or a status name alone"])
            .WithPermission(InvoicingPermissions.ManageEstimates)
            .WithReadPermission(InvoicingPermissions.ViewInvoices)
            .UseCatalogManager<IEstimateManager>()
            .FindUsing(InvoicingRecordKinds.FindEstimateAsync)
            .ListUsing(InvoicingRecordKinds.ListEstimatesAsync)
            .WithProjection(InvoicingRecordKinds.DescribeEstimate)
            .ReadOnly());
    }
}

/// <summary>
/// Registers the recurring plan AI tool and the <c>recurringInvoicePlan</c> record kind when Invoicing Recurring and the
/// AI feature are on: plans are read with the generic record tools, read-only, because their editor is a controller form,
/// and paused or resumed with <c>pauseOrResumeRecurringPlan</c>, which asks for approval: resuming a plan lets the
/// recurring task issue, e-mail or charge its next invoice as soon as it is due, with nobody asked again.
/// </summary>
[Feature(InvoicingConstants.Features.Recurring)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class RecurringPlanAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecurringPlanAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RecurringPlanAIToolsStartup(IStringLocalizer<RecurringPlanAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<PauseOrResumeRecurringPlanTool>(PauseOrResumeRecurringPlanTool.TheName)
            .WithTitle(S["Pause or resume a recurring invoice plan"])
            .WithDescription(S["Pauses a recurring invoice plan, skipping its runs meanwhile, or resumes it from its next date."])
            .WithCategory(S["Invoicing"])
            .RequireApproval()
            .Selectable();

        services.AddAIRecordKind<RecurringPlan>(InvoicingRecordKinds.RecurringPlanKind, kind => kind
            .WithTitle(S["Recurring invoice plan"])
            .WithDescription(S["a plan that makes an invoice for a customer on a schedule (as a draft, issued, issued and e-mailed, or issued and charged to the saved payment method), with its status (draft, active, paused, completed, canceled), next run and recent runs; pause or resume it with pauseOrResumeRecurringPlan, change it on its page; the text filter matches the name, customer or reference, or a status name alone"])
            .WithPermission(InvoicingPermissions.ManageRecurringInvoices)
            .WithReadPermission(InvoicingPermissions.ViewInvoices)
            .UseCatalogManager<IRecurringPlanManager>()
            .FindUsing(InvoicingRecordKinds.FindRecurringPlanAsync)
            .ListUsing(InvoicingRecordKinds.ListRecurringPlansAsync)
            .WithProjection(InvoicingRecordKinds.DescribeRecurringPlan)
            .ReadOnly());
    }
}

/// <summary>
/// Registers the <c>usageMeter</c> and <c>billableItem</c> record kinds when Invoicing Billing and the AI feature are on.
/// Both are written through their admin editors (display drivers), with the permission of their admin pages; a charge an
/// invoice bills is neither changed nor deleted, as on the Unbilled charges page.
/// </summary>
[Feature(InvoicingConstants.Features.Billing)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class BillingAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BillingAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BillingAIToolsStartup(IStringLocalizer<BillingAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIRecordKind<UsageMeter>(InvoicingRecordKinds.UsageMeterKind, kind => kind
            .WithTitle(S["Usage meter"])
            .WithDescription(S["a usage meter that counts what customers use (API calls, gigabytes...) under a unique lowercase code, summed, as a maximum or as the last value, which recurring invoice plans bill by tiers; a deleted meter keeps its usage records, and the plans that bill it stop billing it"])
            .WithPermission(InvoicingPermissions.ManageInvoices)
            .UseCatalogManager<IUsageMeterManager>()
            .ListUsing(InvoicingRecordKinds.ListUsageMetersAsync)
            .WithProjection(InvoicingRecordKinds.DescribeUsageMeter)
            .UseEditorReplay());

        services.AddAIRecordKind<BillableItem>(InvoicingRecordKinds.BillableItemKind, kind => kind
            .WithTitle(S["Billable charge"])
            .WithDescription(S["a charge kept to bill a customer later (an expense with a markup, a fee...), offered on the customer's next draft invoice; a charge an invoice bills is not changed or deleted; the text filter matches the description or customer, or unbilled or billed alone"])
            .WithPermission(InvoicingPermissions.ManageInvoices)
            .UseCatalogManager<IBillableItemManager>()
            .ListUsing(InvoicingRecordKinds.ListBillableItemsAsync)
            .WithProjection(InvoicingRecordKinds.DescribeBillableItem)
            .WithInUseCheck(InvoicingRecordKinds.GetBillableItemUseAsync)
            .UseEditorReplay());
    }
}
