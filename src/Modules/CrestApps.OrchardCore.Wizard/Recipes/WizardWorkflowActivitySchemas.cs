using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using CrestApps.OrchardCore.Wizard.Workflows.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Wizard.Recipes;

/// <summary>
/// Describes the members the wizard workflow events share.
/// </summary>
internal abstract class WizardEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Wizard";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("WizardType", WorkflowActivitySchemaBuilders.String("The wizard type the event is meant for. It is stored with the event but does not restrict which wizards start the workflow: every wizard raises the event, with WizardType, SessionId, DefinitionId, CurrentStep and Status in the workflow input, so check input('WizardType') in the workflow when it matters."));
    }
}

/// <summary>
/// Describes the Wizard Started workflow event.
/// </summary>
internal sealed class WizardStartedEventSchema : WizardEventSchemaBase
{
    public override string Name => WizardStartedEvent.EventName;

    protected override string DisplayText => "Wizard Started";

    protected override string Description => "Starts when a wizard session begins";
}

/// <summary>
/// Describes the Wizard Step Displayed workflow event.
/// </summary>
internal sealed class WizardStepDisplayedEventSchema : WizardEventSchemaBase
{
    public override string Name => WizardStepDisplayedEvent.EventName;

    protected override string DisplayText => "Wizard Step Displayed";

    protected override string Description => "Starts when a wizard session loads a step";
}

/// <summary>
/// Describes the Wizard Completed workflow event.
/// </summary>
internal sealed class WizardCompletedEventSchema : WizardEventSchemaBase
{
    public override string Name => WizardCompletedEvent.EventName;

    protected override string DisplayText => "Wizard Completed";

    protected override string Description => "Starts when a wizard session completes";
}

/// <summary>
/// Describes the Wizard Failed workflow event.
/// </summary>
internal sealed class WizardFailedEventSchema : WizardEventSchemaBase
{
    public override string Name => WizardFailedEvent.EventName;

    protected override string DisplayText => "Wizard Failed";

    protected override string Description => "Starts when a wizard session fails";
}
