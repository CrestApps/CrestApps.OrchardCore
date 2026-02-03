using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

internal sealed class McpPromptHandler : CatalogEntryHandlerBase<McpPrompt>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public McpPromptHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<McpPromptHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<McpPrompt> context)
        => PopulateAsync(context.Model, context.Data, true);

    public override Task UpdatingAsync(UpdatingContext<McpPrompt> context)
        => PopulateAsync(context.Model, context.Data, false);

    public override Task ValidatingAsync(ValidatingContext<McpPrompt> context)
    {
        if (string.IsNullOrEmpty(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["Display text is required."], [nameof(McpPrompt.DisplayText)]));
        }

        if (string.IsNullOrEmpty(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(McpPrompt.Name)]));
        }

        if (context.Model.Messages == null || context.Model.Messages.Count == 0)
        {
            context.Result.Fail(new ValidationResult(S["At least one message is required."], [nameof(McpPrompt.Messages)]));
        }
        else
        {
            for (int i = 0; i < context.Model.Messages.Count; i++)
            {
                var message = context.Model.Messages[i];
                if (string.IsNullOrWhiteSpace(message.Role))
                {
                    context.Result.Fail(new ValidationResult(S["Message {0} requires a role.", i + 1], [$"Messages[{i}].Role"]));
                }
                if (string.IsNullOrWhiteSpace(message.Content))
                {
                    context.Result.Fail(new ValidationResult(S["Message {0} requires content.", i + 1], [$"Messages[{i}].Content"]));
                }
            }
        }

        if (context.Model.Arguments != null)
        {
            for (int i = 0; i < context.Model.Arguments.Count; i++)
            {
                var argument = context.Model.Arguments[i];
                if (string.IsNullOrWhiteSpace(argument.Name))
                {
                    context.Result.Fail(new ValidationResult(S["Argument {0} requires a name.", i + 1], [$"Arguments[{i}].Name"]));
                }
            }
        }

        return Task.CompletedTask;
    }

    private Task PopulateAsync(McpPrompt prompt, JsonNode data, bool isNew)
    {
        if (isNew)
        {
            prompt.CreatedUtc = _clock.UtcNow;

            var user = _httpContextAccessor.HttpContext?.User;

            if (user is not null)
            {
                prompt.Author = user.Identity.Name;
                prompt.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            }
        }

        var displayText = data?[nameof(McpPrompt.DisplayText)]?.ToString();

        if (!string.IsNullOrWhiteSpace(displayText))
        {
            prompt.DisplayText = displayText;
        }

        var name = data?[nameof(McpPrompt.Name)]?.ToString();

        if (!string.IsNullOrWhiteSpace(name))
        {
            prompt.Name = name;
        }

        var description = data?[nameof(McpPrompt.Description)]?.ToString();

        if (!string.IsNullOrWhiteSpace(description))
        {
            prompt.Description = description;
        }

        return Task.CompletedTask;
    }
}
