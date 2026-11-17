namespace CrestApps.OrchardCore.AI.Core.Recipes;

/// <summary>
/// Names the data protection purpose of the secrets each kind of record keeps in one of its metadata objects, such as
/// an Azure AI Search data source's API key or an FTP file source's password, so a recipe step or the API protects a
/// secret given in clear text the way the record's editor does.
/// </summary>
/// <remarks>
/// The module that owns a metadata object registers it, with the purpose its editor and its reader use:
/// <c>services.Configure&lt;IngestionSourceSecretOptions&gt;(options =&gt; options.Protect&lt;AIDataSource&gt;(nameof(AzureAISearchSourceMetadata), AIDataSourceProtectionConstants.SourceSecretPurpose))</c>.
/// The secrets in a registered object are its text members <see cref="IngestionSourceSecrets.IsSecret"/> names.
/// </remarks>
public sealed class IngestionSourceSecretOptions
{
    private readonly Dictionary<string, string> _purposes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers the purpose the secrets of a metadata object are protected with.
    /// </summary>
    /// <typeparam name="TRecord">The record type, such as <c>AIDataSource</c> or <c>FileSource</c>.</typeparam>
    /// <param name="metadataName">The key of the metadata object in the record's properties, its type name.</param>
    /// <param name="purpose">The data protection purpose.</param>
    /// <returns>The options.</returns>
    public IngestionSourceSecretOptions Protect<TRecord>(string metadataName, string purpose)
    {
        ArgumentException.ThrowIfNullOrEmpty(metadataName);
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        _purposes[Key(typeof(TRecord), metadataName)] = purpose;

        return this;
    }

    /// <summary>
    /// Gets the purpose the secrets of a metadata object are protected with.
    /// </summary>
    /// <param name="recordType">The record type.</param>
    /// <param name="metadataName">The key of the metadata object, matched ignoring case.</param>
    /// <returns>The purpose, or <see langword="null"/> when none is registered.</returns>
    public string GetPurpose(Type recordType, string metadataName)
    {
        ArgumentNullException.ThrowIfNull(recordType);

        return !string.IsNullOrEmpty(metadataName) && _purposes.TryGetValue(Key(recordType, metadataName), out var purpose)
            ? purpose
            : null;
    }

    private static string Key(Type recordType, string metadataName)
        => recordType.FullName + "/" + metadataName;
}
