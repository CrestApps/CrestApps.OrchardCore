using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Recipes;

internal abstract class LearningEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Learning";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("CourseIds", WorkflowActivitySchemaBuilders.StringArray("The content item ids of the courses whose enrollments start the workflow. Empty for every course."));
        yield return ("IncludeWorkflowChanges", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether a change a workflow task made starts the workflow too. False by default."));
    }
}

internal abstract class EnrollmentTaskSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Learning";

    protected override IEnumerable<string> Outcomes => ["Done", "NotFound", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["EnrollmentId"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("EnrollmentId", WorkflowActivitySchemaBuilders.String("The enrollment id. A plain string that supports Liquid."));
        yield return ("Reason", WorkflowActivitySchemaBuilders.String("Why. Kept on the enrollment. A plain string that supports Liquid."));
        yield return ("OutputName", WorkflowActivitySchemaBuilders.String("The workflow output name of the result. Defaults to Enrollment."));

        foreach (var property in GetTaskProperties())
        {
            yield return property;
        }
    }

    protected virtual IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
        => [];
}

internal sealed class EnrollmentRequestedEventSchema : LearningEventSchemaBase
{
    public override string Name => "EnrollmentRequestedEvent";

    protected override string DisplayText => "Enrollment Requested";

    protected override string Description => "Starts when a learner asks to enroll in a course that needs approval";
}

internal sealed class LearnerEnrolledEventSchema : LearningEventSchemaBase
{
    public override string Name => "LearnerEnrolledEvent";

    protected override string DisplayText => "Learner Enrolled";

    protected override string Description => "Starts when a learner is enrolled in a course, or a request to enroll is approved";
}

internal sealed class EnrollmentStatusChangedEventSchema : LearningEventSchemaBase
{
    public override string Name => "EnrollmentStatusChangedEvent";

    protected override string DisplayText => "Enrollment Status Changed";

    protected override string Description => "Starts when an enrollment moves to another status";
}

internal sealed class CourseStartedEventSchema : LearningEventSchemaBase
{
    public override string Name => "CourseStartedEvent";

    protected override string DisplayText => "Course Started";

    protected override string Description => "Starts when a learner opens the first item of a course";
}

internal sealed class CourseItemCompletedEventSchema : LearningEventSchemaBase
{
    public override string Name => "CourseItemCompletedEvent";

    protected override string DisplayText => "Course Item Completed";

    protected override string Description => "Starts when a learner completes an item of a course";
}

internal sealed class CourseCompletedEventSchema : LearningEventSchemaBase
{
    public override string Name => "CourseCompletedEvent";

    protected override string DisplayText => "Course Completed";

    protected override string Description => "Starts when a learner completes a course";
}

internal sealed class AccessExpiringEventSchema : LearningEventSchemaBase
{
    public override string Name => "AccessExpiringEvent";

    protected override string DisplayText => "Access Expiring";

    protected override string Description => "Starts once when the access of an enrollment ends soon";
}

internal sealed class LearnerInactiveEventSchema : LearningEventSchemaBase
{
    public override string Name => "LearnerInactiveEvent";

    protected override string DisplayText => "Learner Inactive";

    protected override string Description => "Starts once when a learner has not worked on a course for a while";
}

internal sealed class EnrollUserTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "EnrollUserTask";

    protected override string Category => "Learning";

    protected override string DisplayText => "Enroll User";

    protected override string Description => "Enrolls a user in a course, or adds a workflow grant to the open enrollment the user has";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["UserId", "CourseId"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("UserId", WorkflowActivitySchemaBuilders.String("The user id. A plain string that supports Liquid."));
        yield return ("CourseId", WorkflowActivitySchemaBuilders.String("The content item id of the course. A plain string that supports Liquid."));
        yield return ("AccessEnds", WorkflowActivitySchemaBuilders.String("Optional. When access ends, an ISO 8601 time. Supports Liquid."));
        yield return ("Due", WorkflowActivitySchemaBuilders.String("Optional. When the course is due, an ISO 8601 time. Supports Liquid."));
        yield return ("OutputName", WorkflowActivitySchemaBuilders.String("The workflow output name of the result. Defaults to Enrollment."));
    }
}

internal sealed class ApproveEnrollmentTaskSchema : EnrollmentTaskSchemaBase
{
    public override string Name => "ApproveEnrollmentTask";

    protected override string DisplayText => "Approve Enrollment";

    protected override string Description => "Approves a pending enrollment";
}

internal sealed class RejectEnrollmentTaskSchema : EnrollmentTaskSchemaBase
{
    public override string Name => "RejectEnrollmentTask";

    protected override string DisplayText => "Reject Enrollment";

    protected override string Description => "Rejects a pending enrollment with a reason";
}

internal sealed class ChangeEnrollmentStatusTaskSchema : EnrollmentTaskSchemaBase
{
    public override string Name => "ChangeEnrollmentStatusTask";

    protected override string DisplayText => "Change Enrollment Status";

    protected override string Description => "Moves an enrollment to another status, with a reason";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("Status", WorkflowActivitySchemaBuilders.String("The new status. Supports Liquid.", ["Active", "Suspended", "Completed", "Withdrawn"]));
    }
}

internal sealed class ExtendEnrollmentTaskSchema : EnrollmentTaskSchemaBase
{
    public override string Name => "ExtendEnrollmentTask";

    protected override string DisplayText => "Extend Access or Due Date";

    protected override string Description => "Changes when an enrollment's access ends or when its course is due, with a reason";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("AccessEnds", WorkflowActivitySchemaBuilders.String("Optional. The new end of access, an ISO 8601 time. Supports Liquid."));
        yield return ("Due", WorkflowActivitySchemaBuilders.String("Optional. The new due date, an ISO 8601 time. Supports Liquid."));
    }
}

internal sealed class MarkCourseItemCompleteTaskSchema : EnrollmentTaskSchemaBase
{
    public override string Name => "MarkCourseItemCompleteTask";

    protected override string DisplayText => "Mark Course Item Complete";

    protected override string Description => "Marks a course item complete for an enrollment, with a reason";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("ItemId", WorkflowActivitySchemaBuilders.String("The content item id of the course item. A plain string that supports Liquid."));
    }
}
