using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Default <see cref="IActivityDispositionService"/> implementation. It is the single, source-neutral
/// path for completing an activity with a disposition: it records the disposition and completion
/// metadata, then runs the configured subject actions. Agent, provider, AI, dialer, and inbound voice
/// outcomes all converge here so inbound and outbound work is dispositioned through the same subject flow.
/// </summary>
public sealed class DefaultActivityDispositionService : IActivityDispositionService
{
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly INamedCatalog<OmnichannelDisposition> _dispositionsCatalog;
    private readonly IContentManager _contentManager;
    private readonly ISubjectActionExecutor _subjectActionExecutor;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly IEnumerable<IActivityDispositionHandler> _handlers;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultActivityDispositionService"/> class.
    /// </summary>
    /// <param name="activityManager">The activity manager used to persist the dispositioned activity.</param>
    /// <param name="dispositionsCatalog">The dispositions catalog used to resolve the selected disposition.</param>
    /// <param name="contentManager">The content manager used to load the contact for subject actions.</param>
    /// <param name="subjectActionExecutor">The subject action executor that runs the subject flow.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service used to resolve the required-disposition policy.</param>
    /// <param name="handlers">The handlers notified after a successful disposition.</param>
    /// <param name="clock">The clock used to stamp completion times.</param>
    public DefaultActivityDispositionService(
        IOmnichannelActivityManager activityManager,
        INamedCatalog<OmnichannelDisposition> dispositionsCatalog,
        IContentManager contentManager,
        ISubjectActionExecutor subjectActionExecutor,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IEnumerable<IActivityDispositionHandler> handlers,
        IClock clock)
    {
        _activityManager = activityManager;
        _dispositionsCatalog = dispositionsCatalog;
        _contentManager = contentManager;
        _subjectActionExecutor = subjectActionExecutor;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _handlers = handlers;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<ActivityDispositionResult> ApplyAsync(ActivityDispositionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var activity = request.Activity;

        if (activity is null)
        {
            return ActivityDispositionResult.Failure("An activity is required to apply a disposition.");
        }

        var effectiveDispositionId = !string.IsNullOrEmpty(request.DispositionId)
            ? request.DispositionId
            : activity.DispositionId;

        if (string.IsNullOrEmpty(effectiveDispositionId) &&
            await RequiresDispositionAsync(activity, cancellationToken))
        {
            return ActivityDispositionResult.Failure("A disposition is required to complete this activity.");
        }

        if (!string.IsNullOrEmpty(request.DispositionId))
        {
            activity.DispositionId = request.DispositionId;
        }

        if (!string.IsNullOrEmpty(request.Notes))
        {
            activity.Notes = string.IsNullOrEmpty(activity.Notes)
                ? request.Notes
                : $"{activity.Notes}{Environment.NewLine}{request.Notes}";
        }

        activity.Status = ActivityStatus.Completed;
        activity.CompletedById = request.ActorId;
        activity.CompletedByUsername = request.ActorDisplayName;
        activity.CompletedUtc = _clock.UtcNow;

        await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);

        var disposition = string.IsNullOrEmpty(activity.DispositionId)
            ? null
            : await _dispositionsCatalog.FindByIdAsync(activity.DispositionId, cancellationToken);

        if (disposition is not null)
        {
            ContentItem contact = null;

            if (!string.IsNullOrEmpty(activity.ContactContentItemId))
            {
                contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);
            }

            var executionContext = new SubjectActionExecutionContext
            {
                Activity = activity,
                Contact = contact,
                Subject = activity.Subject,
                Disposition = disposition,
                ActionScheduleDates = request.ActionScheduleDates,
            };

            await _subjectActionExecutor.ExecuteAsync(executionContext, cancellationToken);
        }

        foreach (var handler in _handlers)
        {
            await handler.DispositionedAsync(request, cancellationToken);
        }

        return ActivityDispositionResult.Success(activity);
    }

    private async Task<bool> RequiresDispositionAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(activity.SubjectContentType))
        {
            return false;
        }

        var flowSettings = await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(activity.SubjectContentType, cancellationToken);

        return flowSettings?.RequireDisposition == true;
    }
}
