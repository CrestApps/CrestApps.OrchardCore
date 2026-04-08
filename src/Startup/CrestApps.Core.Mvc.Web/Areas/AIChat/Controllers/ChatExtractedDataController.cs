using System.Text;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Mvc.Web.Areas.AIChat.Services;
using CrestApps.Core.Mvc.Web.Areas.AIChat.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Controllers;

[Area("AIChat")]
[Authorize(Policy = "Admin")]
public sealed class ChatExtractedDataController : Controller
{
    private readonly IAIProfileManager _profileManager;
    private readonly MvcAIChatSessionExtractedDataService _extractedDataService;
    private readonly TimeProvider _timeProvider;

    public ChatExtractedDataController(
        IAIProfileManager profileManager,
        MvcAIChatSessionExtractedDataService extractedDataService,
        TimeProvider timeProvider)
    {
        _profileManager = profileManager;
        _extractedDataService = extractedDataService;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(await BuildViewModelAsync(new ChatExtractedDataIndexViewModel(), showReport: false));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Index))]
    public async Task<IActionResult> IndexPost(ChatExtractedDataIndexViewModel model)
    {
        if (string.IsNullOrEmpty(model.ProfileId))
        {
            ModelState.AddModelError(nameof(model.ProfileId), "AI Profile is required.");
        }

        model = await BuildViewModelAsync(model, showReport: ModelState.IsValid);

        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        var records = await _extractedDataService.GetAsync(model.ProfileId, model.StartDateUtc, model.EndDateUtc);
        ApplyReport(model, records);

        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Export(ChatExtractedDataIndexViewModel model)
    {
        if (string.IsNullOrEmpty(model.ProfileId))
        {
            return BadRequest();
        }

        var records = await _extractedDataService.GetAsync(model.ProfileId, model.StartDateUtc, model.EndDateUtc);
        var rows = BuildRows(records);
        var columns = rows
            .SelectMany(row => row.Values.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var fileName = $"chat-extracted-data-{_timeProvider.GetUtcNow():yyyyMMdd-HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(GenerateCsv(rows, columns)), "text/csv", fileName);
    }

    private async Task<ChatExtractedDataIndexViewModel> BuildViewModelAsync(ChatExtractedDataIndexViewModel model, bool showReport)
    {
        var profiles = await _profileManager.GetAsync(AIProfileType.Chat);

        model.Profiles =
        [
            new SelectListItem("Select a profile", string.Empty, string.IsNullOrEmpty(model.ProfileId)),
            .. profiles
                .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.OrdinalIgnoreCase)
                .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.ItemId, profile.ItemId == model.ProfileId)),
        ];
        model.ShowReport = showReport;

        return model;
    }

    private static void ApplyReport(ChatExtractedDataIndexViewModel model, IReadOnlyList<AIChatSessionExtractedDataRecord> records)
    {
        var rows = BuildRows(records);
        model.Rows = rows;
        model.Columns = rows
            .SelectMany(row => row.Values.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static List<ChatExtractedDataRowViewModel> BuildRows(IReadOnlyList<AIChatSessionExtractedDataRecord> records) =>
        records
            .Select(record => new ChatExtractedDataRowViewModel
            {
                SessionId = record.SessionId,
                SessionStartedUtc = record.SessionStartedUtc,
                Values = record.Values.ToDictionary(
                    pair => pair.Key,
                    pair => string.Join(", ", pair.Value),
                    StringComparer.OrdinalIgnoreCase),
            })
            .ToList();

    private static string GenerateCsv(
        IReadOnlyList<ChatExtractedDataRowViewModel> rows,
        IReadOnlyList<string> columns)
    {
        var builder = new StringBuilder();
        builder.Append("SessionStartedUtc,SessionId");

        foreach (var column in columns)
        {
            builder.Append(',').Append(EscapeCsv(column));
        }

        builder.AppendLine();

        foreach (var row in rows)
        {
            builder.Append(row.SessionStartedUtc.ToString("o")).Append(',')
                .Append(EscapeCsv(row.SessionId));

            foreach (var column in columns)
            {
                row.Values.TryGetValue(column, out var value);
                builder.Append(',').Append(EscapeCsv(value));
            }

            builder.AppendLine();
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
