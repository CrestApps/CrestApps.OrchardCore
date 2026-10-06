using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// The default <see cref="IActivityOutcomeCompleter"/>: chooses the disposition that stands for the outcome and
/// completes the activity with it through the shared disposition service.
/// </summary>
/// <remarks>
/// The choice, in order: a disposition with the outcome that the subject's flow is wired to; one with the fallback
/// outcome the flow is wired to; the disposition the subject's "Try again" action is wired to, so the contact is still
/// tried again; any disposition with the outcome; any with the fallback outcome; a disposition already named for the
/// outcome; and finally one created for it.
/// </remarks>
internal sealed class ActivityOutcomeCompleter : IActivityOutcomeCompleter
{
    private static readonly Dictionary<DispositionOutcome, (string Name, string Description)> _defaults = new()
    {
        [DispositionOutcome.NoAnswer] = ("No Answer", "Nobody answered the call. Applied automatically when the dialer's call rings out or is given up on."),
        [DispositionOutcome.Busy] = ("Busy", "The line was busy. Applied automatically when the network reports the called party busy."),
        [DispositionOutcome.AnsweringMachine] = ("Answering Machine", "A voicemail or answering machine picked up. Applied automatically when the dialer screens the call out as a machine."),
        [DispositionOutcome.Rejected] = ("Rejected", "The called party or the network declined the call. Applied automatically by the dialer."),
        [DispositionOutcome.Failed] = ("Call Failed", "The call could not be completed. Applied automatically when the provider refuses the call or the network fails it."),
        [DispositionOutcome.Disconnected] = ("Disconnected", "The customer answered but hung up before an agent was connected. Applied automatically by the dialer."),
    };

    private readonly IActivityDispositionService _dispositionService;
    private readonly ISourceCatalog<SubjectAction> _actionCatalog;
    private readonly INamedCatalogManager<OmnichannelDisposition> _dispositionManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityOutcomeCompleter"/> class.
    /// </summary>
    /// <param name="dispositionService">The shared disposition service that completes activities.</param>
    /// <param name="actionCatalog">The subject actions, read to find the subject's dispositions.</param>
    /// <param name="dispositionManager">The dispositions, used to find or create the one for the outcome.</param>
    /// <param name="logger">The logger.</param>
    public ActivityOutcomeCompleter(
        IActivityDispositionService dispositionService,
        ISourceCatalog<SubjectAction> actionCatalog,
        INamedCatalogManager<OmnichannelDisposition> dispositionManager,
        ILogger<ActivityOutcomeCompleter> logger)
    {
        _dispositionService = dispositionService;
        _actionCatalog = actionCatalog;
        _dispositionManager = dispositionManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ActivityDispositionResult> CompleteAsync(ActivityOutcomeCompletionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var activity = request.Activity;

        if (activity is null)
        {
            return ActivityDispositionResult.Failure("An activity is required to complete it with an outcome.");
        }

        if (activity.Status.IsTerminal())
        {
            return ActivityDispositionResult.Success(activity);
        }

        var disposition = await ResolveDispositionAsync(activity.SubjectContentType, request, cancellationToken);

        if (disposition is null)
        {
            _logger.LogWarning(
                "No disposition could be found or created for outcome {Outcome}; activity '{ActivityId}' is completed without one.",
                request.Outcome,
                activity.ItemId.SanitizeLogValue());
        }

        if (!string.IsNullOrEmpty(request.TerminalReasonCode))
        {
            activity.TerminalReasonCode = request.TerminalReasonCode;
        }

        return await _dispositionService.ApplyAsync(new ActivityDispositionRequest
        {
            Activity = activity,
            DispositionId = disposition?.ItemId,
            Source = request.Source,
            Notes = request.Notes,
        }, cancellationToken);
    }

    private async Task<OmnichannelDisposition> ResolveDispositionAsync(
        string subjectContentType,
        ActivityOutcomeCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var dispositions = await _dispositionManager.GetAllAsync(cancellationToken);
        var actions = await _actionCatalog.GetAllAsync(cancellationToken);
        var fallback = request.FallbackOutcome == request.Outcome ? DispositionOutcome.None : request.FallbackOutcome;

        var disposition = DispositionOutcomes.Find(dispositions, actions, subjectContentType, request.Outcome, includeUnwired: false)
            ?? DispositionOutcomes.Find(dispositions, actions, subjectContentType, fallback, includeUnwired: false)
            ?? (request.PreferRetriedDisposition ? FindRetriedDisposition(dispositions, actions, subjectContentType) : null)
            ?? DispositionOutcomes.Find(dispositions, actions, subjectContentType, request.Outcome, includeUnwired: true)
            ?? DispositionOutcomes.Find(dispositions, actions, subjectContentType, fallback, includeUnwired: true);

        if (disposition is not null || !_defaults.TryGetValue(request.Outcome, out var defaults))
        {
            return disposition;
        }

        // A disposition already named for the outcome, made by hand before outcomes existed, is taken as meaning what
        // its name says rather than being duplicated under the same name.
        var named = await _dispositionManager.FindByNameAsync(defaults.Name, cancellationToken);

        if (named is not null)
        {
            if (named.Outcome == DispositionOutcome.None)
            {
                named.Outcome = request.Outcome;
                await _dispositionManager.UpdateAsync(named, cancellationToken: cancellationToken);

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Marked the existing '{Disposition}' disposition with the {Outcome} outcome, because no disposition had that outcome.",
                        named.Name.SanitizeLogValue(),
                        request.Outcome);
                }
            }

            return named;
        }

        var created = await _dispositionManager.NewAsync(new JsonObject
        {
            [nameof(OmnichannelDisposition.Name)] = defaults.Name,
            [nameof(OmnichannelDisposition.Description)] = defaults.Description,
        }, cancellationToken);

        created.Outcome = request.Outcome;

        await _dispositionManager.CreateAsync(created, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Created the '{Disposition}' disposition with the {Outcome} outcome, because no disposition had that outcome.",
                defaults.Name,
                request.Outcome);
        }

        return created;
    }

    // The disposition the subject's "Try again" action is wired to, or null when it has none.
    private static OmnichannelDisposition FindRetriedDisposition(
        IEnumerable<OmnichannelDisposition> dispositions,
        IEnumerable<SubjectAction> actions,
        string subjectContentType)
    {
        if (string.IsNullOrEmpty(subjectContentType))
        {
            return null;
        }

        var retriedIds = actions
            .Where(action => action is not null &&
                string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.Ordinal) &&
                string.Equals(action.SubjectContentType, subjectContentType, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(action.DispositionId))
            .Select(action => action.DispositionId)
            .ToHashSet(StringComparer.Ordinal);

        return dispositions
            .Where(disposition => disposition is not null && retriedIds.Contains(disposition.ItemId))
            .OrderBy(disposition => disposition.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}
