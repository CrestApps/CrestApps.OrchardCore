using System.Text.Json;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Expressions;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using ISession = YesSql.ISession;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Controllers;

/// <summary>
/// The JSON endpoints the designer page calls: the data sources, data sets, and fields it can use, the formula
/// functions, a check of the query, the people a report can be shared with, and the report's share links.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
[Admin]
public sealed class ReportDesignerApiController : Controller
{
    private readonly IReportDataSourceManager _dataSourceManager;
    private readonly ReportQueryPlanner _planner;
    private readonly ReportDesignService _designService;
    private readonly ReportShareLinkService _shareLinks;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISession _session;
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignerApiController"/> class.
    /// </summary>
    /// <param name="dataSourceManager">The data source manager.</param>
    /// <param name="planner">The query planner.</param>
    /// <param name="designService">The design service.</param>
    /// <param name="shareLinks">The share link service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="session">The YesSql session used to search users.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportDesignerApiController(
        IReportDataSourceManager dataSourceManager,
        ReportQueryPlanner planner,
        ReportDesignService designService,
        ReportShareLinkService shareLinks,
        IAuthorizationService authorizationService,
        ISession session,
        ILogger<ReportDesignerApiController> logger,
        IStringLocalizer<ReportDesignerApiController> stringLocalizer)
    {
        _dataSourceManager = dataSourceManager;
        _planner = planner;
        _designService = designService;
        _shareLinks = shareLinks;
        _authorizationService = authorizationService;
        _session = session;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the data sources.
    /// </summary>
    /// <returns>The data sources as JSON.</returns>
    [HttpGet]
    [Admin("reports/builder/api/sources", "ReportDesignerApiSources")]
    public async Task<IActionResult> Sources()
    {
        if (!await CanDesignAsync())
        {
            return Forbid();
        }

        return Json(_dataSourceManager.GetDataSources().Select(source => new
        {
            source.Name,
            DisplayName = source.DisplayName.Value,
            Description = source.Description.Value,
        }), ReportDesignerJson.Options);
    }

    /// <summary>
    /// Lists the data sets of a data source the user may read.
    /// </summary>
    /// <param name="source">The data source name.</param>
    /// <returns>The data sets as JSON.</returns>
    [HttpGet]
    [Admin("reports/builder/api/datasets", "ReportDesignerApiDataSets")]
    public async Task<IActionResult> DataSets(string source)
    {
        if (!await CanDesignAsync())
        {
            return Forbid();
        }

        var dataSource = _dataSourceManager.FindDataSource(source);

        if (dataSource is null)
        {
            return NotFound();
        }

        var dataSets = await dataSource.GetDataSetsAsync(new ReportDataSourceContext { User = User }, HttpContext.RequestAborted);

        return Json(dataSets, ReportDesignerJson.Options);
    }

    /// <summary>
    /// Describes the fields of a data set.
    /// </summary>
    /// <param name="source">The data source name.</param>
    /// <param name="dataSet">The data set name.</param>
    /// <returns>The schema as JSON.</returns>
    [HttpGet]
    [Admin("reports/builder/api/schema", "ReportDesignerApiSchema")]
    public async Task<IActionResult> Schema(string source, string dataSet)
    {
        if (!await CanDesignAsync())
        {
            return Forbid();
        }

        var dataSource = _dataSourceManager.FindDataSource(source);

        if (dataSource is null)
        {
            return NotFound();
        }

        try
        {
            var schema = await dataSource.GetSchemaAsync(dataSet, new ReportDataSourceContext { User = User }, HttpContext.RequestAborted);

            return schema is null ? NotFound() : Json(schema, ReportDesignerJson.Options);
        }
        catch (ReportQueryException exception)
        {
            return Json(new
            {
                Errors = exception.Errors,
            }, ReportDesignerJson.Options);
        }
    }

    /// <summary>
    /// Lists the formula functions.
    /// </summary>
    /// <returns>The functions as JSON.</returns>
    [HttpGet]
    [Admin("reports/builder/api/functions", "ReportDesignerApiFunctions")]
    public async Task<IActionResult> Functions()
    {
        if (!await CanDesignAsync())
        {
            return Forbid();
        }

        return Json(ExpressionFunctions.All.Select(function => new
        {
            function.Name,
            Category = S[function.Category].Value,
            function.Signature,
            Description = S[function.Description].Value,
            function.IsAggregate,
        }), ReportDesignerJson.Options);
    }

    /// <summary>
    /// Checks the query being designed and describes every field it can use and the columns it produces.
    /// </summary>
    /// <returns>The fields, columns, and problems as JSON.</returns>
    [HttpPost]
    [Admin("reports/builder/api/plan", "ReportDesignerApiPlan")]
    public async Task<IActionResult> Plan()
    {
        if (!await CanDesignAsync())
        {
            return Forbid();
        }

        var body = await RequestBodyReader.ReadAsync(Request, ReportDesignerController.MaxPayloadBytes, HttpContext.RequestAborted);

        if (body.IsTooLarge || string.IsNullOrWhiteSpace(body.Body))
        {
            return BadRequest();
        }

        ReportQueryDefinition query;

        try
        {
            query = ReportDesignNormalizer.Normalize(JsonSerializer.Deserialize<ReportQueryDefinition>(body.Body, ReportDesignerJson.Options));
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        try
        {
            var plan = await _planner.PlanAsync(query, new ReportDataSourceContext { User = User }, HttpContext.RequestAborted);

            return Json(new
            {
                Fields = plan.Fields.Values.Select(field => new
                {
                    field.Key,
                    field.Label,
                    field.DataType,
                    field.Kind,
                    field.Alias,
                    field.IsAggregate,
                }),
                Columns = plan.Columns.Select(column => new
                {
                    column.Definition.Id,
                    column.Label,
                    column.DataType,
                    column.IsMeasure,
                }),
                plan.Errors,
            }, ReportDesignerJson.Options);
        }
        catch (ReportQueryException exception)
        {
            return Json(new
            {
                Fields = Array.Empty<object>(),
                Columns = Array.Empty<object>(),
                exception.Errors,
            }, ReportDesignerJson.Options);
        }
    }

    /// <summary>
    /// Searches the users a report can be shared with.
    /// </summary>
    /// <param name="query">The text to search for in user names and emails.</param>
    /// <returns>The matching users as <c>{ value, text }</c> JSON.</returns>
    [HttpGet]
    [Admin("reports/builder/api/users", "ReportDesignerApiUsers")]
    public async Task<IActionResult> Users(string query)
    {
        if (!await CanDesignAsync())
        {
            return Forbid();
        }

        var search = query?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(search) || search.Length > 100)
        {
            return Json(Array.Empty<object>());
        }

        var users = await _session.Query<User, UserIndex>(index => index.NormalizedUserName.Contains(search) || index.NormalizedEmail.Contains(search))
            .OrderBy(index => index.NormalizedUserName)
            .Take(20)
            .ListAsync();

        return Json(users.Select(user => new
        {
            value = user.UserName,
            text = user.UserName,
        }));
    }

    /// <summary>
    /// Lists the share links of a report.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <returns>The links as JSON.</returns>
    [HttpGet]
    [Admin("reports/builder/{id}/links", "ReportDesignerApiLinks")]
    public async Task<IActionResult> Links(string id)
    {
        var design = await FindShareableAsync(id);

        if (design is null)
        {
            return NotFound();
        }

        if (!await CanManageLinksAsync(design))
        {
            return Forbid();
        }

        var links = await _shareLinks.ListAsync(design.ItemId);

        return Json(links.Select(link => new
        {
            Id = link.ItemId,
            link.Name,
            link.TokenHint,
            link.ExpiresUtc,
            link.RevokedUtc,
            link.AllowExport,
            link.RequireSignIn,
            link.CreatedBy,
            link.CreatedUtc,
        }), ReportDesignerJson.Options);
    }

    /// <summary>
    /// Creates a share link for a report. The returned URL holds the secret token and is shown only once.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <param name="request">The link settings.</param>
    /// <returns>The new link and its URL as JSON.</returns>
    [HttpPost]
    [Admin("reports/builder/{id}/links/create", "ReportDesignerApiCreateLink")]
    public async Task<IActionResult> CreateLink(string id, [FromBody] ReportShareLinkRequest request)
    {
        var design = await FindShareableAsync(id);

        if (design is null)
        {
            return NotFound();
        }

        if (!await CanManageLinksAsync(design))
        {
            return Forbid();
        }

        if (request is null)
        {
            return BadRequest();
        }

        var (link, token) = await _shareLinks.CreateAsync(
            design.ItemId,
            ReportDesignNormalizer.Truncate(request.Name),
            request.ExpiresUtc,
            request.AllowExport,
            request.RequireSignIn,
            User.Identity?.Name);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User '{UserName}' created share link '{LinkId}' for designed report '{ReportId}', expiring {ExpiresUtc}.", User.Identity?.Name, link.ItemId, design.ItemId, link.ExpiresUtc);
        }

        return Json(new
        {
            Id = link.ItemId,
            Url = Url.RouteUrl("ReportsSharedLink", new { token }, Request.Scheme),
        }, ReportDesignerJson.Options);
    }

    /// <summary>
    /// Revokes a share link of a report.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <param name="linkId">The link identifier.</param>
    /// <returns>An empty result.</returns>
    [HttpPost]
    [Admin("reports/builder/{id}/links/{linkId}/revoke", "ReportDesignerApiRevokeLink")]
    public async Task<IActionResult> RevokeLink(string id, string linkId)
    {
        var design = await FindShareableAsync(id);

        if (design is null)
        {
            return NotFound();
        }

        if (!await CanManageLinksAsync(design))
        {
            return Forbid();
        }

        if (!await _shareLinks.RevokeAsync(design.ItemId, linkId))
        {
            return NotFound();
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User '{UserName}' revoked share link '{LinkId}' of designed report '{ReportId}'.", User.Identity?.Name, linkId, design.ItemId);
        }

        return NoContent();
    }

    private async Task<ReportDesign> FindShareableAsync(string id)
    {
        return await _designService.FindAsync(id);
    }

    private async Task<bool> CanManageLinksAsync(ReportDesign design)
    {
        return await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageAllReportDesigns, design) &&
            await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ShareReportsPublicly);
    }

    private Task<bool> CanDesignAsync()
    {
        return _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageOwnReportDesigns);
    }
}

/// <summary>
/// The settings of a new share link.
/// </summary>
public sealed class ReportShareLinkRequest
{
    /// <summary>
    /// Gets or sets a note that describes what the link is for.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets when the link stops working, in UTC, or <see langword="null"/> for never.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the link also allows exporting.
    /// </summary>
    public bool AllowExport { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the link works only for signed-in people.
    /// </summary>
    public bool RequireSignIn { get; set; }
}
