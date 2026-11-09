using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Skills.Recipes.Workflows;

/// <summary>
/// Describes the filter every skills workflow event shares.
/// </summary>
internal abstract class SkillsEventActivitySchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Skills";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("CertificationIds", WorkflowActivitySchemaBuilders.StringArray("The identifiers of the certifications whose certificates start the workflow. Empty for every certification."));
    }
}

internal sealed class CertificationExpiringEventSchema : SkillsEventActivitySchemaBase
{
    public override string Name => "CertificationExpiringEvent";

    protected override string DisplayText => "Certificate Expiring";

    protected override string Description => "Starts on each of the certification's reminder days before a certificate expires";
}

internal sealed class CertificationExpiredEventSchema : SkillsEventActivitySchemaBase
{
    public override string Name => "CertificationExpiredEvent";

    protected override string DisplayText => "Certificate Expired";

    protected override string Description => "Starts when a certificate expires";
}

internal sealed class SkillsChangedEventSchema : SkillsEventActivitySchemaBase
{
    public override string Name => "SkillsChangedEvent";

    protected override string DisplayText => "Skills Changed";

    protected override string Description => "Starts when the skills a person holds change";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("CertificationIds", WorkflowActivitySchemaBuilders.StringArray("Leave empty. A skill change carries no certification, so any value here keeps the event from starting."));
    }
}

internal sealed class AssignSkillTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "AssignSkillTask";

    protected override string Category => "Skills";

    protected override string DisplayText => "Assign Skill";

    protected override string Description => "Gives a person a skill at a proficiency, changes it, or removes it with proficiency 0";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["SubjectId", "Skill"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("SubjectKind", WorkflowActivitySchemaBuilders.String("Who holds the skill: User (the default) or Worker for a workforce worker. A plain string that supports Liquid.", ["User", "Worker"]));
        yield return ("SubjectId", WorkflowActivitySchemaBuilders.String("The user id or worker id of the person. A plain string that supports Liquid."));
        yield return ("Skill", WorkflowActivitySchemaBuilders.String("The skill, by name or identifier. A plain string that supports Liquid."));
        yield return ("Proficiency", WorkflowActivitySchemaBuilders.String("The proficiency, from 1 (trainee) to 5 (expert); 0 removes the skill. Defaults to 3. A plain string that supports Liquid."));
    }
}
