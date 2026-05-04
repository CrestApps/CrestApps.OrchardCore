using System.Text;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using Cysharp.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Chat.Controllers;

/// <summary>
/// Provides endpoints for managing chat analytics resources.
/// </summary>
[Admin("AI/ChatAnalytics/{action}", "ChatAnalytics.{action}")]
public sealed class ChatAnalyticsController : Controller
{
    private readonly ISession _session;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDisplayManager<AIChatAnalyticsFilter> _filterDisplayManager;
    private readonly IDisplayManager<AIChatAnalyticsReport> _reportDisplayManager;
    private readonly YesSqlStoreOptions _yesSqlStoreOptions;
    private readonly IClock _clock;
    private readonly IUpdateModelAccessor _updateModelAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatAnalyticsController"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="filterDisplayManager">The filter display manager.</param>
    /// <param name="reportDisplayManager">The report display manager.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    public ChatAnalyticsController(
        ISession session,
        IAuthorizationService authorizationService,
        IDisplayManager<AIChatAnalyticsFilter> filterDisplayManager,
        IDisplayManager<AIChatAnalyticsReport> reportDisplayManager,
        IOptions<YesSqlStoreOptions> yesSqlStoreOptions,
        IClock clock,
        IUpdateModelAccessor updateModelAccessor)
    {
        _session = session;
        _authorizationService = authorizationService;
        _filterDisplayManager = filterDisplayManager;
        _reportDisplayManager = reportDisplayManager;
        _yesSqlStoreOptions = yesSqlStoreOptions.Value;
        _clock = clock;
        _updateModelAccessor = updateModelAccessor;
    }

    /// <summary>
    /// Performs the index operation.
    /// </summary>
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

    /// <summary>
    /// Performs the index post operation.
    /// </summary>
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

        return View(nameof(Index), viewModel);
    }

    /// <summary>
    /// Performs the export operation.
    /// </summary>
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
        var fileName = $"chat-analytics-{_clock.UtcNow:yyyyMMdd-HHmmss}.csv";

        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    private async Task<IReadOnlyList<AIChatSessionEvent>> ExecuteQueryAsync(AIChatAnalyticsFilter filter)
    {
        var query = _session.Query<AIChatSessionEvent>(collection: _yesSqlStoreOptions.AICollectionName);

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
        using var builder = ZString.CreateStringBuilder();
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
