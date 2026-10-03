using CrestApps.Core.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Turns the channel endpoints saved with one channel each into omnichannel addresses with a type and capabilities, and
/// merges the records that listed the same number once per channel into one address with every capability.
/// </summary>
/// <remarks>
/// The merged-away records' identifiers stay on the surviving address, so activities and history that name them still
/// find it. Configuration that names one is repointed here: inventory loads and subject flows. Features with their own
/// references, such as queues, repoint theirs in their own migrations.
/// </remarks>
internal sealed class OmnichannelAddressMigrations : DataMigration
{
    private readonly IDocumentManager<DictionaryDocument<OmnichannelChannelEndpoint>> _documentManager;
    private readonly ICatalog<OmnichannelActivityBatch> _batchCatalog;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelAddressMigrations"/> class.
    /// </summary>
    /// <param name="documentManager">The document that holds the address list.</param>
    /// <param name="batchCatalog">The inventory loads, which name the address they send from.</param>
    /// <param name="contentDefinitionManager">The content definitions holding the subject flows.</param>
    /// <param name="phoneNumberService">The phone number service, to compare numbers in one form.</param>
    /// <param name="logger">The logger.</param>
    public OmnichannelAddressMigrations(
        IDocumentManager<DictionaryDocument<OmnichannelChannelEndpoint>> documentManager,
        ICatalog<OmnichannelActivityBatch> batchCatalog,
        IContentDefinitionManager contentDefinitionManager,
        IPhoneNumberService phoneNumberService,
        ILogger<OmnichannelAddressMigrations> logger)
    {
        _documentManager = documentManager;
        _batchCatalog = batchCatalog;
        _contentDefinitionManager = contentDefinitionManager;
        _phoneNumberService = phoneNumberService;
        _logger = logger;
    }

    /// <summary>
    /// Consolidates the address list and repoints the configuration that named a merged-away record.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        var document = await _documentManager.GetOrCreateMutableAsync();

        var retired = OmnichannelAddressConsolidator.Consolidate(document.Records, Canonicalize);

        await _documentManager.UpdateAsync(document);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Brought {Count} omnichannel address(es) forward to address types and capabilities, merging {Merged} record(s) that listed the same address once per channel.",
                document.Records.Count,
                retired.Count);
        }

        if (retired.Count > 0)
        {
            await RepointBatchesAsync(retired);
            await RepointSubjectFlowsAsync(retired);
        }

        return 1;
    }

    private string Canonicalize(string addressType, string value)
        => addressType == OmnichannelAddressTypes.PhoneNumber && _phoneNumberService.TryParse(value, null, out var canonical)
            ? canonical.Value
            : value;

    private async Task RepointBatchesAsync(IReadOnlyDictionary<string, string> retired)
    {
        foreach (var batch in await _batchCatalog.GetAllAsync())
        {
            if (!string.IsNullOrEmpty(batch.ChannelEndpointId) && retired.TryGetValue(batch.ChannelEndpointId, out var survivorId))
            {
                batch.ChannelEndpointId = survivorId;
                await _batchCatalog.UpdateAsync(batch);
            }
        }
    }

    private async Task RepointSubjectFlowsAsync(IReadOnlyDictionary<string, string> retired)
    {
        foreach (var type in await _contentDefinitionManager.ListTypeDefinitionsAsync())
        {
            var part = type.Parts.FirstOrDefault(candidate => candidate.Name == OmnichannelConstants.ContentParts.OmnichannelSubject);

            if (part is null)
            {
                continue;
            }

            var settings = part.GetSettings<OmnichannelSubjectPartSettings>();

            if (string.IsNullOrEmpty(settings.ChannelEndpointId) || !retired.TryGetValue(settings.ChannelEndpointId, out var survivorId))
            {
                continue;
            }

            settings.ChannelEndpointId = survivorId;

            await _contentDefinitionManager.AlterTypeDefinitionAsync(type.Name, builder => builder
                .WithPart(part.Name, partBuilder => partBuilder.WithSettings(settings)));
        }
    }
}
