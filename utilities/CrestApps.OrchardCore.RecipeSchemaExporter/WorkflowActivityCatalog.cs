namespace CrestApps.OrchardCore.RecipeSchemaExporter;

/// <summary>
/// Provides a curated fallback list of the built-in OrchardCore workflow activities
/// (events and tasks) used when the OrchardCore source tree is not available for scanning.
/// </summary>
internal static class WorkflowActivityCatalog
{
    /// <summary>
    /// Gets the known OrchardCore event activity type names.
    /// </summary>
    public static readonly string[] KnownEventActivities =
    [
        "ContentCreatedEvent",
        "ContentDeletedEvent",
        "ContentDraftSavedEvent",
        "ContentPublishedEvent",
        "ContentUnpublishedEvent",
        "ContentUpdatedEvent",
        "ContentVersionedEvent",
        "DataPipelineRunCompletedEvent",
        "HttpRequestEvent",
        "HttpRequestFilterEvent",
        "SignalEvent",
        "StartedByWorkflowEvent",
        "TimerEvent",
        "UserConfirmedEvent",
        "UserCreatedEvent",
        "UserDeletedEvent",
        "UserDisabledEvent",
        "UserEnabledEvent",
        "UserLoggedInEvent",
        "UserLoggedOutEvent",
        "UserTaskEvent",
        "UserUpdatedEvent",
        "WorkflowFaultEvent",
    ];

    /// <summary>
    /// Gets the known OrchardCore task activity type names.
    /// </summary>
    public static readonly string[] KnownTaskActivities =
    [
        "AddModelValidationErrorTask",
        "AssignUserRoleTask",
        "BindModelStateTask",
        "CommitTransactionTask",
        "CorrelateTask",
        "CreateContentTask",
        "CreateTenantTask",
        "DeleteContentTask",
        "DisableTenantTask",
        "EmailTask",
        "EnableTenantTask",
        "ExecuteWorkflowTask",
        "ForEachTask",
        "ForkTask",
        "ForLoopTask",
        "GetUsersByRoleTask",
        "HttpRedirectTask",
        "HttpRedirectToFormLocationTask",
        "HttpRequestTask",
        "HttpResponseTask",
        "IfElseTask",
        "JoinTask",
        "LiquidTask",
        "LogTask",
        "MetaConversionsApiEventTask",
        "NotifyContentOwnerTask",
        "NotifyTask",
        "NotifyUserTask",
        "PublishContentTask",
        "RegisterUserTask",
        "RetrieveContentTask",
        "RunDataPipelineTask",
        "ScriptTask",
        "SetOutputTask",
        "SetPropertyTask",
        "SetVariableTask",
        "SetupTenantTask",
        "SmsTask",
        "UnassignUserRoleTask",
        "UnpublishContentTask",
        "UpdateContentTask",
        "UpdateTwitterStatusTask",
        "ValidateAntiforgeryTokenTask",
        "ValidateFormFieldTask",
        "ValidateFormTask",
        "ValidateReCaptchaTask",
        "ValidateUserTask",
        "WhileLoopTask",
    ];
}
