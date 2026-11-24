using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Certifications.Workflows;

internal sealed class EducationCreditsAwardedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "EducationCreditsAwardedEvent";

    protected override string Category => "Learning";

    protected override string DisplayText => "Education Credits Awarded";

    protected override string Description => "Starts when education credits are awarded to a person";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

internal sealed class ExternalLearningSubmittedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "ExternalLearningSubmittedEvent";

    protected override string Category => "Learning";

    protected override string DisplayText => "External Learning Submitted";

    protected override string Description => "Starts when a person records external learning for education credits";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

internal sealed class AwardEducationCreditsTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "AwardEducationCreditsTask";

    protected override string Category => "Learning";

    protected override string DisplayText => "Award Education Credits";

    protected override string Description => "Awards education credits to a person, once per source id and person";

    protected override IEnumerable<string> Outcomes => ["Done", "AlreadyAwarded", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["CreditType", "UserId", "Amount", "SourceId"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("CreditType", WorkflowActivitySchemaBuilders.String("The credit type, by name or identifier. A plain string that supports Liquid."));
        yield return ("UserId", WorkflowActivitySchemaBuilders.String("The user id of the person. A plain string that supports Liquid."));
        yield return ("Amount", WorkflowActivitySchemaBuilders.String("The number of credits, such as 1.5. A plain string that supports Liquid."));
        yield return ("SourceId", WorkflowActivitySchemaBuilders.String("What the credits are for; the same source id awards each person once. A plain string that supports Liquid."));
    }
}
