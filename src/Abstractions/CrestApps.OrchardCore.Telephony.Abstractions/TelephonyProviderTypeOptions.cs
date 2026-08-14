namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Describes a registered telephony provider type and whether it is currently enabled.
/// </summary>
public sealed class TelephonyProviderTypeOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyProviderTypeOptions"/> class.
    /// </summary>
    /// <param name="type">The provider type, which must implement <see cref="ITelephonyProvider"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="type"/> does not implement <see cref="ITelephonyProvider"/>.</exception>
    public TelephonyProviderTypeOptions(Type type)
    {
        if (!typeof(ITelephonyProvider).IsAssignableFrom(type))
        {
            throw new ArgumentException($"The type must implement the '{nameof(ITelephonyProvider)}' interface.", nameof(type));
        }

        Type = type;
    }

    /// <summary>
    /// Gets the provider type. The type always implements <see cref="ITelephonyProvider"/>.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider is enabled and available for selection.
    /// </summary>
    public bool IsEnabled { get; set; }
}
