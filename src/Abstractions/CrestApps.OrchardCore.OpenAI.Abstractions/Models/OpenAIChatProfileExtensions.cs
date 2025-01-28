using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.OpenAI.Models;

public static class OpenAIChatProfileExtensions
{
    private static readonly JsonSerializerOptions _ignoreDefaultValuesSerializer = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNameCaseInsensitive = true,
    };

    public static T GetSettings<T>(this OpenAIChatProfile profile)
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

    public static bool TryGetSettings<T>(this OpenAIChatProfile profile, out T settings)
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
    /// Alter existing settings or add new settings.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="profile"></param>
    /// <param name="setting"></param>
    /// <returns></returns>
    public static OpenAIChatProfile AlterSettings<T>(this OpenAIChatProfile profile, Action<T> setting)
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

    public static OpenAIChatProfile WithSettings<T>(this OpenAIChatProfile profile, T settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var jObject = JObject.FromObject(settings, _ignoreDefaultValuesSerializer);

        profile.Settings[typeof(T).Name] = jObject;

        return profile;
    }
}
