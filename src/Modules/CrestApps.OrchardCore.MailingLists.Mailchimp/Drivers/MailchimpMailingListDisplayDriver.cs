using System.Text.RegularExpressions;
using CrestApps.Core;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Services;
using CrestApps.OrchardCore.MailingLists.Mailchimp.ViewModels;
using CrestApps.OrchardCore.MailingLists.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Drivers;

/// <summary>
/// The Mailchimp card of the mailing list editor, shown when the list's provider is Mailchimp: the audience, the tags,
/// the merge fields, and the state of the list's webhook. The card's actions (create the coupon merge field, register
/// the webhook) post to <c>MailchimpAdminController</c>.
/// </summary>
internal sealed class MailchimpMailingListDisplayDriver : DisplayDriver<MailingList>
{
    private static readonly TimeSpan _audiencesTimeout = TimeSpan.FromSeconds(15);

    private readonly IMailchimpCredentialsProvider _credentialsProvider;
    private readonly MailchimpClient _client;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public MailchimpMailingListDisplayDriver(
        IMailchimpCredentialsProvider credentialsProvider,
        MailchimpClient client,
        IHttpContextAccessor httpContextAccessor,
        ILocalClock localClock,
        INotifier notifier,
        ILogger<MailchimpMailingListDisplayDriver> logger,
        IStringLocalizer<MailchimpMailingListDisplayDriver> stringLocalizer,
        IHtmlLocalizer<MailchimpMailingListDisplayDriver> htmlLocalizer)
    {
        _credentialsProvider = credentialsProvider;
        _client = client;
        _httpContextAccessor = httpContextAccessor;
        _localClock = localClock;
        _notifier = notifier;
        _logger = logger;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Gives the card's fields their own prefix, so they can't collide with another card of the list editor.
    /// </summary>
    protected override void BuildPrefix(MailingList model, string htmlFieldPrefix)
    {
        Prefix = string.IsNullOrEmpty(htmlFieldPrefix) ? "Mailchimp" : $"{htmlFieldPrefix}.Mailchimp";
    }

    public override IDisplayResult Edit(MailingList list, BuildEditorContext context)
    {
        if (!list.IsMailchimpList())
        {
            return null;
        }

        return Initialize<MailchimpMailingListViewModel>("MailchimpMailingList_Edit", async model =>
        {
            var settings = list.GetMailchimpSettings();

            model.Rendered = true;
            model.ListId = list.ItemId;
            model.AudienceId = settings.AudienceId;
            model.Tags = string.Join(", ", settings.Tags ?? []);
            model.SendNameMergeFields = settings.SendNameMergeFields;
            model.CouponMergeTag = string.IsNullOrWhiteSpace(settings.CouponMergeTag) ? MailchimpConstants.DefaultCouponMergeTag : settings.CouponMergeTag;
            model.ProviderConfirms = list.Confirmation == MailingListConfirmation.Provider;
            model.CouponDeliveredByProvider = list.TryGet<MailingListWelcomeCoupon>(out var coupon)
                && coupon is { IsEnabled: true, Delivery: MailingListCouponDelivery.Provider };
            model.WebhookRegistered = settings.HasWebhook();
            model.WebhookSigned = !string.IsNullOrEmpty(settings.WebhookSigningSecret);

            if (settings.WebhookRegisteredUtc is { } registeredUtc)
            {
                model.WebhookRegisteredLocal = await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(registeredUtc, DateTimeKind.Utc));
            }

            await LoadAudiencesAsync(model, settings.AudienceId);
        }).Location("Content:11");
    }

    public override async Task<IDisplayResult> UpdateAsync(MailingList list, UpdateEditorContext context)
    {
        if (!list.IsMailchimpList())
        {
            return Edit(list, context);
        }

        var model = new MailchimpMailingListViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // The card was not on the page: the list was switched to Mailchimp in this post. Nothing of it was posted.
        if (!model.Rendered)
        {
            return Edit(list, context);
        }

        var settings = list.GetMailchimpSettings();
        var audienceId = string.IsNullOrWhiteSpace(model.AudienceId) ? null : model.AudienceId.Trim();
        var couponMergeTag = string.IsNullOrWhiteSpace(model.CouponMergeTag) ? MailchimpConstants.DefaultCouponMergeTag : model.CouponMergeTag.Trim().ToUpperInvariant();

        if (!string.Equals(audienceId, settings.AudienceId, StringComparison.Ordinal))
        {
            if (settings.HasWebhook())
            {
                settings.ClearWebhook();
                await _notifier.WarningAsync(H["The list's audience changed, so its webhook was forgotten. Register the webhook again, and delete the old one in Mailchimp (Audience > Settings > Webhooks)."]);
            }

            settings.AudienceId = audienceId;
        }

        settings.Tags = ParseTags(model.Tags);
        settings.SendNameMergeFields = model.SendNameMergeFields;
        settings.CouponMergeTag = couponMergeTag;

        list.Put(settings);

        return Edit(list, context);
    }

    /// <summary>
    /// Reads the tags typed in the card: separated by commas, semicolons or lines, trimmed, without repeats.
    /// </summary>
    internal static string[] ParseTags(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => tag.Length <= 100)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task LoadAudiencesAsync(MailchimpMailingListViewModel model, string selected)
    {
        var credentials = await _credentialsProvider.GetAsync();

        if (credentials is null)
        {
            model.AudiencesError = S["Mailchimp is not set up yet: choose the secret that holds the API key under Settings > Mailing Lists. Until then, enter the audience id."];

            return;
        }

        model.IsConfigured = true;

        var requestAborted = _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);

        timeout.CancelAfter(_audiencesTimeout);

        try
        {
            var audiences = await _client.GetAudiencesAsync(credentials, timeout.Token);

            model.Audiences = audiences
                .Select(audience => new SelectListItem(audience.Name, audience.Id, string.Equals(audience.Id, selected, StringComparison.Ordinal)))
                .ToList();

            if (!string.IsNullOrEmpty(selected) && !model.Audiences.Exists(item => item.Value == selected))
            {
                model.Audiences.Insert(0, new SelectListItem(S["{0} (not found in Mailchimp)", selected], selected, selected: true));
            }

            if (model.Audiences.Count == 0)
            {
                model.AudiencesError = S["The Mailchimp account has no audience yet. Create one in Mailchimp first."];
            }
        }
        catch (MailchimpApiException exception)
        {
            model.AudiencesError = S["Mailchimp refused to list the audiences ({0} {1}). Check the API key secret and the server prefix; until then, enter the audience id.", (int)exception.StatusCode, exception.Title ?? exception.StatusCode.ToString()];
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutRejectedException or BrokenCircuitException
            || (exception is OperationCanceledException && !requestAborted.IsCancellationRequested))
        {
            _logger.LogWarning(exception, "The Mailchimp audiences could not be read for the mailing list editor.");

            model.AudiencesError = S["Mailchimp could not be reached to list the audiences; enter the audience id."];
        }
    }
}
