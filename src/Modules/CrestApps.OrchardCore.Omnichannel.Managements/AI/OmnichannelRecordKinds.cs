using System.ComponentModel.DataAnnotations;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// The omnichannel setup records the generic record tools (<c>listRecords</c>, <c>getRecord</c>, <c>saveRecord</c>
/// and <c>deleteRecord</c>) work on: their kind names, the allow-listed fields each returns, and the rules their admin
/// pages apply around the editor (a unique name, the lead status flags moved after a save, the converted status kept,
/// no delete where the page has none). Cadences and addresses are read-only kinds: a cadence's editor posts its steps as
/// a list of objects the editor replay cannot post, and an address is created for an address type the record tools
/// cannot pass, so <c>saveOmnichannelCadence</c> and <c>saveOmnichannelAddress</c> write them, and their projections use
/// those tools' argument names.
/// </summary>
internal static class OmnichannelRecordKinds
{
    /// <summary>
    /// The kind of the lead statuses.
    /// </summary>
    public const string LeadStatusKind = "leadStatus";

    /// <summary>
    /// The kind of the opportunity stages.
    /// </summary>
    public const string OpportunityStageKind = "opportunityStage";

    /// <summary>
    /// The kind of the dispositions.
    /// </summary>
    public const string DispositionKind = "omnichannelDisposition";

    /// <summary>
    /// The kind of the campaign groups.
    /// </summary>
    public const string CampaignGroupKind = "omnichannelCampaignGroup";

    /// <summary>
    /// The kind of the campaigns.
    /// </summary>
    public const string CampaignKind = "omnichannelCampaign";

    /// <summary>
    /// The kind of the cadences, read-only.
    /// </summary>
    public const string CadenceKind = "omnichannelCadence";

    /// <summary>
    /// The kind of the business's own numbers and addresses, read-only.
    /// </summary>
    public const string AddressKind = "omnichannelAddress";

    /// <summary>
    /// Describes a lead status.
    /// </summary>
    /// <param name="status">The lead status.</param>
    /// <returns>The allow-listed fields.</returns>
    public static object DescribeLeadStatus(LeadStatus status)
        => new
        {
            id = status.ItemId,
            name = status.Name,
            description = status.Description,
            order = status.Order,
            kind = LeadStatusKinds.GetKind(status).ToString(),
            isDefault = status.IsDefault,
            isContacted = status.IsContacted,
            isClosed = status.IsClosed,
            isConverted = status.IsConverted,
        };

    /// <summary>
    /// Describes an opportunity stage.
    /// </summary>
    /// <param name="stage">The opportunity stage.</param>
    /// <returns>The allow-listed fields.</returns>
    public static object DescribeOpportunityStage(OpportunityStage stage)
        => new
        {
            id = stage.ItemId,
            name = stage.Name,
            description = stage.Description,
            order = stage.Order,
            probability = stage.Probability,
            isClosed = stage.IsClosed,
            isWon = stage.IsWon,
        };

    /// <summary>
    /// Describes a disposition.
    /// </summary>
    /// <param name="disposition">The disposition.</param>
    /// <returns>The allow-listed fields.</returns>
    public static object DescribeDisposition(OmnichannelDisposition disposition)
        => new
        {
            id = disposition.ItemId,
            name = disposition.Name,
            description = disposition.Description,
            outcome = disposition.Outcome.ToString(),
            captureDate = disposition.CaptureDate,
        };

    /// <summary>
    /// Describes a campaign group.
    /// </summary>
    /// <param name="group">The campaign group.</param>
    /// <returns>The allow-listed fields.</returns>
    public static object DescribeCampaignGroup(OmnichannelCampaignGroup group)
        => new
        {
            id = group.ItemId,
            displayText = group.DisplayText,
            description = group.Description,
        };

