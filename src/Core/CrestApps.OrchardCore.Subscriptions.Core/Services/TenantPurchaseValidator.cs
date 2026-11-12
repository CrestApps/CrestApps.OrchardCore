using System.Text.RegularExpressions;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// What a buyer asks for when they buy a tenant: its name, where it answers, and what it is set up with.
/// </summary>
public sealed class TenantPurchaseRequest
{
    /// <summary>
    /// Gets or sets the tenant's name.
    /// </summary>
    public string TenantName { get; set; }

    /// <summary>
    /// Gets or sets the URL prefix the tenant answers on.
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// Gets or sets the host names the tenant answers on.
    /// </summary>
    public string[] Domains { get; set; } = [];

    /// <summary>
    /// Gets or sets the setup recipe.
    /// </summary>
    public string RecipeName { get; set; }

    /// <summary>
    /// Gets or sets the feature profile that limits which features the tenant may enable.
    /// </summary>
    public string FeatureProfile { get; set; }
}

/// <summary>
/// The rules a tenant purchase is held to: the plan's recipe and the operator's onboarding settings.
/// </summary>
public sealed class TenantPurchaseRules
{
    /// <summary>
    /// Gets or sets the recipe the plan names. A purchase may use no other.
    /// </summary>
    public string PlanRecipeName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the buyer may choose the prefix. Otherwise it is derived from the name.
    /// </summary>
    public bool AllowCustomPrefix { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the buyer may name host names of their own.
    /// </summary>
    public bool AllowCustomDomains { get; set; }

    /// <summary>
    /// Gets or sets the prefixes no tenant may take, compared without case.
    /// </summary>
    public IEnumerable<string> ReservedPrefixes { get; set; } = [];

    /// <summary>
    /// Gets or sets the host names no tenant may take, such as the operator's own, compared without case.
    /// </summary>
    public IEnumerable<string> ReservedHosts { get; set; } = [];

    /// <summary>
    /// Gets or sets the domain under which each sold tenant gets its own host, <c>{name}.{BaseDomain}</c>, when the buyer
    /// may not name their own. Empty means tenants answer on a prefix.
    /// </summary>
    public string BaseDomain { get; set; }

    /// <summary>
    /// Gets or sets the admin URL prefix, which is always reserved.
    /// </summary>
    public string AdminPrefix { get; set; }

    /// <summary>
    /// Gets or sets the feature profiles that exist on the host, or <see langword="null"/> when they can't be read (the
    /// Feature Profiles feature is off). Orchard Core treats a profile it can't find as "every feature allowed", so a
    /// purchase may name only one of these.
    /// </summary>
    public IReadOnlyCollection<string> KnownFeatureProfiles { get; set; }

/// <summary>
/// Why a tenant purchase was refused.
/// </summary>
public enum TenantPurchaseErrorCode
{
    NameRequired,
    InvalidName,
    NameTaken,
    PrefixRequired,
    InvalidPrefix,
    ReservedPrefix,
    PrefixTaken,
    PrefixNotAllowed,
    CustomDomainsNotAllowed,
    InvalidHost,
    ReservedHost,
    HostTaken,
    RecipeNotAllowed,
    FeatureProfileRequired,
    FeatureProfileNotFound,
}

/// <summary>
/// One reason a tenant purchase was refused, and the field it is about.
/// </summary>
/// <param name="Field">The name of the <see cref="TenantPurchaseRequest"/> property.</param>
/// <param name="Code">Why it was refused.</param>
/// <param name="Value">The value that was refused, for the message.</param>
public readonly record struct TenantPurchaseError(string Field, TenantPurchaseErrorCode Code, string Value = null);

/// <summary>
/// Holds a tenant purchase to the rules Orchard Core's own tenant validation applies (name, prefix and host format and
/// uniqueness) and to the operator's: reserved prefixes and hosts, whether the buyer may choose a prefix or a host, the
/// plan's recipe, and a feature profile, without which the buyer could enable host-level features. It runs when the
/// buyer fills in the step, again before payment begins, and again before the tenant is set up, so a step saved
/// before a rule existed can't bypass it.
/// </summary>
public static partial class TenantPurchaseValidator
{
    /// <summary>
    /// Fills in what the buyer may not choose: the prefix from the name, and the host from the base domain.
    /// </summary>
    /// <param name="request">The request as the buyer filled it in.</param>
    /// <param name="rules">The rules.</param>
    /// <returns>A copy of the request with the derived values.</returns>
    public static TenantPurchaseRequest Normalize(TenantPurchaseRequest request, TenantPurchaseRules rules)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rules);

        var name = request.TenantName?.Trim();
        var domains = (request.Domains ?? [])
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Select(domain => domain.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var prefix = request.Prefix?.Trim().Trim('/');

        if (!rules.AllowCustomDomains && !string.IsNullOrWhiteSpace(rules.BaseDomain) && !string.IsNullOrEmpty(name) && domains.Length == 0)
        {
            domains = [$"{name.ToLowerInvariant()}.{rules.BaseDomain.Trim().Trim('.').ToLowerInvariant()}"];
            prefix = string.Empty;
        }
        else if (!rules.AllowCustomPrefix && string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(name))
        {
            prefix = name.ToLowerInvariant();
        }

        return new TenantPurchaseRequest
        {
            TenantName = name,
            Prefix = prefix,
            Domains = domains,
            RecipeName = request.RecipeName,
            FeatureProfile = request.FeatureProfile,
        };
    }

    /// <summary>
    /// Returns every reason the purchase is refused; empty when it may proceed.
    /// </summary>
    /// <param name="request">The normalized request.</param>
    /// <param name="rules">The rules.</param>
    /// <param name="tenants">The settings of every tenant on the host.</param>
    /// <param name="ignoreTenant">A tenant to leave out of the uniqueness checks: the one being set up, which setup
    /// registers before it creates anything.</param>
    /// <returns>The errors.</returns>
    public static IReadOnlyList<TenantPurchaseError> Validate(
        TenantPurchaseRequest request,
        TenantPurchaseRules rules,
        IEnumerable<ShellSettings> tenants,
        string ignoreTenant = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rules);

        var errors = new List<TenantPurchaseError>();
        var others = (tenants ?? [])
            .Where(tenant => ignoreTenant is null || !string.Equals(tenant.Name, ignoreTenant, StringComparison.OrdinalIgnoreCase))
            .ToList();

        ValidateName(request.TenantName, others, errors);
        ValidatePrefix(request, rules, others, errors);
        ValidateDomains(request, rules, others, errors);

        if (!string.Equals(request.RecipeName, rules.PlanRecipeName, StringComparison.Ordinal) || string.IsNullOrEmpty(rules.PlanRecipeName))
        {
            errors.Add(new TenantPurchaseError(nameof(TenantPurchaseRequest.RecipeName), TenantPurchaseErrorCode.RecipeNotAllowed, request.RecipeName));
        }

        if (string.IsNullOrWhiteSpace(request.FeatureProfile))
        {
            errors.Add(new TenantPurchaseError(nameof(TenantPurchaseRequest.FeatureProfile), TenantPurchaseErrorCode.FeatureProfileRequired));
        }
        else if (rules.KnownFeatureProfiles is null ||
            !rules.KnownFeatureProfiles.Contains(request.FeatureProfile.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            // Orchard Core treats a profile it can't find as "every feature allowed": a renamed or deleted profile, or a
            // plan that arrived by recipe, import or API naming one that never existed, would sell an unrestricted site.
            errors.Add(new TenantPurchaseError(nameof(TenantPurchaseRequest.FeatureProfile), TenantPurchaseErrorCode.FeatureProfileNotFound, request.FeatureProfile));
        }

        return errors;
    }

    private static void ValidateName(string name, List<ShellSettings> tenants, List<TenantPurchaseError> errors)
    {
        const string field = nameof(TenantPurchaseRequest.TenantName);

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new TenantPurchaseError(field, TenantPurchaseErrorCode.NameRequired));

            return;
        }

