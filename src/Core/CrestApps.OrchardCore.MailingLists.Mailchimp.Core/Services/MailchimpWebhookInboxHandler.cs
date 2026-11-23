using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// Applies the Mailchimp webhooks the provider webhook inbox stored, through the member service's provider change path:
/// a change older than the member's last status change is ignored there, so applying a webhook again, or late, changes
/// nothing more.
/// </summary>
public sealed class MailchimpWebhookInboxHandler : IProviderWebhookInboxHandler
{
    private readonly IMailingListManager _listManager;
    private readonly IMailingListMemberService _memberService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailchimpWebhookInboxHandler"/> class.
    /// </summary>
    /// <param name="listManager">The mailing list manager.</param>
    /// <param name="memberService">The member service.</param>
    /// <param name="logger">The logger.</param>
    public MailchimpWebhookInboxHandler(
        IMailingListManager listManager,
        IMailingListMemberService memberService,
        ILogger<MailchimpWebhookInboxHandler> logger)
    {
        _listManager = listManager;
        _memberService = memberService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string TechnicalName
        => MailchimpConstants.WebhookHandlerName;

    /// <inheritdoc/>
    public WebhookHandlerReplaySafety ReplaySafety
        => WebhookHandlerReplaySafety.NaturallyIdempotent;

    /// <inheritdoc/>
    public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);

        var envelope = JsonSerializer.Deserialize<MailchimpWebhookPayload>(payload, JsonSerializerOptions.Web);

        if (envelope is null || string.IsNullOrEmpty(envelope.ListId))
        {
            throw new InvalidDataException("The Mailchimp webhook payload could not be read.");
        }

        var webhook = MailchimpWebhookParser.Parse(envelope.Body);

        if (webhook is null || !MailchimpWebhookParser.IsHandled(webhook.Type))
        {
            return;
        }

        var list = await _listManager.FindByIdAsync(envelope.ListId, cancellationToken);

        if (list is null || !list.IsMailchimpList())
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Ignored a Mailchimp webhook for the mailing list {ListId}, which is gone or no longer kept in Mailchimp.", envelope.ListId.SanitizeLogValue());
            }

            return;
        }

        var audienceId = list.GetMailchimpSettings().AudienceId;

        if (!string.IsNullOrEmpty(webhook.AudienceId) && !string.Equals(webhook.AudienceId, audienceId, StringComparison.Ordinal))
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Ignored a Mailchimp webhook for the mailing list {ListId}: it is about the audience {AudienceId}, not the list's audience.", envelope.ListId.SanitizeLogValue(), webhook.AudienceId.SanitizeLogValue());
            }

            return;
        }

        var change = MailchimpChangeMapper.FromWebhook(list, webhook, envelope.ReceivedUtc);

        if (change is null)
        {
            return;
        }

        await _memberService.ApplyProviderChangeAsync(change, cancellationToken);
    }
}
