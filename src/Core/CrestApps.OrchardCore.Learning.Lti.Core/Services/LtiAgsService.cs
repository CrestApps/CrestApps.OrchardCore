using CrestApps.OrchardCore.Learning.Lti.Core.Services.Ags;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Lti.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Services;

/// <summary>
/// Assignment and Grade Services 2.0 for a tool in a course: its line items (list with filters, get, create, update,
/// delete), the scores it posts and the results it reads. A line item belongs to one tool and one course; a fully graded
/// score on a line item bound to an External tool item becomes the item's result in the learner's progress, as a
/// server-to-server result (not reported by the browser), so the gradebook and completion use it.
/// </summary>
public sealed class LtiAgsService
{
    private readonly LtiLineItemStore _lineItems;
    private readonly LtiScoreStore _scores;
    private readonly LtiCourseMembers _members;
    private readonly IEnrollmentStore _enrollments;
    private readonly ILearnerProgressService _progress;
    private readonly IContentManager _contentManager;
    private readonly ICourseStructureService _courseStructure;
    private readonly IEnumerable<ILtiScoreObserver> _observers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LtiAgsService"/> class.
    /// </summary>
    /// <param name="lineItems">The line item store.</param>
    /// <param name="scores">The score store.</param>
    /// <param name="members">The course members.</param>
    /// <param name="enrollments">The enrollment store.</param>
    /// <param name="progress">The learner progress service.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="courseStructure">The course structure reader.</param>
    /// <param name="observers">The score observers.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public LtiAgsService(
        LtiLineItemStore lineItems,
        LtiScoreStore scores,
        LtiCourseMembers members,
        IEnrollmentStore enrollments,
        ILearnerProgressService progress,
        IContentManager contentManager,
        ICourseStructureService courseStructure,
        IEnumerable<ILtiScoreObserver> observers,
        IClock clock,
        ILogger<LtiAgsService> logger)
    {
        _lineItems = lineItems;
        _scores = scores;
        _members = members;
        _enrollments = enrollments;
        _progress = progress;
        _contentManager = contentManager;
        _courseStructure = courseStructure;
        _observers = observers;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Returns a line item of the tool in the course, or <see langword="null"/>.
    /// </summary>
    public async Task<LtiLineItem> GetAsync(ExternalTool tool, string courseId, string lineItemId, CancellationToken cancellationToken = default)
    {
        var lineItem = string.IsNullOrEmpty(lineItemId) ? null : await _lineItems.FindByIdAsync(lineItemId, cancellationToken);

        return lineItem is not null && lineItem.ToolId == tool.ItemId && lineItem.CourseId == courseId ? lineItem : null;
    }

    /// <summary>
    /// Pages the tool's line items in the course.
    /// </summary>
    public Task<(int Count, IReadOnlyList<LtiLineItem> Items)> ListAsync(ExternalTool tool, string courseId, string resourceLinkId, string resourceId, string tag, int page, int limit, CancellationToken cancellationToken = default)
        => _lineItems.PageAsync(courseId, tool.ItemId, resourceLinkId, resourceId, tag, (page - 1) * limit, limit, cancellationToken);

    /// <summary>
    /// Makes a line item.
    /// </summary>
    /// <returns>The line item, or the reason it was refused.</returns>
    public async Task<(LtiLineItem LineItem, string Error)> CreateAsync(ExternalTool tool, string courseId, LtiLineItemInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!await IsResourceLinkOfToolAsync(tool, courseId, input.ResourceLinkId))
        {
            return (null, "resourceLinkId is not an External tool item of this tool in this course.");
        }

        var lineItem = new LtiLineItem
        {
            ItemId = IdGenerator.GenerateId(),
            ToolId = tool.ItemId,
            CourseId = courseId,
            CreatedUtc = _clock.UtcNow,
        };

        Apply(lineItem, input);

        await _lineItems.CreateAsync(lineItem, cancellationToken);

        return (lineItem, null);
    }

    /// <summary>
    /// Changes a line item. Its id, tool and course never change.
    /// </summary>
    /// <returns>The line item, or the reason it was refused.</returns>
    public async Task<(LtiLineItem LineItem, string Error)> UpdateAsync(ExternalTool tool, LtiLineItem lineItem, LtiLineItemInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lineItem);
        ArgumentNullException.ThrowIfNull(input);

        if (!await IsResourceLinkOfToolAsync(tool, lineItem.CourseId, input.ResourceLinkId))
        {
            return (null, "resourceLinkId is not an External tool item of this tool in this course.");
        }

        Apply(lineItem, input);

        await _lineItems.UpdateAsync(lineItem, cancellationToken);

        return (lineItem, null);
    }

    /// <summary>
    /// Removes a line item and its scores. The results already in learners' progress stay.
    /// </summary>
    public async Task DeleteAsync(LtiLineItem lineItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lineItem);

        foreach (var score in await _scores.ListByLineItemAsync(lineItem.ItemId, cancellationToken))
        {
            await _scores.DeleteAsync(score, cancellationToken);
        }

        await _lineItems.DeleteAsync(lineItem, cancellationToken);
    }

    /// <summary>
    /// Stores a score. A score whose timestamp is not later than the stored one is refused, as the specification allows.
    /// </summary>
    /// <returns>What happened.</returns>
    public async Task<LtiScoreOutcome> PostScoreAsync(ExternalTool tool, LtiLineItem lineItem, LtiScoreInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(lineItem);
        ArgumentNullException.ThrowIfNull(input);

        var userId = await _members.FindUserIdAsync(tool, lineItem.CourseId, input.UserId, cancellationToken);

        if (userId is null)
        {
            return LtiScoreOutcome.UnknownUser;
        }

        var score = await _scores.FindAsync(lineItem.ItemId, userId, cancellationToken);

        if (score is not null && input.TimestampUtc <= DateTime.SpecifyKind(score.TimestampUtc, DateTimeKind.Utc))
        {
            return LtiScoreOutcome.NotLater;
        }

        var isNew = score is null;

        score ??= new LtiScore
        {
            ItemId = IdGenerator.GenerateId(),
            LineItemId = lineItem.ItemId,
            ToolId = tool.ItemId,
            CourseId = lineItem.CourseId,
            UserId = userId,
        };

        score.Subject = input.UserId;
        score.ScoreGiven = input.ScoreGiven;
        score.ScoreMaximum = input.ScoreMaximum;
        score.Comment = input.Comment;
        score.ActivityProgress = input.ActivityProgress;
        score.GradingProgress = input.GradingProgress;
        score.TimestampUtc = input.TimestampUtc;
        score.ReceivedUtc = _clock.UtcNow;

        if (isNew)
        {
            await _scores.CreateAsync(score, cancellationToken);
        }
        else
        {
            await _scores.UpdateAsync(score, cancellationToken);
        }

        decimal? percent = score.ScoreGiven is { } given && score.ScoreMaximum is > 0
            ? Math.Clamp(Math.Round(given / score.ScoreMaximum.Value * 100, 2), 0, 100)
            : null;

        var recorded = await RecordInProgressAsync(lineItem, score, percent, cancellationToken);

        foreach (var observer in _observers)
        {
            await observer.ScoreReceivedAsync(new LtiScoreReceivedContext
            {
                Tool = tool,
                LineItem = lineItem,
                Score = score,
                Percent = percent,
                RecordedInProgress = recorded,
            });
        }

        return LtiScoreOutcome.Stored;
    }

    /// <summary>
    /// Pages the results of a line item, optionally of one learner.
    /// </summary>
    public async Task<(int Count, IReadOnlyList<LtiScore> Items)> ResultsAsync(ExternalTool tool, LtiLineItem lineItem, string subject, int page, int limit, CancellationToken cancellationToken = default)
    {
        string userId = null;

        if (!string.IsNullOrEmpty(subject))
        {
            userId = await _members.FindUserIdAsync(tool, lineItem.CourseId, subject, cancellationToken);

            if (userId is null)
            {
                return (0, []);
            }
        }

        return await _scores.PageAsync(lineItem.ItemId, userId, (page - 1) * limit, limit, cancellationToken);
    }

    private async Task<bool> RecordInProgressAsync(LtiLineItem lineItem, LtiScore score, decimal? percent, CancellationToken cancellationToken)
    {
        if (!string.Equals(score.GradingProgress, LtiAgsRules.FullyGraded, StringComparison.Ordinal) || percent is null || string.IsNullOrEmpty(lineItem.ResourceLinkId))
        {
            return false;
        }

        var enrollment = (await _enrollments.ListByCourseForUserAsync(score.UserId, lineItem.CourseId, cancellationToken))
            .FirstOrDefault(candidate => candidate.UserId == score.UserId && candidate.CourseId == lineItem.CourseId && candidate.IsOpen);

        if (enrollment is null)
        {
            return false;
        }

        var update = await _progress.RecordResultAsync(enrollment, new CourseItemResult
        {
            ItemId = lineItem.ResourceLinkId,
            Score = percent,
            Graded = true,
            Submitted = true,
            Attempts = 1,
            SelfReported = false,
        }, cancellationToken);

        if (!string.IsNullOrEmpty(update?.RefusedReason))
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("A tool's score was stored but did not reach the learner's progress: {Reason}", update.RefusedReason);
            }

            return false;
        }

        return true;
    }

    private async Task<bool> IsResourceLinkOfToolAsync(ExternalTool tool, string courseId, string resourceLinkId)
    {
        if (string.IsNullOrEmpty(resourceLinkId))
        {
            return true;
        }

        var item = await _contentManager.GetAsync(resourceLinkId, VersionOptions.Published);

        return item?.Get<ExternalToolItemPart>(nameof(ExternalToolItemPart)) is { } part &&
            part.ToolId == tool.ItemId &&
            _courseStructure.GetCourseId(item) == courseId;
    }

    private static void Apply(LtiLineItem lineItem, LtiLineItemInput input)
    {
        lineItem.ScoreMaximum = input.ScoreMaximum;
        lineItem.Label = input.Label;
        lineItem.ResourceId = input.ResourceId;
        lineItem.Tag = input.Tag;
        lineItem.ResourceLinkId = input.ResourceLinkId;
        lineItem.StartDateTimeUtc = input.StartDateTimeUtc;
        lineItem.EndDateTimeUtc = input.EndDateTimeUtc;
    }
}
