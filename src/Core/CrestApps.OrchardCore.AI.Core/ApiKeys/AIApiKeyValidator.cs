using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.ApiKeys;

/// <summary>
/// Applies the fields a person may change on an API key and checks them, shared by the API key editor and the AI
/// tools: a browser key chats only, from the web sites and with the profiles it names; a server key acts with the
/// permissions of its roles in the areas its scopes name.
/// </summary>
public static class AIApiKeyValidator
{
    /// <summary>
    /// Applies the values onto the key, keeping only the fields of its kind, and checks them. The kind itself is not
    /// changed: it is chosen once, when the key is made.
    /// </summary>
    /// <param name="key">The key to change.</param>
    /// <param name="values">The values to apply.</param>
    /// <param name="S">The localizer of the caller, which words the errors.</param>
    /// <returns>An error per broken rule, named after the field it is about; empty when the key may be saved.</returns>
    public static IReadOnlyList<ValidationResult> Apply(AIApiKey key, AIApiKeyValues values, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(S);

        var errors = new List<ValidationResult>();

        if (string.IsNullOrWhiteSpace(values.Name))
        {
            errors.Add(new ValidationResult(S["The name is required."], [nameof(AIApiKeyValues.Name)]));
        }

        key.Name = values.Name?.Trim();
        key.Enabled = values.Enabled;
        key.ExpiresUtc = values.ExpiresUtc;

        if (key.Kind == AIApiKeyKind.Browser)
        {
            var origins = (values.AllowedOrigins ?? [])
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Select(origin => origin.Trim().TrimEnd('/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (origins.Count == 0 || origins.Any(origin => !IsOrigin(origin)))
            {
                errors.Add(new ValidationResult(S["Enter each web site as its origin, such as https://www.example.com."], [nameof(AIApiKeyValues.AllowedOrigins)]));
            }

            var profiles = (values.AllowedProfileIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();

            if (profiles.Count == 0)
            {
                errors.Add(new ValidationResult(S["Choose the profiles the key may chat with."], [nameof(AIApiKeyValues.AllowedProfileIds)]));
            }

            key.AllowedOrigins = origins;
            key.AllowedProfileIds = profiles;
            key.Scopes = [AIApiKeys.Scopes.Chat];
            key.Roles = [];
        }
        else
        {
            var scopes = (values.Scopes ?? []).Where(AIApiKeys.Scopes.All.Contains).Distinct(StringComparer.Ordinal).ToList();

            if (scopes.Count == 0)
            {
                errors.Add(new ValidationResult(S["Choose at least one area the key may call."], [nameof(AIApiKeyValues.Scopes)]));
            }

            var roles = (values.Roles ?? []).Where(role => !string.IsNullOrEmpty(role)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (roles.Count == 0)
            {
                errors.Add(new ValidationResult(S["Choose the roles whose permissions the key acts with."], [nameof(AIApiKeyValues.Roles)]));
            }

            key.Scopes = scopes;
            key.Roles = roles;
            key.AllowedOrigins = [];
            key.AllowedProfileIds = [];
        }

        return errors;
    }

    /// <summary>
    /// Whether a text is a web site's origin: an absolute http or https address with no path or query.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns><see langword="true"/> for an origin such as https://www.example.com.</returns>
    public static bool IsOrigin(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
            uri.AbsolutePath == "/" &&
            string.IsNullOrEmpty(uri.Query);
}

/// <summary>
/// The fields of an API key a person may change.
/// </summary>
public sealed class AIApiKeyValues
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the key works.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets when the key stops working, in UTC, or <see langword="null"/> for never.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the web sites a browser key may be used from.
    /// </summary>
    public IEnumerable<string> AllowedOrigins { get; set; }

    /// <summary>
    /// Gets or sets the profiles a browser key may chat with.
    /// </summary>
    public IEnumerable<string> AllowedProfileIds { get; set; }

    /// <summary>
    /// Gets or sets the areas a server key may call.
    /// </summary>
    public IEnumerable<string> Scopes { get; set; }

    /// <summary>
    /// Gets or sets the roles whose permissions a server key acts with.
    /// </summary>
    public IEnumerable<string> Roles { get; set; }
}
