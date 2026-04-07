namespace CrestApps.AI.Models;

/// <summary>
/// Runtime options for AI Data Source retrieval behavior.
/// </summary>
public sealed class AIDataSourceOptions
{
    private readonly Dictionary<string, Dictionary<string, DataSourceFieldMapping>> _fieldMappings =
        new(StringComparer.OrdinalIgnoreCase);

    public const int MinStrictness = 1;

    public const int MaxStrictness = 5;

    public const int MinTopNDocuments = 3;

    public const int MaxTopNDocuments = 20;

    public int DefaultStrictness { get; set; } = 3;

    public int DefaultTopNDocuments { get; set; } = 5;

    public AIDataSourceOptions Clone()
    {
        var clone = new AIDataSourceOptions
        {
            DefaultStrictness = DefaultStrictness,
            DefaultTopNDocuments = DefaultTopNDocuments,
        };

        foreach (var (providerName, providerMappings) in _fieldMappings)
        {
            foreach (var (indexProfileType, mapping) in providerMappings)
            {
                clone.AddFieldMapping(providerName, indexProfileType, target =>
                {
                    target.DefaultKeyField = mapping.DefaultKeyField;
                    target.DefaultTitleField = mapping.DefaultTitleField;
                    target.DefaultContentField = mapping.DefaultContentField;
                });
            }
        }

        return clone;
    }

    public void AddFieldMapping(string providerName, string indexProfileType, Action<DataSourceFieldMapping> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(indexProfileType);
        ArgumentNullException.ThrowIfNull(configure);

        if (!_fieldMappings.TryGetValue(providerName, out var providerMappings))
        {
            providerMappings = new Dictionary<string, DataSourceFieldMapping>(StringComparer.OrdinalIgnoreCase);
            _fieldMappings[providerName] = providerMappings;
        }

        if (!providerMappings.TryGetValue(indexProfileType, out var mapping))
        {
            mapping = new DataSourceFieldMapping();
            providerMappings[indexProfileType] = mapping;
        }

        configure(mapping);
    }

    public DataSourceFieldMapping GetFieldMapping(string providerName, string indexProfileType)
    {
        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(indexProfileType))
        {
            return null;
        }

        return _fieldMappings.TryGetValue(providerName, out var providerMappings) &&
            providerMappings.TryGetValue(indexProfileType, out var mapping)
            ? mapping
            : null;
    }

    public int GetTopNDocuments(int? topN)
    {
        if (topN >= MinTopNDocuments && topN <= MaxTopNDocuments)
        {
            return topN.Value;
        }

        if (DefaultTopNDocuments >= MinTopNDocuments && DefaultTopNDocuments <= MaxTopNDocuments)
        {
            return DefaultTopNDocuments;
        }

        return 5;
    }

    public int GetStrictness(int? strictness)
    {
        if (strictness >= MinStrictness && strictness <= MaxStrictness)
        {
            return strictness.Value;
        }

        if (DefaultStrictness >= MinStrictness && DefaultStrictness <= MaxStrictness)
        {
            return DefaultStrictness;
        }

        return 3;
    }

    public static AIDataSourceOptions FromSettings(AIDataSourceSettings settings)
        => settings == null
            ? new AIDataSourceOptions()
            : new AIDataSourceOptions
            {
                DefaultStrictness = settings.DefaultStrictness,
                DefaultTopNDocuments = settings.DefaultTopNDocuments,
            };
}
