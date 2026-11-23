using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Consent.Core.Models;
using CrestApps.OrchardCore.Consent.Core.Services;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Consent.Recipes;

/// <summary>
/// Adds the opt-outs for one purpose recorded against an address from a <c>ConsentAddressOptOuts</c> recipe step, so a
/// person's "stop all marketing" moves with the site. An opt-out is matched by its channel, address and purpose, so
/// running a recipe twice adds nothing; one already recorded takes the imported source and time. The records are
/// written to the store as they are, without asking the other consent sources to record them again.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "ConsentAddressOptOuts", "OptOuts": [ { "Channel": "Email", "Address": "sam@example.com", "Purpose": "Marketing", "Source": "unsubscribe-link", "OptedOutUtc": "2026-11-22T10:00:00Z" } ] }
/// </code>
/// </remarks>
internal sealed class ConsentAddressOptOutsStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The name of the recipe step.
    /// </summary>
    public const string StepKey = "ConsentAddressOptOuts";

    /// <summary>
    /// The property that holds the opt-outs.
    /// </summary>
    public const string EntriesName = "OptOuts";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConsentAddressOptOutStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentAddressOptOutsStep"/> class.
    /// </summary>
    /// <param name="store">The store of address opt-outs.</param>
    /// <param name="clock">The clock, for an opt-out imported without its time.</param>
    public ConsentAddressOptOutsStep(
        IConsentAddressOptOutStore store,
        IClock clock)
        : base(StepKey)
    {
        _store = store;
        _clock = clock;
    }

    /// <inheritdoc/>
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var node in (context.Step[EntriesName] as JsonArray ?? []).OfType<JsonObject>())
        {
            OptOutEntry entry;

            try
            {
                entry = node.Deserialize<OptOutEntry>(_jsonOptions);
            }
            catch (JsonException)
            {
                context.Errors.Add($"{StepKey}: an opt-out could not be read.");

                continue;
            }

            if (entry is null ||
                string.IsNullOrWhiteSpace(entry.Channel) ||
                string.IsNullOrWhiteSpace(entry.Address) ||
                string.IsNullOrWhiteSpace(entry.Purpose))
            {
                context.Errors.Add($"{StepKey}: an opt-out needs a Channel, an Address and a Purpose.");

                continue;
            }

            await _store.SaveAsync(new ConsentAddressOptOut
            {
                Channel = entry.Channel.Trim(),
                Address = entry.Address,
                Purpose = entry.Purpose,
                Source = string.IsNullOrWhiteSpace(entry.Source) ? null : entry.Source.Trim(),
                OptedOutUtc = entry.OptedOutUtc is { } optedOutUtc && optedOutUtc != default
                    ? AsUtc(optedOutUtc)
                    : _clock.UtcNow,
            });
        }
    }

    // A time written with an offset is read as local time; one written without a zone is taken as UTC, as exported.
    private static DateTime AsUtc(DateTime value)
        => value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    // Only the members the step reads, so other members of an entry (an id, a note) can't stop it from being read.
    private sealed class OptOutEntry
    {
        public string Channel { get; set; }

        public string Address { get; set; }

        public string Purpose { get; set; }

        public string Source { get; set; }

        public DateTime? OptedOutUtc { get; set; }
    }
}
