using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Certifications.Workflows;

internal abstract class CertificationPathEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Learning";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("CertificationIds", WorkflowActivitySchemaBuilders.StringArray("The identifiers of the certifications whose paths start the workflow. Empty for every certification."));
    }
}

internal sealed class CertificationRenewalOpenedEventSchema : CertificationPathEventSchemaBase
{
    public override string Name => "CertificationRenewalOpenedEvent";

    protected override string DisplayText => "Certification Renewal Opened";

    protected override string Description => "Starts when the renewal of a certificate earned through learning opens";
}

internal sealed class CertificationLapsedEventSchema : CertificationPathEventSchemaBase
{
    public override string Name => "CertificationLapsedEvent";

    protected override string DisplayText => "Certification Lapsed";

    protected override string Description => "Starts when a certificate earned through learning expires without a renewal";
}

internal sealed class StartCertificationPathTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "StartCertificationPathTask";

    protected override string Category => "Learning";

    protected override string DisplayText => "Start Certification Path";

    protected override string Description => "Starts a person on the certification path of a certification";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["Certification", "UserId"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Certification", WorkflowActivitySchemaBuilders.String("The certification, by name or identifier. A plain string that supports Liquid."));
        yield return ("UserId", WorkflowActivitySchemaBuilders.String("The user id of the person. A plain string that supports Liquid."));
    }
}
