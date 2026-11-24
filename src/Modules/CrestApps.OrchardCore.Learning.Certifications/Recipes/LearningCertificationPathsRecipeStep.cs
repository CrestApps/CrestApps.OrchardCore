using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Learning.Certifications.Recipes;

/// <summary>
/// Creates or updates certification paths, matched to their certification by id or by name.
/// </summary>
internal sealed class LearningCertificationPathsRecipeStep : NamedRecipeStepHandler
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICertificationPathService _paths;
    private readonly ICertificationManager _certifications;

    internal readonly IStringLocalizer S;

    public LearningCertificationPathsRecipeStep(ICertificationPathService paths, ICertificationManager certifications, IStringLocalizer<LearningCertificationPathsRecipeStep> stringLocalizer)
        : base(CertificationsConstants.RecipeStepName)
    {
        _paths = paths;
        _certifications = certifications;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step["Paths"] is not JsonArray rows)
        {
            return;
        }

        foreach (var row in rows.OfType<JsonObject>())
        {
            CertificationPath incoming;

            try
            {
                incoming = row.Deserialize<CertificationPath>(JsonOptions);
            }
            catch (JsonException exception)
            {
                context.Errors.Add(S["A certification path could not be read: {0}", exception.Message]);

                continue;
            }

            if (incoming is null)
            {
                continue;
            }

            if (row["certificationName"]?.GetValue<string>() is { Length: > 0 } certificationName)
            {
                incoming.CertificationId = (await _certifications.FindByNameAsync(certificationName))?.ItemId;
            }

            var existing = await _paths.FindByCertificationAsync(incoming.CertificationId)
                ?? (string.IsNullOrEmpty(incoming.ItemId) ? null : await _paths.FindByIdAsync(incoming.ItemId));

            // Copy onto the instance the session tracks; saving the deserialized copy would store a second document.
            var path = existing ?? incoming;

            if (existing is not null)
            {
                existing.Enabled = incoming.Enabled;
                existing.Join = incoming.Join;
                existing.InitialRequirements = incoming.InitialRequirements;
                existing.RenewalRequirements = incoming.RenewalRequirements;
                existing.ReferenceDate = incoming.ReferenceDate;
                existing.FixedMonth = incoming.FixedMonth;
                existing.FixedDay = incoming.FixedDay;
                existing.MinimumIntervalMonths = incoming.MinimumIntervalMonths;
                existing.RenewalWindowDays = incoming.RenewalWindowDays;
                existing.GracePeriodDays = incoming.GracePeriodDays;
                existing.StartsOnFirstRequirement = incoming.StartsOnFirstRequirement;
                existing.EnrollOnRenewal = incoming.EnrollOnRenewal;
            }

            var result = await _paths.SaveAsync(path);

            foreach (var error in result.Errors)
            {
                context.Errors.Add(S["A certification path was refused: {0}", error]);
            }
        }
    }
}
