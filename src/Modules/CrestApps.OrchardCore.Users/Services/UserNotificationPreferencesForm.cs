using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Reads and writes the notification preferences of a user, the <see cref="UserNotificationPreferencesPart"/> of the
/// Notifications module, for the site's preferences page, with the rules of the module's own profile editor: a method a
/// user turns off is off for every type (<see cref="UserNotificationPreferencesPart.Optout"/>), and each type keeps only
/// the choices that differ from its defaults (<see cref="NotificationTypePreferences"/>).
/// </summary>
public static class UserNotificationPreferencesForm
{
    /// <summary>
    /// Reads the methods a user receives, and the methods on for each type.
    /// </summary>
    /// <param name="part">The user's preferences, or <see langword="null"/> when they chose nothing.</param>
    /// <param name="methods">The available notification methods.</param>
    /// <param name="types">The registered notification types.</param>
    /// <returns>The choices.</returns>
    public static UserNotificationPreferencesChoices Read(UserNotificationPreferencesPart part, IEnumerable<string> methods, IEnumerable<NotificationTypeDescriptor> types)
    {
        ArgumentNullException.ThrowIfNull(methods);
        ArgumentNullException.ThrowIfNull(types);

        var listed = methods.ToArray();
        var optout = new HashSet<string>(part?.Optout ?? [], StringComparer.OrdinalIgnoreCase);
        var choices = new UserNotificationPreferencesChoices();

        foreach (var method in listed.Where(method => !optout.Contains(method)))
        {
            choices.EnabledMethods.Add(method);
        }

        foreach (var type in types)
        {
            choices.TypeMethods[type.Name] = listed.Where(method => NotificationTypePreferences.IsMethodEnabledForType(part, type, method)).ToArray();
        }

        return choices;
    }

    /// <summary>
    /// Writes the choices to the user's preferences.
    /// </summary>
    /// <param name="part">The user's preferences.</param>
    /// <param name="methods">The available notification methods.</param>
    /// <param name="types">The registered notification types.</param>
    /// <param name="enabledMethods">The methods the user receives at all.</param>
    /// <param name="typeMethods">The methods on for each type, by type name; a type left out has every method off.</param>
    public static void Apply(UserNotificationPreferencesPart part, IEnumerable<string> methods, IEnumerable<NotificationTypeDescriptor> types, IEnumerable<string> enabledMethods, IDictionary<string, string[]> typeMethods)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(methods);
        ArgumentNullException.ThrowIfNull(types);

        var listed = methods.ToArray();
        var enabled = new HashSet<string>(enabledMethods ?? [], StringComparer.OrdinalIgnoreCase);

        // A method the page does not list (its feature is off now) keeps the choice made before.
        part.Optout = (part.Optout ?? [])
            .Where(method => !listed.Contains(method, StringComparer.OrdinalIgnoreCase))
            .Concat(listed.Where(method => !enabled.Contains(method)))
            .ToArray();

        foreach (var type in types)
        {
            string[] chosen = null;
            typeMethods?.TryGetValue(type.Name, out chosen);
            NotificationTypePreferences.SetEnabledMethods(part, type, listed, chosen);
        }
    }
}

/// <summary>
/// The notification choices of a user.
/// </summary>
public sealed class UserNotificationPreferencesChoices
{
    /// <summary>
    /// Gets the methods the user receives at all.
    /// </summary>
    public IList<string> EnabledMethods { get; } = [];

    /// <summary>
    /// Gets the methods on for each type, by type name.
    /// </summary>
    public IDictionary<string, string[]> TypeMethods { get; } = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
}
