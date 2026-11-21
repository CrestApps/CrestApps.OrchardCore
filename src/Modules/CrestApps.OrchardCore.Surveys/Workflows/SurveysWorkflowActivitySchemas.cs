using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// The schema base of the Surveys response events.
/// </summary>
internal abstract class SurveyResponseEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    protected override string Category => "Surveys";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("SurveyIds", WorkflowActivitySchemaBuilders.StringArray("The ids of the surveys whose responses start the workflow. Empty for every survey."));
        yield return ("Kinds", WorkflowActivitySchemaBuilders.StringArray("The survey kinds, such as Survey, Quiz or Poll. Empty for every kind."));
        yield return ("CollectorIds", WorkflowActivitySchemaBuilders.StringArray("The ids of the collectors. Empty for every collector."));
        yield return ("ContextKinds", WorkflowActivitySchemaBuilders.StringArray("The kinds of record responses belong to, such as LearningEnrollment. Empty for every response."));
        yield return ("IncludeWorkflowChanges", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether a change a workflow task made starts the workflow too. False by default."));

        foreach (var property in GetExtraProperties())
        {
            yield return property;
        }
    }

    /// <summary>
    /// Gets the properties of a derived event.
    /// </summary>
    /// <returns>The properties.</returns>
    protected virtual IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetExtraProperties()
        => [];
}

/// <summary>
/// The schema base of the Surveys survey events.
/// </summary>
internal abstract class SurveyEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    protected override string Category => "Surveys";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("SurveyIds", WorkflowActivitySchemaBuilders.StringArray("The ids of the surveys that start the workflow. Empty for every survey."));
        yield return ("Kinds", WorkflowActivitySchemaBuilders.StringArray("The survey kinds. Empty for every kind."));

        foreach (var property in GetExtraProperties())
        {
            yield return property;
        }
    }

    /// <summary>
    /// Gets the properties of a derived event.
    /// </summary>
    /// <returns>The properties.</returns>
    protected virtual IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetExtraProperties()
        => [];
}

/// <summary>
/// The schema base of the Surveys tasks.
/// </summary>
internal abstract class SurveyTaskSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    protected override string Category => "Surveys";

    /// <summary>
    /// Gets the settings the task uses.
    /// </summary>
    protected abstract IEnumerable<string> Settings { get; }

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => Settings.Select(name => (name, WorkflowActivitySchemaBuilders.String(Describe(name))));

    private static string Describe(string name)
        => name switch
        {
            "SurveyId" => "The id of the survey, a Liquid expression such as Workflow.Input.SurveyId between double braces. A plain string that supports Liquid.",
            "ResponseId" => "The id of the response, a Liquid expression such as Workflow.Input.ResponseId between double braces. A plain string that supports Liquid.",
            "UserId" => "The id of the user. Supports Liquid. A plain string that supports Liquid.",
            "Email" => "The e-mail address. Supports Liquid. A plain string that supports Liquid.",
            "DisplayName" => "The name of the person invited. Supports Liquid. A plain string that supports Liquid.",
            "ContextKind" => "The kind of record the response will belong to, such as Ticket. Supports Liquid. A plain string that supports Liquid.",
            "ContextId" => "The id of the record the response will belong to. Supports Liquid. A plain string that supports Liquid.",
            "ExpiresInDays" => "In how many days the link expires. Leave it empty for a link that does not expire. A plain string that supports Liquid.",
            "ExtraMinutes" => "The minutes added to the time limit. A plain string that supports Liquid.",
            "ExtraAttempts" => "The attempts added to the limit. A plain string that supports Liquid.",
            "Reason" => "Why. Required; kept with the change. A plain string that supports Liquid.",
            "PropertyName" => "The name of the property to write. A plain string that supports Liquid.",
            "PropertyValue" => "The value to write. Supports Liquid. A plain string that supports Liquid.",
            "OutputName" => "The workflow output that holds the result. A plain string that supports Liquid.",
            _ => name,
        };
}

/// <summary>
/// Describes the Survey Response Started event.
/// </summary>
internal sealed class SurveyResponseStartedEventSchema : SurveyResponseEventSchemaBase
{
    /// <inheritdoc />
    public override string Name => "SurveyResponseStartedEvent";

    /// <inheritdoc />
    protected override string DisplayText => "Survey Response Started";

    /// <inheritdoc />
    protected override string Description => "Starts when a respondent starts a survey";
}

/// <summary>
/// Describes the Survey Page Completed event.
/// </summary>
internal sealed class SurveyPageCompletedEventSchema : SurveyResponseEventSchemaBase
{
    /// <inheritdoc />
    public override string Name => "SurveyPageCompletedEvent";

    /// <inheritdoc />
    protected override string DisplayText => "Survey Page Completed";

    /// <inheritdoc />
    protected override string Description => "Starts when a respondent completes a page of a survey";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetExtraProperties()
    {
        yield return ("PageNames", WorkflowActivitySchemaBuilders.StringArray("The names of the pages that start the workflow. Empty for every page."));
    }
}

