using System.Collections.Concurrent;
using System.Reflection;
using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Drivers;

/// <summary>
/// The authentication values an editor posted, copied onto a stored <see cref="IConnectionAuthMetadata"/>. The
/// interface exposes the members read-only, so the copy goes through the stored type's own setters.
/// </summary>
internal sealed class ConnectionAuthValues : IConnectionAuthMetadata
{
    private static readonly PropertyInfo[] _members = typeof(IConnectionAuthMetadata).GetProperties();

    private static readonly ConcurrentDictionary<Type, (PropertyInfo Source, PropertyInfo Target)[]> _setters = new();

    public ClientAuthenticationType AuthenticationType { get; set; }

    public string ApiKeyHeaderName { get; set; }

    public string ApiKeyPrefix { get; set; }

    public string ApiKey { get; set; }

    public string BasicUsername { get; set; }

    public string BasicPassword { get; set; }

    public string OAuth2TokenEndpoint { get; set; }

    public string OAuth2ClientId { get; set; }

    public string OAuth2ClientSecret { get; set; }

    public string OAuth2Scopes { get; set; }

    public string OAuth2PrivateKey { get; set; }

    public string OAuth2KeyId { get; set; }

    public string OAuth2ClientCertificate { get; set; }

    public string OAuth2ClientCertificatePassword { get; set; }

    public Dictionary<string, string> AdditionalHeaders { get; set; }

    /// <summary>
    /// Copies every authentication member onto the stored metadata.
    /// </summary>
    /// <param name="target">The stored metadata.</param>
    /// <exception cref="InvalidOperationException">The stored type has no setter for an authentication member.</exception>
    public void CopyTo(IConnectionAuthMetadata target)
    {
        ArgumentNullException.ThrowIfNull(target);

        foreach (var (source, setter) in _setters.GetOrAdd(target.GetType(), static type => [.. _members.Select(member => (member, FindSetter(type, member)))]))
        {
            setter.SetValue(target, source.GetValue(this));
        }
    }

    private static PropertyInfo FindSetter(Type type, PropertyInfo member)
    {
        var property = type.GetProperty(member.Name, BindingFlags.Public | BindingFlags.Instance);

        if (property is null || !property.CanWrite || property.PropertyType != member.PropertyType)
        {
            throw new InvalidOperationException($"'{type.FullName}' has no public setter for '{member.Name}'.");
        }

        return property;
    }
}
