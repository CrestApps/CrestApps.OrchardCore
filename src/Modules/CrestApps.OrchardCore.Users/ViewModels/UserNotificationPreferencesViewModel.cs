namespace CrestApps.OrchardCore.Users.ViewModels;

/// <summary>
/// The site's notification preferences page: the notification methods a user receives, and the methods on for each
/// registered type of notification, as the Notifications module keeps them.
/// </summary>
public class UserNotificationPreferencesViewModel
{
    /// <summary>
    /// Gets the notification methods, such as e-mail and SMS.
    /// </summary>
    public IList<UserNotificationMethodOption> Methods { get; } = [];

    /// <summary>
    /// Gets the registered notification types, by name.
    /// </summary>
    public IList<UserNotificationTypeOption> Types { get; } = [];

    /// <summary>
    /// Gets or sets whether the page is framed as a page of the "my account" area.
    /// </summary>
    public bool InAccount { get; set; }
}

/// <summary>
/// A notification method on the preferences page.
/// </summary>
public class UserNotificationMethodOption
{
    /// <summary>
    /// Gets or sets the method's technical name.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Gets or sets the method's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets whether the user receives notifications this way at all.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// A type of notification on the preferences page.
/// </summary>
public class UserNotificationTypeOption
{
    /// <summary>
    /// Gets or sets the type's technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the type's name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets what the type is about.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets the methods on for the type.
    /// </summary>
    public ISet<string> EnabledMethods { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// What the preferences form posts.
/// </summary>
public class UserNotificationPreferencesInput
{
    /// <summary>
    /// Gets or sets the methods the user receives at all.
    /// </summary>
    public string[] EnabledMethods { get; set; } = [];

    /// <summary>
    /// Gets or sets the methods on for each type, by type name.
    /// </summary>
    public Dictionary<string, string[]> TypeMethods { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