    /// <summary>
    /// Describes a campaign: what its editor changes (name, description, group) and, read-only, how its automated
    /// conversations are set up. None of it is a secret.
    /// </summary>
    /// <param name="campaign">The campaign.</param>
    /// <returns>The allow-listed fields.</returns>
    public static object DescribeCampaign(OmnichannelCampaign campaign)
        => new
        {
            id = campaign.ItemId,
            displayText = campaign.DisplayText,
            description = campaign.Description,
            campaignGroupId = campaign.CampaignGroupId,
            interactionType = campaign.InteractionType.ToString(),
            channel = campaign.Channel,
            campaignGoal = campaign.CampaignGoal,
            initialOutboundPromptPattern = campaign.InitialOutboundPromptPattern,
            ai = new
            {
                providerName = campaign.ProviderName,
                connectionName = campaign.ConnectionName,
                deploymentName = campaign.DeploymentName,
                systemMessage = campaign.SystemMessage,
                toolNames = campaign.ToolNames?.ToArray() ?? [],
                allowAIToUpdateContact = campaign.AllowAIToUpdateContact,
                allowAIToUpdateSubject = campaign.AllowAIToUpdateSubject,
                temperature = campaign.Temperature,
                topP = campaign.TopP,
                frequencyPenalty = campaign.FrequencyPenalty,
                presencePenalty = campaign.PresencePenalty,
                maxTokens = campaign.MaxTokens,
            },
        };

    /// <summary>
    /// Describes a cadence with the argument names <c>saveOmnichannelCadence</c> takes, so a cadence read with
    /// <c>getRecord</c> can be changed with that tool.
    /// </summary>
    /// <param name="cadence">The cadence.</param>
    /// <returns>The allow-listed fields.</returns>
    public static object DescribeCadence(Cadence cadence)
        => new
        {
            id = cadence.ItemId,
            displayText = cadence.DisplayText,
            description = cadence.Description,
            enabled = cadence.Enabled,
            steps = (cadence.Steps ?? [])
                .Where(step => step is not null)
                .Select(step => new
                {
                    delayMinutes = step.DelayMinutes,
                    isAiGenerated = step.IsAiGenerated,
                    message = step.Message,
                })
                .ToList(),
        };

    /// <summary>
    /// Describes one of the business's own numbers or addresses with the argument names <c>saveOmnichannelAddress</c>
    /// takes. The settings other features keep on an address (outbound-line agents, messaging line, provider number
    /// details) are named only, never returned.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>The allow-listed fields.</returns>
    public static object DescribeAddress(OmnichannelChannelEndpoint address)
        => new
        {
            id = address.ItemId,
            displayText = address.DisplayText,
            addressType = address.GetAddressType(),
            value = address.Value,
            capabilities = address.GetCapabilities().ToArray(),
            providerName = address.ProviderName,
            description = address.Description,
            featureSettings = address.Properties?.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray() ?? [],
        };

    /// <summary>
    /// Checks a lead status as the Lead Statuses page does before saving: the rules of the status's handlers, and that no
    /// other status has its name.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="status">The lead status, with the posted values bound.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The outcome of the checks.</returns>
    public static ValueTask<ValidationResultDetails> ValidateLeadStatusAsync(IServiceProvider services, LeadStatus status, CancellationToken cancellationToken)
        => ValidateUniqueNameAsync(services.GetRequiredService<INamedCatalogManager<LeadStatus>>(), status, status.Name, nameof(LeadStatus.Name), "A lead status with the same name already exists.", cancellationToken);

