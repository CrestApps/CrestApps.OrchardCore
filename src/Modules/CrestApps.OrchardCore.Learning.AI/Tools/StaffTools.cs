using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Learning.AI.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.AI.Tools;

/// <summary>
/// Lists the learners whose course is overdue, within the caller's scope.
/// </summary>
public sealed class OverdueLearnersTool : LearningAIToolBase
{
    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "take": { "type": "integer", "description": "Optional. The most learners to list, 1 to 50. The default is 20." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => LearningAIConstants.Tools.OverdueLearners;

    /// <inheritdoc />
    public override string Description => "Lists the learners whose course is past its due date and not completed, soonest due first, with the course, the due date and the progress. Only learners the signed-in person may see are listed: everyone with View learner records, the learners of the courses the person teaches, or the people the person manages. It never lists answers or grades.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override Permission Permission => null;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
        => StaffList.Write(await services.GetRequiredService<LearnerRecordsLister>().ListAsync(user, LearnerRecordsListKind.Overdue, 0, ReadInt(arguments, "take") ?? 20, cancellationToken));
}

/// <summary>
/// Lists the learners whose access to a course ends soon, within the caller's scope.
/// </summary>
public sealed class ExpiringLearnersTool : LearningAIToolBase
{
    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "days": { "type": "integer", "description": "Optional. How many days ahead to look, 1 to 90. The default is 14." },
            "take": { "type": "integer", "description": "Optional. The most learners to list, 1 to 50. The default is 20." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => LearningAIConstants.Tools.ExpiringLearners;

    /// <inheritdoc />
    public override string Description => "Lists the learners whose access to a course they are still working on ends within the coming days, soonest first, with the course, when access ends and the progress. Only learners the signed-in person may see are listed: everyone with View learner records, the learners of the courses the person teaches, or the people the person manages.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override Permission Permission => null;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
        => StaffList.Write(await services.GetRequiredService<LearnerRecordsLister>().ListAsync(user, LearnerRecordsListKind.ExpiringSoon, ReadInt(arguments, "days") ?? 14, ReadInt(arguments, "take") ?? 20, cancellationToken));
}

internal static class StaffList
{
    public static object Write(LearnerRecordsList list)
    {
        if (!list.Allowed)
        {
            return "The user isn't allowed to see other learners' records: it needs View learner records, a course the user teaches, or people the user manages.";
        }

        if (list.Rows.Count == 0)
        {
            return "No learner within the user's scope is on this list.";
        }

        return JsonSerializer.Serialize(new
        {
            timeZone = list.TimeZone,
            mayHaveMore = list.MayHaveMore,
            learners = list.Rows,
        }, JsonSerializerOptions.Web);
    }
}
