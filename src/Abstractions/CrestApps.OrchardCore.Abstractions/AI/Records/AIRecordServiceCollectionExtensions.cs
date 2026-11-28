using CrestApps.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Records;

/// <summary>
/// Registers record kinds for the generic record tools.
/// </summary>
public static class AIRecordServiceCollectionExtensions
{
    /// <summary>
    /// Exposes a record type to the generic record tools (<c>listRecords</c>, <c>getRecord</c>, <c>saveRecord</c> and
    /// <c>deleteRecord</c>) under a camelCase kind name. By default records are found, listed, validated, saved and
    /// deleted through the record's <c>ICatalogManager&lt;T&gt;</c>, and written by replaying the record's admin editor
    /// through its display drivers. Call it from the owner module, in a startup that requires the AI feature
    /// (<see cref="AIFeatureIds.Area"/>).
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The camelCase kind name, such as <c>taxCategory</c>.</param>
    /// <param name="configure">Configures the kind: a description, a permission and a projection are required.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentException">Thrown when the name is not camelCase.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the kind has no description, permission or projection.</exception>
    public static IServiceCollection AddAIRecordKind<T>(this IServiceCollection services, string name, Action<AIRecordKindBuilder<T>> configure)
        where T : CatalogItem, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        if (!IsCamelCase(name))
        {
            throw new ArgumentException($"The record kind name '{name}' must be camelCase: a lower-case letter followed by letters and digits.", nameof(name));
        }

        var kind = new AIRecordKind<T>(name);

        configure(new AIRecordKindBuilder<T>(kind));

        if (string.IsNullOrWhiteSpace(kind.Description))
        {
            throw new InvalidOperationException($"The record kind '{name}' needs a description for the model.");
        }

        if (kind.Permission is null)
        {
            throw new InvalidOperationException($"The record kind '{name}' needs a permission.");
        }

        if (kind.Projection is null)
        {
            throw new InvalidOperationException($"The record kind '{name}' needs a projection: the allow-listed fields the tools return.");
        }

        if (string.IsNullOrWhiteSpace(kind.Title))
        {
            kind.Title = name;
        }

        services.Configure<AIRecordKindOptions>(options => options.Add(kind));

        return services;
    }

    private static bool IsCamelCase(string name)
    {
        if (!char.IsAsciiLetterLower(name[0]))
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