    /// <summary>
    /// Saves a lead status as the Lead Statuses page does, then moves the default, contacted and converted flags off
    /// every other status, as the page does after a save.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="status">The validated lead status.</param>
    /// <param name="isNew">Whether the status is new.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>A task that completes when the status is saved.</returns>
    public static async ValueTask SaveLeadStatusAsync(IServiceProvider services, LeadStatus status, bool isNew, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<INamedCatalogManager<LeadStatus>>();

        if (isNew)
        {
            await manager.CreateAsync(status, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(status, null, cancellationToken);
        }

        await services.GetRequiredService<LeadStatusFlagService>().EnforceAsync(status);
    }

    /// <summary>
    /// Keeps the converted status, as the Lead Statuses page does: converting a lead needs it.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="status">The lead status to delete.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>Why the status cannot be deleted, or <see langword="null"/> when it can.</returns>
    public static ValueTask<string> GetLeadStatusUseAsync(IServiceProvider services, LeadStatus status, CancellationToken cancellationToken)
        => ValueTask.FromResult(status.IsConverted
            ? "The converted status cannot be deleted, because converting a lead needs it. Mark another status as the converted status first."
            : null);

    /// <summary>
    /// Checks an opportunity stage as the Opportunity Stages page does before saving: the rules of the stage's handlers,
    /// and that no other stage has its name.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="stage">The opportunity stage, with the posted values bound.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The outcome of the checks.</returns>
    public static ValueTask<ValidationResultDetails> ValidateOpportunityStageAsync(IServiceProvider services, OpportunityStage stage, CancellationToken cancellationToken)
        => ValidateUniqueNameAsync(services.GetRequiredService<INamedCatalogManager<OpportunityStage>>(), stage, stage.Name, nameof(OpportunityStage.Name), "An opportunity stage with the same name already exists.", cancellationToken);

    /// <summary>
    /// Checks a disposition as the Dispositions page does before saving: the rules of the disposition's handlers, and
    /// that no other disposition has its name.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="disposition">The disposition, with the posted values bound.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The outcome of the checks.</returns>
    public static ValueTask<ValidationResultDetails> ValidateDispositionAsync(IServiceProvider services, OmnichannelDisposition disposition, CancellationToken cancellationToken)
        => ValidateUniqueNameAsync(services.GetRequiredService<INamedCatalogManager<OmnichannelDisposition>>(), disposition, disposition.Name, nameof(OmnichannelDisposition.Name), "A disposition with the same name already exists.", cancellationToken);

    /// <summary>
    /// Checks a campaign as the Campaigns page does before saving: the rules of the campaign's handlers, and, as the
    /// editor offers only existing groups, that a group it is given exists. A group the campaign already had is not
    /// checked again.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="campaign">The campaign, with the posted values bound.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The outcome of the checks.</returns>
    public static async ValueTask<ValidationResultDetails> ValidateCampaignAsync(IServiceProvider services, OmnichannelCampaign campaign, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<ICatalogManager<OmnichannelCampaign>>();
        var result = await manager.ValidateAsync(campaign, cancellationToken) ?? new ValidationResultDetails();

        if (string.IsNullOrEmpty(campaign.CampaignGroupId))
        {
            return result;
        }

        var stored = string.IsNullOrEmpty(campaign.ItemId)
            ? null
            : await manager.FindByIdAsync(campaign.ItemId, cancellationToken);

        if (stored is not null && string.Equals(stored.CampaignGroupId, campaign.CampaignGroupId, StringComparison.OrdinalIgnoreCase))
        {
            return result;
        }

        if (await services.GetRequiredService<ICatalogManager<OmnichannelCampaignGroup>>().FindByIdAsync(campaign.CampaignGroupId, cancellationToken) is null)
        {
            result.Fail(new ValidationResult(
                $"No campaign group has the ID '{campaign.CampaignGroupId}'. Use listRecords with the kind {CampaignGroupKind} to find one.",
                [nameof(OmnichannelCampaign.CampaignGroupId)]));
        }

        return result;
    }

    /// <summary>
    /// Refuses to delete a campaign: the Campaigns page offers no delete.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="campaign">The campaign.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>Why the campaign cannot be deleted.</returns>
    public static ValueTask<string> RefuseCampaignDeleteAsync(IServiceProvider services, OmnichannelCampaign campaign, CancellationToken cancellationToken)
        => ValueTask.FromResult("Campaigns cannot be deleted: the Campaigns admin page offers no delete. Change its name or group instead.");

    /// <summary>
    /// Refuses to delete a campaign group: the Campaign Groups page offers no delete.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="group">The campaign group.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>Why the group cannot be deleted.</returns>
    public static ValueTask<string> RefuseCampaignGroupDeleteAsync(IServiceProvider services, OmnichannelCampaignGroup group, CancellationToken cancellationToken)
        => ValueTask.FromResult("Campaign groups cannot be deleted: the Campaign Groups admin page offers no delete. Rename the group instead.");

    // The rule the Lead Statuses, Opportunity Stages and Dispositions pages add to the record's own: a second record with
    // the same name is refused.
    private static async ValueTask<ValidationResultDetails> ValidateUniqueNameAsync<T>(
        INamedCatalogManager<T> manager,
        T record,
        string name,
        string member,
        string message,
        CancellationToken cancellationToken)
        where T : CatalogItem, INameAwareModel
    {
        var result = await manager.ValidateAsync(record, cancellationToken) ?? new ValidationResultDetails();

        if (string.IsNullOrEmpty(name))
        {
            return result;
        }

        var existing = await manager.FindByNameAsync(name, cancellationToken);

        if (existing is not null && !string.Equals(existing.ItemId, record.ItemId, StringComparison.OrdinalIgnoreCase))
        {
            result.Fail(new ValidationResult(message, [member]));
        }

        return result;
    }
}
