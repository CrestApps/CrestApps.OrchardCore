using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>TimerEvent</c> workflow event.
/// </summary>
public sealed class TimerEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "TimerEvent";

    /// <inheritdoc />
    protected override string Category => "Background";

    /// <inheritdoc />
    protected override string DisplayText => "Timer Event";

    /// <inheritdoc />
    protected override string Description => "Triggers a workflow on a recurring schedule defined by a CRON expression";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("CronExpression", WorkflowActivitySchemaBuilders.String("The CRON expression defining the recurring schedule. Defaults to '*/5 * * * *'."));
        yield return ("UseLocalTime", WorkflowActivitySchemaBuilders.Boolean("When true, evaluates the CRON expression against the site's configured time zone instead of UTC. Defaults to false."));
        yield return ("StartedUtc", new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description("Runtime state. The UTC timestamp of when the timer was last armed; used to compute the next scheduled occurrence. Normally omitted from recipes."));
    }
}
