using System.Diagnostics;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Telemetry;
using CrestApps.OrchardCore.Omnichannel.Managements.Telemetry;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Default implementation of <see cref="IActivityBatchLoadCoordinator"/> that selects a source-specific
/// <see cref="IActivityBatchLoader"/> when one is registered, and otherwise falls back to the
/// <see cref="DefaultContactActivityBatchLoader"/>.
/// </summary>
public sealed class DefaultActivityBatchLoadCoordinator : IActivityBatchLoadCoordinator
{
    private readonly ICatalog<OmnichannelActivityBatch> _catalog;
    private readonly DefaultContactActivityBatchLoader _defaultLoader;
    private readonly ActivityBatchLoadRegistry _registry;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly Dictionary<string, IActivityBatchLoader> _loaders;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultActivityBatchLoadCoordinator"/> class.
    /// </summary>
    /// <param name="catalog">The activity batch catalog.</param>
    /// <param name="loaders">The registered source-specific activity batch loaders.</param>
    /// <param name="defaultLoader">The default contact-based activity batch loader used as a fallback.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="clock">The clock used to stamp a load's progress.</param>
    /// <param name="registry">The loads this process is running, which a load registers itself in while it runs.</param>
    public DefaultActivityBatchLoadCoordinator(
        ICatalog<OmnichannelActivityBatch> catalog,
        IEnumerable<IActivityBatchLoader> loaders,
        DefaultContactActivityBatchLoader defaultLoader,
        ILogger<DefaultActivityBatchLoadCoordinator> logger,
        IClock clock,
        ActivityBatchLoadRegistry registry = null)
    {
        _catalog = catalog;
        _defaultLoader = defaultLoader;
        _registry = registry ?? new ActivityBatchLoadRegistry();
        _clock = clock;
        _logger = logger;
        _loaders = loaders
            .Where(loader => !string.IsNullOrEmpty(loader.Source))
            .GroupBy(loader => loader.Source, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task LoadAsync(
        string batchId,
        string loaderId,
        string loaderUserName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(batchId);

        var batch = await _catalog.FindByIdAsync(batchId, cancellationToken);

        if (batch is null)
        {
            _logger.LogError("Unable to load activities. No activity batch was found with the ID '{BatchId}'.", batchId);

            return;
        }

        if (batch.Status != OmnichannelActivityBatchStatus.Started)
        {
            throw new InvalidOperationException($"Unable to load activities for batch with the ID '{batch.ItemId}' since its status is not '{nameof(OmnichannelActivityBatchStatus.Started)}'.");
        }

        // A reload starts from nothing, so the counts shown afterwards describe this load and not the sum of every
        // load the batch has been through.
        batch.ResetLoadCounts();
        batch.LoadError = null;
        batch.LoadCursor = null;
        batch.LoadStartedById ??= loaderId;
        batch.LoadStartedByUsername ??= loaderUserName;

        await RunAsync(batch, new ActivityBatchLoadContext(batch, loaderId, loaderUserName), cancellationToken);
    }

    /// <inheritdoc />
    public async Task ResumeAsync(
        string batchId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(batchId);

        var batch = await _catalog.FindByIdAsync(batchId, cancellationToken);

        if (batch is null)
        {
            return;
        }

        if (batch.Status == OmnichannelActivityBatchStatus.Started)
        {
            // Nothing a load that never got past starting did was committed, so it simply runs.
            await LoadAsync(batchId, batch.LoadStartedById, batch.LoadStartedByUsername, cancellationToken);

            return;
        }

        if (batch.Status != OmnichannelActivityBatchStatus.Loading)
        {
            return;
        }

        var context = new ActivityBatchLoadContext(batch, batch.LoadStartedById, batch.LoadStartedByUsername)
        {
            IsResume = true,
        };

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Resuming the activity load '{BatchId}' after record {LoadCursor}, where it stopped recording progress; it had loaded {TotalLoaded} activities.",
                batch.ItemId,
                batch.LoadCursor ?? 0,
                batch.TotalLoaded ?? 0);
        }

        await RunAsync(batch, context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ActivityBatchLoadPreview> PreviewAsync(
        OmnichannelActivityBatch batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        // Only the default loader knows how to count without writing; a source's own loader would load for real.
        if (ResolveLoader(batch.Source) is not DefaultContactActivityBatchLoader loader)
        {
            return new ActivityBatchLoadPreview
            {
                Error = "Records cannot be counted ahead of loading for this kind of activity load.",
            };
        }

        batch.ResetLoadCounts();

        var context = new ActivityBatchLoadContext(batch, loaderId: null, loaderUserName: null)
        {
            IsPreview = true,
        };

        // Counted in the request that asked, so its time is the time the editor waits for the count.
        using (var telemetry = TelemetryScope.Start(OmnichannelTelemetry.ActivitySource, "crestapps.omnichannel.batch.load.preview", OmnichannelTelemetry.BatchLoadPreviewDuration))
        {
            try
            {
                await loader.LoadAsync(context, cancellationToken);

                telemetry.SetOutcome(context.StopReason is null
                    ? CrestAppsOrchardCoreTelemetry.OutcomeOk
                    : OmnichannelTelemetry.OutcomeStopped);
                telemetry.SetSpanTag("crestapps.omnichannel.batch.matched", batch.TotalMatched ?? 0);
                OmnichannelTelemetry.Record(OmnichannelTelemetry.BatchLoadPreviewMatched, batch.TotalMatched ?? 0);
            }
            catch (Exception ex)
            {
                telemetry.Fail(ex);

                throw;
            }
        }

        return new ActivityBatchLoadPreview
        {
            Error = context.StopReason,
            Matched = batch.TotalMatched ?? 0,
            WouldLoad = batch.TotalLoaded ?? 0,
            SkippedAsDuplicate = batch.TotalSkippedAsDuplicate,
            SkippedAsOptedOut = batch.TotalSkippedAsOptedOut + batch.TotalSkippedAsSharedNumberOptedOut,
            SkippedForNoDestination = batch.TotalSkippedForNoDestination,
            SkippedAsNotInService = batch.TotalSkippedAsNotInService,
            SkippedAsLead = batch.TotalSkippedAsConverted + batch.TotalSkippedAsExistingContact,
            SkippedByLimit = batch.TotalSkippedByLimit,
        };
    }

    private async Task RunAsync(OmnichannelActivityBatch batch, ActivityBatchLoadContext context, CancellationToken cancellationToken)
    {
        var trigger = context.IsResume
            ? OmnichannelTelemetry.TriggerResume
            : OmnichannelTelemetry.TriggerStart;

        if (!_registry.TryBegin(batch.ItemId))
        {
            // This process is running the load already; a second run would load its records twice. It used to return
            // without a word, which left a second Load click looking as if it had been ignored.
            if (_logger.IsEnabled(LogLevel.Information))
            {
                var batchId = batch.ItemId.SanitizeLogValue();

                OmnichannelLog.BatchLoadAlreadyRunning(_logger, batchId);
            }

            OmnichannelTelemetry.Add(OmnichannelTelemetry.BatchLoads, 1, new TagList
            {
                { CrestAppsOrchardCoreTelemetry.OutcomeTag, CrestAppsOrchardCoreTelemetry.OutcomeSkipped },
                { CrestAppsOrchardCoreTelemetry.TriggerTag, trigger },
            });

            return;
        }

        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["BatchId"] = batch.ItemId,
        });

        using var telemetry = TelemetryScope.Start(
            OmnichannelTelemetry.ActivitySource,
            "crestapps.omnichannel.batch.load",
            OmnichannelTelemetry.BatchLoadDuration,
            new TagList
            {
                { CrestAppsOrchardCoreTelemetry.TriggerTag, trigger },
            });

        telemetry.SetSpanTag(OmnichannelTelemetry.BatchIdSpanTag, batch.ItemId);

        try
        {
            var loader = ResolveLoader(batch.Source);

            batch.Status = OmnichannelActivityBatchStatus.Loading;
            batch.LoadHeartbeatUtc = _clock.UtcNow;

            await _catalog.UpdateAsync(batch, cancellationToken);

            try
            {
                await loader.LoadAsync(context, cancellationToken);

                telemetry.SetOutcome(context.StopReason is null
                    ? OmnichannelTelemetry.OutcomeCompleted
                    : OmnichannelTelemetry.OutcomeStopped);
            }
            catch (Exception ex)
            {
                telemetry.Fail(ex);

                _logger.LogError(ex, "An error occurred while loading activities for the batch with ID '{BatchId}'.", batch.ItemId);

                // The loader can have committed part-way through, and a committed session no longer tracks the batch
                // read above; saving it would store a second document. The batch is read again as the commit left it.
                var current = await _catalog.FindByIdAsync(batch.ItemId, cancellationToken) ?? batch;

                // Said on the load itself: a load that only went back to New, with the reason in a log the operator
                // cannot see, looked like it had never been started.
                current.Status = OmnichannelActivityBatchStatus.New;
                current.LoadError = $"The load stopped because of an error after loading {current.TotalLoaded ?? 0} activities: {ex.GetBaseException().Message}";

                await _catalog.UpdateAsync(current, cancellationToken);
            }
        }
        finally
        {
            _registry.End(batch.ItemId);

            OmnichannelTelemetry.Add(OmnichannelTelemetry.BatchLoads, 1, new TagList
            {
                { CrestAppsOrchardCoreTelemetry.OutcomeTag, telemetry.Outcome },
                { CrestAppsOrchardCoreTelemetry.TriggerTag, trigger },
            });

            if (_logger.IsEnabled(LogLevel.Information))
            {
                var batchId = batch.ItemId.SanitizeLogValue();

                OmnichannelLog.BatchLoadEnded(_logger, trigger, batchId, telemetry.Outcome, telemetry.Elapsed.TotalSeconds);
            }
        }
    }

    private IActivityBatchLoader ResolveLoader(string source)
    {
        if (!string.IsNullOrEmpty(source) && _loaders.TryGetValue(source, out var loader))
        {
            return loader;
        }

        return _defaultLoader;
    }
}
