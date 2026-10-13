using System.Net;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Products.Core.Listing;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Services.Attributes;

/// <summary>
/// HTML and Markdown fields, such as a long description. Their plain text is what keyword search matches; they are
/// too long to filter or sort by.
/// </summary>
public sealed partial class RichTextFieldAttributeValueProvider : IProductAttributeValueProvider
{
    public bool CanHandle(string fieldTypeName)
        => fieldTypeName is "HtmlField" or "MarkdownField";

    public ProductAttributeValueKind GetValueKind(ContentPartFieldDefinition field)
        => ProductAttributeValueKind.RichText;

    public ValueTask<IEnumerable<ProductAttributeValue>> GetValuesAsync(ProductAttributeValueContext context)
    {
        var source = FieldValueReader.GetString(context.Value, "Html") ?? FieldValueReader.GetString(context.Value, "Markdown");
        var text = ToPlainText(source);

        if (string.IsNullOrEmpty(text))
        {
            return ValueTask.FromResult(Enumerable.Empty<ProductAttributeValue>());
        }

        IEnumerable<ProductAttributeValue> values =
        [
            new ProductAttributeValue
            {
                Key = context.Key,
                Text = text,
                Searchable = context.Settings.Searchable,
            },
        ];

        return ValueTask.FromResult(values);
    }

    /// <summary>
    /// The words of HTML or Markdown, without tags, markup characters or repeated white space.
    /// </summary>
    public static string ToPlainText(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var text = TagPattern().Replace(source, " ");
        text = WebUtility.HtmlDecode(text);
        text = MarkdownPattern().Replace(text, " ");

        return WhiteSpacePattern().Replace(text, " ").Trim();
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"[#*_`>\[\]]+")]
    private static partial Regex MarkdownPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpacePattern();
}
