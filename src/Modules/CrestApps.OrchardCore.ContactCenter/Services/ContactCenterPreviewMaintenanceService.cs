using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Maintenance;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterPreviewMaintenanceService"/>.
/// </summary>
/// <remarks>
/// The four steps are deliberately coupled rather than independent. An export returns a receipt derived from
/// the exact per-data-set counts it captured; a reset recomputes that receipt from live state and refuses when
/// it no longer matches. A tenant that is still admitting work keeps changing those counts, so a reset cannot
/// succeed until admission is quiesced and an export has been taken against the quiesced state. That makes
/// "export, quiesce, reset, verify" a mechanically enforced order rather than a documented suggestion.
/// </remarks>
public sealed class ContactCenterPreviewMaintenanceService : IContactCenterPreviewMaintenanceService
{
    private const string ExportSchemaVersion = "contact-center-preview-export/v1";

    private readonly IEnumerable<IContactCenterPreviewDataSet> _dataSets;
    private readonly IEnumerable<IContactCenterFeatureLifecycleParticipant> _participants;
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly ShellSettings _shellSettings;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ContactCenterPreviewMaintenanceOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterPreviewMaintenanceService"/> class.
    /// </summary>
    /// <param name="dataSets">The registered preview data sets.</param>
    /// <param name="participants">The Contact Center feature lifecycle participants of the enabled features.</param>
    /// <param name="workManager">The Contact Center feature work manager.</param>
    /// <param name="shellSettings">The current tenant's shell settings.</param>
    /// <param name="hostEnvironment">The host environment.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="options">The preview maintenance options.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterPreviewMaintenanceService(
        IEnumerable<IContactCenterPreviewDataSet> dataSets,
        IEnumerable<IContactCenterFeatureLifecycleParticipant> participants,
        IContactCenterFeatureWorkManager workManager,
        ShellSettings shellSettings,
        IHostEnvironment hostEnvironment,
        ISession session,
        IClock clock,
        IOptions<ContactCenterPreviewMaintenanceOptions> options,
        ILogger<ContactCenterPreviewMaintenanceService> logger)
    {
        _dataSets = dataSets;
        _participants = participants;
        _workManager = workManager;
        _shellSettings = shellSettings;
        _hostEnvironment = hostEnvironment;
        _session = session;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContactCenterPreviewDataSetCount>> GetDataSetCountsAsync(CancellationToken cancellationToken = default)
    {
        var counts = new List<ContactCenterPreviewDataSetCount>();

        foreach (var dataSet in OrderedDataSets())
        {
            counts.Add(new ContactCenterPreviewDataSetCount
            {
                Key = dataSet.Key,
                GovernanceCategoryKey = dataSet.GovernanceCategoryKey,
                IsConfiguration = dataSet.IsConfiguration,
                Count = await dataSet.CountAsync(cancellationToken),
            });
        }

        return counts;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterPreviewMaintenanceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var featureIds = ParticipatingFeatureIds();

        return new ContactCenterPreviewMaintenanceStatus
        {
            TenantName = _shellSettings.Name,
            DataSets = await GetDataSetCountsAsync(cancellationToken),
            ParticipatingFeatureIds = featureIds,
            QuiescedFeatureIds = featureIds.Where(_workManager.IsQuiescing).ToList(),
            IsResetAllowed = _options.AllowReset,
            IsProductionRefusal = _options.RefuseResetInProduction && _hostEnvironment.IsProduction(),
        };
    }

    /// <inheritdoc/>
    public async Task<ContactCenterPreviewExportReport> ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var counts = await GetDataSetCountsAsync(cancellationToken);
        var takenUtc = _clock.UtcNow;
        var receipt = ComputeReceipt(_shellSettings.Name, counts);
        var documentCount = 0;

        await using var writer = new Utf8JsonWriter(destination, new JsonWriterOptions
        {
            Indented = true,
        });

        writer.WriteStartObject();
        writer.WriteString("schemaVersion", ExportSchemaVersion);
        writer.WriteString("tenant", _shellSettings.Name);
        writer.WriteString("takenUtc", takenUtc.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("receipt", receipt);
        writer.WriteStartArray("dataSets");

        foreach (var dataSet in OrderedDataSets())
        {
            var expected = counts.First(count => count.Key == dataSet.Key);

            writer.WriteStartObject();
            writer.WriteString("key", dataSet.Key);
            writer.WriteString("governanceCategory", dataSet.GovernanceCategoryKey);
            writer.WriteBoolean("isConfiguration", dataSet.IsConfiguration);
            writer.WriteNumber("count", expected.Count);
            writer.WriteStartArray("documents");

            var written = 0;

            while (written < expected.Count)
            {
                var page = await dataSet.ReadPageAsync(written, _options.PageSize, cancellationToken);

                if (page.Count == 0)
                {
                    break;
                }

                foreach (var document in page)
                {
                    JsonSerializer.Serialize(writer, document, document.GetType());
                    written++;
                }

                await writer.FlushAsync(cancellationToken);
            }

            documentCount += written;

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Exported {DocumentCount} Contact Center documents from tenant '{TenantName}' with receipt {Receipt}.",
                documentCount,
                _shellSettings.Name,
                receipt);
        }

        return new ContactCenterPreviewExportReport
        {
            TenantName = _shellSettings.Name,
            TakenUtc = takenUtc,
            DataSets = counts,
            DocumentCount = documentCount,
            Receipt = receipt,
        };
    }

    /// <inheritdoc/>
    public async Task<ContactCenterPreviewQuiesceReport> QuiesceAsync(TimeSpan drainTimeout, CancellationToken cancellationToken = default)
    {
        if (drainTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(drainTimeout), "The drain timeout must be greater than zero.");
        }

        var featureIds = ParticipatingFeatureIds();
        var timedOut = new List<string>();

        foreach (var featureId in featureIds)
        {
            _workManager.Quiesce(featureId);
        }

        foreach (var participant in _participants)
        {
            await participant.QuiesceAsync(cancellationToken);
        }

        foreach (var participant in _participants)
        {
            try
            {
                await participant.DrainAsync(cancellationToken).WaitAsync(drainTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                if (!timedOut.Contains(participant.FeatureId, StringComparer.Ordinal))
                {
                    timedOut.Add(participant.FeatureId);
                }
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Quiesced {FeatureCount} Contact Center features on tenant '{TenantName}'; {TimedOutCount} did not drain in time.",
                featureIds.Count,
                _shellSettings.Name,
                timedOut.Count);
        }

        return new ContactCenterPreviewQuiesceReport
        {
            QuiescedFeatureIds = featureIds,
            DrainTimedOutFeatureIds = timedOut,
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ResumeAsync()
    {
        var featureIds = ParticipatingFeatureIds();

        foreach (var participant in _participants)
        {
            await participant.ReconcileAsync();
        }

        foreach (var featureId in featureIds)
        {
            _workManager.Activate(featureId);
        }

        return featureIds;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterPreviewResetReport> ResetAsync(ContactCenterPreviewResetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var refusal = await EvaluateRefusalAsync(request, cancellationToken);

        if (refusal != ContactCenterPreviewResetRefusalReason.None)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Refused a Contact Center preview reset on tenant '{TenantName}' because {RefusalReason}.",
                    _shellSettings.Name,
                    refusal);
            }

            return new ContactCenterPreviewResetReport
            {
                TenantName = _shellSettings.Name,
                RefusalReason = refusal,
                Scope = request.Scope,
                DeletedByDataSet = new Dictionary<string, int>(StringComparer.Ordinal),
                PreservedDataSetKeys = [],
            };
        }

        var deleted = new Dictionary<string, int>(StringComparer.Ordinal);
        var preserved = new List<string>();

        foreach (var dataSet in OrderedDataSets())
        {
            if (!IsInScope(dataSet, request.Scope))
            {
                preserved.Add(dataSet.Key);

                continue;
            }

            deleted[dataSet.Key] = await dataSet.DeleteAllAsync(cancellationToken);
        }

        await _session.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(
                "Reset Contact Center {Scope} data on tenant '{TenantName}', deleting {DeletedCount} documents.",
                request.Scope,
                _shellSettings.Name,
                deleted.Values.Sum());
        }

        return new ContactCenterPreviewResetReport
        {
            TenantName = _shellSettings.Name,
            RefusalReason = ContactCenterPreviewResetRefusalReason.None,
            Scope = request.Scope,
            DeletedByDataSet = deleted,
            PreservedDataSetKeys = preserved,
        };
    }

    /// <inheritdoc/>
    public async Task<ContactCenterPreviewVerificationReport> VerifyAsync(ContactCenterPreviewResetScope scope, CancellationToken cancellationToken = default)
    {
        var counts = await GetDataSetCountsAsync(cancellationToken);
        var residual = new List<string>();

        foreach (var count in counts)
        {
            var inScope = scope == ContactCenterPreviewResetScope.All || !count.IsConfiguration;

            if (inScope && count.Count > 0)
            {
                residual.Add(count.Key);
            }
        }

        return new ContactCenterPreviewVerificationReport
        {
            TenantName = _shellSettings.Name,
            Scope = scope,
            DataSets = counts,
            ResidualDataSetKeys = residual,
        };
    }

    private async Task<ContactCenterPreviewResetRefusalReason> EvaluateRefusalAsync(
        ContactCenterPreviewResetRequest request,
        CancellationToken cancellationToken)
    {
        if (!_options.AllowReset)
        {
            return ContactCenterPreviewResetRefusalReason.ResetNotAllowed;
        }

        if (_options.RefuseResetInProduction && _hostEnvironment.IsProduction())
        {
            return ContactCenterPreviewResetRefusalReason.ProductionEnvironment;
        }

        if (!string.Equals(request.ConfirmationToken, _shellSettings.Name, StringComparison.Ordinal))
        {
            return ContactCenterPreviewResetRefusalReason.ConfirmationTokenMismatch;
        }

        var featureIds = ParticipatingFeatureIds();

        if (featureIds.Any(featureId => !_workManager.IsQuiescing(featureId)))
        {
            return ContactCenterPreviewResetRefusalReason.WorkNotQuiesced;
        }

        if (string.IsNullOrEmpty(request.ExportReceipt))
        {
            return ContactCenterPreviewResetRefusalReason.ExportReceiptMissing;
        }

        var counts = await GetDataSetCountsAsync(cancellationToken);
        var liveReceipt = ComputeReceipt(_shellSettings.Name, counts);

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(liveReceipt),
            Encoding.UTF8.GetBytes(request.ExportReceipt)))
        {
            return ContactCenterPreviewResetRefusalReason.ExportReceiptStale;
        }

        return ContactCenterPreviewResetRefusalReason.None;
    }

    private static bool IsInScope(IContactCenterPreviewDataSet dataSet, ContactCenterPreviewResetScope scope)
        => scope == ContactCenterPreviewResetScope.All || !dataSet.IsConfiguration;

    private List<IContactCenterPreviewDataSet> OrderedDataSets()
        => _dataSets.OrderBy(dataSet => dataSet.Key, StringComparer.Ordinal).ToList();

    private List<string> ParticipatingFeatureIds()
        => _participants
            .Select(participant => participant.FeatureId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

    private static string ComputeReceipt(string tenantName, IReadOnlyList<ContactCenterPreviewDataSetCount> counts)
    {
        var builder = new StringBuilder();
        builder.Append(ExportSchemaVersion);
        builder.Append('\n');
        builder.Append(tenantName);

        foreach (var count in counts.OrderBy(count => count.Key, StringComparer.Ordinal))
        {
            builder.Append('\n');
            builder.Append(count.Key);
            builder.Append('=');
            builder.Append(count.Count.ToString(CultureInfo.InvariantCulture));
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));

        return Convert.ToHexStringLower(digest);
    }
}
