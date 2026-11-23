using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Drivers;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Endpoints;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Recipes;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp;

/// <summary>
/// Registers the Mailchimp provider of mailing lists: the provider and its client, the connection card on the mailing
/// lists settings page, the Mailchimp card of the list editor, and the webhook endpoint.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMailchimpMailingLists();
        services.AddHttpContextAccessor();
        services.AddSignedLinks();
        services.AddScoped<IMailchimpCredentialsProvider, MailchimpCredentialsProvider>();

        services.AddSiteDisplayDriver<MailchimpSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(MailingListsConstants.SettingsGroupId, MailingListsPermissions.ManageMailingListSettings);
        services.AddDisplayDriver<MailingList, MailchimpMailingListDisplayDriver>();
    }

    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddMailchimpWebhookEndpoint();
    }
}

/// <summary>
/// Describes the Mailchimp settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, MailchimpSettingsSchema>();
    }
}
