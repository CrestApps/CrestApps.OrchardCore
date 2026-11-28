using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.AI.Agent.Users;

/// <summary>
/// Builds what the user tools return about a user from an explicit list of fields. The stored user is never
/// serialized: its password hash, security stamp, reset token, sign-in tokens (authenticator key, recovery codes),
/// claims and the keys of its external logins never reach a tool result.
/// </summary>
internal static class UserToolProjection
{
    // The User Time Zone and User Localization features keep their value in a section of the user that is named
    // after a model of the Users module, which this module cannot reference. The section is read by its name.
    private const string TimeZoneSection = "UserTimeZone";
    private const string TimeZoneProperty = "TimeZoneId";
    private const string LocalizationSection = "UserLocalizationSettings";
    private const string CultureProperty = "Culture";

    /// <summary>
    /// Describes a user for a list: who it is, its roles and whether it can sign in.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The fields a list returns.</returns>
    public static object Summarize(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new
        {
            userId = user.UserId,
            userName = user.UserName,
            email = user.Email,
            roles = user.RoleNames?.ToArray() ?? [],
            enabled = user.IsEnabled,
        };
    }

    /// <summary>
    /// Describes one user: profile, roles and sign-in state, without any credential.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The user.</param>
    /// <param name="customSettings">The custom user settings the caller may see, by settings type.</param>
    /// <returns>The fields the tool returns.</returns>
    public static async Task<object> DescribeAsync(IServiceProvider services, User user, IReadOnlyDictionary<string, JsonNode> customSettings = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        var userManager = services.GetRequiredService<UserManager<IUser>>();
        var localClock = services.GetRequiredService<ILocalClock>();
        var lockedOut = await userManager.IsLockedOutAsync(user);

        return new
        {
            userId = user.UserId,
            userName = user.UserName,
            email = user.Email,
            emailConfirmed = user.EmailConfirmed,
            phoneNumber = user.PhoneNumber,
            phoneNumberConfirmed = user.PhoneNumberConfirmed,
            enabled = user.IsEnabled,
            roles = user.RoleNames?.ToArray() ?? [],
            isSiteOwner = await UserToolAccess.IsSiteOwnerAsync(services, user.UserId),
            hasPassword = await userManager.HasPasswordAsync(user),
            twoFactorEnabled = user.TwoFactorEnabled,
            lockedOut,
            lockedOutUntil = lockedOut
                ? await AgentToolResults.ToLocalAsync(localClock, user.LockoutEndUtc)
                : null,
            failedSignInCount = user.AccessFailedCount,
            externalLoginProviders = user.LoginInfos?
                .Select(login => login.LoginProvider)
                .Where(provider => !string.IsNullOrEmpty(provider))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [],
            timeZoneId = ReadSectionValue(user, TimeZoneSection, TimeZoneProperty),
            culture = ReadSectionValue(user, LocalizationSection, CultureProperty),
            customSettings = customSettings is { Count: > 0 }
                ? customSettings
                : null,
            adminUrl = UserToolAccess.GetAdminUrl(services, "Edit", user.UserId),
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
        };
    }

    /// <summary>
    /// Reads the custom user settings of a user, for each settings type the given check lets the caller see.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The user.</param>
    /// <param name="isAllowedAsync">Whether the caller may see the settings of a settings type.</param>
    /// <returns>The parts and fields of each settings type that has a stored value, by the name of the type.</returns>
    public static async Task<Dictionary<string, JsonNode>> ReadCustomSettingsAsync(IServiceProvider services, User user, Func<string, Task<bool>> isAllowedAsync)
    {
        var settings = new Dictionary<string, JsonNode>(StringComparer.Ordinal);

        // The service is registered with the Users feature; there are settings types only when the Custom User
        // Settings feature is on and a content type has the CustomUserSettings stereotype.
        var customUserSettingsService = services.GetService<CustomUserSettingsService>();

        if (customUserSettingsService is null)
        {
            return settings;
        }

        foreach (var settingsType in await customUserSettingsService.GetAllSettingsTypesAsync())
        {
            if (!user.Properties.TryGetPropertyValue(settingsType.Name, out var stored) || stored is not JsonObject storedSettings)
            {
                continue;
            }

            if (!await isAllowedAsync(settingsType.Name))
            {
                continue;
            }

            settings[settingsType.Name] = GetParts(storedSettings);
        }

        return settings;
    }

    /// <summary>
    /// Keeps the parts of a stored custom user settings item and leaves out the properties of the content item itself.
    /// </summary>
    /// <param name="storedSettings">The stored settings item.</param>
    /// <returns>The parts, by name.</returns>
    public static JsonObject GetParts(JsonObject storedSettings)
    {
        ArgumentNullException.ThrowIfNull(storedSettings);

        var parts = new JsonObject();

        foreach (var property in storedSettings)
        {
            // The parts of a content item are objects; its own properties (ID, type, dates, owner) are values.
            if (property.Value is JsonObject part)
            {
                parts[property.Key] = part.DeepClone();
            }
        }

        return parts;
    }

    private static string ReadSectionValue(User user, string sectionName, string propertyName)
    {
        if (user.Properties is null ||
            !user.Properties.TryGetPropertyValue(sectionName, out var section) ||
            section is not JsonObject sectionObject ||
            !sectionObject.TryGetPropertyValue(propertyName, out var value) ||
            value is null ||
            value.GetValueKind() != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetValue<string>();

        return string.IsNullOrEmpty(text)
            ? null
            : text;
    }
}
