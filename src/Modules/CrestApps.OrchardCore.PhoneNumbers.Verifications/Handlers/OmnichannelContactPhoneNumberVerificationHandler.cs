using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Flows.Models;
using OrchardCore.Settings;
using YesSql;

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
        if (contentItem.Id == 0 || !contentItem.TryGet<OmnichannelContactPart>(out _))
        {
            return Task.CompletedTask;
        }

        var phoneNumberContentItem = GetPreferredPhoneNumberContentItem(contentItem);
        var phoneNumber = GetPhoneNumber(phoneNumberContentItem);

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            if (HasPhoneNumberVerification(contentItem))
            {
                AddDeferredTask();
                _contentItemIds.Add(contentItem.ContentItemId);
            }

            return Task.CompletedTask;
        }

        if (!RequiresVerificationUpdate(contentItem, phoneNumberContentItem, phoneNumber, _phoneNumberService))
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
        var logger = services.GetRequiredService<ILogger<OmnichannelContactPhoneNumberVerificationHandler>>();
        var session = services.GetRequiredService<ISession>();
        var phoneNumberService = services.GetRequiredService<IPhoneNumberService>();
        var verificationManager = services.GetRequiredService<IPhoneNumberVerificationManager>();
        var siteService = services.GetRequiredService<ISiteService>();
        var settings = await siteService.GetSettingsAsync<PhoneNumberVerificationsSettings>();
        var hasProvider = verificationManager.GetProviders().Count > 0;

        foreach (var contentItemId in contentItemIds)
        {
            var contentItem = await session.Query<ContentItem, ContentItemIndex>(index =>
                    index.Latest && index.ContentItemId == contentItemId)
                .FirstOrDefaultAsync();

            if (contentItem is null || !contentItem.TryGet<OmnichannelContactPart>(out _))
            {
                continue;
            }

            var phoneNumberContentItem = GetPreferredPhoneNumberContentItem(contentItem);
            var phoneNumber = GetPhoneNumber(phoneNumberContentItem);

            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                if (contentItem.TryGet<PhoneNumberVerificationPart>(out _))
                {
                    contentItem.ClearPhoneNumberVerification();
                    await session.SaveAsync(contentItem);
                }

                continue;
            }

            if (!RequiresVerificationUpdate(contentItem, phoneNumberContentItem, phoneNumber, phoneNumberService))
            {
                continue;
            }

            var normalizedPhoneNumber = NormalizePhoneNumber(phoneNumber, phoneNumberService);

            if (!hasProvider)
            {
                contentItem.AlterPhoneNumberVerificationPending(phoneNumber, normalizedPhoneNumber);
                phoneNumberContentItem.AlterPhoneNumberVerificationPending(phoneNumber, normalizedPhoneNumber);
                await session.SaveAsync(contentItem);

                continue;
            }

            try
            {
                var result = await verificationManager.VerifyAsync(phoneNumber);

                contentItem.AlterPhoneNumberVerificationResult(
                    result,
                    revalidationIntervalDays: settings.RevalidationIntervalDays);

                phoneNumberContentItem.AlterPhoneNumberVerificationResult(
                    result,
                    revalidationIntervalDays: settings.RevalidationIntervalDays);

                await session.SaveAsync(contentItem);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to automatically verify the phone number for contact content item '{ContentItemId}'.", contentItem.ContentItemId);

                contentItem.AlterPhoneNumberVerificationPending(phoneNumber, normalizedPhoneNumber);
                phoneNumberContentItem.AlterPhoneNumberVerificationPending(phoneNumber, normalizedPhoneNumber);
                await session.SaveAsync(contentItem);
            }
        }

        await session.SaveChangesAsync();
    }

    private static ContentItem GetPreferredPhoneNumberContentItem(ContentItem contact)
    {
        if (!contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bagPart)
            || bagPart.ContentItems is null
            || bagPart.ContentItems.Count == 0)
        {
            return null;
        }

        var phoneNumbers = new PriorityQueue<ContentItem, int>();

        foreach (var contentMethod in bagPart.ContentItems)
        {
            if (contentMethod.ContentType != OmnichannelConstants.ContentTypes.PhoneNumber
                || !contentMethod.TryGet<PhoneNumberInfoPart>(out var phonePart)
                || string.IsNullOrWhiteSpace(phonePart.Number?.PhoneNumber))
            {
                continue;
            }

            var priority = GetPhoneNumberPriority(phonePart.Type?.Text);

            if (priority is null)
            {
                continue;
            }

            phoneNumbers.Enqueue(contentMethod, priority.Value);
        }

        return phoneNumbers.Count > 0
            ? phoneNumbers.Dequeue()
            : null;
    }

    private static string GetPhoneNumber(ContentItem contentItem)
    {
        return contentItem is not null
            && contentItem.TryGet<PhoneNumberInfoPart>(out var phonePart)
            && !string.IsNullOrWhiteSpace(phonePart.Number?.PhoneNumber)
            ? phonePart.Number.PhoneNumber.Trim()
            : null;
    }

    private static int? GetPhoneNumberPriority(string type)
    {
        return type switch
        {
            "Cell" => 1,
            "Home" => 2,
            "Office" => 3,
            "Work" => 4,
            "Other" => 5,
            _ => null,
        };
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
