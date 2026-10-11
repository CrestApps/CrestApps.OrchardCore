using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Holds the naming, slug and host rules of the tenant hierarchy.
/// </summary>
public static class TenantHierarchyNaming
{
    /// <summary>
    /// The placeholder a child host pattern must contain exactly once.
    /// </summary>
    public const string BusinessPlaceholder = "{business}";

    /// <summary>
    /// The minimum length of a slug.
    /// </summary>
    public const int MinSlugLength = 3;

    /// <summary>
    /// The maximum length of a slug.
    /// </summary>
    public const int MaxSlugLength = 40;

    private const string TenantNameAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// The slugs no tenant may use, because they name common infrastructure hosts.
    /// </summary>
    public static readonly IReadOnlySet<string> BuiltInReservedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "administrator", "api", "app", "apps", "assets", "auth", "autoconfig", "autodiscover", "blog", "cdn",
        "dashboard", "default", "dev", "dns", "docs", "email", "files", "ftp", "help", "hierarchy", "imap", "localhost",
        "login", "logout", "mail", "media", "mx", "ns", "ns1", "ns2", "oauth", "platform", "pop", "pop3", "portal",
        "prod", "root", "sftp", "signin", "signup", "smtp", "sso", "staging", "static", "status", "support", "system",
        "tenant", "tenants", "test", "webmail", "www",
    };

    /// <summary>
    /// Validates a slug: a DNS label of lowercase letters, digits and inner hyphens, between
    /// <see cref="MinSlugLength"/> and <see cref="MaxSlugLength"/> characters, that is not reserved.
    /// </summary>
    /// <param name="slug">The slug.</param>
    /// <param name="additionalReservedSlugs">More slugs to refuse.</param>
    public static SlugValidationResult ValidateSlug(string slug, IEnumerable<string> additionalReservedSlugs = null)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return SlugValidationResult.Empty;
        }

        if (slug.Length < MinSlugLength)
        {
            return SlugValidationResult.TooShort;
        }

        if (slug.Length > MaxSlugLength)
        {
            return SlugValidationResult.TooLong;
        }

        for (var i = 0; i < slug.Length; i++)
        {
            var character = slug[i];
            var isLetterOrDigit = character is (>= 'a' and <= 'z') or (>= '0' and <= '9');
            var isInnerHyphen = character == '-' && i > 0 && i < slug.Length - 1;

            if (!isLetterOrDigit && !isInnerHyphen)
            {
                return SlugValidationResult.InvalidCharacters;
            }
        }

        // Punycode labels could spell a different name than the one shown, so they are refused.
        if (slug.StartsWith("xn--", StringComparison.Ordinal))
        {
            return SlugValidationResult.InvalidCharacters;
        }

        if (BuiltInReservedSlugs.Contains(slug) ||
            additionalReservedSlugs?.Any(reserved => string.Equals(reserved, slug, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return SlugValidationResult.Reserved;
        }

        return SlugValidationResult.Valid;
    }

    /// <summary>
    /// Suggests a slug for a display name: lowercase letters and digits, words joined by hyphens.
    /// </summary>
    /// <param name="displayName">The display name.</param>
    public static string SuggestSlug(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var pendingHyphen = false;

        foreach (var character in displayName.Normalize(NormalizationForm.FormD))
        {
            var lower = char.ToLowerInvariant(character);

            if (lower is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                if (pendingHyphen && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(lower);
                pendingHyphen = false;
            }
            else if (char.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                pendingHyphen = true;
            }

            if (builder.Length >= MaxSlugLength)
            {
                break;
            }
        }

        return builder.ToString().Trim('-');
    }

    /// <summary>
    /// Suggests the slug of a tenant that becomes a parent. When the tenant already lives at
    /// <c>{slug}.{platform domain}</c>, its current slug is kept, so its address does not change; otherwise the slug is
    /// made from its name.
    /// </summary>
    /// <param name="tenantName">The tenant name.</param>
    /// <param name="currentHost">The current host of the tenant, or <see langword="null"/>.</param>
    /// <param name="platformDomain">The platform domain.</param>
    public static string SuggestParentSlug(string tenantName, string currentHost, string platformDomain)
    {
        if (!string.IsNullOrWhiteSpace(currentHost) && !string.IsNullOrWhiteSpace(platformDomain))
        {
            var suffix = $".{platformDomain.Trim()}";
            var host = currentHost.Trim();

            if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var label = host[..^suffix.Length].ToLowerInvariant();

                if (ValidateSlug(label) == SlugValidationResult.Valid)
                {
                    return label;
                }
            }
        }

        return SuggestSlug(tenantName);
    }

    /// <summary>
    /// Validates a child host pattern. It must contain <see cref="BusinessPlaceholder"/> exactly once, at the start of
    /// the first label, and must not contain a wildcard, a list separator or a path.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    public static bool IsValidHostPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        if (!pattern.StartsWith(BusinessPlaceholder + ".", StringComparison.Ordinal))
        {
            return false;
        }

        var first = pattern.IndexOf(BusinessPlaceholder, StringComparison.Ordinal);
        var last = pattern.LastIndexOf(BusinessPlaceholder, StringComparison.Ordinal);

        if (first != last)
        {
            return false;
        }

        var rest = pattern.Substring(BusinessPlaceholder.Length + 1);

        return IsValidHost(rest);
    }

    /// <summary>
    /// Returns the default child host pattern of a parent: <c>{business}.{parent host}</c>.
    /// </summary>
    /// <param name="parentHost">The host of the parent tenant.</param>
    public static string GetDefaultHostPattern(string parentHost)
    {
        ArgumentException.ThrowIfNullOrEmpty(parentHost);

        return $"{BusinessPlaceholder}.{parentHost}";
    }

    /// <summary>
    /// Builds a host from a pattern and a valid slug.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="slug">The slug.</param>
    public static string BuildHost(string pattern, string slug)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);
        ArgumentException.ThrowIfNullOrEmpty(slug);

        return pattern.Replace(BusinessPlaceholder, slug, StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds the host of a parent tenant from its slug and the platform domain.
    /// </summary>
    /// <param name="slug">The slug of the parent.</param>
    /// <param name="platformDomain">The platform domain, which may carry a port.</param>
    public static string BuildParentHost(string slug, string platformDomain)
    {
        ArgumentException.ThrowIfNullOrEmpty(slug);
        ArgumentException.ThrowIfNullOrEmpty(platformDomain);

        return $"{slug}.{platformDomain.Trim().TrimStart('.')}";
    }

    /// <summary>
    /// Returns whether a value is a single host name, optionally with a port, without wildcards, lists or paths.
    /// </summary>
    /// <param name="host">The host.</param>
    public static bool IsValidHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253)
        {
            return false;
        }

        var name = host;
        var separator = host.LastIndexOf(':');

        if (separator >= 0)
        {
            if (!int.TryParse(host.AsSpan(separator + 1), out var port) || port <= 0 || port > 65535)
            {
                return false;
            }

            name = host.Substring(0, separator);
        }

        var labels = name.Split('.');

        foreach (var label in labels)
        {
            if (label.Length == 0 || label.Length > 63 || label[0] == '-' || label[label.Length - 1] == '-')
            {
                return false;
            }

            foreach (var character in label)
            {
                if (character is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-'))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Generates an opaque tenant name, for example <c>u_7k2m9q4x1c</c>. Opaque names stop one parent from learning
    /// the tenant names of other parents through "name already taken" errors.
    /// </summary>
    public static string GenerateTenantName()
    {
        return "u_" + RandomNumberGenerator.GetString(TenantNameAlphabet, 10);
    }

    /// <summary>
    /// Builds the user name of a linked user, for example <c>alice+firma</c>. Only the characters Orchard Core allows
    /// by default are kept.
    /// </summary>
    /// <param name="parentUserName">The parent user name.</param>
    /// <param name="parentSlug">The slug of the parent tenant.</param>
    /// <param name="attempt">The attempt number. From the second attempt, <c>-2</c>, <c>-3</c> and so on are added.</param>
    public static string BuildLinkedUserName(string parentUserName, string parentSlug, int attempt = 1)
    {
        var name = Clean(parentUserName);

        if (string.IsNullOrEmpty(name))
        {
            name = "user";
        }

        var suffix = Clean(parentSlug);
        var userName = string.IsNullOrEmpty(suffix)
            ? name
            : $"{name}+{suffix}";

        return attempt > 1
            ? $"{userName}-{attempt}"
            : userName;
    }

    private static string Clean(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '.' or '_')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
