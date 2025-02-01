using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Extension methods for managing settings in AIChatProfile.
/// </summary>
public static class AIChatProfileExtensions
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
    /// <typeparam name="T">The type of the settings object.</typeparam>
    /// <param name="profile">The <see cref="AIChatProfile"/> instance.</param>
    /// <returns>An instance of <typeparamref name="T"/>.</returns>
    public static T GetSettings<T>(this AIChatProfile profile)
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

        return node.ToObject<T>() ?? new T();
    }

    /// <summary>
    /// Attempts to retrieve settings of type <typeparamref name="T"/> from the profile.
    /// </summary>
    /// <typeparam name="T">The type of the settings object.</typeparam>
    /// <param name="profile">The <see cref="AIChatProfile"/> instance.</param>
    /// <param name="settings">The retrieved settings object, or null if not found.</param>
    /// <returns>True if the settings were successfully retrieved; otherwise, false.</returns>
    public static bool TryGetSettings<T>(this AIChatProfile profile, out T settings)
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

        settings = node.ToObject<T>();
        return true;
    }

    /// <summary>
    /// Alters existing settings or adds new settings of type <typeparamref name="T"/> if one does not exists.
    /// </summary>
    /// <typeparam name="T">The type of the settings object.</typeparam>
    /// <param name="profile">The <see cref="AIChatProfile"/> instance.</param>
    /// <param name="setting">An action to modify or initialize the settings object.</param>
    /// <returns>The updated <see cref="AIChatProfile"/> instance.</returns>
    public static AIChatProfile AlterSettings<T>(this AIChatProfile profile, Action<T> setting)
        where T : class, new()
    {
        var existingJObject = profile.Settings[typeof(T).Name] as JsonObject;

        // If existing settings do not exist, create.
        if (existingJObject == null)
        {
            existingJObject = JObject.FromObject(new T(), _ignoreDefaultValuesSerializer);
            profile.Settings[typeof(T).Name] = existingJObject;
        }

        var settingsToMerge = existingJObject.ToObject<T>();
        setting(settingsToMerge);

        profile.Settings[typeof(T).Name] = JObject.FromObject(settingsToMerge, _ignoreDefaultValuesSerializer);

        return profile;
    }

    /// <summary>
    /// Sets or replaces the settings of type <typeparamref name="T"/> in the profile.
    /// </summary>
    /// <typeparam name="T">The type of the settings object.</typeparam>
    /// <param name="profile">The <see cref="AIChatProfile"/> instance.</param>
    /// <param name="settings">The settings object to set.</param>
    /// <returns>The updated <see cref="AIChatProfile"/> instance.</returns>
    public static AIChatProfile WithSettings<T>(this AIChatProfile profile, T settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var jObject = JObject.FromObject(settings, _ignoreDefaultValuesSerializer);

        profile.Settings[typeof(T).Name] = jObject;

        return profile;
    }
}
