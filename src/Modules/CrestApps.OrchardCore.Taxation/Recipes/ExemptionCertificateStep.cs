using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Taxation.Deployments;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Taxation.Recipes;

/// <summary>
/// Adds or updates customers' tax exemption certificates from an <c>ExemptionCertificate</c> recipe step. A
/// certificate keeps its <c>ItemId</c>, which customers list among their exemption certificates; a certificate without
/// an id is matched by its customer and number, so a second run changes it instead of adding another. A certificate
/// needs its customer and a name or a number.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "ExemptionCertificate", "ExemptionCertificates": [ { "ItemId": "...", "Name": "Resale", "CertificateNumber": "R-1001", "CustomerId": "...", "ExemptionType": "Resale", "Status": "Active", "TaxTypes": [ "SalesTax" ] } ] }
/// </code>
/// Only the certificate's details travel; a scanned copy, when a store keeps one, stays where it was uploaded.
/// </remarks>
internal sealed class ExemptionCertificateStep : CatalogStoreRecipeStep<ExemptionCertificate>
{
    public const string EntriesName = TaxationDeploymentSteps.ExemptionCertificateEntries;

    private readonly IExemptionCertificateStore _store;

    internal readonly IStringLocalizer S;

    public ExemptionCertificateStep(IExemptionCertificateStore store, IStringLocalizer<ExemptionCertificateStep> stringLocalizer)
        : base(TaxationDeploymentSteps.ExemptionCertificate, EntriesName, store)
    {
        _store = store;
        S = stringLocalizer;
    }

    protected override async ValueTask<CatalogRecipeLookup<ExemptionCertificate>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var customerId = token[nameof(ExemptionCertificate.CustomerId)]?.GetValue<string>();
        var number = token[nameof(ExemptionCertificate.CertificateNumber)]?.GetValue<string>()?.Trim();

        if (string.IsNullOrEmpty(customerId) || string.IsNullOrEmpty(number))
        {
            return CatalogRecipeLookup<ExemptionCertificate>.NotFound;
        }

        var certificate = (await _store.GetAllAsync())
            .FirstOrDefault(entry => entry.CustomerId == customerId && string.Equals(entry.CertificateNumber, number, StringComparison.OrdinalIgnoreCase));

        return certificate is null ? CatalogRecipeLookup<ExemptionCertificate>.NotFound : CatalogRecipeLookup<ExemptionCertificate>.Found(certificate);
    }

    protected override void Preparing(ExemptionCertificate entry, JsonObject token)
    {
        entry.Name = entry.Name?.Trim();
        entry.CertificateNumber = entry.CertificateNumber?.Trim();
        entry.TaxTypes ??= [];
        entry.JurisdictionIds ??= [];
        entry.ClassificationCodes ??= [];
    }

    protected override ValueTask<ValidationResultDetails> ValidateAsync(ExemptionCertificate entry)
    {
        var result = new ValidationResultDetails();

        if (string.IsNullOrEmpty(entry.CustomerId))
        {
            result.Fail(new ValidationResult(S["An exemption certificate needs the customer it belongs to."]));
        }

        if (string.IsNullOrEmpty(entry.Name) && string.IsNullOrEmpty(entry.CertificateNumber))
        {
            result.Fail(new ValidationResult(S["An exemption certificate needs a name or a number."]));
        }

        if (entry.EffectiveFromUtc.HasValue && entry.ExpirationUtc.HasValue && entry.ExpirationUtc <= entry.EffectiveFromUtc)
        {
            result.Fail(new ValidationResult(S["The exemption certificate '{0}' must expire after it takes effect.", entry.Name ?? entry.CertificateNumber]));
        }

        return ValueTask.FromResult(result);
    }
}
