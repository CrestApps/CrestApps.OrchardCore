using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Core.Services;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class DefaultOpenAIChatProfileManager : IOpenAIChatProfileManager
{
    private readonly IOpenAIChatProfileStore _profileStore;
    private readonly IOpenAIChatProfileManagerSession _profileManagerSession;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IOpenAIChatProfileHandler> _handlers;
    private readonly ILogger _logger;

    public DefaultOpenAIChatProfileManager(
        IOpenAIChatProfileStore profileStore,
        IOpenAIChatProfileManagerSession profileManagerSession,
        IServiceProvider serviceProvider,
        IEnumerable<IOpenAIChatProfileHandler> handlers,
        ILogger<DefaultOpenAIChatProfileManager> logger)
    {
        _profileStore = profileStore;
        _profileManagerSession = profileManagerSession;
        _serviceProvider = serviceProvider;
        _handlers = handlers;
        _logger = logger;
    }

    public async Task<bool> DeleteAsync(OpenAIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var deletingContext = new DeletingOpenAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletingAsync(ctx), deletingContext, _logger);

        if (string.IsNullOrEmpty(profile.Id))
        {
            return false;
        }

        var removed = await _profileStore.DeleteAsync(profile);

        _profileManagerSession.Forget(profile.Id);

        var deletedContext = new DeletedOpenAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.DeletedAsync(ctx), deletedContext, _logger);

        return removed;
    }

    public async Task<OpenAIChatProfile> FindByIdAsync(string id)
    {
        var profile = await _profileStore.FindByIdAsync(id);

        if (profile is not null)
        {
            await LoadAsync(profile);

            return profile;
        }

        return null;
    }

    public async Task<OpenAIChatProfile> FindByNameAsync(string name)
    {
        var profile = await _profileStore.FindByNameAsync(name);

        if (profile is not null)
        {
            await LoadAsync(profile);

            return profile;
        }

        return null;
    }

    public async Task<OpenAIChatProfile> NewAsync(string source, JsonNode data = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        var profileSource = _serviceProvider.GetKeyedService<IOpenAIChatProfileSource>(source);

        if (profileSource == null)
        {
            _logger.LogWarning("Unable to find a profile-source that can handle the source '{Source}'.", source);

            return null;
        }

        var id = IdGenerator.GenerateId();

        var profile = new OpenAIChatProfile()
        {
            Id = id,
            Source = source,
        };

        var initializingContext = new InitializingOpenAIChatProfileContext(profile, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializingAsync(ctx), initializingContext, _logger);

        var initializedContext = new InitializedOpenAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.InitializedAsync(ctx), initializedContext, _logger);

        // Set the source again after calling handlers to prevent handlers from updating the source during initialization.
        profile.Source = source;

        if (string.IsNullOrEmpty(profile.Id))
        {
            profile.Id = id;
        }

        return profile;
    }

    public async Task<OpenAIChatProfileResult> PageAsync(int page, int pageSize, QueryContext context)
    {
        var result = await _profileStore.PageAsync(page, pageSize, context);

        foreach (var record in result.Profiles)
        {
            await LoadAsync(record);
        }

        return result;
    }

    public async Task SaveAsync(OpenAIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var savingContext = new SavingOpenAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavingAsync(ctx), savingContext, _logger);

        await _profileStore.SaveAsync(profile);

        var savedContext = new SavedOpenAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.SavedAsync(ctx), savedContext, _logger);
    }

    public async Task UpdateAsync(OpenAIChatProfile profile, JsonNode data = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var updatingContext = new UpdatingOpenAIChatProfileContext(profile, data);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatingAsync(ctx), updatingContext, _logger);

        var updatedContext = new UpdatedOpenAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.UpdatedAsync(ctx), updatedContext, _logger);
    }

    public async Task<OpenAIChatProfileValidateResult> ValidateAsync(OpenAIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var validatingContext = new ValidatingOpenAIChatProfileContext(profile);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatingAsync(ctx), validatingContext, _logger);

        var validatedContext = new ValidatedOpenAIChatProfileContext(profile, validatingContext.Result);
        await _handlers.InvokeAsync((handler, ctx) => handler.ValidatedAsync(ctx), validatedContext, _logger);

        return validatingContext.Result;
    }

    private Task LoadAsync(OpenAIChatProfile profile)
    {
        var loadedContext = new LoadedOpenAIChatProfileContext(profile);

        _profileManagerSession.Store(profile);

        return _handlers.InvokeAsync((handler, context) => handler.LoadedAsync(context), loadedContext, _logger);
    }
}
