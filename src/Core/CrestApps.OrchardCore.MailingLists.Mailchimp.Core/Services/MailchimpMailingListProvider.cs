using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.Extensions.Localization;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// Keeps a mailing list in a Mailchimp audience. Every push is safe to send twice: an upsert is a <c>PUT</c> on the
/// member's subscriber hash, an unsubscribe changes only an existing member, and an erase that finds no member has
/// nothing left to do. The member's id at Mailchimp is its subscriber hash.
/// </summary>
public sealed class MailchimpMailingListProvider : IMailingListProvider
{
    /// <summary>
    /// How many changed members the reconciliation reads per page.
    /// </summary>
    public const int ChangesPageSize = 500;

    private readonly IMailchimpCredentialsProvider _credentialsProvider;
    private readonly MailchimpClient _client;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailchimpMailingListProvider"/> class.
    /// </summary>
    /// <param name="credentialsProvider">The tenant's Mailchimp credentials.</param>
    /// <param name="client">The Mailchimp client.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MailchimpMailingListProvider(
        IMailchimpCredentialsProvider credentialsProvider,
        MailchimpClient client,
        IStringLocalizer<MailchimpMailingListProvider> stringLocalizer)
    {
        _credentialsProvider = credentialsProvider;
        _client = client;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name
        => MailchimpConstants.ProviderName;

    /// <inheritdoc/>
    public LocalizedString DisplayName
        => S["Mailchimp"];

    /// <inheritdoc/>
    public MailingListProviderCapabilities Capabilities
        => MailingListProviderCapabilities.ConfirmsAddresses
            | MailingListProviderCapabilities.DeliversWelcomeOffer
            | MailingListProviderCapabilities.ReportsChanges
            | MailingListProviderCapabilities.SendsCampaigns;

    /// <inheritdoc/>
    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
        => await _credentialsProvider.GetAsync(cancellationToken) is not null;

    /// <inheritdoc/>
    public async Task<MailingListPushResult> PushAsync(MailingList list, MailingListMember member, MailingListPushKind kind, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(member);

        var plan = MailchimpPushPlanner.Plan(list, member, kind);

        if (plan.Action == MailchimpPushAction.Skip)
        {
            return MailingListPushResult.Success(member.ProviderMemberId);
        }

        var settings = list.GetMailchimpSettings();

        if (string.IsNullOrWhiteSpace(settings.AudienceId))
        {
            return MailingListPushResult.Failure(S["The list has no Mailchimp audience. Choose one on the list's Mailchimp card."], canRetry: false);
        }

        var email = string.IsNullOrWhiteSpace(member.Contact?.Email) ? member.NormalizedEmail : member.Contact.Email.Trim();
        var subscriberHash = ResolveSubscriberHash(member, email);

        if (subscriberHash is null)
        {
            return MailingListPushResult.Failure(S["The member has no e-mail address to send to Mailchimp."], canRetry: false);
        }

        var credentials = await _credentialsProvider.GetAsync(cancellationToken);

        if (credentials is null)
        {
            return MailingListPushResult.Failure(S["Mailchimp is not set up: choose the secret that holds the API key on the mailing lists settings page."], canRetry: false);
        }

        try
        {
            switch (plan.Action)
            {
                case MailchimpPushAction.Erase:
                    await _client.DeleteMemberPermanentlyAsync(credentials, settings.AudienceId, subscriberHash, cancellationToken);
                    break;

                case MailchimpPushAction.Unsubscribe:
                    await _client.UpdateMemberStatusAsync(credentials, settings.AudienceId, subscriberHash, MailchimpMemberStatuses.Unsubscribed, cancellationToken);
                    break;

                default:
                    if (string.IsNullOrEmpty(email))
                    {
                        return MailingListPushResult.Failure(S["The member has no e-mail address to send to Mailchimp."], canRetry: false);
                    }

                    await UpsertAsync(credentials, list, member, settings, plan, email, subscriberHash, cancellationToken);
                    break;
            }
        }
        catch (MailchimpApiException exception)
        {
            return MailingListPushResult.Failure(Describe(exception), exception.IsRetryable);
        }
        catch (TimeoutRejectedException)
        {
            return MailingListPushResult.Failure(S["Mailchimp did not answer in time."], canRetry: true);
        }
        catch (BrokenCircuitException)
        {
            return MailingListPushResult.Failure(S["Calls to Mailchimp are paused for a moment after repeated failures."], canRetry: true);
        }
        catch (HttpRequestException)
        {
            return MailingListPushResult.Failure(S["Mailchimp could not be reached."], canRetry: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return MailingListPushResult.Failure(S["Mailchimp did not answer in time."], canRetry: true);
        }

        return MailingListPushResult.Success(subscriberHash);
    }

    /// <inheritdoc/>
    public async Task<MailingListProviderChangePage> GetChangesAsync(MailingList list, DateTime sinceUtc, string cursor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);

        var settings = list.GetMailchimpSettings();

        if (string.IsNullOrWhiteSpace(settings.AudienceId))
        {
            return MailingListProviderChangePage.Empty;
        }

        // Failing here keeps the reconciliation's last run time, so no change is skipped while Mailchimp is not set up.
        var credentials = await _credentialsProvider.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException("Mailchimp is not set up, so the changes of the list's audience can't be read.");

        var offset = int.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        var page = await _client.GetMembersChangedSinceAsync(credentials, settings.AudienceId, sinceUtc, offset, ChangesPageSize, cancellationToken);
        var changes = new List<MailingListProviderChange>();

        foreach (var member in page.Members)
        {
            var change = MailchimpChangeMapper.FromMember(list, member, sinceUtc);

            if (change is not null)
            {
                changes.Add(change);
            }
        }

        var next = offset + page.Members.Count;

        return new MailingListProviderChangePage
        {
            Changes = changes,
            NextCursor = page.Members.Count > 0 && next < page.TotalItems ? next.ToString(CultureInfo.InvariantCulture) : null,
        };
    }

    private async Task UpsertAsync(
        MailchimpCredentials credentials,
        MailingList list,
        MailingListMember member,
        MailchimpListSettings settings,
        MailchimpPushPlan plan,
        string email,
        string subscriberHash,
        CancellationToken cancellationToken)
    {
        // The status is set only for a new address; an address Mailchimp has keeps its status, so a retried push changes
        // nothing and never makes Mailchimp send a second confirmation e-mail.
        var body = new JsonObject
        {
            ["email_address"] = email,
            ["status_if_new"] = plan.StatusIfNew,
        };

        var mergeFields = BuildMergeFields(list, member, settings);

        if (mergeFields.Count > 0)
        {
            body["merge_fields"] = mergeFields;
        }

        var result = await _client.UpsertMemberAsync(credentials, settings.AudienceId, subscriberHash, body, cancellationToken);
        var followUp = MailchimpPushPlanner.FollowUpStatus(plan, result.Status);

        if (followUp is not null)
        {
            // The address was at Mailchimp already and does not receive mail there: an unsubscribed or cleaned address
            // is asked to confirm again (pending), so Mailchimp mails it only after the person says yes there too.
            await _client.UpsertMemberAsync(
                credentials,
                settings.AudienceId,
                subscriberHash,
                new JsonObject
                {
                    ["email_address"] = email,
                    ["status_if_new"] = plan.StatusIfNew,
                    ["status"] = followUp,
                },
                cancellationToken);
        }

        if (settings.Tags is { Length: > 0 })
        {
            await _client.AddTagsAsync(credentials, settings.AudienceId, subscriberHash, settings.Tags, cancellationToken);
        }
    }

    private static JsonObject BuildMergeFields(
        MailingList list,
        MailingListMember member,
        MailchimpListSettings settings)
    {
        var mergeFields = new JsonObject();

        if (settings.SendNameMergeFields)
        {
            var (firstName, lastName) = MailchimpNames.Split(member.Contact?.Name);

            if (firstName is not null)
            {
                mergeFields[MailchimpNames.FirstNameTag] = firstName;
            }

            if (lastName is not null)
            {
                mergeFields[MailchimpNames.LastNameTag] = lastName;
            }
        }

        if (!string.IsNullOrEmpty(member.CouponCodeId)
            && MailchimpListSettingsExtensions.IsValidMergeTag(settings.CouponMergeTag)
            && list.TryGet<MailingListWelcomeCoupon>(out var coupon)
            && coupon is { IsEnabled: true, Delivery: MailingListCouponDelivery.Provider })
        {
            // The welcome coupon feature keeps the issued code on the member, so the provider reads it there.
            if (member.TryGet<MailingListMemberCoupon>(out var issued) && !string.IsNullOrWhiteSpace(issued?.Code))
            {
                mergeFields[settings.CouponMergeTag] = issued.Code.Trim();
            }
        }

        return mergeFields;
    }

    private static string ResolveSubscriberHash(MailingListMember member, string email)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            return MailchimpSubscriberHash.Compute(email);
        }

        // An erased member may have no address left; the hash the last push returned still names it at Mailchimp.
        return MailchimpSubscriberHash.IsHash(member.ProviderMemberId) ? member.ProviderMemberId : null;
    }

    private string Describe(MailchimpApiException exception)
        => string.IsNullOrEmpty(exception.Detail)
            ? S["Mailchimp refused the change: {0} {1}.", (int)exception.StatusCode, exception.Title ?? exception.StatusCode.ToString()]
            : S["Mailchimp refused the change: {0} {1}. {2}", (int)exception.StatusCode, exception.Title ?? exception.StatusCode.ToString(), exception.Detail];
}
