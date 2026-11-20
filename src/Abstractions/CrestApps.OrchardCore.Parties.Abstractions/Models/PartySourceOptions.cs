using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Parties.Models;

/// <summary>
/// What the party pickers know about each kind of party: its name and who may search it. The module that registers a
/// party source describes its kind with
/// <see cref="PartiesServiceCollectionExtensions.AddPartySource{TSource}(Microsoft.Extensions.DependencyInjection.IServiceCollection, string, LocalizationSource, Permission)"/>.
/// </summary>
public sealed class PartySourceOptions
{
    /// <summary>
    /// Gets the described kinds, by kind. Kinds compare without case.
    /// </summary>
    public Dictionary<string, PartyKindDescriptor> Kinds { get; } = new(StringComparer.OrdinalIgnoreCase);
}
