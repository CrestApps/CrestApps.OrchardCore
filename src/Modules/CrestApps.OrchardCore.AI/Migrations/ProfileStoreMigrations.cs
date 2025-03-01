using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore.Data.Migration;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AI.Migrations;

[Obsolete("This class will be removed before the v1 is released.")]
public sealed class ProfileStoreMigrations : DataMigration
{
    private readonly INamedModelStore<AIProfile> _profilesStore;
    private readonly IDocumentManager<AIProfileDocument> _profileDocument;

    public ProfileStoreMigrations(
        INamedModelStore<AIProfile> profilesStore,
        IDocumentManager<AIProfileDocument> profileDocument)
    {
        _profilesStore = profilesStore;
        _profileDocument = profileDocument;
    }

    public async Task<int> CreateAsync()
    {
        var profilesDocument = await _profileDocument.GetOrCreateImmutableAsync();

        foreach (var profile in profilesDocument.Profiles.Values)
        {
            try
            {
                await _profilesStore.SaveAsync(profile);
            }
            catch { }
        }

        return 1;
    }
}
