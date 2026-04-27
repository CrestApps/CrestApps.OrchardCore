using CrestApps.Core.Support;
using CrestApps.OrchardCore.Users.Core.Models;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Liquid;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// Resolves a user's display name based on site-level <see cref="DisplayNameSettings"/>,
/// supporting first/last name composition, explicit display name, and custom Liquid templates.
/// </summary>
public sealed class DisplayNameProvider : IDisplayNameProvider
{
    private readonly ISiteService _siteService;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayNameProvider"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to retrieve display-name settings.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager used for custom display-name templates.</param>
    /// <param name="logger">The logger instance.</param>
    public DisplayNameProvider(
        ISiteService siteService,
        ILiquidTemplateManager liquidTemplateManager,
        ILogger<DisplayNameProvider> logger)
    {
        _siteService = siteService;
        _liquidTemplateManager = liquidTemplateManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GetAsync(IUser user)
    {
        if (user == null)
        {
            return string.Empty;
        }

        if (user is not User u)
        {
            return user.UserName;
        }

        if (!u.TryGet<UserFullNamePart>(out var userPart))
        {
            if (_logger?.IsEnabled(LogLevel.Trace) == true)
            {
                _logger.LogTrace("Attempting to access user '{UserName}' full name where the part '{PartName}' is not available.", user.UserName, nameof(UserFullNamePart));
            }

            return user.UserName;
        }

        var settings = await _siteService.GetSettingsAsync<DisplayNameSettings>();

        if (settings.Type == DisplayNameType.DisplayName)
        {
            if (string.IsNullOrWhiteSpace(userPart.DisplayName))
            {
                return user.UserName;
            }

            return userPart.DisplayName;
        }

        if (settings.Type == DisplayNameType.Other)
        {
            return await GetDisplayFromTemplate(user, userPart, settings);
        }

        var displayName = GetDisplayName(userPart, settings);

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        return user.UserName;
    }

    private async Task<string> GetDisplayFromTemplate(IUser user, UserFullNamePart userPart, DisplayNameSettings setting)
    {
        var customName = await _liquidTemplateManager.RenderStringAsync(setting.Template, NullEncoder.Default,
        new Dictionary<string, FluidValue>()
        {
            ["User"] = new ObjectValue(user),
            [nameof(userPart.FirstName)] = new StringValue(userPart.FirstName),
            [nameof(userPart.MiddleName)] = new StringValue(userPart.MiddleName),
            [nameof(userPart.LastName)] = new StringValue(userPart.LastName),
            [nameof(userPart.DisplayName)] = new StringValue(userPart.DisplayName),
            [nameof(IUser.UserName)] = new StringValue(user.UserName),
        });

        if (!string.IsNullOrWhiteSpace(customName))
        {
            return customName;
        }

        return userPart.DisplayName;
    }

    private static string GetDisplayName(UserFullNamePart userPart, DisplayNameSettings settings)
    {
        var middleName = settings.MiddleName != DisplayNamePropertyType.None ? userPart?.MiddleName : null;

        var displayName = string.Empty;

        var fullName = Str.Merge(userPart?.FirstName, middleName, userPart?.LastName);

        if (!string.IsNullOrEmpty(fullName))
        {
            // at this point we know there is data in the full-name
            if (settings.Type == DisplayNameType.LastThenFirst)
            {
                var lastName = userPart?.LastName;
                var firstName = Str.Merge(userPart?.FirstName, middleName);

                if (string.IsNullOrEmpty(lastName) && !string.IsNullOrEmpty(firstName))
                {
                    // at this point we know there is not last name, so we merge first then middle
                    displayName = firstName;
                }
                else if (!string.IsNullOrEmpty(lastName) && string.IsNullOrEmpty(firstName))
                {
                    displayName = lastName;
                }
                else if (!string.IsNullOrEmpty(lastName) && !string.IsNullOrEmpty(firstName))
                {
                    displayName = lastName + ", " + firstName;
                }
            }
            else if (settings.Type == DisplayNameType.FirstThenLast)
            {
                displayName = fullName;
            }
        }

        return displayName;
    }
}
