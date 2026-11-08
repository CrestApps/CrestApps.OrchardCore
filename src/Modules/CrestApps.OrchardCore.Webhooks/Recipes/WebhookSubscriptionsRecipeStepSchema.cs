using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Webhooks.Recipes;

/// <summary>
/// Describes the <c>WebhookSubscriptions</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class WebhookSubscriptionsRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    private static readonly string[] _knownEvents =
    [
        "*",
        "webhook.ping",
        "customer.created",
        "order.created",
        "order.paid",
        "order.canceled",
        "order.refunded",
        "shipment.shipped",
        "shipment.delivered",
        "stock.changed",
        "stock.low",
        "stock.sold-out",
    ];

    public override string Name => WebhookSubscriptionsStep.Name;

    protected override string EntriesName => WebhookSubscriptionsStep.EntriesName;

    protected override string Description => "Creates or updates the outgoing webhook subscriptions of the Webhooks feature. A subscription is matched by its ItemId; one without a matching ItemId is created. Every subscription needs a Name, a full https:// Url outside the site's own network, and at least one event.";

    protected override string EntryDescription => "An outgoing webhook subscription. Members that are omitted keep their stored values on an update.";

    protected override IEnumerable<string> RequiredEntryProperties => ["Name", "Url", "Events"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
    {
        yield return ("Name", RecipeStepSchemaBuilders.String().Description("The name of the subscription shown in the admin."));
        yield return ("Url", RecipeStepSchemaBuilders.String().Description("The full https:// address the events are posted to. Addresses on the site's own network are refused, except in development."));
        yield return ("Events", RecipeStepSchemaBuilders.Array(RecipeStepSchemaBuilders.SuggestedString(_knownEvents), 1).Description("The names of the events posted to the address, or '*' for every event. Modules register their own events, so the suggested names are not a closed list."));
        yield return ("IsEnabled", RecipeStepSchemaBuilders.Boolean().Description("Whether events are posted to the subscription."));
        yield return ("Secret", RecipeStepSchemaBuilders.String().Description("Optional signing secret in clear text, encrypted when stored. A deployment plan never exports it. When omitted on a new subscription a random secret is generated; when omitted on an update the stored one is kept."));
    }
}
