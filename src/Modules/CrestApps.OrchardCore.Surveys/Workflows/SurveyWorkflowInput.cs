namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// The names of the input values Surveys workflow events receive.
/// </summary>
internal static class SurveyWorkflowInput
{
    /// <summary>
    /// The actor of a change a workflow task made.
    /// </summary>
    public const string WorkflowActor = "Workflow";

    /// <summary>
    /// The survey id.
    /// </summary>
    public const string SurveyId = "SurveyId";

    /// <summary>
    /// The survey's technical name.
    /// </summary>
    public const string SurveyName = "SurveyName";

    /// <summary>
    /// The survey kind.
    /// </summary>
    public const string Kind = "Kind";

    /// <summary>
    /// The collector id.
    /// </summary>
    public const string CollectorId = "CollectorId";

    /// <summary>
    /// The kind of the record the response belongs to.
    /// </summary>
    public const string ContextKind = "ContextKind";

    /// <summary>
    /// The id of the user or the system that caused the event.
    /// </summary>
    public const string Actor = "Actor";

    /// <summary>
    /// Whether a workflow task caused the event.
    /// </summary>
    public const string CausedByWorkflow = "CausedByWorkflow";

    /// <summary>
    /// The name of the page completed.
    /// </summary>
    public const string PageName = "PageName";

    /// <summary>
    /// The name of the ending used.
    /// </summary>
    public const string EndingName = "EndingName";

    /// <summary>
    /// The name of the quota reached.
    /// </summary>
    public const string QuotaName = "QuotaName";

    /// <summary>
    /// The values conditions compare, by question id, question name and question.row.
    /// </summary>
    public const string ConditionValues = "ConditionValues";
}
