using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Xapi.Core;
using CrestApps.OrchardCore.Learning.Xapi.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Cmi5;

/// <summary>
/// Decides what a cmi5 registration has satisfied and what it counts for in Learning. An AU is satisfied when its
/// moveOn is met (Passed, Completed, CompletedAndPassed, CompletedOrPassed; NotApplicable at once); a block when all its
/// AUs are; the course when all AUs are. The LMS writes <c>satisfied</c> for each AU, block and course the moment it is
/// satisfied, once. The package item's result goes to Learning on the learner path, marked self-reported: complete when
/// the course is satisfied, passed or failed from the AUs whose moveOn needs a pass, and the score from the
/// <c>passed</c> and <c>failed</c> scaled scores times 100.
/// </summary>
public sealed class Cmi5Progress
{
    private readonly XapiStatementService _statements;
    private readonly PackageRuntimeStore _store;
    private readonly ILearnerProgressService _progress;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Cmi5Progress"/> class.
    /// </summary>
    public Cmi5Progress(
        XapiStatementService statements,
        PackageRuntimeStore store,
        ILearnerProgressService progress,
        ILogger<Cmi5Progress> logger)
    {
        _statements = statements;
        _store = store;
        _progress = progress;
        _logger = logger;
    }

    /// <summary>
    /// Gets whether an AU's moveOn is met.
    /// </summary>
    /// <param name="au">The AU.</param>
    /// <param name="state">What it reported, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the moveOn is met.</returns>
    public static bool MoveOnMet(Cmi5Au au, Cmi5AuState state)
    {
        ArgumentNullException.ThrowIfNull(au);

        var completed = state?.Completed == true;
        var passed = state?.Passed == true;

        return au.MoveOn switch
        {
            "Passed" => passed,
            "Completed" => completed,
            "CompletedAndPassed" => completed && passed,
            "CompletedOrPassed" => completed || passed,
            _ => true,
        };
    }

    /// <summary>
    /// Builds the result of the package item from the cmi5 state.
    /// </summary>
    /// <param name="course">The course.</param>
    /// <param name="state">The cmi5 state.</param>
    /// <param name="itemId">The package item.</param>
    /// <param name="attempts">The launches so far.</param>
    /// <returns>The result, marked self-reported.</returns>
    public static CourseItemResult Map(Cmi5Course course, Cmi5State state, string itemId, int attempts)
    {
        ArgumentNullException.ThrowIfNull(course);
        ArgumentNullException.ThrowIfNull(state);

        var needPass = course.Aus.Where(au => au.MoveOn is "Passed" or "CompletedAndPassed").ToList();
        var states = course.Aus.Select(au => state.Aus.FirstOrDefault(item => item.AuId == au.Id)).ToList();
        bool? passed = null;

        if (needPass.Count > 0)
        {
            var needed = needPass.Select(au => state.Aus.FirstOrDefault(item => item.AuId == au.Id)).ToList();

            passed = needed.All(item => item?.Passed == true)
                ? true
                : needed.Any(item => item?.Failed == true && !item.Passed) ? false : null;
        }
        else if (states.Any(item => item?.Passed == true))
        {
            passed = true;
        }
        else if (states.Any(item => item?.Failed == true))
        {
            passed = false;
        }

        var scores = states.Where(item => item?.ScoreScaled is not null).Select(item => item.ScoreScaled.Value).ToList();

        return new CourseItemResult
        {
            ItemId = itemId,
            Score = scores.Count == 0 ? null : Math.Round(Math.Clamp(scores.Average() * 100, 0, 100), 2, MidpointRounding.AwayFromZero),
            Passed = passed,
            ReportedComplete = state.CourseSatisfied,
            Attempts = attempts,

            // The AU in the browser reported it: Learning keeps the mark on the item's progress and in the completion snapshot.
            SelfReported = true,
        };
    }

