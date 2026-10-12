using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Indexes;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using YesSql;

namespace CrestApps.OrchardCore.Addresses.Handlers;

/// <summary>
/// Normalizes and validates the ISO code of <c>Country</c> content items so only well-formed, unique ISO
/// 3166-1 alpha-2 codes reach the taxation and checkout country selectors.
/// </summary>
public sealed class CountryContentHandler : ContentHandlerBase
{
    private readonly ISession _session;

    /// <summary>
    /// Gets the localizer used to produce validation messages.
    /// </summary>
    internal IStringLocalizer S { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CountryContentHandler"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used to enforce country code uniqueness.</param>
    /// <param name="stringLocalizer">The localizer used to produce validation messages.</param>
    public CountryContentHandler(
        ISession session,
        IStringLocalizer<CountryContentHandler> stringLocalizer)
    {
        _session = session;
        S = stringLocalizer;
    }

    /// <summary>
    /// Normalizes the country code to an upper-case value and validates that it is a unique ISO 3166-1
    /// alpha-2 code before the content item is persisted.
    /// </summary>
    /// <param name="context">The validation context containing the country content item.</param>
    public override async Task ValidatingAsync(ValidateContentContext context)
    {
        if (!string.Equals(context.ContentItem.ContentType, AddressConstants.Country, StringComparison.Ordinal))
        {
            return;
        }

        JsonNode content = context.ContentItem.Content;
        var codeField = content?[AddressConstants.CountryPart]?["Code"] as JsonObject;
        var code = codeField?["Text"]?.GetValue<string>()?.Trim();

        if (string.IsNullOrEmpty(code))
        {
            context.Fail(new ValidationResult(S["An ISO country code is required."], ["CountryPart.Code"]));

            return;
        }

        code = code.ToUpperInvariant();
        codeField["Text"] = code;

        if (code.Length != 2 || !char.IsAsciiLetterUpper(code[0]) || !char.IsAsciiLetterUpper(code[1]))
        {
            context.Fail(new ValidationResult(S["The ISO country code must be two letters, for example US or CA."], ["CountryPart.Code"]));

            return;
        }

        var contentItemId = context.ContentItem.ContentItemId;

        var duplicate = await _session.QueryIndex<GeographicAreaIndex>(index =>
                index.ContentType == AddressConstants.Country &&
                index.Code == code &&
                index.ContentItemId != contentItemId &&
                index.Latest)
            .FirstOrDefaultAsync();

        if (duplicate is not null)
        {
            context.Fail(new ValidationResult(S["A country with the code '{0}' already exists.", code], ["CountryPart.Code"]));
        }
    }
}