/// <summary>
/// Describes the Survey Response Completed event.
/// </summary>
internal sealed class SurveyResponseCompletedEventSchema : SurveyResponseEventSchemaBase
{
    /// <inheritdoc />
    public override string Name => "SurveyResponseCompletedEvent";

    /// <inheritdoc />
    protected override string DisplayText => "Survey Response Completed";

    /// <inheritdoc />
    protected override string Description => "Starts when a survey response is completed";
}

/// <summary>
/// Describes the Survey Response Screened Out event.
/// </summary>
internal sealed class SurveyResponseScreenedOutEventSchema : SurveyResponseEventSchemaBase
{
    /// <inheritdoc />
    public override string Name => "SurveyResponseScreenedOutEvent";

    /// <inheritdoc />
    protected override string DisplayText => "Survey Response Screened Out";

    /// <inheritdoc />
    protected override string Description => "Starts when a respondent is screened out";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetExtraProperties()
    {
        yield return ("EndingNames", WorkflowActivitySchemaBuilders.StringArray("The names of the endings that start the workflow. Empty for every ending."));
    }
}

/// <summary>
/// Describes the Survey Response Abandoned event.
/// </summary>
internal sealed class SurveyResponseAbandonedEventSchema : SurveyResponseEventSchemaBase
{
    /// <inheritdoc />
    public override string Name => "SurveyResponseAbandonedEvent";

    /// <inheritdoc />
    protected override string DisplayText => "Survey Response Abandoned";

    /// <inheritdoc />
    protected override string Description => "Starts when an unfinished survey response is abandoned";
}

/// <summary>
/// Describes the Survey Response Needs Grading event.
/// </summary>
internal sealed class SurveyResponseNeedsGradingEventSchema : SurveyResponseEventSchemaBase
{
    /// <inheritdoc />
    public override string Name => "SurveyResponseNeedsGradingEvent";

    /// <inheritdoc />
    protected override string DisplayText => "Survey Response Needs Grading";

    /// <inheritdoc />
    protected override string Description => "Starts when a completed response has answers a person must grade";
}

/// <summary>
/// Describes the Survey Response Graded event.
/// </summary>
internal sealed class SurveyResponseGradedEventSchema : SurveyResponseEventSchemaBase
{
    /// <inheritdoc />
    public override string Name => "SurveyResponseGradedEvent";

    /// <inheritdoc />
    protected override string DisplayText => "Survey Response Graded";

    /// <inheritdoc />
    protected override string Description => "Starts when every answer of a response has a score";
}

/// <summary>
/// Describes the Survey Response Passed event.
/// </summary>
internal sealed class SurveyResponsePassedEventSchema : SurveyResponseEventSchemaBase
{
    /// <inheritdoc />
    public override string Name => "SurveyResponsePassedEvent";

    /// <inheritdoc />
    protected override string DisplayText => "Survey Response Passed";

    /// <inheritdoc />
    protected override string Description => "Starts when a graded response reaches the pass mark";
}

/// <summary>
/// Describes the Survey Response Failed event.
/// </summary>
internal sealed class SurveyResponseFailedEventSchema : SurveyResponseEventSchemaBase
{
    /// <inheritdoc />
    public override string Name => "SurveyResponseFailedEvent";

    /// <inheritdoc />
    protected override string DisplayText => "Survey Response Failed";

    /// <inheritdoc />
    protected override string Description => "Starts when a graded response stays under the pass mark";
}

/// <summary>
/// Describes the Survey Response Score Changed event.
/// </summary>
internal sealed class SurveyResponseScoreChangedEventSchema : SurveyResponseEventSchemaBase
{
    /// <inheritdoc />
    public override string Name => "SurveyResponseScoreChangedEvent";

    /// <inheritdoc />
    protected override string DisplayText => "Survey Response Score Changed";

    /// <inheritdoc />
    protected override string Description => "Starts when the score of a graded response is regraded or changed";
}

/// <summary>
/// Describes the Survey Response Matches Condition event.
/// </summary>
internal sealed class SurveyResponseMatchesConditionEventSchema : SurveyResponseEventSchemaBase
{
    /// <inheritdoc />
    public override string Name => "SurveyResponseMatchesConditionEvent";

    /// <inheritdoc />
    protected override string DisplayText => "Survey Response Matches Condition";

    /// <inheritdoc />
    protected override string Description => "Starts when a completed response matches a rule over its answers";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetExtraProperties()
    {
        yield return ("Rule", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Description("The rule over the answers: Match, Conditions (Source, Operator, Values) and Groups. Sources are question names or ids, or question.row."));
    }
}

/// <summary>
/// Describes the Survey Published event.
/// </summary>
internal sealed class SurveyPublishedEventSchema : SurveyEventSchemaBase
{
    /// <inheritdoc />
    public override string Name => "SurveyPublishedEvent";

    /// <inheritdoc />
    protected override string DisplayText => "Survey Published";

    /// <inheritdoc />
    protected override string Description => "Starts when a survey version is published";
}

/// <summary>
/// Describes the Survey Closed event.
/// </summary>
internal sealed class SurveyClosedEventSchema : SurveyEventSchemaBase
{
    /// <inheritdoc />
    public override string Name => "SurveyClosedEvent";

