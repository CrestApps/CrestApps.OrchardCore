using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Addresses.Handlers;

/// <summary>
/// Stores a Country's ISO code in capitals and refuses a malformed or duplicate one, for countries created by recipes
/// or the content API. The admin editor checks the code through <see cref="Drivers.CountryCodeDisplayDriver"/>.
/// </summary>
public sealed class CountryContentHandler : ContentHandlerBase
{
    private readonly CountryCodeValidator _validator;

    public CountryContentHandler(CountryCodeValidator validator)
    {
        _validator = validator;
    }

    public override async Task ValidatingAsync(ValidateContentContext context)
    {
        if (!string.Equals(context.ContentItem.ContentType, AddressConstants.Country, StringComparison.Ordinal))
        {
            return;
        }

        JsonNode content = context.ContentItem.Content;
        var codeField = content?[AddressConstants.CountryPart]?[AddressConstants.CodeField] as JsonObject;
        var code = codeField?["Text"]?.GetValue<string>()?.Trim();

        var error = await _validator.GetErrorAsync(code, context.ContentItem.ContentItemId);

        if (error is not null)
        {
            context.Fail(new ValidationResult(error, ["CountryPart.Code"]));

            return;
        }

        codeField["Text"] = code.ToUpperInvariant();
    }
}
