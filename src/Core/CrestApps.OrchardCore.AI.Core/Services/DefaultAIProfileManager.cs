using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIProfileManager : NamedModelManager<AIProfile>, IAIProfileManager
{
    private readonly IAIProfileManagerSession _sessionManager;

    public DefaultAIProfileManager(
        INamedModelStore<AIProfile> profileStore,
        IAIProfileManagerSession sessionManager,
        IEnumerable<IModelHandler<AIProfile>> handlers,
        ILogger<DefaultAIProfileManager> logger)
        : base(profileStore, handlers, logger)
    {
        _sessionManager = sessionManager;
    }

    public async ValueTask<IEnumerable<AIProfile>> GetAsync(AIProfileType type)
    {
        var profiles = await Store.GetAsync(type);

        foreach (var profile in profiles)
        {
            await LoadAsync(profile);
        }

        return profiles;
    }

    protected override async ValueTask DeletedAsync(AIProfile model)
    {
        await Store.DeleteAsync(model);

        _sessionManager.Forget(model.Id);
    }

    protected override async Task LoadAsync(AIProfile profile)
    {
        await base.LoadAsync(profile);

        _sessionManager.Store(profile);
    }
}