    /// <summary>
    /// Writes <c>satisfied</c> for what the registration has newly satisfied, saves the state, and records the item's
    /// result in Learning. The caller holds the runtime lock of the state, and gives a state read after the last commit
    /// of the session: writing the statements commits it, so the state is saved first.
    /// </summary>
    /// <param name="state">The runtime state, changed.</param>
    /// <param name="part">The package part.</param>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="actor">The learner's agent.</param>
    /// <param name="homePage">The site's home page.</param>
    /// <param name="nowUtc">The time, in UTC.</param>
    /// <param name="recordProgress">Whether to record the result in Learning (not for a review).</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What Learning recorded, or <see langword="null"/>.</returns>
    public async Task<ProgressUpdate> EvaluateAsync(PackageRuntimeState state, LearningPackagePart part, Enrollment enrollment, JsonObject actor, string homePage, DateTime nowUtc, bool recordProgress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(part);

        var course = part.Cmi5;
        var cmi5 = state.Cmi5;

        if (course is null || cmi5 is null)
        {
            return null;
        }

        var statements = new List<JsonObject>();
        var itemId = state.PackageItemId;

        foreach (var au in course.Aus)
        {
            var auState = cmi5.Aus.FirstOrDefault(item => item.AuId == au.Id);

            if (auState?.Satisfied == true || !MoveOnMet(au, auState))
            {
                continue;
            }

            if (auState is null)
            {
                auState = new Cmi5AuState { AuId = au.Id };
                cmi5.Aus.Add(auState);
            }

            auState.Satisfied = true;
            statements.Add(Satisfied(actor, cmi5.Registration, Cmi5Vocabulary.AuActivityId(homePage, itemId, au.Id), au.Title, au.Id, nowUtc));
        }

        bool IsSatisfied(Cmi5Au au)
            => cmi5.Aus.FirstOrDefault(item => item.AuId == au.Id)?.Satisfied == true;

        foreach (var block in course.Blocks)
        {
            if (cmi5.SatisfiedBlocks.Contains(block.Id))
            {
                continue;
            }

            var aus = course.Aus.Where(au => au.BlockIds.Contains(block.Id)).ToList();

            if (aus.Count > 0 && aus.All(IsSatisfied))
            {
                cmi5.SatisfiedBlocks.Add(block.Id);
                statements.Add(Satisfied(actor, cmi5.Registration, Cmi5Vocabulary.BlockActivityId(homePage, itemId, block.Id), block.Title, block.Id, nowUtc));
            }
        }

        if (!cmi5.CourseSatisfied && course.Aus.All(IsSatisfied))
        {
            cmi5.CourseSatisfied = true;
            statements.Add(Satisfied(actor, cmi5.Registration, Cmi5Vocabulary.CourseActivityId(homePage, itemId), course.Title, course.Id, nowUtc));
        }

        await _store.UpdateAsync(state, cancellationToken);

        if (statements.Count > 0)
        {
            var written = await _statements.WriteTrustedAsync(statements, Cmi5LaunchService.Authority(homePage), Cmi5LaunchService.AuthorityKey, cancellationToken);

            if (!written.Succeeded && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("The cmi5 satisfied statements of registration {Registration} were not stored: {Message}", cmi5.Registration, written.Message);
            }
        }

        if (!recordProgress || enrollment is null)
        {
            return null;
        }

        var update = await _progress.RecordWithRetryAsync(enrollment, Map(course, cmi5, itemId, state.Sessions), cancellationToken);

        if (update?.RefusedReason is not null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("The cmi5 result of item {ItemId} was not recorded on enrollment {EnrollmentId}: {Reason}.", itemId, enrollment.ItemId, update.RefusedReason);
        }

        return update;
    }

    private static JsonObject Satisfied(JsonObject actor, string registration, string activityId, string title, string publisherId, DateTime nowUtc)
        => Cmi5LaunchService.Statement(actor, Cmi5Vocabulary.Verbs.Satisfied, "Satisfied", Cmi5LaunchService.Activity(activityId, title), registration, publisherId, extensions: null, nowUtc);
}
