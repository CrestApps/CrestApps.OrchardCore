using System.Globalization;
using CrestApps.OrchardCore.Learning.Lti.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Lti.Workflows;

/// <summary>
/// Starts the External Score Received workflows once a tool's score is stored, after the request, with the score's id
/// as the correlation id.
/// </summary>
internal sealed class LtiScoreWorkflowObserver : ILtiScoreObserver
{
    public Task ScoreReceivedAsync(LtiScoreReceivedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var input = Input(context);
        var correlationId = context.Score.ItemId;

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(nameof(ExternalScoreReceivedEvent), input, correlationId));

        return Task.CompletedTask;
    }

    internal static Dictionary<string, object> Input(LtiScoreReceivedContext context)
        => new()
        {
            ["ToolId"] = context.Tool.ItemId,
            ["ToolName"] = context.Tool.Name,
            ["ClientId"] = context.Tool.ClientId,
            ["CourseId"] = context.LineItem.CourseId,
            ["CourseItemId"] = context.LineItem.ResourceLinkId,
            ["LineItemId"] = context.LineItem.ItemId,
            ["LineItemLabel"] = context.LineItem.Label,
            ["UserId"] = context.Score.UserId,
            ["ScoreGiven"] = context.Score.ScoreGiven,
            ["ScoreMaximum"] = context.Score.ScoreMaximum,
            ["Percent"] = context.Percent,
            ["ActivityProgress"] = context.Score.ActivityProgress,
            ["GradingProgress"] = context.Score.GradingProgress,
            ["RecordedInProgress"] = context.RecordedInProgress,
            ["TimestampUtc"] = DateTime.SpecifyKind(context.Score.TimestampUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
        };
}
