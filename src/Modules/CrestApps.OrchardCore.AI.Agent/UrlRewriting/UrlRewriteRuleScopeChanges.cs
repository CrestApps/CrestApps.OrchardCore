using Microsoft.Extensions.DependencyInjection;
using OrchardCore.UrlRewriting;
using OrchardCore.UrlRewriting.Models;

namespace CrestApps.OrchardCore.AI.Agent.UrlRewriting;

/// <summary>
/// The rules the URL rewriting tools saved or deleted in the current scope. The rule manager reads the rules from the
/// cached document, which is refreshed only once the scope commits, while every tool call of one chat message runs in
/// the same scope. Without this record, a second change to a rule in the same message would start from the rule as it
/// was before the first change and undo it.
/// </summary>
internal sealed class UrlRewriteRuleScopeChanges
{
    private readonly Dictionary<string, RewriteRule> _saved = new(StringComparer.Ordinal);
    private readonly HashSet<string> _deleted = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the record of the scope of a tool invocation.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The record, or a new empty one when none is registered.</returns>
    public static UrlRewriteRuleScopeChanges Get(IServiceProvider services)
        => services.GetService<UrlRewriteRuleScopeChanges>() ?? new UrlRewriteRuleScopeChanges();

    /// <summary>
    /// Finds a rule as it is after the changes of this scope.
    /// </summary>
    /// <param name="rulesManager">The rule manager.</param>
    /// <param name="id">The ID of the rule.</param>
    /// <returns>The rule, or <see langword="null"/> when there is none or it was deleted in this scope.</returns>
    public async Task<RewriteRule> FindAsync(IRewriteRulesManager rulesManager, string id)
    {
        if (_deleted.Contains(id))
        {
            return null;
        }

        return _saved.TryGetValue(id, out var saved)
            ? saved
            : await rulesManager.FindByIdAsync(id);
    }

    /// <summary>
    /// Gets every rule as it is after the changes of this scope, in the order they are applied.
    /// </summary>
    /// <param name="rulesManager">The rule manager.</param>
    /// <returns>The rules.</returns>
    public async Task<List<RewriteRule>> GetAllAsync(IRewriteRulesManager rulesManager)
    {
        var rules = (await rulesManager.GetAllAsync())
            .Where(rule => !_deleted.Contains(rule.Id) && !_saved.ContainsKey(rule.Id))
            .Concat(_saved.Values)
            .OrderBy(rule => rule.Order)
            .ThenBy(rule => rule.CreatedUtc)
            .ToList();

        return rules;
    }

    /// <summary>
    /// Records a saved rule.
    /// </summary>
    /// <param name="rule">The rule as it was saved.</param>
    public void Saved(RewriteRule rule)
    {
        _saved[rule.Id] = rule;
        _deleted.Remove(rule.Id);
    }

    /// <summary>
    /// Records a deleted rule.
    /// </summary>
    /// <param name="id">The ID of the rule.</param>
    public void Deleted(string id)
    {
        _saved.Remove(id);
        _deleted.Add(id);
    }
}
