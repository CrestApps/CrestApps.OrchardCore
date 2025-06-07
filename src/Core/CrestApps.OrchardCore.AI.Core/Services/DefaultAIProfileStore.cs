using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIProfileStore : NamedCatalog<AIProfile>
{
    public DefaultAIProfileStore(IDocumentManager<DictionaryDocument<AIProfile>> documentManager)
        : base(documentManager)
    {
    }

    protected override void Deleting(AIProfile profile, DictionaryDocument<AIProfile> document)
    {
        var settings = profile.GetSettings<AIProfileSettings>();

        if (!settings.IsRemovable)
        {
            throw new InvalidOperationException("The profile cannot be removed.");
        }
    }

    protected override async ValueTask<IEnumerable<AIProfile>> LocateInstancesAsync(QueryContext context)
    {
        var profiles = await base.LocateInstancesAsync(context);

        if (context is AIProfileQueryContext ctx && ctx.IsListableOnly)
        {
            return profiles.Where(x => x.GetSettings<AIProfileSettings>().IsListable);
        }

        return profiles;
    }
}
