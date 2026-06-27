using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Settings;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Handlers;

/// <summary>
/// Automatically verifies omnichannel contacts when their preferred phone number is created or changed.
/// </summary>
internal sealed class OmnichannelContactPhoneNumberVerificationHandler : ContentHandlerBase
{
    private readonly HashSet<string> _contentItemIds = new(StringComparer.Ordinal);
    private readonly IPhoneNumberService _phoneNumberService;

    private bool _taskAdded;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactPhoneNumberVerificationHandler"/> class.
    /// </summary>
    /// <param name="phoneNumberService">The phone number service.</param>
    public OmnichannelContactPhoneNumberVerificationHandler(IPhoneNumberService phoneNumberService)
    {
        _phoneNumberService = phoneNumberService;
    }

    /// <inheritdoc/>
    public override Task CreatedAsync(CreateContentContext context)
        => TrackAsync(context.ContentItem);

    /// <inheritdoc/>
    public override Task UpdatedAsync(UpdateContentContext context)
        => TrackAsync(context.ContentItem);

    private Task TrackAsync(ContentItem contentItem)
    {
        if (string.IsNullOrEmpty(contentItem.ContentItemId) || !contentItem.TryGet<OmnichannelContactPart>(out _))
        {
            return Task.CompletedTask;
        }

        if (!PreparePendingVerification(contentItem, _phoneNumberService))
        {
            return Task.CompletedTask;
        }

        AddDeferredTask();
        _contentItemIds.Add(contentItem.ContentItemId);

        return Task.CompletedTask;
    }

    private void AddDeferredTask()
    {
        if (_taskAdded)
        {
            return;
        }

        _taskAdded = true;

        var contentItemIds = _contentItemIds;

        ShellScope.AddDeferredTask(scope => VerifyAsync(scope, contentItemIds));
    }

    private static async Task VerifyAsync(ShellScope scope, HashSet<string> contentItemIds)
    {
        if (contentItemIds.Count == 0)
        {
            return;
        }

        var services = scope.ServiceProvider;
        var session = services.GetRequiredService<ISession>();
        var verificationManager = services.GetRequiredService<IPhoneNumberVerificationManager>();
        var siteService = services.GetRequiredService<ISiteService>();
        var queueProcessor = services.GetRequiredService<IPhoneNumberVerificationQueueProcessor>();
        var settings = await siteService.GetSettingsAsync<PhoneNumberVerificationsSettings>();

        if ((await verificationManager.GetEnabledProvidersAsync()).Count == 0)
        {
            return;
        }

        var contentItems = await session.Query<ContentItem, ContentItemIndex>(index =>
                index.Latest && index.ContentItemId.IsIn(contentItemIds))
            .ListAsync();

        await queueProcessor.ProcessAsync(contentItems, settings);

        foreach (var contentItem in contentItems)
        {
            await session.SaveAsync(contentItem);
        }

        await session.SaveChangesAsync();
    }

    private static bool IsSamePhoneNumber(
        PhoneNumberVerificationPart part,
        string phoneNumber,
        IPhoneNumberService phoneNumberService)
    {
        var normalizedPhoneNumber = NormalizePhoneNumber(phoneNumber, phoneNumberService);

        return string.Equals(part.NormalizedPhoneNumber, normalizedPhoneNumber, StringComparison.OrdinalIgnoreCase)
            || string.Equals(part.PhoneNumber, phoneNumber, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresVerificationUpdate(
        ContentItem contact,
        ContentItem phoneNumberContentItem,
        string phoneNumber,
        IPhoneNumberService phoneNumberService)
    {
        if (!contact.TryGet<PhoneNumberVerificationPart>(out var contactVerificationPart)
            || !IsSamePhoneNumber(contactVerificationPart, phoneNumber, phoneNumberService))
        {
            return true;
        }

        return phoneNumberContentItem is null
            || !phoneNumberContentItem.TryGet<PhoneNumberVerificationPart>(out var phoneVerificationPart)
            || !IsSamePhoneNumber(phoneVerificationPart, phoneNumber, phoneNumberService);
    }

    internal static bool PreparePendingVerification(
        ContentItem contentItem,
        IPhoneNumberService phoneNumberService)
    {
        ArgumentNullException.ThrowIfNull(contentItem);
        ArgumentNullException.ThrowIfNull(phoneNumberService);

        var phoneNumberContentItem = OmnichannelContactPhoneNumberResolver.GetPreferredPhoneNumberContentItem(contentItem);
        var phoneNumber = OmnichannelContactPhoneNumberResolver.GetPhoneNumber(phoneNumberContentItem);

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            if (HasPhoneNumberVerification(contentItem))
            {
                contentItem.ClearPhoneNumberVerification();
            }

            return false;
        }

        if (!RequiresVerificationUpdate(contentItem, phoneNumberContentItem, phoneNumber, phoneNumberService))
        {
            return false;
        }

        var normalizedPhoneNumber = NormalizePhoneNumber(phoneNumber, phoneNumberService);

        contentItem.AlterPhoneNumberVerificationPending(phoneNumber, normalizedPhoneNumber);
        phoneNumberContentItem.AlterPhoneNumberVerificationPending(phoneNumber, normalizedPhoneNumber);

        return true;
    }

    private static bool HasPhoneNumberVerification(ContentItem contentItem)
    {
        return contentItem.TryGet<PhoneNumberVerificationPart>(out var part)
            && (!string.IsNullOrEmpty(part.PhoneNumber) || !string.IsNullOrEmpty(part.NormalizedPhoneNumber));
    }

    private static string NormalizePhoneNumber(string phoneNumber, IPhoneNumberService phoneNumberService)
    {
        if (phoneNumberService.TryFormatToE164(phoneNumber, regionCode: null, out var e164Number))
        {
            return e164Number;
        }

        return phoneNumber;
    }
}
