using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using ModelContextProtocol.Protocol;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

internal sealed class McpPromptHandler : CatalogEntryHandlerBase<McpPrompt>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpPromptHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public McpPromptHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<McpPromptHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<McpPrompt> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data, true);

    public override Task UpdatingAsync(UpdatingContext<McpPrompt> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data, false);

    public override Task ValidatingAsync(ValidatingContext<McpPrompt> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], ["Name"]));
        }

        if (string.IsNullOrEmpty(context.Model.Prompt?.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], ["Prompt.Name"]));
        }

        if (string.IsNullOrEmpty(context.Model.Prompt?.Title))
        {
            context.Result.Fail(new ValidationResult(S["Title is required."], ["Prompt.Title"]));
        }

        if (context.Model.Prompt?.Arguments != null)
        {
            for (var i = 0; i < context.Model.Prompt.Arguments.Count; i++)
            {
                var argument = context.Model.Prompt.Arguments[i];
                if (string.IsNullOrWhiteSpace(argument.Name))
                {
                    context.Result.Fail(new ValidationResult(S["Argument {0} requires a name.", i + 1], [$"Prompt.Arguments[{i}].Name"]));
                }
            }
        }

        return Task.CompletedTask;
    }

    private Task PopulateAsync(McpPrompt entry, JsonNode data, bool isNew)
    {
        if (isNew)
        {
            entry.CreatedUtc = _clock.UtcNow;

            var user = _httpContextAccessor.HttpContext?.User;

            if (user is not null)
            {
                entry.Author = user.Identity.Name;
                entry.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            }
        }

        var name = data?[nameof(McpPrompt.Name)]?.ToString();

        if (!string.IsNullOrWhiteSpace(name))
        {
            entry.Name = name;
        }

        // Populate the Prompt from data if provided
        var promptData = data?[nameof(McpPrompt.Prompt)];

        if (promptData != null)
        {
            entry.Prompt ??= new Prompt
            {
                Name = name,
            };

            var title = promptData[nameof(Prompt.Title)]?.ToString();
            if (!string.IsNullOrWhiteSpace(title))
            {
                entry.Prompt.Title = title;
            }

            var description = promptData[nameof(Prompt.Description)]?.ToString();
            if (!string.IsNullOrWhiteSpace(description))
            {
                entry.Prompt.Description = description;
            }
        }

        return Task.CompletedTask;
    }
}
