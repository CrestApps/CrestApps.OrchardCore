using System.Data.Common;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Migrations;

/// <summary>
/// Carries a tenant from the SMS-only portal that preceded the messaging workspace onto the workspace, once, when the
/// workspace feature is first enabled: its conversations, templates and broadcasts, the inbound routing configured
/// on each SMS endpoint, and the portal permissions granted to roles. Without it a tenant that switched features
/// would find every thread, template and routing rule gone and every agent locked out.
/// </summary>
/// <remarks>
/// It is safe to run on a tenant that never had the portal (there is nothing to read) and safe to run twice (an
/// item already imported is skipped). The message bodies need no import: they live in the shared Omnichannel message
/// store and keep pointing at their conversation by its unchanged identifier.
/// </remarks>
internal static class LegacySmsPortalImport
{
    private const string LegacyConversationType = ".SmsConversation, ";
    private const string LegacyTemplateType = ".SmsTemplate, ";
    private const string LegacyBroadcastType = ".SmsBroadcast, ";

    private const string LegacyRoutingSettingsKey = "SmsEndpointRoutingSettings";
    private const string RoutingSettingsKey = nameof(MessagingEndpointRoutingSettings);

    private static readonly Dictionary<string, string> _permissionRenames = new(StringComparer.Ordinal)
    {
        ["UseSmsPortal"] = MessagingPermissions.UseMessagingWorkspace.Name,
        ["ManageSmsNumberRoutes"] = MessagingPermissions.ManageMessaging.Name,
        ["SendSmsDuringQuietHours"] = MessagingPermissions.SendDuringQuietHours.Name,
        ["SendGroupSms"] = MessagingPermissions.SendGroupMessages.Name,
        ["ViewAllSmsConversations"] = MessagingPermissions.ViewAllConversations.Name,
    };

