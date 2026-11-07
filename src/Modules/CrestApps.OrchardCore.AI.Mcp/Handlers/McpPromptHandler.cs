using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using ModelContextProtocol.Protocol;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

internal sealed class McpPromptHandler : CatalogEntryAuditHandlerBase<McpPrompt>
{
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
        : base(httpContextAccessor, clock)
    {
        S = stringLocalizer;
    }

    protected override void SetCreated(McpPrompt entry, DateTime createdUtc, string ownerId, string author, bool hasUser)
    {
        entry.CreatedUtc = createdUtc;

        if (hasUser)
        {
            entry.OwnerId = ownerId;
            entry.Author = author;
        }
    }

    public override Task InitializingAsync(InitializingContext<McpPrompt> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data, true);

    public override Task UpdatingAsync(UpdatingContext<McpPrompt> context, CancellationToken cancellationToken = default)
    {
        StampModified(context.Model);

        return PopulateAsync(context.Model, context.Data, false);
    }

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

        if (context.Model.Messages != null)
        {
            for (var i = 0; i < context.Model.Messages.Count; i++)
            {
                var role = context.Model.Messages[i].Role;
                if (!string.IsNullOrWhiteSpace(role)
                    && !string.Equals(role, "user", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
                {
                    context.Result.Fail(new ValidationResult(S["Message {0} must have a role of 'user' or 'assistant'.", i + 1], [$"Messages[{i}].Role"]));
                }
            }
        }

        return Task.CompletedTask;
    }

    private Task PopulateAsync(McpPrompt entry, JsonNode data, bool isNew)
    {
        if (isNew)
        {
            StampCreated(entry);
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

        // Populate the messages from data if provided (recipe/programmatic creation).
        var messagesData = data?[nameof(McpPrompt.Messages)]?.AsArray();

        if (messagesData is not null)
        {
            entry.Messages = messagesData
                .Where(m => m is not null)
                .Select(m =>
                {
                    var obj = m.AsObject();
                    return new McpPromptMessage
                    {
                        Role = obj[nameof(McpPromptMessage.Role)]?.ToString(),
                        Content = obj[nameof(McpPromptMessage.Content)]?.ToString(),
                    };
                })
                .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                .ToList();
        }

        return Task.CompletedTask;
    }
}
