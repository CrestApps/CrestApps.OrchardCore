using CrestApps.OrchardCore.AI.Core.Recipes;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.AI.Core.ApiKeys;

/// <summary>
/// What the records API protects the secrets a request gives with: the tenant's data protection provider and the
/// purpose of each metadata object's secrets.
/// </summary>
/// <param name="DataProtectionProvider">The tenant's data protection provider.</param>
/// <param name="Options">The purposes of the metadata objects.</param>
internal sealed record AISourceSecrets(IDataProtectionProvider DataProtectionProvider, IngestionSourceSecretOptions Options);