    /// <summary>
    /// Runs the import.
    /// </summary>
    /// <param name="serviceProvider">The services of the deferred scope the migration scheduled.</param>
    public static async Task ImportAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(LegacySmsPortalImport));

        try
        {
            var documents = await ReadLegacyDocumentsAsync(serviceProvider, logger);

            if (documents.Count > 0)
            {
                await ImportDocumentsAsync(serviceProvider, documents, logger);
            }

            await ImportEndpointRoutingAsync(serviceProvider, logger);
            await ImportRolePermissionsAsync(serviceProvider, logger);
        }
        catch (Exception ex)
        {
            // The workspace itself is fully usable without the import, so a failure is reported rather than allowed to
            // fail the tenant.
            logger.LogError(ex, "Importing the SMS portal data into the messaging workspace failed.");
        }
    }

    private static async Task<IReadOnlyList<Document>> ReadLegacyDocumentsAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        var store = serviceProvider.GetRequiredService<IStore>();
        var dbConnectionAccessor = serviceProvider.GetRequiredService<IDbConnectionAccessor>();

        var dialect = store.Configuration.SqlDialect;
        var table = store.Configuration.TablePrefix + store.Configuration.TableNameConvention.GetDocumentTable(MessagingStorage.LegacySmsPortalCollectionName);
        var quotedTableName = dialect.QuoteForTableName(table, store.Configuration.Schema);
        var quotedTypeColumnName = dialect.QuoteForColumnName(nameof(Document.Type));
        var quotedContentColumnName = dialect.QuoteForColumnName(nameof(Document.Content));

        await using var connection = dbConnectionAccessor.CreateConnection();
        await connection.OpenAsync();

        try
        {
            return (await connection.QueryAsync<Document>(
                $"SELECT {quotedTypeColumnName}, {quotedContentColumnName} FROM {quotedTableName} " +
                $"WHERE {quotedTypeColumnName} LIKE @Conversation OR {quotedTypeColumnName} LIKE @Template OR {quotedTypeColumnName} LIKE @Broadcast",
                new
                {
                    Conversation = $"%{LegacyConversationType}%",
                    Template = $"%{LegacyTemplateType}%",
                    Broadcast = $"%{LegacyBroadcastType}%",
                })).ToArray();
        }
        catch (DbException ex)
        {
            // A tenant that never ran the SMS portal has no table to read, which is the ordinary case.
            logger.LogDebug(ex, "No SMS portal documents were found to import.");

            return [];
        }
    }

    private static async Task ImportDocumentsAsync(IServiceProvider serviceProvider, IReadOnlyList<Document> documents, ILogger logger)
    {
        var store = serviceProvider.GetRequiredService<IStore>();
        var session = serviceProvider.GetRequiredService<ISession>();
        var conversationStore = serviceProvider.GetRequiredService<IMessagingConversationStore>();
        var templateStore = serviceProvider.GetRequiredService<IMessageTemplateStore>();
        var broadcastStore = serviceProvider.GetRequiredService<IMessagingBroadcastStore>();
        var serializer = store.Configuration.ContentSerializer;

        int conversations = 0, templates = 0, broadcasts = 0;

        foreach (var document in documents)
        {
            if (JsonNode.Parse(document.Content) is not JsonObject content)
            {
                continue;
            }

            if (document.Type.Contains(LegacyConversationType, StringComparison.Ordinal))
            {
                // The portal only ever served SMS, and older documents carry no channel at all.
                content[nameof(MessagingConversation.Channel)] ??= OmnichannelConstants.Channels.Sms;

                var conversation = (MessagingConversation)serializer.Deserialize(content.ToJsonString(), typeof(MessagingConversation));

                if (!string.IsNullOrEmpty(conversation?.ItemId) && await conversationStore.FindByIdAsync(conversation.ItemId) is null)
                {
                    await conversationStore.CreateAsync(conversation);
                    conversations++;
                }
            }
            else if (document.Type.Contains(LegacyTemplateType, StringComparison.Ordinal))
            {
                var template = (MessageTemplate)serializer.Deserialize(content.ToJsonString(), typeof(MessageTemplate));

                if (!string.IsNullOrEmpty(template?.ItemId) && await templateStore.FindByIdAsync(template.ItemId) is null)
                {
                    await templateStore.CreateAsync(template);
                    templates++;
                }
            }
            else if (document.Type.Contains(LegacyBroadcastType, StringComparison.Ordinal))
            {
                // A broadcast named its sending number FromNumber; the workspace names it the service address of a channel.
                if (content["FromNumber"] is JsonNode fromNumber && content[nameof(MessagingBroadcast.ServiceAddress)] is null)
                {
                    content.Remove("FromNumber");
                    content[nameof(MessagingBroadcast.ServiceAddress)] = fromNumber;
                }

                content[nameof(MessagingBroadcast.Channel)] ??= OmnichannelConstants.Channels.Sms;

                var broadcast = (MessagingBroadcast)serializer.Deserialize(content.ToJsonString(), typeof(MessagingBroadcast));

                if (!string.IsNullOrEmpty(broadcast?.ItemId) && await broadcastStore.FindByIdAsync(broadcast.ItemId) is null)
                {
                    await broadcastStore.CreateAsync(broadcast);
                    broadcasts++;
                }
            }
        }

        await session.SaveChangesAsync();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Imported {Conversations} conversations, {Templates} templates and {Broadcasts} broadcasts from the SMS portal into the messaging workspace.",
                conversations,
                templates,
                broadcasts);
        }
    }

    // The routing editor stored its settings on each endpoint under the portal's type name. The workspace reads them
    // under its own, so the settings are copied across; the old entry is left in place, since it is harmless.
    private static async Task ImportEndpointRoutingAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        var endpointManager = serviceProvider.GetRequiredService<IOmnichannelChannelEndpointManager>();
        var session = serviceProvider.GetRequiredService<ISession>();
        var updated = 0;

        foreach (var endpoint in await endpointManager.GetAllAsync())
        {
            if (endpoint.Properties is null ||
                endpoint.Properties.ContainsKey(RoutingSettingsKey) ||
                endpoint.Properties[LegacyRoutingSettingsKey] is not JsonObject legacy)
            {
                continue;
            }

            endpoint.Properties[RoutingSettingsKey] = legacy.DeepClone();

            await endpointManager.UpdateAsync(endpoint);
            updated++;
        }

        if (updated > 0)
        {
            await session.SaveChangesAsync();

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Carried the SMS routing of {Count} endpoints over to the messaging workspace.", updated);
            }
        }
    }

    // A role granted a portal permission is granted the workspace permission that replaced it, so nobody who could work
    // the SMS inbox is locked out of the workspace the day it is enabled.
    private static async Task ImportRolePermissionsAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        var roleService = serviceProvider.GetService<IRoleService>();
        var roleManager = serviceProvider.GetService<RoleManager<IRole>>();

        if (roleService is null || roleManager is null)
        {
            return;
        }

        foreach (var role in await roleService.GetRolesAsync())
        {
            if (role is not Role editable || editable.RoleClaims is null)
            {
                continue;
            }

            var granted = editable.RoleClaims
                .Where(claim => claim.ClaimType == Permission.ClaimType)
                .Select(claim => claim.ClaimValue)
                .ToHashSet(StringComparer.Ordinal);

            var additions = _permissionRenames
                .Where(rename => granted.Contains(rename.Key) && !granted.Contains(rename.Value))
                .Select(rename => rename.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (additions.Length == 0)
            {
                continue;
            }

            foreach (var permission in additions)
            {
                editable.RoleClaims.Add(new RoleClaim { ClaimType = Permission.ClaimType, ClaimValue = permission });
            }

            await roleManager.UpdateAsync(editable);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Granted the role {Role} the messaging workspace permissions that replace its SMS portal permissions.", editable.RoleName);
            }
        }
    }
}
