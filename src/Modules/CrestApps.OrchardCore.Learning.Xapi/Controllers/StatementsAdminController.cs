using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Xapi.Core;
using CrestApps.OrchardCore.Learning.Xapi.Core.Services;
using CrestApps.OrchardCore.Learning.Xapi.Core.Validation;
using CrestApps.OrchardCore.Learning.Xapi.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Xapi.Controllers;

/// <summary>
/// The statements of the Learning Record Store in the admin: a list with filters by actor, verb, activity,
/// registration and date, and the page of one statement. Both need Read all learning statements.
/// </summary>
[Admin]
public sealed class StatementsAdminController : Controller
{
    private static readonly (string Id, string Label)[] _commonVerbs =
    [
        ("http://adlnet.gov/expapi/verbs/answered", "answered"),
        ("http://adlnet.gov/expapi/verbs/attempted", "attempted"),
        ("http://adlnet.gov/expapi/verbs/completed", "completed"),
        ("http://adlnet.gov/expapi/verbs/experienced", "experienced"),
        ("http://adlnet.gov/expapi/verbs/failed", "failed"),
        ("http://adlnet.gov/expapi/verbs/initialized", "initialized"),
        ("http://adlnet.gov/expapi/verbs/launched", "launched"),
        ("http://adlnet.gov/expapi/verbs/passed", "passed"),
        ("http://adlnet.gov/expapi/verbs/progressed", "progressed"),
        ("http://adlnet.gov/expapi/verbs/terminated", "terminated"),
        ("http://adlnet.gov/expapi/verbs/voided", "voided"),
        ("https://w3id.org/xapi/adl/verbs/abandoned", "abandoned"),
        ("https://w3id.org/xapi/adl/verbs/satisfied", "satisfied"),
        ("https://w3id.org/xapi/adl/verbs/waived", "waived"),
    ];

    private readonly XapiStatementStore _store;
    private readonly IAuthorizationService _authorizationService;
    private readonly LearnerUserLookup _userLookup;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatementsAdminController"/> class.
    /// </summary>
    public StatementsAdminController(
        XapiStatementStore store,
        IAuthorizationService authorizationService,
        LearnerUserLookup userLookup,
        ILocalClock localClock,
        IStringLocalizer<StatementsAdminController> stringLocalizer)
    {
        _store = store;
        _authorizationService = authorizationService;
        _userLookup = userLookup;
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the statements, newest first.
    /// </summary>
    /// <param name="options">The filters.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [HttpGet]
    [Admin("learning/statements", "LearningStatements")]
    public async Task<IActionResult> Index(StatementListOptions options, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningXapiPermissions.ReadAllLearningStatements))
        {
            return Forbid();
        }

        options ??= new StatementListOptions();

        var filter = new XapiStatementAdminFilter
        {
            Verb = Clean(options.Verb),
            Activity = Clean(options.Activity),
            Registration = XapiFormats.IsUuid(Clean(options.Registration)) ? Clean(options.Registration) : null,
            FromUtc = await LocalDayToUtcAsync(options.From, 0),
            ToUtc = await LocalDayToUtcAsync(options.To, 1),
        };

        await AddActorAsync(filter, Clean(options.Actor));

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var nothingMatches = !string.IsNullOrEmpty(Clean(options.Registration)) && filter.Registration is null;
        var (count, items) = nothingMatches
            ? (0, [])
            : await _store.PageForAdminAsync(filter, (pager.Page - 1) * pager.PageSize, pager.PageSize, HttpContext.RequestAborted);

        var rows = await RowsAsync(items);
        var route = new RouteValueDictionary
        {
            ["Options.Actor"] = options.Actor,
            ["Options.Verb"] = options.Verb,
            ["Options.Activity"] = options.Activity,
            ["Options.Registration"] = options.Registration,
            ["Options.From"] = options.From,
            ["Options.To"] = options.To,
        };

        var model = new StatementListViewModel
        {
            Options = options,
            Verbs = _commonVerbs
                .Select(verb => new SelectListItem(verb.Label, verb.Id, string.Equals(verb.Id, filter.Verb, StringComparison.Ordinal)))
                .ToList(),
        };

        if (!string.IsNullOrEmpty(filter.Verb) && !_commonVerbs.Any(verb => verb.Id == filter.Verb))
        {
            model.Verbs.Add(new SelectListItem(filter.Verb, filter.Verb, selected: true));
        }

        model.List = new AdminListContext<XapiStatementRow>
        {
            Source = rows,
            Pager = await shapeFactory.PagerAsync(pager, count, route),
            ToolbarActions = await shapeFactory.CreateAsync("LearningStatementListFilters", Arguments.From(new { ListModel = model })),
        };

        return View(model);
    }

    /// <summary>
    /// Shows a statement.
    /// </summary>
    /// <param name="id">The statement id.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("learning/statements/{id}", "LearningStatement")]
    public async Task<IActionResult> Display(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningXapiPermissions.ReadAllLearningStatements))
        {
            return Forbid();
        }

        var record = XapiFormats.IsUuid(id) ? await _store.FindByStatementIdAsync(id, HttpContext.RequestAborted) : null;

        if (record is null)
        {
            return NotFound();
        }

        var statement = JsonNode.Parse(record.Statement).AsObject();
        var row = (await RowsAsync([record]))[0];

        return View(new StatementDetailViewModel
        {
            Record = record,
            Summary = row.Summary,
            Actor = row.Actor,
            Timestamp = XapiAgents.Text(statement["timestamp"]),
            Json = statement.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
        });
    }

    private async Task<List<XapiStatementRow>> RowsAsync(IReadOnlyList<global::CrestApps.OrchardCore.Learning.Xapi.Models.XapiStatementRecord> records)
    {
        // An actor whose account is a user of this site is named by that user's name.
        var userNames = await _userLookup.GetUserNamesAsync(records.Select(record => record.ActorAccountName).Where(name => !string.IsNullOrEmpty(name)).Distinct());

        return records.Select(record =>
        {
            var statement = JsonNode.Parse(record.Statement).AsObject();
            var summary = XapiStatementSummary.Of(statement);
            var actor = record.ActorAccountName is { } accountName && userNames.TryGetValue(accountName, out var userName)
                ? userName
                : summary.ActorName ?? record.ActorAccountName ?? record.ActorKey ?? S["Anonymous group"].Value;

            return new XapiStatementRow
            {
                Record = record,
                Summary = summary,
                Actor = actor,
            };
        }).ToList();
    }

    private async Task AddActorAsync(XapiStatementAdminFilter filter, string actor)
    {
        if (string.IsNullOrEmpty(actor))
        {
            return;
        }

        if (actor.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            filter.ActorKeys.Add("mbox:" + actor);

            return;
        }

        if (actor.Contains('@'))
        {
            filter.ActorKeys.Add("mbox:mailto:" + actor);
        }

        // A user name names the account a launched course gave the user; otherwise the text is an account name.
        filter.ActorAccountName = await _userLookup.FindUserIdAsync(actor) ?? actor;
    }

    private async Task<DateTime?> LocalDayToUtcAsync(string day, int addDays)
    {
        if (!DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        return await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(date.AddDays(addDays), DateTimeKind.Unspecified));
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
