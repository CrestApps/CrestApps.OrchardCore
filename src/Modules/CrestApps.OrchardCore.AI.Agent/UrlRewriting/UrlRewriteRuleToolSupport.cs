using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.UrlRewriting.Models;
using OrchardCore.UrlRewriting.Services;

namespace CrestApps.OrchardCore.AI.Agent.UrlRewriting;

/// <summary>
/// What the URL rewriting tools share: how a rule is described in a result, how a stored rule is copied before it is
/// changed, and how the change reaches live requests.
/// </summary>
internal static class UrlRewriteRuleToolSupport
{
    /// <summary>
    /// Describes a rule for a tool result, from an explicit list of fields.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="position">The position of the rule in the order rules are applied, starting at 1.</param>
    /// <param name="createdTime">When the rule was created, in the user's or the site's time zone.</param>
    /// <returns>The description.</returns>
    public static object Describe(RewriteRule rule, int? position, string createdTime)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var (redirect, rewrite) = GetMetadata(rule);

        return new
        {
            id = rule.Id,
            name = rule.Name,
            type = rule.Source,
            position,
            pattern = redirect?.Pattern ?? rewrite?.Pattern,
            substitutionPattern = redirect?.SubstitutionPattern ?? rewrite?.SubstitutionPattern,
            isCaseInsensitive = redirect?.IsCaseInsensitive ?? rewrite?.IsCaseInsensitive,
            queryStringPolicy = (redirect?.QueryStringPolicy ?? rewrite?.QueryStringPolicy)?.ToString(),
            redirectType = redirect?.RedirectType.ToString(),
            redirectStatusCode = redirect is null ? (int?)null : (int)redirect.RedirectType,
            skipFurtherRules = rewrite?.SkipFurtherRules,
            createdTime,
            author = string.IsNullOrEmpty(rule.Author) ? null : rule.Author,
        };
    }

    /// <summary>
    /// Whether the name or the patterns of a rule contain a text.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="search">The text.</param>
    /// <returns><see langword="true"/> when the name, the pattern or the substitution contains the text.</returns>
    public static bool Matches(RewriteRule rule, string search)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var (redirect, rewrite) = GetMetadata(rule);

        return new[] { rule.Name, redirect?.Pattern, redirect?.SubstitutionPattern, rewrite?.Pattern, rewrite?.SubstitutionPattern }
            .Any(text => text?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>
    /// Copies a stored rule before it is changed. The rule manager returns the instance of the shared, cached
    /// document, and copying only its fields would still share its properties, so they are copied too.
    /// </summary>
    /// <param name="rule">The stored rule.</param>
    /// <returns>A copy that can be changed.</returns>
    public static RewriteRule Copy(RewriteRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return new RewriteRule
        {
            Id = rule.Id,
            Source = rule.Source,
            Name = rule.Name,
            Order = rule.Order,
            CreatedUtc = rule.CreatedUtc,
            OwnerId = rule.OwnerId,
            Author = rule.Author,
            Properties = rule.Properties?.DeepClone() as JsonObject ?? [],
        };
    }

    /// <summary>
    /// Asks for the tenant to be reloaded at the end of the request, as the URL Rewriting page does after a change:
    /// the rules are read when the tenant starts.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    public static void RequestRelease(IServiceProvider services)
        => services.GetService<IShellReleaseManager>()?.RequestRelease();

    private static (UrlRedirectSourceMetadata Redirect, UrlRewriteSourceMetadata Rewrite) GetMetadata(RewriteRule rule)
    {
        var redirect = rule.Source == UrlRedirectRuleSource.SourceName && rule.TryGet<UrlRedirectSourceMetadata>(out var redirectMetadata)
            ? redirectMetadata
            : null;

        var rewrite = rule.Source == UrlRewriteRuleSource.SourceName && rule.TryGet<UrlRewriteSourceMetadata>(out var rewriteMetadata)
            ? rewriteMetadata
            : null;

        return (redirect, rewrite);
    }
}
