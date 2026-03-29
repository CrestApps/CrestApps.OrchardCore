using OrchardCore.Indexing;

namespace CrestApps.OrchardCore.AI.Core.Services;

public static class OptionalIndexProfileSelectionValidator
{
    public static async Task<bool> IsValidAsync(
        IIndexProfileStore indexProfileStore,
        string indexProfileName,
        string expectedIndexProfileType)
    {
        if (string.IsNullOrWhiteSpace(indexProfileName))
        {
            return true;
        }

        var indexProfile = await indexProfileStore.FindByNameAsync(indexProfileName);

        return indexProfile is not null
            && string.Equals(indexProfile.Type, expectedIndexProfileType, StringComparison.OrdinalIgnoreCase);
    }
}
