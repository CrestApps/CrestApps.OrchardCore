using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Checks, compares and publishes survey drafts as immutable versions.
/// </summary>
public sealed class SurveyPublisher : ISurveyPublisher
{
    private readonly ISurveyManager _surveyManager;
    private readonly SurveyVersionStore _versionStore;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly ISurveyVersionCache _versionCache;
    private readonly ISurveyDefinitionValidator _validator;
    private readonly ISurveyDefinitionDiffer _differ;
    private readonly SurveyBankDrawResolver _bankDrawResolver;
    private readonly IEnumerable<ISurveyObserver> _observers;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyPublisher"/> class.
    /// </summary>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="versionStore">The version store.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="versionCache">The version cache.</param>
    /// <param name="validator">The definition validator.</param>
    /// <param name="differ">The definition differ.</param>
    /// <param name="bankDrawResolver">The bank draw resolver.</param>
    /// <param name="observers">The survey observers.</param>
    /// <param name="clock">The clock.</param>
    public SurveyPublisher(
        ISurveyManager surveyManager,
        SurveyVersionStore versionStore,
        SurveyCollectorStore collectorStore,
        ISurveyVersionCache versionCache,
        ISurveyDefinitionValidator validator,
        ISurveyDefinitionDiffer differ,
        SurveyBankDrawResolver bankDrawResolver,
        IEnumerable<ISurveyObserver> observers,
        IClock clock)
    {
        _surveyManager = surveyManager;
        _versionStore = versionStore;
        _collectorStore = collectorStore;
        _versionCache = versionCache;
        _validator = validator;
        _differ = differ;
        _bankDrawResolver = bankDrawResolver;
        _observers = observers;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<SurveyPublishPreview> PreviewAsync(Survey survey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);

        var (resolved, messages) = await ResolveAndValidateAsync(survey, cancellationToken);
        var published = await _versionCache.GetPublishedAsync(survey, cancellationToken);

        return new SurveyPublishPreview
        {
            Messages = messages,
            Diff = _differ.Compare(published?.Definition, resolved),
        };
    }

    /// <inheritdoc />
    public async Task<SurveyPublishResult> PublishAsync(string surveyId, string notes, string actorId, CancellationToken cancellationToken = default)
    {
        var survey = await _surveyManager.FindByIdAsync(surveyId, cancellationToken);

        if (survey is null)
        {
            return new SurveyPublishResult { Error = SurveyErrorCode.NotFound };
        }

        if (survey.Status == SurveyStatus.Archived)
        {
            return new SurveyPublishResult { Error = SurveyErrorCode.InvalidState };
        }

        var (resolved, messages) = await ResolveAndValidateAsync(survey, cancellationToken);

        if (messages.Any(message => message.IsError))
        {
            return new SurveyPublishResult { Error = SurveyErrorCode.InvalidDefinition, Messages = messages };
        }

        var versions = await _versionStore.ListBySurveyAsync(survey.ItemId, cancellationToken);
        var number = Math.Max(survey.PublishedVersionNumber, versions.Count == 0 ? 0 : versions.Max(version => version.Number)) + 1;
        var version = new SurveyVersion
        {
            ItemId = IdGenerator.GenerateId(),
            SurveyId = survey.ItemId,
            Number = number,
            Definition = resolved,
            PublishedUtc = _clock.UtcNow,
            PublishedBy = actorId,
            Notes = notes,
            ContentHash = SurveyContentHash.Compute(resolved),
        };

        await _versionStore.CreateAsync(version, cancellationToken);

        survey.PublishedVersionId = version.ItemId;
        survey.PublishedVersionNumber = number;
        survey.HasUnpublishedChanges = false;

        if (survey.Status == SurveyStatus.Draft)
        {
            survey.Status = SurveyStatus.Open;
        }

        await _surveyManager.UpdateAsync(survey, data: null, cancellationToken);

        if (!survey.NoDefaultWebLink && await _collectorStore.FindDefaultAsync(survey.ItemId, cancellationToken) is null)
        {
            await _collectorStore.CreateAsync(new SurveyCollector
            {
                ItemId = IdGenerator.GenerateId(),
                SurveyId = survey.ItemId,
                Name = "Web link",
                Kind = SurveyCollectorKinds.WebLink,
                Token = SurveyNames.NewToken(),
                Status = CollectorStatus.Open,
                IsDefault = true,
                CreatedUtc = _clock.UtcNow,
                ModifiedUtc = _clock.UtcNow,
                OwnerId = actorId,
            }, cancellationToken);
        }

        foreach (var observer in _observers)
        {
            await observer.PublishedAsync(survey, version);
        }

        return new SurveyPublishResult { Version = version, Messages = messages };
    }

    /// <inheritdoc />
    public async Task<SurveyErrorCode> RestoreAsync(string surveyId, string versionId, CancellationToken cancellationToken = default)
    {
        var survey = await _surveyManager.FindByIdAsync(surveyId, cancellationToken);

        if (survey is null)
        {
            return SurveyErrorCode.NotFound;
        }

        var version = await _versionCache.GetAsync(versionId, cancellationToken);

        if (version is null || version.SurveyId != survey.ItemId)
        {
            return SurveyErrorCode.NotFound;
        }

        var draft = SurveyDefinitionCloner.Clone(version.Definition);
        SurveyBankDrawResolver.RemoveResolved(draft);

        return await _surveyManager.SaveDraftAsync(survey, draft, cancellationToken: cancellationToken);
    }

    private async Task<(SurveyDefinition Resolved, List<SurveyValidationMessage> Messages)> ResolveAndValidateAsync(Survey survey, CancellationToken cancellationToken)
    {
        var resolved = SurveyDefinitionCloner.Clone(survey.Draft) ?? new SurveyDefinition();
        var messages = new List<SurveyValidationMessage>();

        messages.AddRange(await _bankDrawResolver.ResolveAsync(resolved, cancellationToken));

        var context = await _validator.ValidateAsync(survey, resolved, cancellationToken);
        messages.AddRange(context.Messages);

        return (resolved, messages);
    }
}
