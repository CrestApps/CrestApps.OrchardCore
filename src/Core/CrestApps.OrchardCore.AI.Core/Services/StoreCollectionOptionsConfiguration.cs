using CrestApps.Core.Data.YesSql;
using Microsoft.Extensions.Options;
using OrchardCore.Data;

namespace CrestApps.OrchardCore.AI.Core.Services;

internal sealed class StoreCollectionOptionsConfiguration : IConfigureOptions<StoreCollectionOptions>
{
    private readonly YesSqlStoreOptions _yessqlStoreOptions;

    public StoreCollectionOptionsConfiguration(IOptions<YesSqlStoreOptions> yessqlStoreOptions)
    {
        _yessqlStoreOptions = yessqlStoreOptions.Value;
    }

    public void Configure(StoreCollectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(_yessqlStoreOptions.AICollectionName))
        {
            return;
        }

        options.Collections.Add(_yessqlStoreOptions.AICollectionName);
    }
}
