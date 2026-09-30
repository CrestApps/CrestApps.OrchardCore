using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Lists.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Keeps the list of every account type offering the contact and opportunity types. A type is added to an
/// account's list once, the first time it is seen, and recorded as offered, so an administrator who removes it from
/// the list keeps it removed. Lead types are never contained, and are taken out of the list when they become leads.
/// </summary>
internal sealed class CrmAccountListSynchronizer
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrmAccountListSynchronizer"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="logger">The logger.</param>
    public CrmAccountListSynchronizer(
        IContentDefinitionManager contentDefinitionManager,
        ILogger<CrmAccountListSynchronizer> logger)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _logger = logger;
    }

    /// <summary>
    /// Brings every account type's list up to date with the current contact, lead and opportunity types.
    /// </summary>
    public async Task SynchronizeAsync()
    {
        var definitions = await _contentDefinitionManager.ListTypeDefinitionsAsync();

        var accountChildTypes = definitions
            .Where(OmnichannelRecordKinds.IsAccountChild)
            .Select(definition => definition.Name)
            .ToArray();

        var leadTypes = definitions
            .Where(definition => OmnichannelRecordKinds.HasPart(definition, OmnichannelConstants.ContentParts.Lead))
            .Select(definition => definition.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var accountType in definitions.Where(OmnichannelRecordKinds.IsAccount))
        {
            await SynchronizeAsync(accountType, accountChildTypes, leadTypes);
        }
    }

    private async Task SynchronizeAsync(ContentTypeDefinition accountType, string[] accountChildTypes, HashSet<string> leadTypes)
    {
        var listPart = accountType.Parts.FirstOrDefault(part => part.PartDefinition?.Name == OmnichannelConstants.ContentParts.List);
        var accountPart = accountType.Parts.FirstOrDefault(part => part.PartDefinition?.Name == OmnichannelConstants.ContentParts.Account);

        if (listPart is null || accountPart is null)
        {
            return;
        }

        var listSettings = listPart.GetSettings<ListPartSettings>();
        var accountSettings = accountPart.GetSettings<AccountPartSettings>();

        var contained = (listSettings.ContainedContentTypes ?? []).ToList();
        var offered = (accountSettings.OfferedContentTypes ?? []).ToHashSet(StringComparer.Ordinal);

        var added = accountChildTypes.Where(type => !offered.Contains(type)).ToArray();
        var removed = contained.Where(leadTypes.Contains).ToArray();

        if (added.Length == 0 && removed.Length == 0)
        {
            return;
        }

        foreach (var type in added)
        {
            offered.Add(type);

            if (!contained.Contains(type, StringComparer.Ordinal))
            {
                contained.Add(type);
            }
        }

        contained.RemoveAll(leadTypes.Contains);

        listSettings.ContainedContentTypes = contained.ToArray();
        accountSettings.OfferedContentTypes = offered.Order(StringComparer.Ordinal).ToArray();

        await _contentDefinitionManager.AlterTypeDefinitionAsync(accountType.Name, type => type
            .WithPart(listPart.Name, listPart.PartDefinition.Name, part => part.WithSettings(listSettings))
            .WithPart(accountPart.Name, accountPart.PartDefinition.Name, part => part.WithSettings(accountSettings)));

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Updated the list of the '{AccountType}' account type: added {AddedTypes}, removed lead types {RemovedTypes}.",
                accountType.Name,
                string.Join(", ", added),
                string.Join(", ", removed));
        }
    }
}
