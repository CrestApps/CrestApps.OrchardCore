using CrestApps.Core.AI.Security;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Shared mapping and validation logic for the per-profile and per-template anti-spam throttle overrides.
/// </summary>
internal static class AIProfilePromptSecurityMapper
{
    private const int MaxCountLimit = 1_000;
    private const int MaxWindowSeconds = 86_400;

    /// <summary>
    /// Populates the inherited site-level defaults on the editor model.
    /// </summary>
    /// <param name="model">The editor model to populate.</param>
    /// <param name="options">The site-level prompt security options.</param>
    public static void PopulateSiteDefaults(AIProfilePromptSecurityViewModel model, PromptSecurityOptions options)
    {
        model.SiteMaxMessagesPerWindow = options.MaxMessagesPerWindow;
        model.SiteRateLimitWindowSeconds = (int)Math.Round(options.RateLimitWindow.TotalSeconds);
        model.SiteMaxAnonymousSessionsPerWindow = options.MaxAnonymousSessionsPerWindow;
        model.SiteAnonymousSessionRateLimitWindowSeconds = (int)Math.Round(options.AnonymousSessionRateLimitWindow.TotalSeconds);
    }

    /// <summary>
    /// Populates the override values on the editor model from stored settings.
    /// </summary>
    /// <param name="model">The editor model to populate.</param>
    /// <param name="settings">The stored per-profile throttle settings.</param>
    public static void PopulateOverrides(AIProfilePromptSecurityViewModel model, PromptSecurityProfileSettings settings)
    {
        model.MaxMessagesPerWindow = settings.MaxMessagesPerWindow;
        model.RateLimitWindowSeconds = settings.RateLimitWindow.HasValue
            ? (int)Math.Round(settings.RateLimitWindow.Value.TotalSeconds)
            : null;
        model.MaxAnonymousSessionsPerWindow = settings.MaxAnonymousSessionsPerWindow;
        model.AnonymousSessionRateLimitWindowSeconds = settings.AnonymousSessionRateLimitWindow.HasValue
            ? (int)Math.Round(settings.AnonymousSessionRateLimitWindow.Value.TotalSeconds)
            : null;
    }

    /// <summary>
    /// Applies the override values from the editor model onto the throttle settings.
    /// </summary>
    /// <param name="model">The submitted editor model.</param>
    /// <param name="settings">The throttle settings to update.</param>
    public static void ApplyOverrides(AIProfilePromptSecurityViewModel model, PromptSecurityProfileSettings settings)
    {
        settings.MaxMessagesPerWindow = model.MaxMessagesPerWindow;
        settings.RateLimitWindow = model.RateLimitWindowSeconds.HasValue
            ? TimeSpan.FromSeconds(model.RateLimitWindowSeconds.Value)
            : null;
        settings.MaxAnonymousSessionsPerWindow = model.MaxAnonymousSessionsPerWindow;
        settings.AnonymousSessionRateLimitWindow = model.AnonymousSessionRateLimitWindowSeconds.HasValue
            ? TimeSpan.FromSeconds(model.AnonymousSessionRateLimitWindowSeconds.Value)
            : null;
    }

    /// <summary>
    /// Validates the submitted override values, adding model errors for out-of-range entries.
    /// </summary>
    /// <param name="model">The submitted editor model.</param>
    /// <param name="updater">The updater used to record validation errors.</param>
    /// <param name="prefix">The editor prefix used to scope model errors.</param>
    /// <param name="S">The string localizer.</param>
    public static void Validate(
        AIProfilePromptSecurityViewModel model,
        IUpdateModel updater,
        string prefix,
        IStringLocalizer S)
    {
        if (model.MaxMessagesPerWindow.HasValue && (model.MaxMessagesPerWindow < 0 || model.MaxMessagesPerWindow > MaxCountLimit))
        {
            updater.ModelState.AddModelError(prefix, nameof(model.MaxMessagesPerWindow), S["Maximum messages per window must be between {0} and {1}.", 0, MaxCountLimit]);
        }

        if (model.RateLimitWindowSeconds.HasValue && (model.RateLimitWindowSeconds < 1 || model.RateLimitWindowSeconds > MaxWindowSeconds))
        {
            updater.ModelState.AddModelError(prefix, nameof(model.RateLimitWindowSeconds), S["Message rate-limit window must be between {0} and {1} second(s).", 1, MaxWindowSeconds]);
        }

        if (model.MaxAnonymousSessionsPerWindow.HasValue && (model.MaxAnonymousSessionsPerWindow < 0 || model.MaxAnonymousSessionsPerWindow > MaxCountLimit))
        {
            updater.ModelState.AddModelError(prefix, nameof(model.MaxAnonymousSessionsPerWindow), S["Maximum anonymous sessions per window must be between {0} and {1}.", 0, MaxCountLimit]);
        }

        if (model.AnonymousSessionRateLimitWindowSeconds.HasValue && (model.AnonymousSessionRateLimitWindowSeconds < 1 || model.AnonymousSessionRateLimitWindowSeconds > MaxWindowSeconds))
        {
            updater.ModelState.AddModelError(prefix, nameof(model.AnonymousSessionRateLimitWindowSeconds), S["Anonymous session window must be between {0} and {1} second(s).", 1, MaxWindowSeconds]);
        }
    }
}
