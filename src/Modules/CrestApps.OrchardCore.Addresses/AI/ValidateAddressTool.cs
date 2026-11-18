using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Addresses.AI;

/// <summary>
/// Checks a postal address with the shared address editor's rules, as every address form does, and returns the problems
/// per field in the editor's own words, the tidied address, its printed lines and its country's layout. It needs a
/// signed-in caller and no permission, as the address format endpoint is public.
/// </summary>
internal sealed class ValidateAddressTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "validateAddress";

    /// <summary>
    /// The answer to a caller who is not signed in.
    /// </summary>
    public const string NotAllowedMessage = "You are not allowed to use this function.";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonElement _jsonSchema = JsonSerializer.SerializeToElement(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["address"] = AddressArguments.CreateSchema("The address to check. Give the two-letter country code; the other fields are read the way that country writes them."),
        },
        ["required"] = new JsonArray("address"),
        ["additionalProperties"] = false,
    });

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Checks a postal address against its country's rules and returns the problems per field, the normalized address, the lines as printed, and which fields that country uses and requires. Call it before saving an address on a customer, account location, staff member or content item.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (GetCaller(services) is null)
        {
            return NotAllowedMessage;
        }

        if (arguments.Keys.FirstOrDefault(name => name != "address") is { } unknown)
        {
            return $"Nothing was checked: '{unknown}' is not an argument of {Name}. Give the address in address.";
        }

        if (!arguments.TryGetValue("address", out var value) || value is null)
        {
            return "Nothing was checked: give the address in address, with its two-letter country code.";
        }

        var element = value is JsonElement json ? json : JsonSerializer.SerializeToElement(value);

        if (!AddressArguments.TryRead(element, includeRecipient: true, out var address, out var error))
        {
            return $"Nothing was checked: {error}";
        }

        var editor = services.GetRequiredService<IAddressEditorService>();
        var formatter = services.GetRequiredService<IAddressFormatter>();
        var modelState = new ModelStateDictionary();
        var result = await editor.ValidateAsync(AddressEditorViewModel.From(address), modelState, prefix: null);
        var layout = await editor.GetLayoutAsync(result.Address.Country);
        var lines = await formatter.GetLinesAsync(result.Address);

        var problems = result.Problems
            .Select(problem =>
            {
                var field = FieldName(problem.Component);
                var key = problem.Component == AddressComponent.StreetAddress ? nameof(AddressEditorViewModel.AddressLine1) : problem.Component.ToString();

                return new
                {
                    field,
                    problem = problem.Kind.ToString(),
                    message = modelState.TryGetValue(key, out var entry) ? entry.Errors.FirstOrDefault()?.ErrorMessage : null,
                };
            })
            .ToList();

        var countryUnknown = result.Problems.Any(problem => problem.Component == AddressComponent.Country);
        var regionUnknown = result.Problems.Any(problem => problem.Component == AddressComponent.Region && problem.Kind == AddressProblemKind.Unknown);
        IReadOnlyList<CountryInfo> countries = countryUnknown
            ? await services.GetRequiredService<ICountryService>().GetCountriesAsync()
            : null;

        return JsonSerializer.Serialize(new
        {
            valid = result.IsValid,
            problems,
            normalizedAddress = AddressArguments.Describe(result.Address),
            lines,
            country = countryUnknown ? null : new
            {
                code = layout.Country,
                usedFields = layout.Lines.SelectMany(line => line).Select(component => ComponentFieldName(component)).Distinct().ToList(),
                requiredFields = layout.Required.Select(component => ComponentFieldName(component)).Distinct().ToList(),
                postalCodeExample = layout.PostalCodeExample,
                regionCount = layout.Regions.Count,
                regions = regionUnknown ? layout.Regions.Select(region => new { code = region.Code, name = region.Name }).ToList() : null,
            },
            countries = countries?.Select(country => new { code = country.Code, name = country.Name }).ToList(),
            message = result.IsValid
                ? "The address has its country's form. This does not check that the place exists."
                : countryUnknown
                    ? "Give one of the country codes in countries."
                    : "Fix the fields in problems and check the address again.",
        }, _jsonOptions);
    }

    private static string FieldName(AddressComponent component)
        => component == AddressComponent.StreetAddress ? "addressLine1" : JsonNamingPolicy.CamelCase.ConvertName(component.ToString());

    private static string ComponentFieldName(string component)
        => Enum.TryParse<AddressComponent>(component, out var value) ? FieldName(value) : JsonNamingPolicy.CamelCase.ConvertName(component);

    private static ClaimsPrincipal GetCaller(IServiceProvider services)
    {
        var user = services.GetService<IUserAccessor>()?.User
            ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true
            ? user
            : null;
    }
}
