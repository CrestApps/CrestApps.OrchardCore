namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class AIDataSourceOptions
{
    private readonly Dictionary<string, Dictionary<string, DataSourceFieldMapping>> _fieldMappings = new(StringComparer.OrdinalIgnoreCase);

    public void AddFieldMapping(string providerName, string indexProfileType, Action<DataSourceFieldMapping> mapper)
    {
        ArgumentNullException.ThrowIfNull(providerName);
        ArgumentNullException.ThrowIfNull(indexProfileType);

        if (!_fieldMappings.TryGetValue(providerName, out var providerMappings))
        {
            providerMappings = new Dictionary<string, DataSourceFieldMapping>(StringComparer.OrdinalIgnoreCase);

            _fieldMappings[providerName] = providerMappings;
        }

        if (!providerMappings.TryGetValue(indexProfileType, out var mapping))
        {
            mapping = new DataSourceFieldMapping();
        }

        mapper.Invoke(mapping);

        providerMappings[indexProfileType] = mapping;
    }

    public DataSourceFieldMapping GetFieldMapping(string providerName, string indexProfileType)
    {
        if (providerName == null || indexProfileType == null)
        {
            return null;
        }

        if (_fieldMappings.TryGetValue(providerName, out var providerMappings)
            && providerMappings.TryGetValue(indexProfileType, out var mapping))
        {
            return mapping;
        }

        return null;
    }
}

public sealed class DataSourceFieldMapping
{
    public string DefaultKeyField { get; set; }

    public string DefaultTitleField { get; set; }

    public string DefaultContentField { get; set; }
}
