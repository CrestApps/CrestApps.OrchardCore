using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// The default <see cref="INotInServiceActivityCompleter"/>: marks the number, then completes the activity through
/// the shared disposition service with the subject's not-in-service disposition.
/// </summary>
/// <remarks>
/// The disposition is the one with the <see cref="DispositionOutcome.NotInService"/> outcome that the subject's flow
/// uses, so the outcome the platform records on its own is the same one an agent would choose and runs the same
/// actions; failing that, any disposition with the outcome. A tenant with none still gets one, named
/// <see cref="OmnichannelConstants.NotInServiceDispositionName"/> and created the first time it is needed, because a
/// completed call with no disposition is invisible to every report that counts outcomes, and a subject that requires
/// one would refuse the completion outright.
/// </remarks>
internal sealed class NotInServiceActivityCompleter : INotInServiceActivityCompleter
{
    private readonly INotInServiceNumberService _notInServiceNumbers;
    private readonly IActivityDispositionService _dispositionService;
    private readonly ISourceCatalog<SubjectAction> _actionCatalog;
    private readonly INamedCatalogManager<OmnichannelDisposition> _dispositionManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotInServiceActivityCompleter"/> class.
    /// </summary>
    /// <param name="notInServiceNumbers">The list of numbers known not to be in service.</param>
    /// <param name="dispositionService">The shared disposition service that completes activities.</param>
    /// <param name="actionCatalog">The subject actions, read to find the subject's not-in-service disposition.</param>
    /// <param name="dispositionManager">The dispositions, used to find or create the default one.</param>
    /// <param name="logger">The logger.</param>
    public NotInServiceActivityCompleter(
        INotInServiceNumberService notInServiceNumbers,
        IActivityDispositionService dispositionService,
        ISourceCatalog<SubjectAction> actionCatalog,
        INamedCatalogManager<OmnichannelDisposition> dispositionManager,
        ILogger<NotInServiceActivityCompleter> logger)
    {
        _notInServiceNumbers = notInServiceNumbers;
        _dispositionService = dispositionService;
        _actionCatalog = actionCatalog;
        _dispositionManager = dispositionManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ActivityDispositionResult> CompleteAsync(NotInServiceCompletionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var activity = request.Activity;

        if (activity is null)
        {
            return ActivityDispositionResult.Failure("An activity is required to complete it as not in service.");
        }

        var phoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? activity.PreferredDestination
            : request.PhoneNumber;

        // The number is marked first and whatever becomes of the activity, so a completion that is refused (the
        // activity already finished, the contact unresolved) still keeps the number out of every later load.
        await _notInServiceNumbers.MarkAsync(new NotInServiceMark
        {
            PhoneNumber = phoneNumber,
            Source = request.Source,
            Reason = request.Reason,
            ActivityId = activity.ItemId,
            CampaignId = activity.CampaignId,
            ContactContentItemId = activity.ContactContentItemId,
        }, cancellationToken);

        if (activity.Status.IsTerminal())
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Marked '{PhoneNumber}' as not in service but left activity '{ActivityId}' as it was, because it had already finished as {Status}.",
                    phoneNumber.SanitizeLogValue(),
                    activity.ItemId.SanitizeLogValue(),
                    activity.Status);
            }

            return ActivityDispositionResult.Success(activity);
        }

        var disposition = await ResolveDispositionAsync(activity.SubjectContentType, cancellationToken);

        activity.TerminalReasonCode = OmnichannelConstants.TerminalReasons.NumberNotInService;

        var result = await _dispositionService.ApplyAsync(new ActivityDispositionRequest
        {
            Activity = activity,
            DispositionId = disposition?.ItemId,
            Source = ActivityDispositionSource.Provider,
            Notes = BuildNotes(phoneNumber, request.Reason),
            NotInServiceSource = request.Source,
        }, cancellationToken);

        if (result.Succeeded)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Completed activity '{ActivityId}' as not in service with disposition '{Disposition}'. PhoneNumber={PhoneNumber}, Source={Source}, Reason={Reason}, CampaignId={CampaignId}.",
                    activity.ItemId.SanitizeLogValue(),
                    disposition?.Name.SanitizeLogValue(),
                    phoneNumber.SanitizeLogValue(),
                    request.Source.SanitizeLogValue(),
                    request.Reason.SanitizeLogValue(),
                    activity.CampaignId.SanitizeLogValue());
            }
        }
        else
        {
            _logger.LogWarning(
                "Could not complete activity '{ActivityId}' as not in service: {Error}. The number '{PhoneNumber}' is still marked.",
                activity.ItemId.SanitizeLogValue(),
                result.ErrorMessage.SanitizeLogValue(),
                phoneNumber.SanitizeLogValue());
        }

        return result;
    }

    private async Task<OmnichannelDisposition> ResolveDispositionAsync(string subjectContentType, CancellationToken cancellationToken)
    {
        var dispositions = await _dispositionManager.GetAllAsync(cancellationToken);
        var actions = await _actionCatalog.GetAllAsync(cancellationToken);

        // The subject's own not-in-service disposition, so its actions run; otherwise any disposition marked as the
        // not-in-service outcome.
        var disposition = DispositionOutcomes.Find(dispositions, actions, subjectContentType, DispositionOutcome.NotInService, includeUnwired: true);

        if (disposition is not null)
        {
            return disposition;
        }

        // A disposition already named for it, made by hand before outcomes existed, is taken as meaning what its name
        // says rather than being duplicated under the same name.
        var named = await _dispositionManager.FindByNameAsync(OmnichannelConstants.NotInServiceDispositionName, cancellationToken);

        if (named is not null)
        {
            named.Outcome = DispositionOutcome.NotInService;
            await _dispositionManager.UpdateAsync(named, cancellationToken: cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Marked the existing '{Disposition}' disposition as the not-in-service outcome, because no disposition had that outcome.",
                    named.Name.SanitizeLogValue());
            }

            return named;
        }

        var created = await _dispositionManager.NewAsync(new JsonObject
        {
            [nameof(OmnichannelDisposition.Name)] = OmnichannelConstants.NotInServiceDispositionName,
            [nameof(OmnichannelDisposition.Description)] = "The number is not in service: the network reported it unallocated, disconnected or invalid. Applied automatically when a call finds the number out of service.",
        }, cancellationToken);

        created.Outcome = DispositionOutcome.NotInService;

        await _dispositionManager.CreateAsync(created, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Created the '{Disposition}' disposition with the not-in-service outcome, because no disposition had that outcome.",
                OmnichannelConstants.NotInServiceDispositionName);
        }

        return created;
    }

    private static string BuildNotes(string phoneNumber, string reason)
        => string.IsNullOrWhiteSpace(reason)
            ? $"The number {phoneNumber} is not in service. The activity was completed automatically, without an agent or the AI, and the number will not be dialed again."
            : $"The number {phoneNumber} is not in service: the network reported {reason}. The activity was completed automatically, without an agent or the AI, and the number will not be dialed again.";
}
