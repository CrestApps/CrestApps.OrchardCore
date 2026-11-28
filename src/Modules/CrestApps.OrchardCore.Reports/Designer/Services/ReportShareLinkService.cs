using CrestApps.OrchardCore.Reports.Designer.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.DataPipelines.SharedLinks;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Lists, creates, and revokes the shared links of a designed report, for the designer page and the AI tools. The
/// caller checks that the person may manage the report's links: <see cref="ReportDesignerPermissions.ManageAllReportDesigns"/>
/// on the report and <see cref="ReportDesignerPermissions.ShareReportsPublicly"/>.
/// </summary>
public sealed class ReportShareLinkService
{
    private readonly ISharedLinkManager _sharedLinks;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportShareLinkService"/> class.
    /// </summary>
    /// <param name="sharedLinks">The shared links of Orchard Core.</param>
    /// <param name="logger">The logger.</param>
    public ReportShareLinkService(
        ISharedLinkManager sharedLinks,
        ILogger<ReportShareLinkService> logger)
    {
        _sharedLinks = sharedLinks;
        _logger = logger;
    }

    /// <summary>
    /// Lists the shared links of a report, whether they work or not.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The links.</returns>
    public Task<IReadOnlyList<SharedLink>> ListAsync(ReportDesign design, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(design);

        return _sharedLinks.ListAsync(ReportsConstants.SharedLinkKind, design.ItemId, cancellationToken);
    }

    /// <summary>
    /// Shares a report with users of the site through a link. The returned URL holds the secret token; the link is
    /// emailed to its recipients when asked to.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <param name="request">The link settings; <see cref="ReportShareLinkRequest.ExpiresAfterDays"/> is at least 1.</param>
    /// <param name="userName">The name of the person creating the link, for the log.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The new link, or the reason it could not be created.</returns>
    public async Task<ReportShareLinkOutcome> CreateAsync(ReportDesign design, ReportShareLinkRequest request, string userName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(request);

        var note = ReportDesignNormalizer.Truncate(request.Note);
        var linkRequest = new SharedLinkRequest
        {
            Kind = ReportsConstants.SharedLinkKind,
            ResourceId = design.ItemId,
            Title = design.DisplayText,
            SourceName = design.DisplayText,
            Recipients = (request.Recipients ?? []).Where(recipient => !string.IsNullOrWhiteSpace(recipient)).Select(recipient => recipient.Trim()).ToArray(),
            Lifetime = TimeSpan.FromDays(request.ExpiresAfterDays),
            MaxUses = Math.Max(0, request.MaxUses),
            NotifyByEmail = request.NotifyByEmail,
            Message = note,
        };

        linkRequest.Properties[ReportSharedLinkHandler.AllowExportProperty] = request.AllowExport ? "true" : "false";

        if (!string.IsNullOrEmpty(note))
        {
            linkRequest.Properties[ReportSharedLinkHandler.NoteProperty] = note;
        }

        SharedLinkResult result;

        try
        {
            result = await _sharedLinks.CreateAsync(linkRequest, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            // A recipient is not a user of the site, there is no recipient, or emailing is asked for while the Email feature
            // is off.
            return new ReportShareLinkOutcome { Error = exception.Message };
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User '{UserName}' shared designed report '{ReportId}' with link '{LinkId}', expiring {ExpiresUtc}.", userName, design.ItemId, result.Link.LinkId, result.Link.ExpiresUtc);
        }

        return new ReportShareLinkOutcome { Result = result };
    }

    /// <summary>
    /// Revokes a shared link of a report.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <param name="linkId">The link identifier.</param>
    /// <param name="userName">The name of the person revoking the link, for the log.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The revoked link, or <see langword="null"/> when the report has no link with that identifier.</returns>
    public async Task<SharedLink> RevokeAsync(ReportDesign design, string linkId, string userName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(design);

        var link = string.IsNullOrEmpty(linkId) ? null : await _sharedLinks.GetAsync(linkId, cancellationToken);

        if (link is null ||
            !string.Equals(link.Kind, ReportsConstants.SharedLinkKind, StringComparison.Ordinal) ||
            !string.Equals(link.ResourceId, design.ItemId, StringComparison.Ordinal))
        {
            return null;
        }

        await _sharedLinks.RevokeAsync(link, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User '{UserName}' revoked link '{LinkId}' of designed report '{ReportId}'.", userName, link.LinkId, design.ItemId);
        }

        return link;
    }
}

/// <summary>
/// The outcome of creating a shared link of a report.
/// </summary>
public sealed class ReportShareLinkOutcome
{
    /// <summary>
    /// Gets the new link and its URL, which holds the secret token, or <see langword="null"/> when it was not created.
    /// </summary>
    public SharedLinkResult Result { get; init; }

    /// <summary>
    /// Gets why the link was not created, such as a recipient who is not a user of the site.
    /// </summary>
    public string Error { get; init; }
}

/// <summary>
/// The settings of a new shared link of a report.
/// </summary>
public sealed class ReportShareLinkRequest
{
    /// <summary>
    /// Gets or sets the user names or emails of the users of the site the link is for.
    /// </summary>
    public IList<string> Recipients { get; set; } = [];

    /// <summary>
    /// Gets or sets an optional note about what the link is for, sent with the email and shown with the link.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets the number of days the link works.
    /// </summary>
    public int ExpiresAfterDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets the most times the link may be used, or <c>0</c> for no limit. Opening the report, changing its
    /// filters, and exporting it are each a use.
    /// </summary>
    public int MaxUses { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the link also allows exporting.
    /// </summary>
    public bool AllowExport { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the link is emailed to its recipients.
    /// </summary>
    public bool NotifyByEmail { get; set; } = true;
}
