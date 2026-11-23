using CrestApps.Core.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Creates, changes and removes surveys. Writes go through the catalog entry handlers.
/// </summary>
public sealed class SurveyManager : NamedCatalogManager<Survey>, ISurveyManager
{
    private readonly SurveyStore _store;
    private readonly SurveyVersionStore _versionStore;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly ISurveyResponseQueries _responseQueries;
    private readonly IEnumerable<ISurveyTemplateProvider> _templateProviders;
    private readonly IEnumerable<ISurveyObserver> _observers;
    private readonly ISiteService _siteService;
    private readonly SurveyAnonymitySecrets _secrets;
    private readonly SurveyDataPurger _purger;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyManager"/> class.
    /// </summary>
    /// <param name="store">The survey store.</param>
    /// <param name="versionStore">The version store.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="responseQueries">The response queries.</param>
    /// <param name="templateProviders">The template providers.</param>
    /// <param name="observers">The survey observers.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="secrets">The anonymity secrets.</param>
    /// <param name="purger">The purger of the records a deleted survey leaves behind.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="handlers">The catalog entry handlers.</param>
    /// <param name="logger">The logger.</param>
    public SurveyManager(
        SurveyStore store,
        SurveyVersionStore versionStore,
        SurveyCollectorStore collectorStore,
        ISurveyResponseQueries responseQueries,
        IEnumerable<ISurveyTemplateProvider> templateProviders,
        IEnumerable<ISurveyObserver> observers,
        ISiteService siteService,
        SurveyAnonymitySecrets secrets,
        SurveyDataPurger purger,
        IClock clock,
        IEnumerable<ICatalogEntryHandler<Survey>> handlers,
        ILogger<NamedCatalogManager<Survey>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
        _versionStore = versionStore;
        _collectorStore = collectorStore;
        _responseQueries = responseQueries;
        _templateProviders = templateProviders;
        _observers = observers;
        _siteService = siteService;
        _secrets = secrets;
        _purger = purger;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Survey> CreateSurveyAsync(SurveyCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = await _siteService.GetSettingsAsync<SurveySettings>() ?? new SurveySettings();
        var survey = await NewAsync(data: null, cancellationToken);
        var definition = request.Definition is not null
            ? SurveyDefinitionCloner.Clone(request.Definition)
            : await BuildFromTemplateAsync(request.Template, cancellationToken);

        if (definition is null)
        {
            definition = new SurveyDefinition();
        }
        else
        {
            // A template or a passed-in definition is copied with new ids, so two surveys never share one.
            SurveyDefinitionCloner.RegenerateIds(definition);
        }

        definition.Title ??= request.Title;
        SurveyDefinitions.EnsureBasics(definition);

        survey.Title = string.IsNullOrWhiteSpace(request.Title) ? definition.Title : request.Title.Trim();
        survey.Kind = string.IsNullOrWhiteSpace(request.Kind) ? SurveyKinds.Survey : request.Kind;
        survey.Name = await UniqueNameAsync(string.IsNullOrWhiteSpace(request.Name) ? SurveyNames.Slugify(survey.Title) : SurveyNames.Slugify(request.Name), cancellationToken);
        survey.Status = SurveyStatus.Draft;
        survey.Draft = definition;
        survey.DraftModifiedUtc = _clock.UtcNow;
        survey.HasUnpublishedChanges = true;
        survey.Owner = request.Owner;
        _secrets.Assign(survey);
        survey.Access.IncompleteExpiryHours = settings.DefaultIncompleteExpiryHours > 0 ? settings.DefaultIncompleteExpiryHours : 168;
        survey.Access.MinimumGroupSize = Math.Max(settings.DefaultMinimumGroupSize, SurveyAnonymity.Floor(settings));
        survey.Retention.Days = settings.DefaultRetentionDays;

        if (survey.CreatedUtc == default)
        {
            survey.CreatedUtc = _clock.UtcNow;
        }

        await CreateAsync(survey, cancellationToken);

        return survey;
    }

    /// <inheritdoc />
    public async Task<SurveyErrorCode> SaveDraftAsync(Survey survey, SurveyDefinition draft, DateTime? expectedDraftModifiedUtc = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(draft);

        if (expectedDraftModifiedUtc is { } expected && survey.DraftModifiedUtc is { } actual && Truncate(actual) != Truncate(expected))
        {
            return SurveyErrorCode.Busy;
        }

        SurveyDefinitions.EnsureBasics(draft);
        survey.Draft = draft;
        survey.DraftModifiedUtc = _clock.UtcNow;
        survey.HasUnpublishedChanges = true;

        await UpdateAsync(survey, data: null, cancellationToken);

        return SurveyErrorCode.None;
    }

    /// <inheritdoc />
    public async Task<Survey> DuplicateAsync(string surveyId, string name = null, CancellationToken cancellationToken = default)
    {
        var source = await FindByIdAsync(surveyId, cancellationToken);

        if (source is null)
        {
            return null;
        }

        // The copy keeps the question codes: they only need to be unique inside one survey, and piping, formulas and
        // exports name them. CreateSurveyAsync gives every item a new id and carries the translations over to them.
        var definition = SurveyDefinitionCloner.Clone(source.Draft) ?? new SurveyDefinition();

        var copy = await CreateSurveyAsync(new SurveyCreateRequest
        {
            Name = string.IsNullOrWhiteSpace(name) ? source.Name + "-copy" : name,
            Title = source.Title,
            Kind = source.Kind,
            Definition = definition,
        }, cancellationToken);

        copy.Description = source.Description;
        copy.Tags = source.Tags.ToList();
        copy.Access = SurveyDefinitionClonerExtensions.CloneAccess(source.Access);
        copy.Retention = new SurveyRetentionSettings { Days = source.Retention?.Days, Action = source.Retention?.Action ?? RetentionAction.Delete };

        await UpdateAsync(copy, data: null, cancellationToken);

        return copy;
    }

    /// <inheritdoc />
    public async Task<SurveyErrorCode> ChangeStatusAsync(string surveyId, SurveyStatus status, CancellationToken cancellationToken = default)
    {
        var survey = await FindByIdAsync(surveyId, cancellationToken);

        if (survey is null)
        {
            return SurveyErrorCode.NotFound;
        }

        if (survey.Status == status)
        {
            return SurveyErrorCode.None;
        }

        if (!SurveyStatusTransitions.Manual.CanMove(survey.Status, status))
        {
            return SurveyErrorCode.InvalidState;
        }

        var previous = survey.Status;
        survey.Status = status;

        await UpdateAsync(survey, data: null, cancellationToken);

        foreach (var observer in _observers)
        {
            await observer.StatusChangedAsync(survey, previous);

            if (status == SurveyStatus.Closed)
            {
                await observer.ClosedAsync(survey);
            }
        }

        return SurveyErrorCode.None;
    }

    /// <inheritdoc />
    public async Task<SurveyErrorCode> DeleteSurveyAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        var survey = await FindByIdAsync(surveyId, cancellationToken);

        if (survey is null)
        {
            return SurveyErrorCode.NotFound;
        }

        if (await _responseQueries.CountForSurveyAsync(survey.ItemId, cancellationToken) > 0)
        {
            return SurveyErrorCode.HasResponses;
        }

        // The records that hang off the survey go first, a batch at a time, each committed on its own: invitations (with
        // e-mail addresses and names), accommodations, participation, counters, and test responses with their files.
        // Nothing revisits them once the survey is gone, and a delete that stops half way leaves the survey in place, so
        // deleting it again finishes the job.
        await PurgeAsync(survey.ItemId, cancellationToken);

        // A batch that ran on this scope's session committed it, which detaches what it had loaded: read the survey again.
        survey = await FindByIdAsync(surveyId, cancellationToken);

        if (survey is null)
        {
            return SurveyErrorCode.NotFound;
        }

        foreach (var collector in await _collectorStore.ListBySurveyAsync(survey.ItemId, cancellationToken))
        {
            await _collectorStore.DeleteAsync(collector, cancellationToken);
        }

        foreach (var version in await _versionStore.ListBySurveyAsync(survey.ItemId, cancellationToken))
        {
            await _versionStore.DeleteAsync(version, cancellationToken);
        }

        await DeleteAsync(survey, cancellationToken);

        foreach (var observer in _observers)
        {
            await observer.DeletedAsync(survey);
        }

        return SurveyErrorCode.None;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Survey>> ListByOwnerAsync(RecordRef owner, CancellationToken cancellationToken = default)
        => _store.ListByOwnerAsync(owner.ToString(), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Survey>> ListByOwnersAsync(IReadOnlyCollection<RecordRef> owners, CancellationToken cancellationToken = default)
        => _store.ListByOwnersAsync((owners ?? []).Where(owner => !owner.IsEmpty).Select(owner => owner.ToString()).ToArray(), cancellationToken);

    private async Task PurgeAsync(string surveyId, CancellationToken cancellationToken)
    {
        int removed;

        do
        {
            removed = 0;

            if (ShellScope.Current is null)
            {
                // Outside a shell (tools and tests) the batches run on this scope's session.
                removed = await _purger.DeleteBatchAsync(surveyId, SurveyDataPurger.BatchSize, cancellationToken);
            }
            else
            {
                await ShellScope.UsingChildScopeAsync(async scope =>
                {
                    removed = await scope.ServiceProvider.GetRequiredService<SurveyDataPurger>().DeleteBatchAsync(surveyId, SurveyDataPurger.BatchSize, cancellationToken);
                });
            }
        }
        while (removed >= SurveyDataPurger.BatchSize);
    }

    private async Task<SurveyDefinition> BuildFromTemplateAsync(string template, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(template))
        {
            return null;
        }

        foreach (var provider in _templateProviders)
        {
            var definition = await provider.BuildAsync(template, cancellationToken);

            if (definition is not null)
            {
                return definition;
            }
        }

        return null;
    }

    private async Task<string> UniqueNameAsync(string baseName, CancellationToken cancellationToken)
    {
        var candidate = baseName;

        for (var i = 2; await _store.FindByNameAsync(candidate, cancellationToken) is not null; i++)
        {
            candidate = $"{baseName}-{i}";
        }

        return candidate;
    }

    private static DateTime Truncate(DateTime value)
        => new(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Unspecified);
}
