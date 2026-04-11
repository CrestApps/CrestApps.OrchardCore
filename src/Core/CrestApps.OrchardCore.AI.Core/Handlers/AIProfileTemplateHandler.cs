using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class AIProfileTemplateHandler : CatalogEntryHandlerBase<AIProfileTemplate>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INamedCatalog<AIProfileTemplate> _templatesCatalog;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public AIProfileTemplateHandler(
        IHttpContextAccessor httpContextAccessor,
        INamedCatalog<AIProfileTemplate> templatesCatalog,
        IClock clock,
        IStringLocalizer<AIProfileTemplateHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _templatesCatalog = templatesCatalog;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AIProfileTemplate> context)
        => PopulateAsync(context.Model, context.Data, true);

    public override Task UpdatingAsync(UpdatingContext<AIProfileTemplate> context)
        => PopulateAsync(context.Model, context.Data, false);

    public override async Task ValidatingAsync(ValidatingContext<AIProfileTemplate> context)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Template name is required."], [nameof(AIProfileTemplate.Name)]));
        }
        else
        {
            var existing = await _templatesCatalog.FindByNameAsync(context.Model.Name);

            if (existing is not null && existing.ItemId != context.Model.ItemId)
            {
                context.Result.Fail(new ValidationResult(S["A template with this name already exists. The name must be unique."], [nameof(AIProfileTemplate.Name)]));
            }
        }

        if (string.IsNullOrWhiteSpace(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["Title is required."], [nameof(AIProfileTemplate.DisplayText)]));
        }
    }

    public override Task InitializedAsync(InitializedContext<AIProfileTemplate> context)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user != null)
        {
            context.Model.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Model.Author = user.Identity.Name;
        }

        return Task.CompletedTask;
    }

    public override Task CreatingAsync(CreatingContext<AIProfileTemplate> context)
    {
        if (string.IsNullOrWhiteSpace(context.Model.DisplayText))
        {
            context.Model.DisplayText = context.Model.Name;
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(AIProfileTemplate template, JsonNode data, bool isNew)
    {
        if (isNew)
        {
            var name = data[nameof(AIProfileTemplate.Name)]?.GetValue<string>()?.Trim();

            if (!string.IsNullOrEmpty(name))
            {
                template.Name = name;
            }

            var source = data[nameof(AIProfileTemplate.Source)]?.GetValue<string>()?.Trim();

            if (!string.IsNullOrEmpty(source))
            {
                template.Source = source;
            }
        }

        var displayText = data[nameof(AIProfileTemplate.DisplayText)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(displayText))
        {
            template.DisplayText = displayText;
        }

        var description = data[nameof(AIProfileTemplate.Description)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(description))
        {
            template.Description = description;
        }

        var category = data[nameof(AIProfileTemplate.Category)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(category))
        {
            template.Category = category;
        }

        var isListable = data[nameof(AIProfileTemplate.IsListable)];

        if (isListable != null)
        {
            template.IsListable = isListable.GetValue<bool>();
        }

        var properties = data[nameof(AIProfileTemplate.Properties)]?.AsObject();

        if (properties != null)
        {
            template.Properties ??= new Dictionary<string, object>();

            // Convert current properties to JsonObject for merge.
            var currentJson = JsonSerializer.SerializeToNode(template.Properties)?.AsObject() ?? [];

            // Snapshot existing properties before merge so named entries can be
            // merged by name (upsert) instead of being fully replaced.
            var existingSnapshot = currentJson.Clone();

            // Merge incoming properties.
            currentJson.Merge(properties, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
            });

            AIPropertiesMergeHelper.MergeNamedEntries(currentJson, existingSnapshot);

            // Convert back to dictionary.
            template.Properties = JsonSerializer.Deserialize<Dictionary<string, object>>(currentJson) ?? [];
        }

        if (string.IsNullOrWhiteSpace(template.DisplayText))
        {
            template.DisplayText = template.Name;
        }

        return Task.CompletedTask;
    }
}
