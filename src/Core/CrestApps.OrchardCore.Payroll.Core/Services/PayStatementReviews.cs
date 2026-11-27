using System.Security.Claims;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Settings;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Employees review their coming pay before a run is submitted (Settings &gt; Payroll): each sees their draft statement
/// of a calculated run in My Pay and says it is right or asks about it. A question tells the run's preparer. A
/// calculation that changes the net pay makes the answer out of date. The review never blocks a submission.
/// </summary>
public sealed class PayStatementReviewService
{
    private const int MaxNoteLength = 1000;

    private readonly PayStatementStore _statements;
    private readonly IPayRunManager _runs;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayStatementReviewService"/> class.
    /// </summary>
    public PayStatementReviewService(PayStatementStore statements, IPayRunManager runs, ISiteService siteService, IClock clock, IServiceProvider services, IStringLocalizer<PayStatementReviewService> stringLocalizer)
    {
        _statements = statements;
        _runs = runs;
        _siteService = siteService;
        _clock = clock;
        _services = services;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets whether employees review their pay.
    /// </summary>
    /// <returns><see langword="true"/> when the setting is on.</returns>
    public async Task<bool> IsOnAsync()
        => (await _siteService.GetSettingsAsync<PayrollSettings>())?.EmployeeReview == true;

    /// <summary>
    /// Gets whether a run takes reviews now: calculated and not yet submitted.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <returns><see langword="true"/> when a draft of the run may be reviewed.</returns>
    public static bool TakesReviews(PayRun run)
        => run?.Status == PayRunStatus.Calculated;

    /// <summary>
    /// Lists a staff member's draft statements that may be reviewed now, with their runs.
    /// </summary>
    /// <param name="staffMemberId">The staff member.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The statements and their runs; none when the setting is off.</returns>
    public async Task<IReadOnlyList<(PayStatement Statement, PayRun Run)>> ListOpenAsync(string staffMemberId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(staffMemberId) || !await IsOnAsync())
        {
            return [];
        }

        var draft = nameof(PayStatementStatus.Draft);
        var open = new List<(PayStatement, PayRun)>();

        foreach (var statement in await _statements.ListAsync(index => index.StaffMemberId == staffMemberId && index.Status == draft, cancellationToken))
        {
            if (await _runs.FindByIdAsync(statement.RunId, cancellationToken) is { } run && TakesReviews(run))
            {
                open.Add((statement, run));
            }
        }

        return [.. open.OrderBy(pair => pair.Item1.PayDate)];
    }

    /// <summary>
    /// Records an employee's answer about their draft pay.
    /// </summary>
    /// <param name="statement">The draft statement, the employee's own.</param>
    /// <param name="user">The employee.</param>
    /// <param name="confirmed"><see langword="true"/> when it is right; <see langword="false"/> to ask about it.</param>
    /// <param name="note">What the employee asks; needed for a question.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    public async Task<PayRunResult> ReviewAsync(PayStatement statement, ClaimsPrincipal user, bool confirmed, string note, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var result = new PayRunResult { Statement = statement };
        var run = await _runs.FindByIdAsync(statement.RunId, cancellationToken);
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (!await IsOnAsync())
        {
            result.Errors.Add(S["Pay isn't reviewed before submission on this site."]);
        }
        else if (statement.Status != PayStatementStatus.Draft || !TakesReviews(run))
        {
            result.Errors.Add(S["This pay can no longer be reviewed: the run was submitted, processed or is being calculated again."]);
        }
        else if (!confirmed && note is null)
        {
            result.Errors.Add(S["Say what looks wrong, so payroll can look into it."]);
        }
        else if (note?.Length > MaxNoteLength)
        {
            result.Errors.Add(S["Keep the note under {0} characters.", MaxNoteLength]);
        }

        if (!result.Succeeded)
        {
            return result;
        }