        // Orchard Core uses the name for shell folders and table prefixes; "Default" is the host itself.
        if (!TenantNameRegex().IsMatch(name) || string.Equals(name, ShellSettings.DefaultShellName, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new TenantPurchaseError(field, TenantPurchaseErrorCode.InvalidName, name));

            return;
        }

        if (tenants.Any(tenant => string.Equals(tenant.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(new TenantPurchaseError(field, TenantPurchaseErrorCode.NameTaken, name));
        }
    }

    private static void ValidatePrefix(TenantPurchaseRequest request, TenantPurchaseRules rules, List<ShellSettings> tenants, List<TenantPurchaseError> errors)
    {
        const string field = nameof(TenantPurchaseRequest.Prefix);
        var prefix = request.Prefix;
        var hasDomains = request.Domains is { Length: > 0 };

        if (string.IsNullOrEmpty(prefix))
        {
            // Without a prefix the tenant answers on its host alone, so it needs a host of its own; otherwise it would
            // compete with the default tenant for every request.
            if (!hasDomains)
            {
                errors.Add(new TenantPurchaseError(field, TenantPurchaseErrorCode.PrefixRequired));
            }

            return;
        }

        if (!PrefixRegex().IsMatch(prefix))
        {
            errors.Add(new TenantPurchaseError(field, TenantPurchaseErrorCode.InvalidPrefix, prefix));

            return;
        }

        if (!rules.AllowCustomPrefix && !string.Equals(prefix, request.TenantName?.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new TenantPurchaseError(field, TenantPurchaseErrorCode.PrefixNotAllowed, prefix));

            return;
        }

        if (IsReservedPrefix(prefix, rules))
        {
            errors.Add(new TenantPurchaseError(field, TenantPurchaseErrorCode.ReservedPrefix, prefix));

            return;
        }

        if (tenants.Any(tenant => string.Equals(tenant.RequestUrlPrefix?.Trim('/'), prefix, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(new TenantPurchaseError(field, TenantPurchaseErrorCode.PrefixTaken, prefix));
        }
    }

    private static void ValidateDomains(TenantPurchaseRequest request, TenantPurchaseRules rules, List<ShellSettings> tenants, List<TenantPurchaseError> errors)
    {
        const string field = nameof(TenantPurchaseRequest.Domains);
        var domains = request.Domains ?? [];

        if (domains.Length == 0)
        {
            return;
        }

        var derived = !rules.AllowCustomDomains &&
            !string.IsNullOrWhiteSpace(rules.BaseDomain) &&
            domains.Length == 1 &&
            string.Equals(domains[0], $"{request.TenantName?.ToLowerInvariant()}.{rules.BaseDomain.Trim().Trim('.')}", StringComparison.OrdinalIgnoreCase);

        if (!rules.AllowCustomDomains && !derived)
        {
            errors.Add(new TenantPurchaseError(field, TenantPurchaseErrorCode.CustomDomainsNotAllowed));

            return;
        }

        var reserved = new HashSet<string>((rules.ReservedHosts ?? []).Where(host => !string.IsNullOrWhiteSpace(host)).Select(host => host.Trim()), StringComparer.OrdinalIgnoreCase)
        {
            "localhost",
        };

        var takenHosts = new HashSet<string>(
            tenants.SelectMany(tenant => (tenant.RequestUrlHosts ?? [])).Where(host => !string.IsNullOrWhiteSpace(host)).Select(host => host.Trim()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var domain in domains)
        {
            // A wildcard would claim every subdomain, the operator's included; a port or a path is not a host name.
            if (!HostRegex().IsMatch(domain) || domain.Length > 253)
            {
                errors.Add(new TenantPurchaseError(field, TenantPurchaseErrorCode.InvalidHost, domain));

                continue;
            }

            if (reserved.Contains(domain))
            {
                errors.Add(new TenantPurchaseError(field, TenantPurchaseErrorCode.ReservedHost, domain));

                continue;
            }

            if (takenHosts.Contains(domain))
            {
                errors.Add(new TenantPurchaseError(field, TenantPurchaseErrorCode.HostTaken, domain));
            }
        }
    }

    private static bool IsReservedPrefix(string prefix, TenantPurchaseRules rules)
    {
        if (!string.IsNullOrWhiteSpace(rules.AdminPrefix) && string.Equals(prefix, rules.AdminPrefix.Trim('/'), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return (rules.ReservedPrefixes ?? []).Any(reserved => string.Equals(prefix, reserved?.Trim().Trim('/'), StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex TenantNameRegex();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$")]
    private static partial Regex PrefixRegex();

    [GeneratedRegex("^(?=.{1,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)(\\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)+$", RegexOptions.IgnoreCase)]
    private static partial Regex HostRegex();
}
