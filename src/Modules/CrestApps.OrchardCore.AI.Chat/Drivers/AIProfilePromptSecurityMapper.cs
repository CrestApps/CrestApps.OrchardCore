using CrestApps.Core.AI.Security;
using CrestApps.OrchardCore.AI.Chat.Services;
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
        model.SiteAnonymousMessageRateLimitTiers = ChatRateLimitTierTextFormatter.FormatInline(options.AnonymousMessageRateLimitTiers);
        model.SiteAnonymousSessionStartRateLimitTiers = ChatRateLimitTierTextFormatter.FormatInline(options.AnonymousSessionStartRateLimitTiers);
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

        // A null list inherits the site tiers; an empty list is a deliberate opt-out that falls back to
        // this profile's single-window values.
        model.AnonymousMessageRateLimitTiers = ChatRateLimitTierTextFormatter.Format(settings.AnonymousMessageRateLimitTiers);
        model.DisableAnonymousMessageRateLimitTiers = settings.AnonymousMessageRateLimitTiers?.Count == 0;
        model.AnonymousSessionStartRateLimitTiers = ChatRateLimitTierTextFormatter.Format(settings.AnonymousSessionStartRateLimitTiers);
        model.DisableAnonymousSessionStartRateLimitTiers = settings.AnonymousSessionStartRateLimitTiers?.Count == 0;
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
        settings.AnonymousMessageRateLimitTiers = ResolveTiers(model.AnonymousMessageRateLimitTiers, model.DisableAnonymousMessageRateLimitTiers);
        settings.AnonymousSessionStartRateLimitTiers = ResolveTiers(model.AnonymousSessionStartRateLimitTiers, model.DisableAnonymousSessionStartRateLimitTiers);
    }

    /// <summary>
    /// Resolves the stored tier list for one field. Returns an empty list when the profile opts out of
    /// tiered limits, <see langword="null"/> when the field is blank so the site tiers are inherited,
    /// and the parsed tiers otherwise.
    /// </summary>
    /// <param name="text">The submitted tier text.</param>
    /// <param name="isDisabled">Whether the profile opts out of tiered limits for this field.</param>
    /// <returns>The list to store on the profile settings.</returns>
    private static List<ChatRateLimitTier> ResolveTiers(string text, bool isDisabled)
    {
        if (isDisabled)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // Validation runs before this, so a failure here is not expected; inherit rather than store
        // an unusable list if it ever happens.
        return ChatRateLimitTierTextFormatter.TryParse(text, out var tiers, out _)
            ? tiers
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

        ValidateTiers(
            model.AnonymousMessageRateLimitTiers,
            model.DisableAnonymousMessageRateLimitTiers,
            nameof(model.AnonymousMessageRateLimitTiers),
            updater,
            prefix,
            S);

        ValidateTiers(
            model.AnonymousSessionStartRateLimitTiers,
            model.DisableAnonymousSessionStartRateLimitTiers,
            nameof(model.AnonymousSessionStartRateLimitTiers),
            updater,
            prefix,
            S);
    }

    private static void ValidateTiers(
        string text,
        bool isDisabled,
        string field,
        IUpdateModel updater,
        string prefix,
        IStringLocalizer S)
    {
        if (isDisabled)
        {
            // Keeping both would hide one of them, so make the contradiction explicit rather than
            // silently discarding the tiers the author typed.
            if (!string.IsNullOrWhiteSpace(text))
            {
                updater.ModelState.AddModelError(prefix, field, S["Clear the tiers or uncheck the option that disables them; they cannot both be set."]);
            }

            return;
        }

        if (!ChatRateLimitTierTextFormatter.TryParse(text, out _, out var error))
        {
            updater.ModelState.AddModelError(prefix, field, ChatRateLimitTierTextFormatter.Describe(error, S));
        }
    }
}
