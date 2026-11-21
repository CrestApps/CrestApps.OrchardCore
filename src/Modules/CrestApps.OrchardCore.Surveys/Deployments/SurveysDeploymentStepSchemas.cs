using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Describes the Surveys deployment step.
/// </summary>
internal sealed class SurveysDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => nameof(SurveysDeploymentStep);

    /// <inheritdoc />
    protected override string DisplayText => "Surveys";

    /// <inheritdoc />
    protected override string Description => "Exports surveys with their draft, versions, collectors and access settings.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether every survey is exported."));
        yield return ("SurveyNames", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The technical names of the surveys exported when not all are."));
        yield return ("IncludeSecrets", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether collector tokens and password hashes are exported; needs the Export survey responses permission."));
    }
}

/// <summary>
/// Describes the question banks deployment step.
/// </summary>
internal sealed class SurveyQuestionBanksDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => nameof(SurveyQuestionBanksDeploymentStep);

    /// <inheritdoc />
    protected override string DisplayText => "Question banks";

    /// <inheritdoc />
    protected override string Description => "Exports question banks with their questions.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether every bank is exported."));
        yield return ("BankNames", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The technical names of the banks exported when not all are."));
    }
}

/// <summary>
/// Describes the survey responses deployment step.
/// </summary>
internal sealed class SurveyResponsesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => nameof(SurveyResponsesDeploymentStep);

    /// <inheritdoc />
    protected override string DisplayText => "Survey responses";

    /// <inheritdoc />
    protected override string Description => "Exports survey responses; needs the Export survey responses permission.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the responses of every survey are exported."));
        yield return ("SurveyNames", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The technical names of the surveys when not all are."));
        yield return ("FromDate", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.Date).Description("The first day responses started on."));
        yield return ("ToDate", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.Date).Description("The last day responses started on."));
        yield return ("IncludeTest", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether test responses are exported."));
        yield return ("IncludeInProgress", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether responses in progress are exported."));
    }
}

/// <summary>
/// Describes the survey accommodations deployment step.
/// </summary>
internal sealed class SurveyOverridesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => nameof(SurveyOverridesDeploymentStep);

    /// <inheritdoc />
    protected override string DisplayText => "Survey accommodations";

    /// <inheritdoc />
    protected override string Description => "Exports the accommodations given to respondents.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the accommodations of every survey are exported."));
        yield return ("SurveyNames", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The technical names of the surveys when not all are."));
    }
}

/// <summary>
/// Describes the survey invitations deployment step.
/// </summary>
internal sealed class SurveyInvitationsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => nameof(SurveyInvitationsDeploymentStep);

    /// <inheritdoc />
    protected override string DisplayText => "Survey invitations";

    /// <inheritdoc />
    protected override string Description => "Exports personal invitations; tokens travel only as hashes.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the invitations of every survey are exported."));
        yield return ("SurveyNames", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The technical names of the surveys when not all are."));
    }
}
