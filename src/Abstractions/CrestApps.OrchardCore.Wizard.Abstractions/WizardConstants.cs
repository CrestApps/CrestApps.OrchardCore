namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// Well-known identifiers used throughout the wizard framework.
/// </summary>
public static class WizardConstants
{
    /// <summary>
    /// The prefix used for wizard flow step keys that collect a content item, so a content-driven step can
    /// be recognized without inspecting its data bag.
    /// </summary>
    public const string ContentStepPrefix = "Content-";

    /// <summary>
    /// The step data key that stores the content type collected by a content-driven step.
    /// </summary>
    public const string ContentTypeDataKey = "ContentType";

    /// <summary>
    /// The step data key that stores the identifier of the authored step-definition content item a
    /// content-driven step was projected from.
    /// </summary>
    public const string DefinitionContentItemIdDataKey = "DefinitionContentItemId";

    /// <summary>
    /// The step data key that stores the authored step-definition content JSON used to seed a new per-session
    /// response content item with the authored default field values.
    /// </summary>
    public const string StepTemplateDataKey = "Template";

    /// <summary>
    /// The feature identifiers exposed by the wizard module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The core wizard feature that provides the reusable, multi-step wizard framework.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Wizard";
    }

    /// <summary>
    /// Route names exposed by the wizard module.
    /// </summary>
    public static class RouteNames
    {
        /// <summary>
        /// The route that starts or resumes a wizard for a given wizard type.
        /// </summary>
        public const string Start = "WizardStart";

        /// <summary>
        /// The route that displays a specific saved step of a wizard session.
        /// </summary>
        public const string Step = "WizardStep";

        /// <summary>
        /// The route that displays the confirmation of a completed wizard session.
        /// </summary>
        public const string Confirmation = "WizardConfirmation";
    }

    /// <summary>
    /// Rate-limit group names attached to the public, anonymous-facing wizard routes. They are consumed by
    /// the optional Orchard Core <c>OrchardCore.RateLimits</c> module.
    /// </summary>
    public static class RateLimitGroups
    {
        /// <summary>
        /// The public wizard requests (the wizard form and each step submission).
        /// </summary>
        public const string Wizard = "wizard";
    }
}
