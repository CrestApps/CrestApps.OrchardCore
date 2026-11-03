using CrestApps.OrchardCore.AI.Core.ApiKeys;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// The editor of an API key.
/// </summary>
public class AIApiKeyViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the kind of key.
    /// </summary>
    public AIApiKeyKind Kind { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the key is new, so its kind can still be chosen.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the scopes of a server key.
    /// </summary>
    public string[] Scopes { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles of a server key.
    /// </summary>
    public string[] Roles { get; set; } = [];

    /// <summary>
    /// Gets or sets the web sites of a browser key, one per line.
    /// </summary>
    public string AllowedOrigins { get; set; }

    /// <summary>
    /// Gets or sets the profiles of a browser key.
    /// </summary>
    public string[] AllowedProfileIds { get; set; } = [];

    /// <summary>
    /// Gets or sets when the key stops working.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the key works.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the public key of a browser key, shown on its editor.
    /// </summary>
    public string PublicKey { get; set; }

    /// <summary>
    /// Gets or sets the roles to choose from.
    /// </summary>
    public IList<SelectListItem> AvailableRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets the profiles to choose from.
    /// </summary>
    public IList<SelectListItem> AvailableProfiles { get; set; } = [];
}

/// <summary>
/// The page shown once a key is made, the only time a server key is shown.
/// </summary>
public class AIApiKeyCreatedViewModel
{
    /// <summary>
    /// Gets or sets the key record.
    /// </summary>
    public AIApiKey ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the whole key.
    /// </summary>
    public string Key { get; set; }
}
