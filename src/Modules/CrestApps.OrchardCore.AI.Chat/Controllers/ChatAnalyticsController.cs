using System.Text;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Chat.Controllers;

[Admin("AI/ChatAnalytics/{action=Index}", "ChatAnalytics.{action}")]
public sealed class ChatAnalyticsController : Controller
{
    private readonly ISession _session;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDisplayManager<AIChatAnalyticsFilter> _filterDisplayManager;
    private readonly IDisplayManager<AIChatAnalyticsReport> _reportDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;

    public ChatAnalyticsController(
        ISession session,
        IAuthorizationService authorizationService,
        IDisplayManager<AIChatAnalyticsFilter> filterDisplayManager,
        IDisplayManager<AIChatAnalyticsReport> reportDisplayManager,
        IUpdateModelAccessor updateModelAccessor)
    {
        _session = session;
        _authorizationService = authorizationService;
        _filterDisplayManager = filterDisplayManager;
        _reportDisplayManager = reportDisplayManager;
        _updateModelAccessor = updateModelAccessor;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChatAnalyticsPermissionProvider.ViewChatAnalytics))
        {
            return Forbid();
        }

        var filter = new AIChatAnalyticsFilter();
        var filterShape = await _filterDisplayManager.BuildEditorAsync(filter, _updateModelAccessor.ModelUpdater, false);

        var viewModel = new ChatAnalyticsIndexViewModel
        {
            FilterShape = filterShape,
            Filter = filter,
            ShowReport = false,
        };

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    public async Task<IActionResult> IndexPost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChatAnalyticsPermissionProvider.ViewChatAnalytics))
        {
            return Forbid();
        }

        var filter = new AIChatAnalyticsFilter();
        var filterShape = await _filterDisplayManager.UpdateEditorAsync(filter, _updateModelAccessor.ModelUpdater, false);

        if (!ModelState.IsValid)
        {
            var errorViewModel = new ChatAnalyticsIndexViewModel
            {
                FilterShape = filterShape,
                Filter = filter,
                ShowReport = false,
            };

            return View("Index", errorViewModel);
        }

        // Build and execute the query with accumulated conditions from display drivers.
        var events = await ExecuteQueryAsync(filter);

        // Build the report using display drivers.
        var reportContext = new AIChatAnalyticsReport
        {
            Events = events,
            Filter = filter,
        };

        var reportShape = await _reportDisplayManager.BuildDisplayAsync(reportContext, _updateModelAccessor.ModelUpdater);

        var viewModel = new ChatAnalyticsIndexViewModel
        {
            FilterShape = filterShape,
            ReportShape = reportShape,
            Filter = filter,
            ShowReport = true,
        };

        return View("Index", viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> Export()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChatAnalyticsPermissionProvider.ExportChatAnalytics))
        {
            return Forbid();
        }

        var filter = new AIChatAnalyticsFilter();
        await _filterDisplayManager.UpdateEditorAsync(filter, _updateModelAccessor.ModelUpdater, false);

        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        var events = await ExecuteQueryAsync(filter);

        // Generate CSV content for export.
        var csv = GenerateCsvContent(events);
        var fileName = $"chat-analytics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    private async Task<IReadOnlyList<AIChatSessionEvent>> ExecuteQueryAsync(AIChatAnalyticsFilter filter)
    {
        var query = _session.Query<AIChatSessionEvent>(collection: AIConstants.CollectionName);

        // Apply all conditions accumulated by display drivers.
        foreach (var condition in filter.Conditions)
        {
            query = condition(query);
        }

        var events = await query.ListAsync();

        return events.ToList();
    }

    private static string GenerateCsvContent(IReadOnlyList<AIChatSessionEvent> events)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SessionId,ProfileId,VisitorId,UserId,IsAuthenticated,SessionStartedUtc,SessionEndedUtc,MessageCount,HandleTimeSeconds,IsResolved");

        foreach (var evt in events)
        {
            builder.Append(EscapeCsv(evt.SessionId));
            builder.Append(',');
            builder.Append(EscapeCsv(evt.ProfileId));
            builder.Append(',');
            builder.Append(EscapeCsv(evt.VisitorId));
            builder.Append(',');
            builder.Append(EscapeCsv(evt.UserId));
            builder.Append(',');
            builder.Append(evt.IsAuthenticated);
            builder.Append(',');
            builder.Append(evt.SessionStartedUtc.ToString("o"));
            builder.Append(',');
            builder.Append(evt.SessionEndedUtc?.ToString("o") ?? string.Empty);
            builder.Append(',');
            builder.Append(evt.MessageCount);
            builder.Append(',');
            builder.Append(evt.HandleTimeSeconds);
            builder.Append(',');
            builder.AppendLine(evt.IsResolved.ToString());
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
