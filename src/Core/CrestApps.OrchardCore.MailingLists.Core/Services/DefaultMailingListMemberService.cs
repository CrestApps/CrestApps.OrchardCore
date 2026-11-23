using System.Diagnostics;
using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.MailingLists.Core.Emails;
using CrestApps.OrchardCore.MailingLists.Core.Sync;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Signs people up to mailing lists, verifies their addresses, unsubscribes them, applies their providers' changes and
/// erases them. Each change of one address on one list runs under a distributed lock on the pair, and the unit of work is
/// committed before the lock is released, so two requests never create the same membership twice.
/// </summary>
public sealed class DefaultMailingListMemberService : IMailingListMemberService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(1);

    private readonly IMailingListManager _lists;
    private readonly IMailingListMemberStore _members;
    private readonly IMailingListProviderResolver _providers;
    private readonly IMailingListSyncQueue _syncQueue;
    private readonly IMailingListEmails _emails;
    private readonly MailingListConfirmationThrottle _throttle;
    private readonly MailingListMemberObserverRunner _observers;
    private readonly IConsentService _consent;
    private readonly IDistributedLock _locks;
    private readonly SignedTokenService _tokens;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMailingListMemberService"/> class.
    /// </summary>
    /// <param name="lists">The list manager.</param>
    /// <param name="members">The member store.</param>
    /// <param name="providers">The provider resolver.</param>
    /// <param name="syncQueue">The provider outbox.</param>
    /// <param name="emails">The mailing lists e-mails.</param>
    /// <param name="throttle">The per-address cap of confirmation e-mails.</param>
    /// <param name="observers">The member observers.</param>
    /// <param name="consent">The consent service.</param>
    /// <param name="locks">The distributed lock.</param>
    /// <param name="tokens">The signed token service, for verification links.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public DefaultMailingListMemberService(
        IMailingListManager lists,
        IMailingListMemberStore members,
        IMailingListProviderResolver providers,
        IMailingListSyncQueue syncQueue,
        IMailingListEmails emails,
        MailingListConfirmationThrottle throttle,
        MailingListMemberObserverRunner observers,
        IConsentService consent,
        IDistributedLock locks,
        SignedTokenService tokens,
        IClock clock,
        ILogger<DefaultMailingListMemberService> logger)
    {
        _lists = lists;
        _members = members;
        _providers = providers;
        _syncQueue = syncQueue;
        _emails = emails;
        _throttle = throttle;
        _observers = observers;
        _consent = consent;
        _locks = locks;
        _tokens = tokens;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MailingListSubscribeResult> SubscribeAsync(MailingListSubscribeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await TrySubscribeAsync(request, cancellationToken);

        RecordSignUp(result.Outcome, request.Source);

        return result;
    }

    /// <inheritdoc/>
    public async Task<MailingListVerifyResult> ReadVerificationAsync(string token, CancellationToken cancellationToken = default)
    {
        var (member, list, ticket) = await ReadTokenAsync(token, cancellationToken);

        if (member is null)
        {
            return new MailingListVerifyResult { Outcome = MailingListVerifyOutcome.Invalid };
        }

        return new MailingListVerifyResult
        {
            Outcome = MailingListVerification.Evaluate(member, ticket, _clock.UtcNow),
            Member = member,
            List = list,
        };
    }

    /// <inheritdoc/>
    public async Task<MailingListVerifyResult> VerifyAsync(string token, CancellationToken cancellationToken = default)
    {
        var (found, list, ticket) = await ReadTokenAsync(token, cancellationToken);

        if (found is null)
        {
            return new MailingListVerifyResult { Outcome = MailingListVerifyOutcome.Invalid };
        }

        await using var locker = await LockAsync(list.ItemId, found.NormalizedEmail);

        var member = await _members.FindByIdAsync(found.ItemId, cancellationToken) ?? found;
        var now = _clock.UtcNow;
        var outcome = MailingListVerification.Evaluate(member, ticket, now);

        if (outcome != MailingListVerifyOutcome.Ready)
        {
            return new MailingListVerifyResult { Outcome = outcome, Member = member, List = list };
        }

        var previous = member.Status;

        member.EmailVerifiedUtc = now;
        member.VerificationNonce = null;

        if (previous == MailingListMemberStatus.Pending)
        {
            MailingListMemberChanges.Move(member, MailingListMemberStatus.Subscribed, now);
        }

        await _syncQueue.QueueAsync(list, member, MailingListPushKind.Upsert, cancellationToken);
        await _members.UpdateAsync(member, cancellationToken);

        var context = MailingListMemberChanges.Context(member, list, previous, member.Source, silent: false);

        if (previous != MailingListMemberStatus.Subscribed)
        {
            await _observers.SubscribedAsync(context);
        }

        await _observers.VerifiedAsync(context);
        await RecordOptInAsync(member, cancellationToken);

        await _members.UpdateAsync(member, cancellationToken);
        await _members.SaveChangesAsync(cancellationToken);

        // Sent once the change is committed. A member subscribed by the claim link of a list without confirmation got
        // its welcome e-mail already.
        if (context.IssuedCoupon is not null || (previous == MailingListMemberStatus.Pending && list.SendWelcomeEmail))
        {
            await _emails.SendWelcomeAsync(list, member, context.IssuedCoupon, cancellationToken);
            await _members.SaveChangesAsync(cancellationToken);
        }

        MailingListsTelemetry.Verifications.Add(1);

        return new MailingListVerifyResult
        {
            Outcome = MailingListVerifyOutcome.Verified,
            Member = member,
            List = list,
            Coupon = context.IssuedCoupon,
        };
    }

    /// <inheritdoc/>
    public async Task<bool> ResendConfirmationAsync(string memberId, CancellationToken cancellationToken = default)
    {
        var found = await _members.FindByIdAsync(memberId, cancellationToken);

        if (found is null || !MailingListVerification.CanResend(found) || await _lists.FindByIdAsync(found.ListId, cancellationToken) is not { } list)
        {
            return false;
        }

        await using var locker = await LockAsync(list.ItemId, found.NormalizedEmail);

        var member = await _members.FindByIdAsync(memberId, cancellationToken);

        if (!MailingListVerification.CanResend(member) || !await _throttle.CanSendAsync(list.ItemId, member.NormalizedEmail, cancellationToken))
        {
            return false;
        }

        member.VerificationNonce = MailingListMemberChanges.NewNonce();
        await _members.UpdateAsync(member, cancellationToken);
        await _members.SaveChangesAsync(cancellationToken);

        var sent = member.Status == MailingListMemberStatus.Pending
            ? await _emails.SendConfirmAsync(list, member, coupon: null, cancellationToken)
            : await _emails.SendClaimAsync(list, member, cancellationToken);

        if (sent)
        {
            await _throttle.RecordAsync(list.ItemId, member.NormalizedEmail, cancellationToken);
        }

        await _members.SaveChangesAsync(cancellationToken);

        return sent;
    }

    /// <inheritdoc/>
    public async Task<int> UnsubscribeAsync(string listId, string email, string source, string reason = null, CancellationToken cancellationToken = default)
    {
        var normalized = EmailAddresses.Normalize(email);

        if (normalized is null)
        {
            return 0;
        }

        IReadOnlyList<MailingListMember> memberships;

        if (string.IsNullOrEmpty(listId))
        {
            memberships = await _members.ListByEmailAsync(normalized, cancellationToken);
        }
        else
        {
            var one = await _members.FindAsync(listId, normalized, cancellationToken);

            memberships = one is null ? [] : [one];
        }

        var changed = 0;

        foreach (var membership in memberships.Where(MailingListMemberChanges.IsActive))
        {
            if (await _lists.FindByIdAsync(membership.ListId, cancellationToken) is not { } list)
            {
                continue;
            }

            await using var locker = await LockAsync(list.ItemId, normalized);

            var member = await _members.FindByIdAsync(membership.ItemId, cancellationToken);

            if (!MailingListMemberChanges.IsActive(member))
            {
                continue;
            }

            var previous = member.Status;

            MailingListMemberChanges.Move(member, MailingListMemberStatus.Unsubscribed, _clock.UtcNow);
            member.UnsubscribeReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

            await _syncQueue.QueueAsync(list, member, MailingListPushKind.Unsubscribe, cancellationToken);
            await _members.UpdateAsync(member, cancellationToken);
            await _observers.UnsubscribedAsync(MailingListMemberChanges.Context(member, list, previous, source, silent: true));
            await _members.SaveChangesAsync(cancellationToken);

            changed++;
        }

        if (changed > 0)
        {
            MailingListsTelemetry.Unsubscribes.Add(changed, new TagList { { MailingListsTelemetry.SourceTag, MailingListMemberChanges.SourceTag(source) } });
        }

        return changed;
    }

    /// <inheritdoc/>
    public async Task<bool> ApplyProviderChangeAsync(MailingListProviderChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        var normalized = EmailAddresses.Normalize(change.Email);

        if (normalized is null || await _lists.FindByIdAsync(change.ListId, cancellationToken) is not { } list)
        {
            return false;
        }

        await using var locker = await LockAsync(list.ItemId, normalized);

        var member = await _members.FindAsync(list.ItemId, normalized, cancellationToken);

        if (MailingListProviderChanges.IsStale(member, change))
        {
            return false;
        }

        var occurred = change.OccurredUtc == default ? _clock.UtcNow : DateTime.SpecifyKind(change.OccurredUtc, DateTimeKind.Utc);
        var isNew = member is null;
        MailingListMemberStatus? previous = member?.Status;
        MailingListMemberStatus? next;

        if (isNew)
        {
            if (change.Status is not (MailingListMemberStatus.Subscribed or MailingListMemberStatus.Pending) || !EmailAddresses.IsValid(change.Email))
            {
                return false;
            }

            member = NewMember(list, normalized, change.Email, change.Name, MailingListMemberSources.Provider, sourceId: null, userId: null);
            next = change.Status;
        }
        else
        {
            next = MailingListProviderChanges.NextStatus(member, change);
        }

        var profileChanged = MailingListMemberChanges.ApplyProfile(member, change);

        if (!string.IsNullOrEmpty(change.NewEmail) && EmailAddresses.IsValid(change.NewEmail) && !isNew)
        {
            var newNormalized = EmailAddresses.Normalize(change.NewEmail);

            if (!string.Equals(newNormalized, member.NormalizedEmail, StringComparison.Ordinal)
                && await _members.FindAsync(list.ItemId, newNormalized, cancellationToken) is null)
            {
                member.Contact = member.Contact with { Email = change.NewEmail.Trim() };
                member.NormalizedEmail = newNormalized;
                profileChanged = true;
            }
        }

        if (next is { } status)
        {
            MailingListMemberChanges.Move(member, status, occurred);
        }

        var verifiedNow = change.Verified && member.EmailVerifiedUtc is null && member.Status != MailingListMemberStatus.Unsubscribed && member.Status != MailingListMemberStatus.Cleaned;

        if (verifiedNow)
        {
            member.EmailVerifiedUtc = occurred;

            if (member.Status == MailingListMemberStatus.Pending)
            {
                MailingListMemberChanges.Move(member, MailingListMemberStatus.Subscribed, occurred);
                next = MailingListMemberStatus.Subscribed;
            }
        }

        if (!isNew && next is null && !verifiedNow && !profileChanged)
        {
            return false;
        }

        // The provider already holds this state, so nothing is pushed back to it.
        member.SyncState = MailingListSyncState.Synced;
        member.SyncError = null;

        if (isNew)
        {
            await _members.CreateAsync(member, cancellationToken);
        }
        else
        {
            await _members.UpdateAsync(member, cancellationToken);
        }

        // A member who never signed up on the site (the provider reported it, an admin or an import added it) gets no
        // welcome e-mail or welcome coupon from the site, as an import doesn't.
        var context = MailingListMemberChanges.Context(member, list, previous, MailingListMemberSources.Provider, silent: MailingListProviderChanges.IsSilent(member));

        if (isNew)
        {
            await _observers.SignedUpAsync(context);
        }

        if (member.Status == MailingListMemberStatus.Subscribed && previous != MailingListMemberStatus.Subscribed)
        {
            await _observers.SubscribedAsync(context);
        }

        if (verifiedNow)
        {
            await _observers.VerifiedAsync(context);
            await RecordOptInAsync(member, cancellationToken);
            MailingListsTelemetry.Verifications.Add(1);
        }

        if (member.Status is MailingListMemberStatus.Unsubscribed or MailingListMemberStatus.Cleaned && previous != member.Status)
        {
            await _observers.UnsubscribedAsync(context);
            MailingListsTelemetry.Unsubscribes.Add(1, new TagList { { MailingListsTelemetry.SourceTag, MailingListMemberSources.Provider } });
        }

        // The provider already holds its own change, but not what an observer added to it, such as a welcome code the
        // provider's own welcome e-mail sends: the member goes to the provider again, queued in this unit of work.
        if (context.RequestsProviderPush)
        {
            await _syncQueue.QueueAsync(list, member, MailingListPushKind.Upsert, cancellationToken);
        }

        await _members.UpdateAsync(member, cancellationToken);
        await _members.SaveChangesAsync(cancellationToken);

        // The provider sends its own welcome; the site sends only the welcome coupon it issued and delivers, once committed.
        if (context.IssuedCoupon is not null)
        {
            await _emails.SendWelcomeAsync(list, member, context.IssuedCoupon, cancellationToken);
            await _members.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> RetrySyncAsync(string memberId, CancellationToken cancellationToken = default)
    {
        var found = await _members.FindByIdAsync(memberId, cancellationToken);

        if (found?.SyncState != MailingListSyncState.Failed || await _lists.FindByIdAsync(found.ListId, cancellationToken) is not { } list)
        {
            return false;
        }

        await using var locker = await LockAsync(list.ItemId, found.NormalizedEmail);

        var member = await _members.FindByIdAsync(memberId, cancellationToken);

        if (member?.SyncState != MailingListSyncState.Failed)
        {
            return false;
        }

        // The push sends the member as it is when it leaves, so its state is what is queued again.
        var kind = member.Status == MailingListMemberStatus.Unsubscribed ? MailingListPushKind.Unsubscribe : MailingListPushKind.Upsert;

        await _syncQueue.QueueAsync(list, member, kind, cancellationToken);
        await _members.UpdateAsync(member, cancellationToken);
        await _members.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public async Task<int> EraseAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = EmailAddresses.Normalize(email);

        if (normalized is null)
        {
            return 0;
        }

        var erased = 0;

        foreach (var membership in await _members.ListByEmailAsync(normalized, cancellationToken))
        {
            var list = await _lists.FindByIdAsync(membership.ListId, cancellationToken);

            await using var locker = await LockAsync(membership.ListId, normalized);

            var member = await _members.FindByIdAsync(membership.ItemId, cancellationToken);

            if (member is null)
            {
                continue;
            }

            if (list is not null)
            {
                await _syncQueue.QueueAsync(list, member, MailingListPushKind.Erase, cancellationToken);
            }

            await _members.DeleteAsync(member, cancellationToken);
            await _emails.ForgetAsync(member, cancellationToken);
            await _observers.ErasedAsync(MailingListMemberChanges.Context(member, list, member.Status, MailingListMemberSources.Admin, silent: true));
            await _members.SaveChangesAsync(cancellationToken);

            erased++;
        }

        return erased;
    }

    private async Task<MailingListSubscribeResult> TrySubscribeAsync(MailingListSubscribeRequest request, CancellationToken cancellationToken)
    {
        if (await FindListAsync(request.ListId, cancellationToken) is not { } list)
        {
            return Refused("The mailing list does not exist.");
        }

        if (!request.Silent && !list.IsEnabled)
        {
            return Refused("The mailing list is turned off.");
        }

        if (request.Status is { } wanted && wanted is not (MailingListMemberStatus.Pending or MailingListMemberStatus.Subscribed))
        {
            return Refused("A sign-up gives a member the pending or subscribed state only.");
        }

        if (!EmailAddresses.IsValid(request.Email))
        {
            return Refused("The e-mail address is not valid.");
        }

        var normalized = EmailAddresses.Normalize(request.Email);

        // A do-not-contact of every purpose (a CRM Do not email, a bounce suppression) refuses the sign-up itself.
        var blocked = await _consent.GetAsync(new ConsentQuery { Channel = ConsentChannels.Email, Address = normalized }, cancellationToken);

        if (blocked.HasOptedOut)
        {
            return Refused("The address may not be e-mailed.");
        }

        var plan = await PlanAsync(list, request, normalized, cancellationToken);

        await using var locker = await LockAsync(list.ItemId, normalized);

        var member = await _members.FindAsync(list.ItemId, normalized, cancellationToken);

        if (member?.Status == MailingListMemberStatus.Subscribed)
        {
            return new MailingListSubscribeResult { Outcome = MailingListSubscribeOutcome.AlreadySubscribed, Member = member };
        }

        // A bounced address confirms again, whatever the list says.
        if (member?.Status == MailingListMemberStatus.Cleaned && plan.Status == MailingListMemberStatus.Subscribed)
        {
            if (request.Status is not null)
            {
                return Refused("A cleaned address must confirm its sign-up again.");
            }

            plan = new SignUpPlan(MailingListMemberStatus.Pending, SendsLink: true, VerifiedNow: false);
        }

        var sendsLink = plan.SendsLink && !request.Silent;

        if (sendsLink && !await _throttle.CanSendAsync(list.ItemId, normalized, cancellationToken))
        {
            return Refused("Too many confirmation e-mails were sent to the address.");
        }

        var now = _clock.UtcNow;
        var isNew = member is null;
        MailingListMemberStatus? previous = member?.Status;

        member ??= NewMember(list, normalized, request.Email, request.Name, request.Source, request.SourceId, request.UserId);
        MailingListMemberChanges.ApplySignUp(member, list, request, now);

        if (previous != plan.Status)
        {
            MailingListMemberChanges.Move(member, plan.Status, now);
        }

        if (plan.VerifiedNow)
        {
            member.EmailVerifiedUtc = now;
        }

        if (sendsLink)
        {
            member.VerificationNonce = MailingListMemberChanges.NewNonce();
        }

        await _syncQueue.QueueAsync(list, member, MailingListPushKind.Upsert, cancellationToken);

        if (isNew)
        {
            await _members.CreateAsync(member, cancellationToken);
        }
        else
        {
            await _members.UpdateAsync(member, cancellationToken);
        }

        var context = MailingListMemberChanges.Context(member, list, previous, request.Source, request.Silent);

        await _observers.SignedUpAsync(context);

        if (member.Status == MailingListMemberStatus.Subscribed && previous != MailingListMemberStatus.Subscribed)
        {
            await _observers.SubscribedAsync(context);
        }

        if (plan.VerifiedNow)
        {
            await _observers.VerifiedAsync(context);
            await RecordOptInAsync(member, cancellationToken);
            MailingListsTelemetry.Verifications.Add(1);
        }

        var email = request.Silent ? SignUpEmail.None : await ChooseSignUpEmailAsync(list, member, context, sendsLink, cancellationToken);

        if (email == SignUpEmail.Claim)
        {
            member.VerificationNonce = MailingListMemberChanges.NewNonce();
        }

        await _members.UpdateAsync(member, cancellationToken);
        await _members.SaveChangesAsync(cancellationToken);

        // The e-mail leaves only once the sign-up is committed.
        if (email != SignUpEmail.None)
        {
            await SendSignUpEmailAsync(list, member, context, email, cancellationToken);
            await _members.SaveChangesAsync(cancellationToken);
        }

        return new MailingListSubscribeResult
        {
            Outcome = member.Status == MailingListMemberStatus.Pending ? MailingListSubscribeOutcome.ConfirmationSent : MailingListSubscribeOutcome.Subscribed,
            Member = member,

            // A coupon is shown on the page only once the address is proven.
            Coupon = plan.VerifiedNow ? context.IssuedCoupon : null,
        };
    }

    private async Task<SignUpPlan> PlanAsync(MailingList list, MailingListSubscribeRequest request, string normalized, CancellationToken cancellationToken)
    {
        if (request.Status is { } status)
        {
            return new SignUpPlan(status, SendsLink: status == MailingListMemberStatus.Pending, VerifiedNow: false);
        }

        if (request.EmailVerifiedByAccount && !string.IsNullOrEmpty(request.UserId) && list.SkipConfirmationForVerifiedUsers)
        {
            return new SignUpPlan(MailingListMemberStatus.Subscribed, SendsLink: false, VerifiedNow: true);
        }

        switch (list.Confirmation)
        {
            case MailingListConfirmation.None:
                // The person's own "stop all marketing" does not block a new sign-up, but a stranger must not undo it:
                // the address confirms by e-mail first.
                var optedOut = await _consent.GetAsync(new ConsentQuery { Channel = ConsentChannels.Email, Address = normalized, Purpose = ConsentPurposes.Marketing }, cancellationToken);

                return optedOut.HasOptedOut
                    ? new SignUpPlan(MailingListMemberStatus.Pending, SendsLink: true, VerifiedNow: false)
                    : new SignUpPlan(MailingListMemberStatus.Subscribed, SendsLink: false, VerifiedNow: false);

            case MailingListConfirmation.Provider when _providers.Resolve(list)?.Capabilities.HasFlag(MailingListProviderCapabilities.ConfirmsAddresses) == true:
                return new SignUpPlan(MailingListMemberStatus.Pending, SendsLink: false, VerifiedNow: false);

            default:
                return new SignUpPlan(MailingListMemberStatus.Pending, SendsLink: true, VerifiedNow: false);
        }
    }

    private async Task<SignUpEmail> ChooseSignUpEmailAsync(MailingList list, MailingListMember member, MailingListMemberContext context, bool sendsLink, CancellationToken cancellationToken)
    {
        if (sendsLink)
        {
            return SignUpEmail.Confirm;
        }

        if (member.Status != MailingListMemberStatus.Subscribed || context.PreviousStatus == MailingListMemberStatus.Subscribed)
        {
            return SignUpEmail.None;
        }

        // Something waits for the address to be proven (the welcome coupon): the welcome e-mail carries a claim link.
        if (context.RequestsVerification && member.EmailVerifiedUtc is null && await _throttle.CanSendAsync(list.ItemId, member.NormalizedEmail, cancellationToken))
        {
            return SignUpEmail.Claim;
        }

        return list.SendWelcomeEmail || context.IssuedCoupon is not null ? SignUpEmail.Welcome : SignUpEmail.None;
    }

    private async Task SendSignUpEmailAsync(MailingList list, MailingListMember member, MailingListMemberContext context, SignUpEmail email, CancellationToken cancellationToken)
    {
        switch (email)
        {
            case SignUpEmail.Confirm:
                if (await _emails.SendConfirmAsync(list, member, context.IssuedCoupon, cancellationToken))
                {
                    await _throttle.RecordAsync(list.ItemId, member.NormalizedEmail, cancellationToken);
                }

                break;

            case SignUpEmail.Claim:
                if (await _emails.SendClaimAsync(list, member, cancellationToken))
                {
                    await _throttle.RecordAsync(list.ItemId, member.NormalizedEmail, cancellationToken);
                }

                break;

            case SignUpEmail.Welcome:
                await _emails.SendWelcomeAsync(list, member, context.IssuedCoupon, cancellationToken);
                break;
        }
    }

    private MailingListMember NewMember(MailingList list, string normalized, string email, string name, string source, string sourceId, string userId)
    {
        var now = _clock.UtcNow;

        return new MailingListMember
        {
            ItemId = UniqueId.GenerateId(),
            ListId = list.ItemId,
            NormalizedEmail = normalized,
            Contact = new ContactInfo
            {
                Email = email?.Trim(),
                Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            },
            Source = source,
            SourceId = sourceId,
            UserId = userId,
            Status = MailingListMemberStatus.Pending,
            StatusChangedUtc = now,
            CreatedUtc = now,
        };
    }

    private async Task RecordOptInAsync(MailingListMember member, CancellationToken cancellationToken)
    {
        try
        {
            // The person proved the address is theirs: their marketing opt-in clears an earlier marketing opt-out. The
            // mailing lists consent source ignores changes recorded under its own name.
            await _consent.RecordAsync(ConsentChannels.Email, member.NormalizedEmail, new ConsentChange
            {
                OptedOut = false,
                Purpose = ConsentPurposes.Marketing,
                Source = MailingListsConstants.ConsentSourceName,
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The marketing opt-in of mailing list member {MemberId} could not be recorded.", member.ItemId.SanitizeLogValue());
        }
    }

    private async Task<(MailingListMember Member, MailingList List, MailingListVerificationTicket Ticket)> ReadTokenAsync(string token, CancellationToken cancellationToken)
    {
        var ticket = MailingListVerificationTokens.Read(_tokens, token);

        if (ticket is null
            || await _members.FindByIdAsync(ticket.MemberId, cancellationToken) is not { } member
            || await _lists.FindByIdAsync(member.ListId, cancellationToken) is not { } list)
        {
            return (null, null, null);
        }

        return (member, list, ticket);
    }

    private async Task<MailingList> FindListAsync(string listId, CancellationToken cancellationToken)
        => string.IsNullOrEmpty(listId) ? null : await _lists.FindByIdAsync(listId, cancellationToken);

    private async Task<IAsyncDisposable> LockAsync(string listId, string normalizedEmail)
    {
        var (locker, locked) = await _locks.TryAcquireLockAsync(MailingListMemberChanges.LockKey(listId, normalizedEmail), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            throw new TimeoutException("Another change of the same mailing list member is running; try again.");
        }

        return locker;
    }

    private static MailingListSubscribeResult Refused(string error)
        => new() { Outcome = MailingListSubscribeOutcome.Refused, Error = error };

    private static void RecordSignUp(MailingListSubscribeOutcome outcome, string source)
        => MailingListsTelemetry.SignUps.Add(1, new TagList
        {
            { MailingListsTelemetry.OutcomeTag, outcome.ToString() },
            { MailingListsTelemetry.SourceTag, MailingListMemberChanges.SourceTag(source) },
        });

    private enum SignUpEmail
    {
        None,
        Confirm,
        Claim,
        Welcome,
    }

    private readonly record struct SignUpPlan(MailingListMemberStatus Status, bool SendsLink, bool VerifiedNow);
}
