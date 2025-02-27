using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIProfileManager : IAIProfileManager
{
    private readonly IAIProfileStore _profileStore;
    private readonly IAIProfileManagerSession _profileManagerSession;
    private readonly AICompletionOptions _options;
    private readonly IEnumerable<IAIProfileHandler> _handlers;
    private readonly ILogger _logger;

    public DefaultAIProfileManager(
        IAIProfileStore profileStore,
        IAIProfileManagerSession profileManagerSession,
        IOptions<AICompletionOptions> options,
        IEnumerable<IAIProfileHandler> handlers,
        ILogger<DefaultAIProfileManager> logger)
    {
        _profileStore = profileStore;
        _profileManagerSession = profileManagerSession;
        _options = options.Value;
        _handlers = handlers;
        _logger = logger;
    }

    public async ValueTask<bool> DeleteAsync(AIProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var deletingContext = new DeletingContext<AIProfile>(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletingAsync(ctx), deletingContext, _logger);

        if (string.IsNullOrEmpty(profile.Id))
        {
            return false;
        }

        var removed = await _profileStore.DeleteAsync(profile);

        _profileManagerSession.Forget(profile.Id);

        var deletedContext = new DeletedContext<AIProfile>(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletedAsync(ctx), deletedContext, _logger);

        return removed;
    }

    public async ValueTask<AIProfile> FindByIdAsync(string id)
    {
        var profile = await _profileStore.FindByIdAsync(id);

        if (profile is not null)
        {
            await LoadAsync(profile);

            return profile;
        }

        return null;
    }

    public async ValueTask<AIProfile> FindByNameAsync(string name)
    {
        var profile = await _profileStore.FindByNameAsync(name);

        if (profile is not null)
        {
            await LoadAsync(profile);

            return profile;
        }

        return null;
    }

    public async ValueTask<IEnumerable<AIProfile>> GetAsync(AIProfileType type)
    {
        var profiles = await _profileStore.GetProfilesAsync(type);

        foreach (var profile in profiles)
        {
            await LoadAsync(profile);
        }

        return profiles;
    }

    public async ValueTask<AIProfile> NewAsync(string source, JsonNode data = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        if (!_options.ProfileSources.TryGetValue(source, out var profileSource))
        {
            _logger.LogWarning("Unable to find a profile-source that can handle the source '{Source}'.", source);

            return null;
        }

        var id = IdGenerator.GenerateId();

        var profile = new AIProfile()
        {
            Id = id,
            Source = source,
        };

        var initializingContext = new InitializingContext<AIProfile>(profile, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializingAsync(ctx), initializingContext, _logger);

        var initializedContext = new InitializedContext<AIProfile>(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializedAsync(ctx), initializedContext, _logger);

        // Set the source again after calling handlers to prevent handlers from updating the source during initialization.
        profile.Source = source;

        if (string.IsNullOrEmpty(profile.Id))
        {
            profile.Id = id;
        }

        return profile;
    }

    public async ValueTask<PageResult<AIProfile>> PageAsync(int page, int pageSize, QueryContext context)
    {
        var result = await _profileStore.PageAsync(page, pageSize, context);

        foreach (var profile in result.Records)
        {
            await LoadAsync(profile);
        }

        return result;
    }

    public async ValueTask<PageResult<AIProfile>> PageAsync(int page, int pageSize, AIProfileQueryContext context)
    {
        var result = await _profileStore.PageAsync(page, pageSize, context);

        foreach (var profile in result.Records)
        {
            await LoadAsync(profile);
        }

        return result;
    }

    public async ValueTask SaveAsync(AIProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var savingContext = new SavingContext<AIProfile>(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavingAsync(ctx), savingContext, _logger);

        await _profileStore.SaveAsync(profile);

        var savedContext = new SavedContext<AIProfile>(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavedAsync(ctx), savedContext, _logger);
    }

    public async ValueTask UpdateAsync(AIProfile profile, JsonNode data = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var updatingContext = new UpdatingContext<AIProfile>(profile, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatingAsync(ctx), updatingContext, _logger);

        var updatedContext = new UpdatedContext<AIProfile>(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatedAsync(ctx), updatedContext, _logger);
    }

    public async ValueTask<ValidationResultDetails> ValidateAsync(AIProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var validatingContext = new ValidatingContext<AIProfile>(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatingAsync(ctx), validatingContext, _logger);

        var validatedContext = new ValidatedContext<AIProfile>(profile, validatingContext.Result);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatedAsync(ctx), validatedContext, _logger);

        return validatingContext.Result;
    }

    private Task LoadAsync(AIProfile profile)
    {
        var loadedContext = new LoadedContext<AIProfile>(profile);

        _profileManagerSession.Store(profile);

        return _handlers.InvokeAsync((handler, context) => handler.LoadedAsync(context), loadedContext, _logger);
    }
}
