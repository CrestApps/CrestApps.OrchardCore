using System.Text.Json;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Omnichannel.Messaging;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Tests.Agent.Records;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel.AITools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.AITools;

/// <summary>
/// Message templates are the <c>messageTemplate</c> record kind of the generic record tools: registered by the workspace
/// feature with the permission the Templates page checks, read and written through the template manager and editor, and
/// returned as their name, body, scope and placeholder setting only.
/// </summary>
public sealed class MessagingRecordKindTests
{
    [Fact]
    public void MessageTemplateKind_IsReadAndWrittenWithTheTemplatesPagesPermission()
    {
        // Act
        var kind = GetKind();

        // Assert
        Assert.True(kind.CanWrite);
        Assert.Equal(MessagingPermissions.ManageMessaging.Name, kind.Permission.Name);
        Assert.Equal(MessagingPermissions.ManageMessaging.Name, kind.EffectiveReadPermission.Name);
        Assert.False(string.IsNullOrWhiteSpace(kind.Description));
    }

    [Fact]
    public void MessageTemplateKind_ReturnsTheNameBodyScopeAndPlaceholderSetting_Only()
    {
        // Act
        var projected = JsonSerializer.SerializeToElement(GetKind().Projection(new MessageTemplate
        {
            ItemId = "template-1",
            Name = "Shipping",
            Body = "Your order ships tomorrow, {{ Contact.DisplayName }}.",
            Scope = "Tickets",
            FillsPlaceholders = true,
            Properties = { ["Internal"] = "never" },
        }));
        var workspaceTemplate = JsonSerializer.SerializeToElement(GetKind().Projection(new MessageTemplate { ItemId = "template-2", Name = "Hello", Body = "Hello", Scope = string.Empty }));

        // Assert
        Assert.Equal(new[] { "body", "fillsPlaceholders", "id", "name", "scope" }, projected.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Your order ships tomorrow, {{ Contact.DisplayName }}.", projected.GetProperty("body").GetString());
        Assert.Equal("Tickets", projected.GetProperty("scope").GetString());
        Assert.True(projected.GetProperty("fillsPlaceholders").GetBoolean());
        Assert.Equal(JsonValueKind.Null, workspaceTemplate.GetProperty("scope").ValueKind);
        Assert.False(workspaceTemplate.GetProperty("fillsPlaceholders").GetBoolean());
    }

    [Fact]
    public async Task MessageTemplateKind_ListsAndValidatesThroughTheTemplateManager()
    {
        // Arrange
        var harness = new MessagingToolHarness();
        harness.Templates.Add(new MessageTemplate { ItemId = "template-1", Name = "Shipping", Body = "Your order ships tomorrow." });
        harness.Templates.Add(new MessageTemplate { ItemId = "template-2", Name = "Closed Friday", Body = "The office is closed on Friday." });
        harness.Templates.Validate = FakeCatalogManager<MessageTemplate>.RequireName;
        var services = harness.Build();
        var kind = GetKind();

        // Act
        var page = await kind.ListAsync(services, new AIRecordQuery { Text = "ship" }, CancellationToken.None);
        var validation = await kind.ValidateAsync(services, new MessageTemplate { Body = "Hello" }, CancellationToken.None);

        // Assert
        Assert.Equal("template-1", Assert.Single(page.Entries).ItemId);
        Assert.False(validation.Succeeded);
    }

    private static AIRecordKind<MessageTemplate> GetKind()
    {
        var services = new ServiceCollection();

        new AIToolsStartup(new RecordTestLocalizer<AIToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return (AIRecordKind<MessageTemplate>)provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds["messageTemplate"];
    }
}
