using System.Data;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Documents;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class DefaultAIChatProfileManager : IAIChatProfileManager
{
    private readonly IDocumentManager<AIChatProfileDocument> _documentManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IAIChatProfileHandler> _handlers;
    private readonly ILogger _logger;

    public DefaultAIChatProfileManager(
        IDocumentManager<AIChatProfileDocument> documentManager,
        IServiceProvider serviceProvider,
        IEnumerable<IAIChatProfileHandler> handlers,
        ILogger<DefaultAIChatProfileManager> logger)
    {
        _documentManager = documentManager;
        _serviceProvider = serviceProvider;
        _handlers = handlers;
        _logger = logger;
    }

    public async Task<bool> DeleteAsync(AIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var deletingContext = new DeletingAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletingAsync(ctx), deletingContext, _logger);

        if (string.IsNullOrEmpty(profile.Id))
        {
            return false;
        }

        var document = await _documentManager.GetOrCreateMutableAsync();

        var removed = document.Profiles.Remove(profile.Id);

        if (removed)
        {
            await _documentManager.UpdateAsync(document);
        }

        var deletedContext = new DeletedAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletedAsync(ctx), deletedContext, _logger);

        return removed;
    }

    public async Task<AIChatProfile> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var document = await _documentManager.GetOrCreateImmutableAsync();

        if (document.Profiles.TryGetValue(id, out var profile))
        {
            await LoadAsync(profile);

            return profile;
        }

        return null;
    }

    public async Task<AIChatProfile> NewAsync(string source, JsonNode data = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        var ruleSource = _serviceProvider.GetKeyedService<IAIChatProfileSource>(source);

        if (ruleSource == null)
        {
            _logger.LogWarning("Unable to find a profile-source that can handle the source '{Source}'.", source);

            return null;
        }

        var id = IdGenerator.GenerateId();

        var profile = new AIChatProfile()
        {
            Id = id,
            Source = source,
        };

        var initializingContext = new InitializingAIChatProfileContext(profile, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializingAsync(ctx), initializingContext, _logger);

        var initializedContext = new InitializedAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializedAsync(ctx), initializedContext, _logger);

        // Set the source again after calling handlers to prevent handlers from updating the source during initialization.
        profile.Source = source;

        if (string.IsNullOrEmpty(profile.Id))
        {
            profile.Id = id;
        }

        return profile;
    }

    public async Task<AIProfileResult> PageQueriesAsync(int page, int pageSize, QueryContext context)
    {
        var records = await LocateQueriesAsync(context);

        var skip = (page - 1) * pageSize;

        var result = new AIProfileResult
        {
            Count = records.Count(),
            Profiles = records.Skip(skip).Take(pageSize).ToArray()
        };

        foreach (var record in result.Profiles)
        {
            await LoadAsync(record);
        }

        return result;
    }

    public async Task SaveAsync(AIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var savingContext = new SavingAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavingAsync(ctx), savingContext, _logger);

        var document = await _documentManager.GetOrCreateMutableAsync();

        if (string.IsNullOrEmpty(profile.Id))
        {
            profile.Id = IdGenerator.GenerateId();
        }

        document.Profiles[profile.Id] = profile;

        await _documentManager.UpdateAsync(document);

        var savedContext = new SavedAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavedAsync(ctx), savedContext, _logger);
    }

    public async Task UpdateAsync(AIChatProfile profile, JsonNode data = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var updatingContext = new UpdatingAIChatProfileContext(profile, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatingAsync(ctx), updatingContext, _logger);

        var updatedContext = new UpdatedAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatedAsync(ctx), updatedContext, _logger);
    }

    public async Task<AIChatProfileValidateResult> ValidateAsync(AIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var validatingContext = new ValidatingAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatingAsync(ctx), validatingContext, _logger);

        var validatedContext = new ValidatedAIChatProfileContext(profile, validatingContext.Result);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatedAsync(ctx), validatedContext, _logger);

        return validatingContext.Result;
    }

    private Task LoadAsync(AIChatProfile profile)
    {
        var loadedContext = new LoadedAIChatProfileContext(profile);

        return _handlers.InvokeAsync((handler, context) => handler.LoadedAsync(context), loadedContext, _logger);
    }

    private async Task<IEnumerable<AIChatProfile>> LocateQueriesAsync(QueryContext context)
    {
        var document = await _documentManager.GetOrCreateImmutableAsync();

        if (context == null)
        {
            return document.Profiles.Values;
        }

        var queries = document.Profiles.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(context.Source))
        {
            queries = queries.Where(x => x.Source.Equals(context.Source, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(context.Name))
        {
            queries = queries.Where(x => x.Title.Contains(context.Name, StringComparison.OrdinalIgnoreCase));
        }

        if (context.Sorted)
        {
            queries = queries.OrderBy(x => x.Title);
        }

        return queries;
    }
}
