using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Checks.Workflows;

internal sealed class MarkStaleChecksTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => nameof(MarkStaleChecksTask);

    protected override string DisplayText => "Mark Stale Checks";

    protected override string Description => "Marks stale every printed check whose check date is more than AgeInDays days before today, in the site's time zone, such as from a Timer Event once a day. Writes how many checks became stale to the workflow output StaleChecks.";

    protected override string Category => "Checks";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("AgeInDays", WorkflowActivitySchemaBuilders.Integer($"How many days after its check date a printed check becomes stale, 1 to 3650. Defaults to {MarkStaleChecksTask.DefaultAgeInDays}."));
    }
}
