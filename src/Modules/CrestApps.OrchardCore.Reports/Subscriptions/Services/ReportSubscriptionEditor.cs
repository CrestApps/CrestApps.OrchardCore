using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Reports.Subscriptions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Reports.Subscriptions.Services;

/// <summary>
/// What a person enters for a report subscription, on its admin page or through an AI tool.
/// </summary>
public sealed class ReportSubscriptionInput
{
    public string Name { get; set; }

    public string ReportName { get; set; }

    public ReportSubscriptionFrequency Frequency { get; set; } = ReportSubscriptionFrequency.Weekly;

    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;

    public int DayOfMonth { get; set; } = 1;

    public int Hour { get; set; } = 7;

    /// <summary>
    /// Gets or sets the email addresses, separated by commas, semicolons or new lines.
    /// </summary>
    public string Recipients { get; set; }

    public string AttachmentFormat { get; set; }

    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// A problem with what was entered for a subscription: the field it is about (empty for the whole form) and what to do.
/// </summary>
public sealed record ReportSubscriptionError(string Key, string Message);

/// <summary>
/// Checks what a person entered for a report subscription and copies it onto the subscription: a report they may view,
/// up to 50 email addresses, an hour and a day in range, and an attachment format they may export the report in.
/// </summary>
public sealed class ReportSubscriptionEditor
{
    /// <summary>
    /// The most addresses a subscription goes to.
    /// </summary>
    public const int MaxRecipients = 50;

    private static readonly EmailAddressAttribute _emailAddress = new();

    private readonly IReportManager _reports;
    private readonly IReportExportManager _exports;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public ReportSubscriptionEditor(
        IReportManager reports,
        IReportExportManager exports,
        IStringLocalizerFactory localizerFactory,
        IAuthorizationService authorizationService,
        IStringLocalizer<ReportSubscriptionEditor> stringLocalizer)
    {
        _reports = reports;
        _exports = exports;
        _localizerFactory = localizerFactory;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Splits recipients as the editor does: on commas, semicolons and new lines, trimmed, each address once.
    /// </summary>
    /// <param name="recipients">The addresses as entered.</param>
    /// <returns>The addresses.</returns>
    public static List<string> SplitRecipients(string recipients)
        => (recipients ?? string.Empty)
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Checks what was entered for <paramref name="user"/> and, when it is all right, copies it onto
    /// <paramref name="subscription"/>.
    /// </summary>
    /// <param name="user">The person saving the subscription.</param>
    /// <param name="input">What they entered.</param>
    /// <param name="subscription">The subscription to change, or <see langword="null"/> to check only.</param>
    /// <returns>The problems, in the order the form shows them; empty when the subscription was changed.</returns>
    public async Task<IReadOnlyList<ReportSubscriptionError>> ApplyAsync(ClaimsPrincipal user, ReportSubscriptionInput input, ReportSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = new List<ReportSubscriptionError>();

        // Only a report the person can see is theirs to send.
        if (string.IsNullOrEmpty(input.ReportName) || _reports.GetDescriptor(input.ReportName) is not { } descriptor ||
            !await _authorizationService.AuthorizeAsync(user, descriptor.Permission))
        {
            errors.Add(new(nameof(ReportSubscriptionInput.ReportName), S["Choose a report."]));
            descriptor = null;
        }

        var recipients = SplitRecipients(input.Recipients);

        if (recipients.Count == 0 || recipients.Count > MaxRecipients || recipients.Any(address => !_emailAddress.IsValid(address)))
        {
            errors.Add(new(nameof(ReportSubscriptionInput.Recipients), S["Enter up to 50 email addresses, separated by commas."]));
        }

        if (input.Hour is < 0 or > 23 || input.DayOfMonth is < 1 or > 28)
        {
            errors.Add(new(string.Empty, S["Choose an hour from 0 to 23, and a day of the month from 1 to 28."]));
        }

        if (!string.IsNullOrWhiteSpace(input.AttachmentFormat) && _exports.GetDescriptor(input.AttachmentFormat) is null)
        {
            errors.Add(new(nameof(ReportSubscriptionInput.AttachmentFormat), S["Choose a file format, or none."]));
        }
        else if (!string.IsNullOrWhiteSpace(input.AttachmentFormat) &&
            descriptor?.ExportPermission is not null &&
            !await _authorizationService.AuthorizeAsync(user, descriptor.ExportPermission))
        {
            // Attaching the file is exporting the report, which this report allows only with its export permission.
            errors.Add(new(nameof(ReportSubscriptionInput.AttachmentFormat), S["You may not export this report, so no file can be attached. Choose no file format."]));
        }

        if (errors.Count > 0 || subscription is null)
        {
            return errors;
        }

        subscription.Name = string.IsNullOrWhiteSpace(input.Name) ? _localizerFactory.Localize(descriptor.DisplayName).Value : input.Name.Trim();
        subscription.ReportName = descriptor.Name;
        subscription.Frequency = input.Frequency;
        subscription.DayOfWeek = input.DayOfWeek;
        subscription.DayOfMonth = input.DayOfMonth;
        subscription.Hour = input.Hour;
        subscription.Recipients = string.Join(", ", recipients);
        subscription.AttachmentFormat = string.IsNullOrWhiteSpace(input.AttachmentFormat) ? null : input.AttachmentFormat;
        subscription.IsEnabled = input.IsEnabled;

        return errors;
    }
}