        // Under the run's lock and on a fresh read, so an answer never writes over a statement calculated meanwhile.
        var (locker, locked) = await _services.GetRequiredService<IDistributedLock>().TryAcquireLockAsync("Payroll:PayRun:" + statement.RunId, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1));

        if (!locked)
        {
            result.Errors.Add(S["The pay run is being worked on. Try again in a moment."]);

            return result;
        }

        await using (locker)
        {
            var fresh = await _statements.FindByIdAsync(statement.ItemId, cancellationToken);

            run = await _runs.FindByIdAsync(statement.RunId, cancellationToken);

            if (fresh is null || fresh.Status != PayStatementStatus.Draft || !TakesReviews(run))
            {
                result.Errors.Add(S["This pay can no longer be reviewed: the run was submitted, processed or is being calculated again."]);

                return result;
            }

            fresh.Review ??= new PayStatementReview();
            fresh.Review.Status = confirmed ? PayStatementReviewStatus.Confirmed : PayStatementReviewStatus.Questioned;
            fresh.Review.Note = confirmed ? null : note;
            fresh.Review.ReviewedNet = fresh.Net;
            fresh.Review.UserId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
            fresh.Review.ReviewedUtc = _clock.UtcNow;
            await _statements.UpdateAsync(fresh, cancellationToken);
            await _services.GetRequiredService<IPayrollCommitter>().CommitAsync(cancellationToken);

            result.Statement = fresh;

            if (!confirmed)
            {
                await TellPreparerAsync(run, fresh, cancellationToken);
            }
        }

        return result;
    }

    // A question tells the person who prepared the run, when Notifications and Users are on.
    private async Task TellPreparerAsync(PayRun run, PayStatement statement, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(run.PreparedById)
            || _services.GetService<INotificationService>() is not { } notifications
            || _services.GetService<UserManager<IUser>>() is not { } userManager
            || await userManager.FindByIdAsync(run.PreparedById) is not { } preparer)
        {
            return;
        }

        await notifications.SendAsync(preparer, new NotificationMessage
        {
            Subject = S["{0} asked about their pay in {1}", statement.Snapshot?.EmployeeName, PayRunTitles.Name(run, S)],
            Summary = S["Look into it before you submit the run."],
            TextBody = S["{0} asked about their pay in {1}. Look into it before you submit the run.", statement.Snapshot?.EmployeeName, PayRunTitles.Name(run, S)],
            Url = "~/Admin/payroll/pay-runs/" + run.ItemId,
            Type = PayrollConstants.PayReviewQuestionedNotificationType,
            ReferenceId = statement.ItemId,
        }, cancellationToken);
    }
}

/// <summary>
/// After a calculation, asks each employee of the run to review their coming pay in My Pay, when employees review their
/// pay. A person is asked again only when their net pay changed since they were last asked.
/// </summary>
public sealed class PayReviewNotificationObserver : IPayRunObserver
{
    private readonly PayStatementStore _statements;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayReviewNotificationObserver"/> class.
    /// </summary>
    public PayReviewNotificationObserver(PayStatementStore statements, ISiteService siteService, IServiceProvider services, IStringLocalizer<PayReviewNotificationObserver> stringLocalizer)
    {
        _statements = statements;
        _siteService = siteService;
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task ChangedAsync(PayRunChange change, CancellationToken cancellationToken = default)
    {
        if (change?.Kind != PayRunChangeKind.Calculated || (await _siteService.GetSettingsAsync<PayrollSettings>())?.EmployeeReview != true)
        {
            return;
        }

        var runId = change.Run.ItemId;
        var draft = nameof(PayStatementStatus.Draft);

        foreach (var statement in await _statements.ListAsync(index => index.RunId == runId && index.Status == draft, cancellationToken))
        {
            if (statement.Review?.NotifiedNet == statement.Net)
            {
                continue;
            }

            await StaffNotices.SendAsync(_services, statement.StaffMemberId, new NotificationMessage
            {
                Subject = S["Review your coming pay"],
                Summary = S["Your pay for {0} is ready to look at before it is approved.", statement.PayDate.ToString("d", System.Globalization.CultureInfo.CurrentCulture)],
                TextBody = S["Your pay is ready to look at in My Pay before it is approved. Say it is right, or ask about it."],
                Url = MyPayUrls.Review(statement.ItemId),
                Type = PayrollConstants.PayReviewRequestedNotificationType,
                ReferenceId = statement.ItemId,
            }, cancellationToken);

            statement.Review ??= new PayStatementReview();
            statement.Review.NotifiedNet = statement.Net;
            await _statements.UpdateAsync(statement, cancellationToken);
        }
    }
}
