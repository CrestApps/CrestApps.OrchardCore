using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.AI.Core.ApiKeys;

/// <summary>
/// A key a program uses to call the AI API instead of an OpenID token. A server key acts with the permissions of its
/// roles, limited to its scopes. A browser key is public: it may only chat with the profiles it names, from the web
/// sites it names.
/// </summary>
public sealed class AIApiKey : CatalogItem, INameAwareModel
{
    /// <summary>
    /// Gets or sets the key's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets whether the key is kept on a server or put in a web page.
    /// </summary>
    public AIApiKeyKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the start of the key, which finds it and shows it in lists.
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the whole key. The key itself is shown once, when it is made.
    /// </summary>
    public string Hash { get; set; }

    /// <summary>
    /// Gets or sets the whole key, for browser keys only, since they are public anyway.
    /// </summary>
    public string PublicKey { get; set; }

    /// <summary>
    /// Gets or sets the areas of the API the key may call, from <see cref="AIApiKeys.Scopes"/>.
    /// </summary>
    public List<string> Scopes { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles whose permissions a server key acts with.
    /// </summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>
    /// Gets or sets the web sites a browser key may be used from, such as <c>https://www.example.com</c>.
    /// </summary>
    public List<string> AllowedOrigins { get; set; } = [];

    /// <summary>
    /// Gets or sets the AI profiles a browser key may chat with.
    /// </summary>
    public List<string> AllowedProfileIds { get; set; } = [];

    /// <summary>
    /// Gets or sets when the key stops working, or <see langword="null"/> when it does not.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the key works.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets when the key was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets who made it.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets when the key was last used, to the hour.
    /// </summary>
    public DateTime? LastUsedUtc { get; set; }
}

/// <summary>
/// Where an API key is kept.
/// </summary>
public enum AIApiKeyKind
{
    /// <summary>
    /// A secret key kept on a server.
    /// </summary>
    Server,

    /// <summary>
    /// A public key put in a web page.
    /// </summary>
    Browser,
}