    /// <inheritdoc />
    protected override string DisplayText => "Survey Closed";

    /// <inheritdoc />
    protected override string Description => "Starts when a survey closes";
}

/// <summary>
/// Describes the Survey Quota Reached event.
/// </summary>
internal sealed class SurveyQuotaReachedEventSchema : SurveyEventSchemaBase
{
    /// <inheritdoc />
    public override string Name => "SurveyQuotaReachedEvent";

    /// <inheritdoc />
    protected override string DisplayText => "Survey Quota Reached";

    /// <inheritdoc />
    protected override string Description => "Starts when a quota of a survey reaches its limit";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetExtraProperties()
    {
        yield return ("QuotaNames", WorkflowActivitySchemaBuilders.StringArray("The names of the quotas that start the workflow. Empty for every quota."));
    }
}

/// <summary>
/// Describes the Create Survey Link task.
/// </summary>
internal sealed class CreateSurveyLinkTaskSchema : SurveyTaskSchemaBase
{
    /// <inheritdoc />
    public override string Name => "CreateSurveyLinkTask";

    /// <inheritdoc />
    protected override string DisplayText => "Create Survey Link";

    /// <inheritdoc />
    protected override string Description => "Makes a personal link to respond, or the public link, and writes it to the output";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> Settings => ["SurveyId", "UserId", "Email", "ContextKind", "ContextId", "ExpiresInDays", "OutputName"];
}

/// <summary>
/// Describes the Send Survey Invitation task.
/// </summary>
internal sealed class SendSurveyInvitationTaskSchema : SurveyTaskSchemaBase
{
    /// <inheritdoc />
    public override string Name => "SendSurveyInvitationTask";

    /// <inheritdoc />
    protected override string DisplayText => "Send Survey Invitation";

    /// <inheritdoc />
    protected override string Description => "Creates and sends a personal survey invitation";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> Settings => ["SurveyId", "UserId", "Email", "DisplayName", "ContextKind", "ContextId", "OutputName"];
}

/// <summary>
/// Describes the Close Survey task.
/// </summary>
internal sealed class CloseSurveyTaskSchema : SurveyTaskSchemaBase
{
    /// <inheritdoc />
    public override string Name => "CloseSurveyTask";

    /// <inheritdoc />
    protected override string DisplayText => "Close Survey";

    /// <inheritdoc />
    protected override string Description => "Closes a survey so it collects no more responses";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> Settings => ["SurveyId", "OutputName"];
}

/// <summary>
/// Describes the Open Survey task.
/// </summary>
internal sealed class OpenSurveyTaskSchema : SurveyTaskSchemaBase
{
    /// <inheritdoc />
    public override string Name => "OpenSurveyTask";

    /// <inheritdoc />
    protected override string DisplayText => "Open Survey";

    /// <inheritdoc />
    protected override string Description => "Opens a published survey again";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> Settings => ["SurveyId", "OutputName"];
}

/// <summary>
/// Describes the Void Survey Response task.
/// </summary>
internal sealed class VoidSurveyResponseTaskSchema : SurveyTaskSchemaBase
{
    /// <inheritdoc />
    public override string Name => "VoidSurveyResponseTask";

    /// <inheritdoc />
    protected override string DisplayText => "Void Survey Response";

    /// <inheritdoc />
    protected override string Description => "Voids a survey response with a reason";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "NotFound", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> Settings => ["ResponseId", "Reason", "OutputName"];
}

/// <summary>
/// Describes the Regrade Survey Response task.
/// </summary>
internal sealed class RegradeSurveyResponseTaskSchema : SurveyTaskSchemaBase
{
    /// <inheritdoc />
    public override string Name => "RegradeSurveyResponseTask";

    /// <inheritdoc />
    protected override string DisplayText => "Regrade Survey Response";

    /// <inheritdoc />
    protected override string Description => "Scores a survey response again with its version's key";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "NotFound", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> Settings => ["ResponseId", "Reason", "OutputName"];
}

/// <summary>
/// Describes the Grant Survey Accommodation task.
/// </summary>
internal sealed class GrantSurveyOverrideTaskSchema : SurveyTaskSchemaBase
{
    /// <inheritdoc />
    public override string Name => "GrantSurveyOverrideTask";

    /// <inheritdoc />
    protected override string DisplayText => "Grant Survey Accommodation";

    /// <inheritdoc />
    protected override string Description => "Gives a respondent extra minutes or extra attempts, with a reason";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> Settings => ["SurveyId", "UserId", "ExtraMinutes", "ExtraAttempts", "Reason", "OutputName"];
}

/// <summary>
/// Describes the Set Survey Response Property task.
/// </summary>
internal sealed class SetSurveyResponsePropertyTaskSchema : SurveyTaskSchemaBase
{
    /// <inheritdoc />
    public override string Name => "SetSurveyResponsePropertyTask";

    /// <inheritdoc />
    protected override string DisplayText => "Set Survey Response Property";

    /// <inheritdoc />
    protected override string Description => "Writes a value into the properties of a survey response";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "NotFound", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> Settings => ["ResponseId", "PropertyName", "PropertyValue", "OutputName"];
}
