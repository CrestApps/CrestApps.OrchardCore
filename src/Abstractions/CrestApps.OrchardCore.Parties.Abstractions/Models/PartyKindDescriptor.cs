using System.Text.Json.Serialization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Parties.Models;

/// <summary>
/// The description of one kind of party.
/// </summary>
public sealed class PartyKindDescriptor
{
    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the name of the kind shown to users, such as "Contact".
    /// </summary>
    [JsonIgnore]
    public LocalizationSource DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the permission a user needs to search parties of this kind in a picker. A kind without one can't be
    /// searched through the shared picker.
    /// </summary>
    public Permission SearchPermission { get; set; }
}
