using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using OrchardCore.DataPipelines.SharedLinks;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Opens a designed report through a shared link of Orchard Core, once the link checked that the signed-in user is one
/// of its recipients and holds its token. The report runs with its owner's access, like a shared report, and its
/// filters and exports stay on the link's address, so each of them is a use of the link.
/// </summary>
public sealed class ReportSharedLinkHandler : ISharedLinkHandler
{
    /// <summary>
    /// The property of a link that lets its recipients export the report, <c>true</c> or <c>false</c>.
    /// </summary>
    public const string AllowExportProperty = "AllowExport";

    /// <summary>
    /// The property of a link that holds the note of the person who shared it.
    /// </summary>
    public const string NoteProperty = "Note";

    // The query field of the link's address that asks for an export, with the name of the format.
    private const string FormatField = "format";

    // The query field of the link's address that holds its token.
    private const string TokenField = "token";

    private const string ViewPath = "~/Areas/CrestApps.OrchardCore.Reports/Views/SharedReports/Report.cshtml";

    private readonly ReportDesignService _designService;
    private readonly DesignedReportPresenter _presenter;
    private readonly IModelMetadataProvider _metadataProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportSharedLinkHandler"/> class.
    /// </summary>
    /// <param name="designService">The design service.</param>
    /// <param name="presenter">The report presenter.</param>
    /// <param name="metadataProvider">The model metadata provider of the report page.</param>
    /// <param name="logger">The logger.</param>
    public ReportSharedLinkHandler(
        ReportDesignService designService,
        DesignedReportPresenter presenter,
        IModelMetadataProvider metadataProvider,
        ILogger<ReportSharedLinkHandler> logger)
    {
        _designService = designService;
        _presenter = presenter;
        _metadataProvider = metadataProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public static string Kind => ReportsConstants.SharedLinkKind;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ReportSharedLinkHandler>("Report");

    /// <summary>
    /// Tells whether a link lets its recipients export the report.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <returns><see langword="true"/> when the link allows exports.</returns>
    public static bool AllowsExport(SharedLink link)
    {
        ArgumentNullException.ThrowIfNull(link);

        return link.Properties.TryGetValue(AllowExportProperty, out var value) && bool.TryParse(value, out var allow) && allow;
    }

    /// <inheritdoc/>
    public async Task<IActionResult> OpenAsync(SharedLink link, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(httpContext);

        var design = await _designService.FindAsync(link.ResourceId);

        if (design is null)
        {
            return new NotFoundResult();
        }

        var request = httpContext.Request;
        var userName = httpContext.User.Identity?.Name;
        var allowExport = AllowsExport(link);
        var format = request.Query[FormatField].ToString();

        httpContext.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        httpContext.Response.Headers["Referrer-Policy"] = "no-referrer";

        if (!string.IsNullOrEmpty(format))
        {
            if (!allowExport)
            {
                return new NotFoundResult();
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Shared link '{LinkId}' exported designed report '{ReportId}' for '{UserName}'.", link.LinkId, design.ItemId, userName);
            }

            var file = await _presenter.ExportAsync(design, request.Query, format, httpContext.RequestAborted, ReportEventSource.Link, userName);

            return file is null
                ? new NotFoundResult()
                : new FileContentResult(file.Content, file.ContentType) { FileDownloadName = file.FileName };
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Shared link '{LinkId}' opened designed report '{ReportId}' for '{UserName}'.", link.LinkId, design.ItemId, userName);
        }

        // The filters and the exports submit to the link's address, with its token.
        var address = (request.PathBase + request.Path).Value;
        var model = await _presenter.BuildAsync(
            design,
            request.Query,
            allowExport,
            _ => address,
            address,
            httpContext.RequestAborted,
            ReportEventSource.Link,
            userName);

        model.IsPublic = true;
        model.ExportFormatField = FormatField;
        model.HiddenFields[TokenField] = request.Query[TokenField].ToString();

        return new ViewResult
        {
            ViewName = ViewPath,
            ViewData = new ViewDataDictionary(_metadataProvider, new ModelStateDictionary())
            {
                Model = model,
            },
        };
    }

    /// <inheritdoc/>
    public string GetSourceUrl(SharedLink link, IUrlHelper urlHelper)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(urlHelper);

        return urlHelper.RouteUrl("ReportDesignerEdit", new { id = link.ResourceId });
    }
}
