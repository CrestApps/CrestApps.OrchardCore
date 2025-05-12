using CrestApps.OrchardCore.Users.Core.Models;
using CrestApps.Support;
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

public sealed class DisplayNameProvider : IDisplayNameProvider
{
    private readonly ISiteService _siteService;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly ILogger _logger;

    public DisplayNameProvider(
        ISiteService siteService,
        ILiquidTemplateManager liquidTemplateManager,
        ILogger<DisplayNameProvider> logger)
    {
        _siteService = siteService;
        _liquidTemplateManager = liquidTemplateManager;
        _logger = logger;
    }

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

        var userPart = u.As<UserFullNamePart>();

        if (userPart == null)
        {
            _logger?.LogTrace("Attempting to access user '{UserName}' full name where the part '{PartName}' is not available.", user.UserName, nameof(UserFullNamePart));

            return user.UserName;
        }

        var setting = (await _siteService.GetSiteSettingsAsync()).As<DisplayNameSettings>();

        if (setting.Type == DisplayNameType.DisplayName)
        {
            if (string.IsNullOrWhiteSpace(userPart.DisplayName))
            {
                return user.UserName;
            }

            return userPart.DisplayName;
        }

        if (setting.Type == DisplayNameType.Other)
        {
            return await GetDisplayFromTemplate(user, userPart, setting);
        }

        var displayName = GetDisplayName(userPart, setting);

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
