using System.Text.Json;
using CrestApps.AI.Mcp.Models;
using CrestApps.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Protocol;

namespace CrestApps.Mvc.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Admin")]
public sealed class McpPromptController : Controller
{
    private static readonly JsonSerializerOptions _indentedJsonOptions = new() { WriteIndented = true };

    private readonly INamedCatalog<McpPrompt> _catalog;
    private readonly TimeProvider _timeProvider;

    public McpPromptController(INamedCatalog<McpPrompt> catalog, TimeProvider timeProvider)
    {
        _catalog = catalog;
        _timeProvider = timeProvider;
    }

    public async Task<IActionResult> Index()
        => View((await _catalog.GetAllAsync())
            .OrderBy(prompt => prompt.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());

    public IActionResult Create()
        => View(new McpPromptViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(McpPromptViewModel model)
    {
        var arguments = ParseArguments(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var prompt = new McpPrompt
        {
            ItemId = UniqueId.GenerateId(),
            CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime,
        };

        Apply(model, prompt, arguments);

        await _catalog.CreateAsync(prompt);
        await _catalog.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var prompt = await _catalog.FindByIdAsync(id);
        if (prompt == null)
        {
            return NotFound();
        }

        return View(new McpPromptViewModel
        {
            ItemId = prompt.ItemId,
            Name = prompt.Name,
            Title = prompt.Prompt?.Title,
            Description = prompt.Prompt?.Description,
            Arguments = prompt.Prompt?.Arguments is { Count: > 0 }
                ? JsonSerializer.Serialize(prompt.Prompt.Arguments, _indentedJsonOptions)
                : "[]",
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(McpPromptViewModel model)
    {
        var prompt = await _catalog.FindByIdAsync(model.ItemId);
        if (prompt == null)
        {
            return NotFound();
        }

        // Preserve the original name since it is readonly after creation.
        model.Name = prompt.Name;

        var arguments = ParseArguments(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        Apply(model, prompt, arguments);

        await _catalog.UpdateAsync(prompt);
        await _catalog.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var prompt = await _catalog.FindByIdAsync(id);
        if (prompt == null)
        {
            return NotFound();
        }

        await _catalog.DeleteAsync(prompt);
        await _catalog.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private List<PromptArgument> ParseArguments(McpPromptViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Arguments))
        {
            return [];
        }

        try
        {
            var arguments = JsonSerializer.Deserialize<List<PromptArgument>>(model.Arguments) ?? [];

            for (var i = 0; i < arguments.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(arguments[i].Name))
                {
                    ModelState.AddModelError(nameof(model.Arguments), $"Argument {i + 1} requires a name.");
                }
            }

            return arguments.Where(argument => !string.IsNullOrWhiteSpace(argument.Name)).ToList();
        }
        catch (JsonException)
        {
            ModelState.AddModelError(nameof(model.Arguments), "Arguments must be valid JSON.");
            return [];
        }
    }

    private static void Apply(McpPromptViewModel model, McpPrompt prompt, List<PromptArgument> arguments)
    {
        var name = model.Name.Trim();
        prompt.Name = name;
        prompt.Prompt = new Prompt
        {
            Name = name,
            Title = model.Title?.Trim(),
            Description = model.Description?.Trim(),
            Arguments = arguments,
        };
    }
}
