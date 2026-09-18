using System.Reflection;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// Fills an options instance from the tenant's site settings of the same type.
/// </summary>
/// <remarks>
/// <para>
/// The suite's settings classes are plain models that carry no Orchard Core types, so a settings
/// class can serve as its own options type. This bridges the two without a hand-written property
/// list, which is the point: a property added to a settings class would otherwise keep its default
/// everywhere the options are read, and nothing would report it.
/// </para>
/// <para>
/// Read-only and init-only properties are skipped, because there is nothing to assign. Reference
/// values are shared rather than cloned, which is safe here only because options are never mutated
/// after configuration.
/// </para>
/// </remarks>
/// <typeparam name="TOptions">The settings type, which is also the options type.</typeparam>
public sealed class SiteSettingsOptionsConfiguration<TOptions> : IConfigureOptions<TOptions>
    where TOptions : class, new()
{
    private static readonly PropertyInfo[] _properties = typeof(TOptions)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.CanRead && property.GetSetMethod() is not null)
        .ToArray();

    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiteSettingsOptionsConfiguration{TOptions}"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public SiteSettingsOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public void Configure(TOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var settings = _siteService.GetSettings<TOptions>();

        if (settings is null)
        {
            return;
        }

        foreach (var property in _properties)
        {
            property.SetValue(options, property.GetValue(settings));
        }
    }
}
