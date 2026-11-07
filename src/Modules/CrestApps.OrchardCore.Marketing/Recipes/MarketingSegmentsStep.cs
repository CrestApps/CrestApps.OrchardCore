using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Marketing.Recipes;

/// <summary>
/// Adds or updates marketing segments from a <c>MarketingSegments</c> recipe step. A segment is matched by its
/// <c>ItemId</c>, so running a recipe twice adds nothing, and it is checked the way the segment editor checks it.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "MarketingSegments", "Segments": [ { "ItemId": "...", "Name": "Lapsed buyers", "Rules": { "MinOrders": 1, "LastOrderAtLeastDaysAgo": 180 } } ] }
/// </code>
/// </remarks>
internal sealed class MarketingSegmentsStep : CatalogStoreRecipeStep<Segment>
{
    public const string StepKey = "MarketingSegments";

    public const string EntriesName = "Segments";

    internal readonly IStringLocalizer S;

    public MarketingSegmentsStep(ISegmentStore store, IStringLocalizer<MarketingSegmentsStep> stringLocalizer)
        : base(StepKey, EntriesName, store)
    {
        S = stringLocalizer;
    }

    protected override void Preparing(Segment entry, JsonObject token)
    {
        entry.Name = entry.Name?.Trim();
        entry.Rules ??= new SegmentRules();
        entry.Rules.Country = string.IsNullOrWhiteSpace(entry.Rules.Country) ? null : entry.Rules.Country.Trim().ToUpperInvariant();
    }

    protected override ValueTask<ValidationResultDetails> ValidateAsync(Segment entry)
    {
        var result = new ValidationResultDetails();
        var rules = entry.Rules;

        if (string.IsNullOrEmpty(entry.Name))
        {
            result.Fail(new ValidationResult(S["Name the segment, such as Lapsed buyers."]));
        }

        if (rules.MinOrders < 0 || rules.MaxOrders < 0 || rules.MinSpent < 0 || rules.MaxSpent < 0 || rules.LastOrderAtLeastDaysAgo < 0 || rules.LastOrderWithinDays < 0 || rules.FirstOrderWithinDays < 0)
        {
            result.Fail(new ValidationResult(S["Counts and days can't be negative."]));
        }

        if (rules.Country is { Length: not 2 })
        {
            result.Fail(new ValidationResult(S["Enter the country's two-letter code, such as DE, or leave it blank."]));
        }

        return ValueTask.FromResult(result);
    }
}
