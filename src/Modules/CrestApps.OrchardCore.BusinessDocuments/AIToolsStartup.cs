using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.BusinessDocuments;

/// <summary>
/// Exposes the business document templates (the look invoices, receipts and other documents are printed with) to the
/// generic record tools when the AI feature is on. Writes replay the Document templates editor, with its permission, and
/// a template saved as the default takes over from the others of its kinds, as on that page.
/// </summary>
[Feature(BusinessDocumentsConstants.Features.Area)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    /// <summary>
    /// The name of the business document template record kind.
    /// </summary>
    public const string TemplateKind = "businessDocumentTemplate";

    // How much of a footer the record tools show.
    private const int FooterLength = 500;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIRecordKind<BusinessDocumentTemplate>(TemplateKind, kind => kind
            .WithTitle(S["Business document template"])
            .WithDescription(S["the look printed business documents (invoices, receipts...) take: the document kinds it is for (none means every kind), whether it is their default, the logo, the accent color, the paper size and the footer. Saving a default takes the default from the other templates of its kinds"])
            .WithPermission(BusinessDocumentsPermissions.ManageBusinessDocumentTemplates)
            .UseCatalogManager<IBusinessDocumentTemplateManager>()
            .WithProjection(DescribeTemplate)
            .ValidateUsing(ValidateTemplateAsync)
            .SaveUsing(SaveTemplateAsync)
            .UseEditorReplay());
    }

    /// <summary>
    /// The allow-listed fields of a template; the footer is cut at 500 characters.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <returns>The fields.</returns>
    internal static object DescribeTemplate(BusinessDocumentTemplate template)
        => new
        {
            id = template.ItemId,
            name = template.Name,
            description = template.Description,
            kinds = template.Kinds ?? [],
            isDefault = template.IsDefault,
            logoUrl = template.LogoUrl,
            accentColor = template.AccentColor,
            paperSize = template.PaperSize.ToString(),
            footer = template.Footer is { Length: > FooterLength } footer ? footer[..FooterLength] : template.Footer,
            footerTruncated = template.Footer?.Length > FooterLength,
        };

    /// <summary>
    /// Validates a template as the page does, then checks that every document kind it is given is one an enabled
    /// feature registers: the page offers only those, plus the kinds the template already had.
    /// </summary>
    /// <param name="services">The services of the tool call.</param>
    /// <param name="template">The template with the values the editor bound.</param>
    /// <param name="cancellationToken">A token to cancel the checks.</param>
    /// <returns>The validation result.</returns>
    internal static async ValueTask<ValidationResultDetails> ValidateTemplateAsync(IServiceProvider services, BusinessDocumentTemplate template, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<IBusinessDocumentTemplateManager>();
        var result = await manager.ValidateAsync(template, cancellationToken) ?? new ValidationResultDetails();
        var stored = string.IsNullOrEmpty(template.ItemId) ? null : await manager.FindByIdAsync(template.ItemId, cancellationToken);
        var registered = services.GetRequiredService<IOptions<BusinessDocumentOptions>>().Value.Kinds;
        var unknown = (template.Kinds ?? [])
            .Where(kind => !registered.ContainsKey(kind) && stored?.Kinds?.Contains(kind, StringComparer.OrdinalIgnoreCase) != true)
            .ToList();

        if (unknown.Count > 0)
        {
            result.Fail(new ValidationResult(
                $"These are not document kinds: {string.Join(", ", unknown)}. The document kinds are: {string.Join(", ", registered.Keys.Order(StringComparer.OrdinalIgnoreCase))}.",
                [nameof(BusinessDocumentTemplate.Kinds)]));
        }

        return result;
    }

    /// <summary>
    /// Saves a template as the page does: created or updated through the manager, then, when it is a default, it takes
    /// the default from the other templates of its kinds.
    /// </summary>
    /// <param name="services">The services of the tool call.</param>
    /// <param name="template">The template.</param>
    /// <param name="isNew">Whether it is new.</param>
    /// <param name="cancellationToken">A token to cancel the save.</param>
    /// <returns>A task that completes when it is saved.</returns>
    internal static async ValueTask SaveTemplateAsync(IServiceProvider services, BusinessDocumentTemplate template, bool isNew, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<IBusinessDocumentTemplateManager>();

        if (isNew)
        {
            await manager.CreateAsync(template, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(template, cancellationToken: cancellationToken);
        }

        await BusinessDocumentTemplateDefaults.TakeOverDefaultAsync(manager, template, cancellationToken);
    }
}
