using CrestApps.OrchardCore.AI.Core.Integrations;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Integrations;

/// <summary>
/// The editor of an integration connection.
/// </summary>
public class AIIntegrationConnectionViewModel
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the provider.</summary>
    public string Provider { get; set; }

    /// <summary>Gets or sets a value indicating whether the connection is new.</summary>
    public bool IsNew { get; set; }

    /// <summary>Gets or sets the client id.</summary>
    public string ClientId { get; set; }

    /// <summary>Gets or sets a new client secret.</summary>
    public string ClientSecret { get; set; }

    /// <summary>Gets or sets a new token, for HubSpot and Notion.</summary>
    public string Token { get; set; }

    /// <summary>Gets or sets a value indicating whether a secret or token is saved.</summary>
    public bool HasSecret { get; set; }

    /// <summary>Gets or sets the Microsoft tenant.</summary>
    public string TenantId { get; set; }

    /// <summary>Gets or sets the Salesforce address.</summary>
    public string InstanceUrl { get; set; }

    /// <summary>Gets or sets the calendar.</summary>
    public string CalendarId { get; set; }

    /// <summary>Gets or sets the time zone.</summary>
    public string TimeZoneId { get; set; }

    /// <summary>Gets or sets the first working hour.</summary>
    public int WorkdayStartHour { get; set; }

    /// <summary>Gets or sets the hour work ends.</summary>
    public int WorkdayEndHour { get; set; }

    /// <summary>Gets or sets the address to give the provider as the redirect.</summary>
    public string RedirectUrl { get; set; }
}

/// <summary>
/// Displays an integration connection in the admin list and edits it.
/// </summary>
public sealed class AIIntegrationConnectionDisplayDriver : DisplayDriver<AIIntegrationConnection>
{
    private readonly AIIntegrationTokens _tokens;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIIntegrationConnectionDisplayDriver"/> class.
    /// </summary>
    /// <param name="tokens">Encrypts the secrets.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIIntegrationConnectionDisplayDriver(AIIntegrationTokens tokens, IStringLocalizer<AIIntegrationConnectionDisplayDriver> stringLocalizer)
    {
        _tokens = tokens;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(AIIntegrationConnection connection, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("AIIntegrationConnection_Fields_SummaryAdmin", connection)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AIIntegrationConnection_DefaultTags_SummaryAdmin", connection)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Provider:5"),
            View("AIIntegrationConnection_Buttons_SummaryAdmin", connection).Location("Actions:5"),
            View("AIIntegrationConnection_ActionsMenu_SummaryAdmin", connection).Location("ActionsMenu:10"));
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AIIntegrationConnection connection, BuildEditorContext context)
    {
        return Initialize<AIIntegrationConnectionViewModel>("AIIntegrationConnectionFields_Edit", model =>
        {
            model.Name = connection.Name;
            model.Provider = connection.Provider ?? AIIntegrationProviders.Google;
            model.IsNew = context.IsNew;
            model.ClientId = connection.ClientId;
            model.HasSecret = !string.IsNullOrEmpty(connection.ProtectedClientSecret) || !string.IsNullOrEmpty(connection.ProtectedAccessToken);
            model.TenantId = connection.TenantId;
            model.InstanceUrl = connection.InstanceUrl;
            model.CalendarId = connection.CalendarId;
            model.TimeZoneId = connection.TimeZoneId;
            model.WorkdayStartHour = connection.WorkdayStartHour;
            model.WorkdayEndHour = connection.WorkdayEndHour;
        }).Location("Content:1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIIntegrationConnection connection, UpdateEditorContext context)
    {
        var model = new AIIntegrationConnectionViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            connection.Provider = AIIntegrationProviders.All.Contains(model.Provider) ? model.Provider : null;
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["The name is required."]);
        }

        if (connection.Provider is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Provider), S["Choose the provider."]);
        }
        else if (AIIntegrationProviders.UsesOAuth(connection.Provider))
        {
            if (string.IsNullOrWhiteSpace(model.ClientId))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ClientId), S["Enter the client id of the app you registered with {0}.", connection.Provider]);
            }

            if (string.IsNullOrWhiteSpace(model.ClientSecret) && string.IsNullOrEmpty(connection.ProtectedClientSecret))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ClientSecret), S["Enter the app's client secret."]);
            }
        }
        else if (string.IsNullOrWhiteSpace(model.Token) && string.IsNullOrEmpty(connection.ProtectedAccessToken))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Token), S["Enter the {0} token.", connection.Provider]);
        }

        if (!string.IsNullOrWhiteSpace(model.TimeZoneId) && !TimeZoneInfo.TryFindSystemTimeZoneById(model.TimeZoneId.Trim(), out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TimeZoneId), S["Enter a time zone such as America/Los_Angeles."]);
        }

        if (model.WorkdayStartHour is < 0 or > 23 || model.WorkdayEndHour is < 1 or > 24 || model.WorkdayEndHour <= model.WorkdayStartHour)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.WorkdayEndHour), S["Working hours run from a start hour to a later end hour, from 0 to 24."]);
        }

        connection.Name = model.Name?.Trim();
        connection.ClientId = model.ClientId?.Trim();
        connection.TenantId = model.TenantId?.Trim();
        connection.InstanceUrl = model.InstanceUrl?.Trim();
        connection.CalendarId = model.CalendarId?.Trim();
        connection.TimeZoneId = model.TimeZoneId?.Trim();
        connection.WorkdayStartHour = model.WorkdayStartHour;
        connection.WorkdayEndHour = model.WorkdayEndHour;

        // An empty secret or token keeps the saved one: neither is shown again.
        if (!string.IsNullOrWhiteSpace(model.ClientSecret))
        {
            connection.ProtectedClientSecret = _tokens.Protect(model.ClientSecret);
        }

        if (!string.IsNullOrWhiteSpace(model.Token))
        {
            connection.ProtectedAccessToken = _tokens.Protect(model.Token);
            connection.AccessTokenExpiresUtc = null;
        }

        return Edit(connection, context);
    }
}
