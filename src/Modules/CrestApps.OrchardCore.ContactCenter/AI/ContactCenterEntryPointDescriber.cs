using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Describes inbound entry points for the setup tools, from an explicit list of fields. The settings other features keep
/// on an entry point (messaging, the AI agent for texts or calls) are named, never returned.
/// </summary>
internal sealed class ContactCenterEntryPointDescriber
{
    private readonly ILocalClock _localClock;
    private readonly IReadOnlyCollection<OmnichannelChannelEndpoint> _addresses;
    private readonly Dictionary<string, string> _queueNames;
    private readonly Dictionary<string, string> _agentNames;
    private readonly Dictionary<string, string> _calendarNames;

    private ContactCenterEntryPointDescriber(
        ILocalClock localClock,
        IReadOnlyCollection<OmnichannelChannelEndpoint> addresses,
        Dictionary<string, string> queueNames,
        Dictionary<string, string> agentNames,
        Dictionary<string, string> calendarNames)
    {
        _localClock = localClock;
        _addresses = addresses;
        _queueNames = queueNames;
        _agentNames = agentNames;
        _calendarNames = calendarNames;
    }

    /// <summary>
    /// Reads the names an entry point refers to.
    /// </summary>
    public static async Task<ContactCenterEntryPointDescriber> CreateAsync(IServiceProvider services, CancellationToken cancellationToken)
        => new(
            services.GetService<ILocalClock>(),
            services.GetService<IOmnichannelChannelEndpointStore>() is { } store ? await store.GetAllAsync(cancellationToken) : [],
            await ContactCenterSetupLookups.GetQueueNamesAsync(services, cancellationToken),
            await ContactCenterSetupLookups.GetAgentNamesAsync(services, cancellationToken),
            await ContactCenterSetupLookups.GetCalendarNamesAsync(services, cancellationToken));

    /// <summary>
    /// Describes an entry point in full, with the names of the records it refers to, for the result of a save.
    /// </summary>
    public async Task<object> DescribeAsync(ContactCenterEntryPoint entryPoint)
        => new
        {
            id = entryPoint.ItemId,
            name = entryPoint.Name,
            description = entryPoint.Description,
            enabled = entryPoint.Enabled,
            channel = entryPoint.GetChannel(),
            addresses = DescribeAddresses(entryPoint),
            dialedNumbers = entryPoint.DialedNumbers ?? [],
            target = DescribeTarget(entryPoint),
            priority = entryPoint.Priority.ToString(),
            businessHoursCalendar = ContactCenterSetupLookups.Reference(entryPoint.BusinessHoursCalendarId, _calendarNames),
            closedAction = entryPoint.ClosedAction.ToString(),
            overflowQueue = ContactCenterSetupLookups.Reference(entryPoint.OverflowQueueId, _queueNames),
            welcomeMessage = entryPoint.WelcomeMessage,
            closedMessage = entryPoint.ClosedMessage,
            ringTimeoutSeconds = entryPoint.RingTimeoutSeconds,
            voicemail = new
            {
                enabled = entryPoint.VoicemailEnabled,
                destination = entryPoint.VoicemailDestination.ToString(),
                recipientAgent = ContactCenterSetupLookups.Reference(entryPoint.VoicemailRecipientAgentId, _agentNames),
                greetingText = entryPoint.VoicemailGreetingText,
            },
            phoneMenu = DescribeMenu(entryPoint.IvrFlow),

            // What other features keep on the entry point, such as messaging or AI agent settings, is changed on its page.
            otherSettings = (entryPoint.Properties ?? new Dictionary<string, object>()).Keys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
            created = await ContactCenterSetupToolBase.ToLocalAsync(_localClock, entryPoint.CreatedUtc),
            modified = await ContactCenterSetupToolBase.ToLocalAsync(_localClock, entryPoint.ModifiedUtc),
        };

    private object DescribeMenu(IvrFlow flow)
        => flow is null
            ? null
            : new
            {
                rootNodeId = flow.RootNodeId,
                maxRetries = flow.MaxRetries,
                fallbackAction = DescribeAction(flow.FallbackAction),
                nodes = (flow.Nodes ?? [])
                    .Where(node => node is not null)
                    .Select(node => new
                    {
                        nodeId = node.NodeId,
                        prompt = node.Prompt,
                        promptMediaId = node.PromptMediaId,
                        options = (node.Options ?? [])
                            .Where(option => option is not null)
                            .Select(option => new
                            {
                                digit = option.Digit,
                                action = DescribeAction(option.Action),
                            })
                            .ToList(),
                    })
                    .ToList(),
            };

    private object DescribeAction(IvrAction action)
        => action is null
            ? null
            : new
            {
                kind = action.Kind.ToString(),
                targetId = action.TargetId,
                targetName = action.Kind switch
                {
                    IvrActionKind.RouteToQueue => _queueNames.GetValueOrDefault(action.TargetId ?? string.Empty),
                    IvrActionKind.RouteToAgent => _agentNames.GetValueOrDefault(action.TargetId ?? string.Empty),
                    _ => null,
                },
            };

    private object DescribeTarget(ContactCenterEntryPoint entryPoint)
        => new
        {
            type = entryPoint.TargetType.ToString(),
            queue = ContactCenterSetupLookups.Reference(entryPoint.TargetQueueId, _queueNames),
            agent = ContactCenterSetupLookups.Reference(entryPoint.TargetAgentId, _agentNames),
            aiProfileId = entryPoint.TargetAIProfileId,
        };

    private List<object> DescribeAddresses(ContactCenterEntryPoint entryPoint)
        => (entryPoint.AddressIds ?? [])
            .Select(addressId => Find(addressId) is { } address
                ? (object)new { id = address.ItemId, name = address.DisplayText, value = address.Value }
                : new { id = addressId, name = (string)null, value = (string)null })
            .ToList();

    private OmnichannelChannelEndpoint Find(string addressId)
        => _addresses.FirstOrDefault(address => address is not null && address.IsKnownAs(addressId));
}
