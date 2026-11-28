using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Reports.Subscriptions.Models;
using CrestApps.OrchardCore.Reports.Subscriptions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// What the report subscription tools return about a subscription, from an explicit list of fields.
/// </summary>
internal static class ReportSubscriptionToolResults
{
    /// <summary>
    /// The sentence that says the site cannot email reports.
    /// </summary>
    public const string NoEmailNote = "The site's email isn't set up, so no subscription can be emailed until the Email feature is configured.";

    /// <summary>
    /// Describes a subscription.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="subscription">The subscription.</param>
    /// <param name="toLocalAsync">Converts a stored UTC time to the user's or the site's time zone.</param>
    /// <returns>The description.</returns>
    public static async Task<object> DescribeAsync(IServiceProvider services, ReportSubscription subscription, Func<DateTime?, Task<string>> toLocalAsync)
    {
        var descriptor = string.IsNullOrEmpty(subscription.ReportName) ? null : services.GetService<IReportManager>()?.GetDescriptor(subscription.ReportName);

        return new
        {
            id = subscription.ItemId,
            name = subscription.Name,
            report = subscription.ReportName,
            reportTitle = descriptor is null ? null : services.GetRequiredService<IStringLocalizerFactory>().Localize(descriptor.DisplayName).Value,
            reportAvailable = descriptor is not null,
            frequency = subscription.Frequency.ToString(),
            dayOfWeek = subscription.Frequency == ReportSubscriptionFrequency.Weekly ? subscription.DayOfWeek.ToString() : null,
            dayOfMonth = subscription.Frequency == ReportSubscriptionFrequency.Monthly ? subscription.DayOfMonth : (int?)null,
            hour = subscription.Hour,
            hourNote = "The hour is in the site's time zone.",
            recipients = ReportSubscriptionEditor.SplitRecipients(subscription.Recipients),
            attachmentFormat = subscription.AttachmentFormat,
            enabled = subscription.IsEnabled,
            nextRun = await toLocalAsync(subscription.NextRunUtc),
            lastRun = await toLocalAsync(subscription.LastRunUtc),
            lastError = subscription.LastError,
            createdBy = subscription.CreatedBy,
            created = await toLocalAsync(subscription.CreatedUtc),
            modifiedBy = subscription.ModifiedBy,
            modified = await toLocalAsync(subscription.ModifiedUtc),
            editUrl = GetEditPath(services, subscription.ItemId),
        };
    }

    private static string GetEditPath(IServiceProvider services, string id)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        return httpContext is null || linkGenerator is null
            ? null
            : linkGenerator.GetPathByRouteValues(httpContext, "ReportSubscriptionsEdit", new { itemId = id });
    }
}
