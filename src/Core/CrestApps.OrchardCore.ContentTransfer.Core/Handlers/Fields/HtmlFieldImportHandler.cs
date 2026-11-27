using Microsoft.Extensions.Localization;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Infrastructure.Html;

namespace CrestApps.OrchardCore.ContentTransfer.Handlers.Fields;

public sealed class HtmlFieldImportHandler : StandardFieldImportHandler
{
    internal readonly IStringLocalizer S;

    private readonly IHtmlSanitizerService _htmlSanitizerService;

    public HtmlFieldImportHandler(
        IStringLocalizer<HtmlFieldImportHandler> stringLocalizer,
        IHtmlSanitizerService htmlSanitizerService)
    {
        S = stringLocalizer;
        _htmlSanitizerService = htmlSanitizerService;
    }

    protected override string BindingPropertyName
        => nameof(HtmlField.Html);

    protected override Task SetValueAsync(ContentFieldImportMapContext context, string text)
    {
        // The HTML field editor sanitizes what it saves unless the field turns that off; an import must not be a way
        // around it.
        if (text != null && context.ContentPartFieldDefinition?.GetSettings<HtmlFieldSettings>()?.SanitizeHtml != false)
        {
            text = _htmlSanitizerService.Sanitize(text);
        }

        context.ContentPart.Alter<HtmlField>(context.ContentPartFieldDefinition.Name, field =>
        {
            field.Html = text;
        });

        return Task.CompletedTask;
    }

    protected override Task<object> GetValueAsync(ContentFieldExportMapContext context)
    {
        var field = context.ContentPart.Get<HtmlField>(context.ContentPartFieldDefinition.Name);

        return Task.FromResult<object>(field?.Html);
    }

    protected override string Description(ImportContentFieldContext context)
        => S["An HTML value for {0}", context.ContentPartFieldDefinition.DisplayName()];
}
