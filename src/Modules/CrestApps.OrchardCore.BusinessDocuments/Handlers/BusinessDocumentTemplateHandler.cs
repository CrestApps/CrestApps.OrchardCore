using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.BusinessDocuments.Core;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Core.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.BusinessDocuments.Handlers;

/// <summary>
/// Stamps document templates, reads them from recipes, and checks them: a template has a name, an accent color written
/// <c>#RRGGBB</c>, and a logo address an image may be shown from.
/// </summary>
internal sealed class BusinessDocumentTemplateHandler : AuditedCatalogEntryHandlerBase<BusinessDocumentTemplate>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentTemplateHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BusinessDocumentTemplateHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<BusinessDocumentTemplateHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidatingContext<BusinessDocumentTemplate> context, CancellationToken cancellationToken = default)
    {
        var template = context.Model;

        template.Name = template.Name?.Trim();
        template.Kinds = (template.Kinds ?? [])
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
            .Select(kind => kind.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(BusinessDocumentTemplate.Name)]));
        }

        if (string.IsNullOrWhiteSpace(template.AccentColor))
        {
            template.AccentColor = null;
        }
        else if (BusinessDocumentStyle.TryNormalizeAccentColor(template.AccentColor, out var color))
        {
            template.AccentColor = color;
        }
        else
        {
            context.Result.Fail(new ValidationResult(S["Enter the accent color as # followed by six hexadecimal digits, such as #2563EB."], [nameof(BusinessDocumentTemplate.AccentColor)]));
        }

        if (string.IsNullOrWhiteSpace(template.LogoUrl))
        {
            template.LogoUrl = null;
        }
        else if (!BusinessDocumentStyle.IsSafeImageUrl(template.LogoUrl))
        {
            context.Result.Fail(new ValidationResult(S["Enter the logo as an address in the media library, such as /media/logo.png, or a full address that starts with https://."], [nameof(BusinessDocumentTemplate.LogoUrl)]));
        }
        else
        {
            template.LogoUrl = template.LogoUrl.Trim();
        }

        if (!Enum.IsDefined(template.PaperSize))
        {
            context.Result.Fail(new ValidationResult(S["Choose a paper size."], [nameof(BusinessDocumentTemplate.PaperSize)]));
        }

        return Task.CompletedTask;
    }
}
