namespace CrestApps.Core.Infrastructure.Indexing;

public sealed class IndexProfileSourceOptions
{
    public List<IndexProfileSourceDescriptor> Sources { get; } = [];

    public void AddOrUpdate(
        string providerName,
        string providerDisplayName,
        string type,
        Action<IndexProfileSourceDescriptor> configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        var descriptor = Sources.FirstOrDefault(source =>
            string.Equals(source.ProviderName, providerName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(source.Type, type, StringComparison.OrdinalIgnoreCase));

        descriptor ??= new IndexProfileSourceDescriptor
        {
            ProviderName = providerName,
            ProviderDisplayName = providerDisplayName,
            Type = type,
            DisplayName = type,
            Description = type,
        };

        configure?.Invoke(descriptor);

        if (!Sources.Contains(descriptor))
        {
            Sources.Add(descriptor);
        }
    }
}
