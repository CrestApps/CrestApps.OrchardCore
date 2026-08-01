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
        "HttpRequestEvent",
        "HttpRequestFilterEvent",
        "SignalEvent",
        "TimerEvent",
        "UserConfirmedEvent",
        "UserCreatedEvent",
        "UserDeletedEvent",
        "UserDisabledEvent",
        "UserEnabledEvent",
        "UserLoggedInEvent",
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
        "NotifyContentOwnerTask",
        "NotifyTask",
        "NotifyUserTask",
        "PublishContentTask",
        "RegisterUserTask",
        "RetrieveContentTask",
        "ScriptTask",
        "SetOutputTask",
        "SetPropertyTask",
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
        "WriteLineTask",
    ];
}
