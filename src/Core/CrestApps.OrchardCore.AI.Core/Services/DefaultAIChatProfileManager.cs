using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Azure.Core.Services;

public sealed class DefaultAIChatProfileManager : IAIChatProfileManager
{
    private readonly IAIChatProfileStore _profileStore;
    private readonly IAIChatProfileManagerSession _profileManagerSession;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IAIChatProfileHandler> _handlers;
    private readonly ILogger _logger;

    public DefaultAIChatProfileManager(
        IAIChatProfileStore profileStore,
        IAIChatProfileManagerSession profileManagerSession,
        IServiceProvider serviceProvider,
        IEnumerable<IAIChatProfileHandler> handlers,
        ILogger<DefaultAIChatProfileManager> logger)
    {
        _profileStore = profileStore;
        _profileManagerSession = profileManagerSession;
        _serviceProvider = serviceProvider;
        _handlers = handlers;
        _logger = logger;
    }

    public async ValueTask<bool> DeleteAsync(AIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var deletingContext = new DeletingAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletingAsync(ctx), deletingContext, _logger);

        if (string.IsNullOrEmpty(profile.Id))
        {
            return false;
        }

        var removed = await _profileStore.DeleteAsync(profile);

        _profileManagerSession.Forget(profile.Id);

        var deletedContext = new DeletedAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletedAsync(ctx), deletedContext, _logger);

        return removed;
    }

    public async ValueTask<AIChatProfile> FindByIdAsync(string id)
    {
        var profile = await _profileStore.FindByIdAsync(id);

        if (profile is not null)
        {
            await LoadAsync(profile);

            return profile;
        }

        return null;
    }

    public async ValueTask<AIChatProfile> FindByNameAsync(string name)
    {
        var profile = await _profileStore.FindByNameAsync(name);

        if (profile is not null)
        {
            await LoadAsync(profile);

            return profile;
        }

        return null;
    }

    public async ValueTask<IEnumerable<AIChatProfile>> GetAsync(AIChatProfileType type)
    {
        var profiles = await _profileStore.GetProfilesAsync(type);

        foreach (var profile in profiles)
        {
            await LoadAsync(profile);
        }

        return profiles;
    }

    public async ValueTask<AIChatProfile> NewAsync(string source, JsonNode data = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        var profileSource = _serviceProvider.GetKeyedService<IAIChatProfileSource>(source);

        if (profileSource == null)
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

    public async ValueTask<AIChatProfileResult> PageAsync(int page, int pageSize, AIChatProfileQueryContext context)
    {
        var result = await _profileStore.PageAsync(page, pageSize, context);

        foreach (var record in result.Profiles)
        {
            await LoadAsync(record);
        }

        return result;
    }

    public async ValueTask SaveAsync(AIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var savingContext = new SavingAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavingAsync(ctx), savingContext, _logger);

        await _profileStore.SaveAsync(profile);

        var savedContext = new SavedAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavedAsync(ctx), savedContext, _logger);
    }

    public async ValueTask UpdateAsync(AIChatProfile profile, JsonNode data = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var updatingContext = new UpdatingAIChatProfileContext(profile, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatingAsync(ctx), updatingContext, _logger);

        var updatedContext = new UpdatedAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatedAsync(ctx), updatedContext, _logger);
    }

    public async ValueTask<AIChatProfileValidateResult> ValidateAsync(AIChatProfile profile)
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

        _profileManagerSession.Store(profile);

        return _handlers.InvokeAsync((handler, context) => handler.LoadedAsync(context), loadedContext, _logger);
    }
}
