using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CrestApps.Core.AI.Models;

/// <summary>
/// Extension methods for managing settings in AIProfile.
/// </summary>
public static class AIProfileExtensions
{
    private static readonly JsonSerializerOptions _ignoreDefaultValuesSerializer = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Retrieves settings of type <typeparamref name="T"/> from the profile.
    /// If the settings do not exist, a new instance of <typeparamref name="T"/> is returned.
    /// </summary>
    public static T GetSettings<T>(this AIProfile profile)
        where T : new()
    {
        if (profile.Settings == null)
        {
            return new T();
        }

        var node = profile.Settings[typeof(T).Name];

        if (node == null)
        {
            return new T();
        }

        return node.Deserialize<T>(_ignoreDefaultValuesSerializer) ?? new T();
    }

    /// <summary>
    /// Attempts to retrieve settings of type <typeparamref name="T"/> from the profile.
    /// </summary>
    public static bool TryGetSettings<T>(this AIProfile profile, out T settings)
        where T : class
    {
        if (profile.Settings == null)
        {
            settings = null;
            return false;
        }

        var node = profile.Settings[typeof(T).Name];

        if (node == null)
        {
            settings = null;
            return false;
        }

        settings = node.Deserialize<T>(_ignoreDefaultValuesSerializer);

        return true;
    }

    /// <summary>
    /// Alters existing settings or adds new settings of type <typeparamref name="T"/> if one does not exists.
    /// </summary>
    public static AIProfile AlterSettings<T>(this AIProfile profile, Action<T> setting)
        where T : class, new()
    {
        var existingJObject = profile.Settings[typeof(T).Name] as JsonObject;

        if (existingJObject == null)
        {
            existingJObject = JsonExtensions.FromObject(new T(), _ignoreDefaultValuesSerializer);
            profile.Settings[typeof(T).Name] = existingJObject;
        }

        var settingsToMerge = existingJObject.Deserialize<T>(_ignoreDefaultValuesSerializer);

        setting(settingsToMerge);

        profile.Settings[typeof(T).Name] = JsonExtensions.FromObject(settingsToMerge, _ignoreDefaultValuesSerializer);

        return profile;
    }

    /// <summary>
    /// Sets or replaces the settings of type <typeparamref name="T"/> in the profile.
    /// </summary>
    public static AIProfile WithSettings<T>(this AIProfile profile, T settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var jObject = JsonExtensions.FromObject(settings, _ignoreDefaultValuesSerializer);

        profile.Settings[typeof(T).Name] = jObject;

        return profile;
    }
}
