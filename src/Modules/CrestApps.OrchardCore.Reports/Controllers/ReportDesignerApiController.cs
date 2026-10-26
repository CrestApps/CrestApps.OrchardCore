using System.Text.Json;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;
using OrchardCore.DataPipelines.SharedLinks;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;
using OrchardCore.Modules;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Reports.Controllers;

/// <summary>
/// The JSON endpoints the designer page calls: the data sources, data sets, and fields it can use, the formula
/// functions, a check of the query, the people a report can be shared with, and the report's share links.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
[Admin]
public sealed class ReportDesignerApiController : Controller
{
    private readonly IDataSourceManager _dataSourceManager;
    private readonly IExpressionFunctionCatalog _functions;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly ReportQueryPlanner _planner;
    private readonly ReportDesignService _designService;
    private readonly ISharedLinkManager _sharedLinks;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISession _session;
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignerApiController"/> class.
    /// </summary>
    /// <param name="dataSourceManager">The data source manager.</param>
    /// <param name="functions">The functions formulas may call.</param>
    /// <param name="localizerFactory">The localizer factory, for the names of the data sources.</param>
    /// <param name="planner">The query planner.</param>
    /// <param name="designService">The design service.</param>
    /// <param name="sharedLinks">The shared links of Orchard Core.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="session">The YesSql session used to search users.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportDesignerApiController(
        IDataSourceManager dataSourceManager,
        IExpressionFunctionCatalog functions,
        IStringLocalizerFactory localizerFactory,
        ReportQueryPlanner planner,
        ReportDesignService designService,
        ISharedLinkManager sharedLinks,
        IAuthorizationService authorizationService,
        ISession session,
        ILogger<ReportDesignerApiController> logger,
        IStringLocalizer<ReportDesignerApiController> stringLocalizer)
    {
        _dataSourceManager = dataSourceManager;
        _functions = functions;
        _localizerFactory = localizerFactory;
        _planner = planner;
        _designService = designService;
        _sharedLinks = sharedLinks;
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
            DisplayName = _localizerFactory.Localize(source.DisplayName).Value,
            Description = _localizerFactory.Localize(source.Description).Value,
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

        var dataSource = _dataSourceManager.GetDataSource(source);

        if (dataSource is null)
        {
            return NotFound();
        }

        var dataSets = await dataSource.GetDataSetsAsync(new DataSourceContext { User = User }, HttpContext.RequestAborted);

        return Json(dataSets.Select(DescribeDataSet), ReportDesignerJson.Options);
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

        var dataSource = _dataSourceManager.GetDataSource(source);

        if (dataSource is null)
        {
            return NotFound();
        }

        try
        {
            var schema = await dataSource.GetSchemaAsync(dataSet, new DataSourceContext { User = User }, HttpContext.RequestAborted);

            if (schema is null)
            {
                return NotFound();
            }

            return Json(new
            {
                DataSet = DescribeDataSet(schema.DataSet),
                Fields = schema.Fields.Select(field => new
                {
                    field.Name,
                    field.DisplayName,
                    DataType = field.Type,
                    field.Group,
                    field.Description,
                    field.IsIdentifier,
                    field.IsKeyFilterable,
                    field.References,
                }),
            }, ReportDesignerJson.Options);
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

        // The function catalog belongs to Orchard Core and describes each function with plain strings, which Orchard
        // Core's own formula editor translates the same way.
#pragma warning disable OCLOC104 // The localizer key is not a compile-time constant
        return Json(_functions.Functions.Select(function => new
        {
            function.Name,
            Category = S[function.Category].Value,
            function.Signature,
            Description = S[function.Description].Value,
            function.IsAggregate,
        }), ReportDesignerJson.Options);
#pragma warning restore OCLOC104
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
            var plan = await _planner.PlanAsync(query, new DataSourceContext { User = User }, HttpContext.RequestAborted);

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
    /// Lists the shared links of a report, whether they work or not.
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

        var links = await _sharedLinks.ListAsync(ReportsConstants.SharedLinkKind, design.ItemId, HttpContext.RequestAborted);

        return Json(links.Select(link => new
        {
            Id = link.LinkId,
            Note = link.Properties.GetValueOrDefault(ReportSharedLinkHandler.NoteProperty),
            Recipients = link.RecipientNames,
            link.CreatedUtc,
            link.ExpiresUtc,
            link.RevokedUtc,
            link.Uses,
            link.MaxUses,
            link.IsUsedUp,
            AllowExport = ReportSharedLinkHandler.AllowsExport(link),
        }), ReportDesignerJson.Options);
    }

    /// <summary>
    /// Shares a report with users of the site through a link. The returned URL holds the secret token and is shown only
    /// once; the link is emailed to its recipients when asked to.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <param name="request">The link settings.</param>
    /// <returns>The new link, its URL, and the recipients it was emailed to, as JSON.</returns>
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

        if (request is null || request.ExpiresAfterDays < 1)
        {
            return BadRequest();
        }

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
            result = await _sharedLinks.CreateAsync(linkRequest, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException exception)
        {
            // A recipient is not a user of the site, there is no recipient, or emailing is asked for while the Email feature
            // is off.
            return Json(new
            {
                Errors = new[] { exception.Message },
            }, ReportDesignerJson.Options);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User '{UserName}' shared designed report '{ReportId}' with link '{LinkId}', expiring {ExpiresUtc}.", User.Identity?.Name, design.ItemId, result.Link.LinkId, result.Link.ExpiresUtc);
        }

        return Json(new
        {
            Id = result.Link.LinkId,
            result.Url,
            Recipients = result.Link.RecipientNames,
            result.Notified,
        }, ReportDesignerJson.Options);
    }

    /// <summary>
    /// Revokes a shared link of a report.
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

        var link = await _sharedLinks.GetAsync(linkId, HttpContext.RequestAborted);

        if (link is null ||
            !string.Equals(link.Kind, ReportsConstants.SharedLinkKind, StringComparison.Ordinal) ||
            !string.Equals(link.ResourceId, design.ItemId, StringComparison.Ordinal))
        {
            return NotFound();
        }

        await _sharedLinks.RevokeAsync(link, HttpContext.RequestAborted);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User '{UserName}' revoked link '{LinkId}' of designed report '{ReportId}'.", User.Identity?.Name, link.LinkId, design.ItemId);
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

    private static object DescribeDataSet(DataSetDescriptor dataSet)
    {
        return new
        {
            dataSet.Name,
            dataSet.DisplayName,
            dataSet.Description,
            dataSet.Group,
            dataSet.DefaultDateField,
            dataSet.References,
        };
    }
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
