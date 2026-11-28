using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentAccessControl.AI;
using CrestApps.OrchardCore.Roles.Core.Models;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.ContentAccessControl.AI;

/// <summary>
/// The content access tools need <c>EditContent</c> on the item, read the restricting Role Picker parts as the access
/// handler does, apply the part editor's rules, and never publish someone's pending draft.
/// </summary>
public sealed class ContentAccessAIToolTests
{
    private readonly List<string> _actions = [];
    private ContentItem _published;
    private ContentItem _latest;
    private ContentItem _draft;
    private ContentTypeDefinition _definition = CreateDefinition(Part("RolePickerPart", restricted: true, allowMultiple: true, excluded: ["Anonymous"]));

    public static TheoryData<string> ToolNames => new()
    {
        GetContentItemAccessRolesTool.TheName,
        SetContentItemAccessRolesTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Arrange
        UsePublished(["Editor"]);
        var services = Build(signedIn: false);

        // Act
        var result = await InvokeAsync(toolName, services, toolName == GetContentItemAccessRolesTool.TheName ? new() { ["contentItemId"] = "minutes" } : new() { ["contentItemId"] = "minutes", ["roles"] = new[] { "Editor" } });

        // Assert
        Assert.Equal(ContentAccessControlAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_actions);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutEditContentOnTheItem(string toolName)
    {
        // Arrange
        UsePublished(["Editor"]);
        var services = Build(authorize: (permission, _) => permission.Name != "EditContent");

        // Act
        var result = await InvokeAsync(toolName, services, toolName == GetContentItemAccessRolesTool.TheName ? new() { ["contentItemId"] = "minutes" } : new() { ["contentItemId"] = "minutes", ["roles"] = new[] { "Editor" } });

        // Assert
        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal(ContentAccessControlAIToolBase.NotAllowedMessage, result);
        Assert.Equal("EditContent", check.Permission);
        Assert.Same(_latest, check.Resource);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task GetContentItemAccessRoles_ReturnsTheRolesOfThePublishedItem()
    {
        // Arrange
        UsePublished(["Editor", "Board"]);
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(GetContentItemAccessRolesTool.TheName, services, new() { ["contentItemId"] = "minutes" }));

        // Assert
        var part = Assert.Single(json.GetProperty("parts").EnumerateArray());

        Assert.True(json.GetProperty("restricted").GetBoolean());
        Assert.Equal(new[] { "Board", "Editor" }, json.GetProperty("visibleToRoles").EnumerateArray().Select(role => role.GetString()));
        Assert.DoesNotContain(part.GetProperty("availableRoles").EnumerateArray(), role => role.GetString() == "Anonymous");
    }

    [Fact]
    public async Task GetContentItemAccessRoles_SaysWhenTheTypeDoesNotRestrictAccess()
    {
        // Arrange
        _definition = CreateDefinition(Part("RolePickerPart", restricted: false));
        UsePublished(["Editor"]);
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(GetContentItemAccessRolesTool.TheName, services, new() { ["contentItemId"] = "minutes" }));

        // Assert
        Assert.False(json.GetProperty("restrictable").GetBoolean());
    }

    [Fact]
    public async Task SetContentItemAccessRoles_PublishesTheRolesOfAPublishedItem()
    {
        // Arrange
        UsePublished([]);
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SetContentItemAccessRolesTool.TheName, services, new()
        {
            ["contentItemId"] = "minutes",
            ["roles"] = new[] { "editor", "Board" },
        }));

        // Assert
        Assert.True(json.GetProperty("published").GetBoolean());
        Assert.Equal(new[] { "update", "validate", "publish" }, _actions);
        Assert.Equal(new[] { "Editor", "Board" }, _draft.Get<RolePickerPart>(nameof(RolePickerPart)).RoleNames);
        Assert.Contains(services.GetRequiredService<FakeAuthorizationService>().Checks, check => check.Permission == "PublishContent");
    }

    [Fact]
    public async Task SetContentItemAccessRoles_NeverPublishesAPendingDraft()
    {
        // Arrange
        UsePublished(["Editor"]);
        _latest = Version("v2", published: false, ["Editor"]);
        var services = Build();

        // Act
        var refused = await InvokeAsync(SetContentItemAccessRolesTool.TheName, services, new() { ["contentItemId"] = "minutes", ["roles"] = new[] { "Board" } });
        var draft = AgentToolTestContext.ReadJson(await InvokeAsync(SetContentItemAccessRolesTool.TheName, services, new() { ["contentItemId"] = "minutes", ["roles"] = new[] { "Board" }, ["saveAsDraft"] = true }));

        // Assert
        Assert.Contains("unpublished changes", refused, StringComparison.Ordinal);
        Assert.True(draft.GetProperty("savedAsDraft").GetBoolean());
        Assert.Equal(new[] { "update", "validate", "saveDraft" }, _actions);
    }

    [Fact]
    public async Task SetContentItemAccessRoles_NeedsPublishContentToPublish()
    {
        // Arrange
        UsePublished([]);
        var services = Build(authorize: (permission, _) => permission.Name != "PublishContent");

        // Act
        var result = await InvokeAsync(SetContentItemAccessRolesTool.TheName, services, new() { ["contentItemId"] = "minutes", ["roles"] = new[] { "Board" } });

        // Assert
        Assert.Contains("permission to publish", result, StringComparison.Ordinal);
        Assert.Empty(_actions);
    }

    [Theory]
    [InlineData(new[] { "Nobody" }, "no role named 'Nobody'")]
    [InlineData(new[] { "Anonymous" }, "is excluded")]
    public async Task SetContentItemAccessRoles_RefusesARoleTheEditorWouldNotOffer(string[] roles, string expected)
    {
        // Arrange
        UsePublished([]);
        var services = Build();

        // Act
        var result = await InvokeAsync(SetContentItemAccessRolesTool.TheName, services, new() { ["contentItemId"] = "minutes", ["roles"] = roles });

        // Assert
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task SetContentItemAccessRoles_AppliesThePartSettings()
    {
        // Arrange
        _definition = CreateDefinition(Part("RolePickerPart", restricted: true, required: true, allowMultiple: false));
        UsePublished(["Editor"]);
        var services = Build();

        // Act
        var several = await InvokeAsync(SetContentItemAccessRolesTool.TheName, services, new() { ["contentItemId"] = "minutes", ["roles"] = new[] { "Editor", "Board" } });
        var none = await InvokeAsync(SetContentItemAccessRolesTool.TheName, services, new() { ["contentItemId"] = "minutes", ["roles"] = Array.Empty<string>() });

        // Assert
        Assert.Contains("allows only one role", several, StringComparison.Ordinal);
        Assert.Contains("requires a role", none, StringComparison.Ordinal);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task SetContentItemAccessRoles_NeedsAPartNameWhenTheTypeHasSeveralRestrictingParts()
    {
        // Arrange
        _definition = CreateDefinition(Part("BoardAccess", restricted: true, allowMultiple: true), Part("StaffAccess", restricted: true, allowMultiple: true));
        UsePublished([]);
        var services = Build();

        // Act
        var refused = await InvokeAsync(SetContentItemAccessRolesTool.TheName, services, new() { ["contentItemId"] = "minutes", ["roles"] = new[] { "Board" } });
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SetContentItemAccessRolesTool.TheName, services, new() { ["contentItemId"] = "minutes", ["roles"] = new[] { "Board" }, ["partName"] = "BoardAccess" }));

        // Assert
        Assert.Contains("BoardAccess, StaffAccess", refused, StringComparison.Ordinal);
        Assert.Equal("BoardAccess", json.GetProperty("partName").GetString());
        Assert.Equal(new[] { "Board" }, _draft.Get<RolePickerPart>("BoardAccess").RoleNames);
    }

    [Fact]
    public async Task SetContentItemAccessRoles_RefusesATypeThatDoesNotRestrictAccess()
    {
        // Arrange
        _definition = CreateDefinition();
        UsePublished([]);
        var services = Build();

        // Act
        var result = await InvokeAsync(SetContentItemAccessRolesTool.TheName, services, new() { ["contentItemId"] = "minutes", ["roles"] = new[] { "Board" } });

        // Assert
        Assert.Contains("does not restrict access by role", result, StringComparison.Ordinal);
        Assert.Empty(_actions);
    }

    private void UsePublished(string[] roles)
    {
        _published = Version("v1", published: true, roles);
        _latest = _published;
    }

    private static ContentItem Version(string versionId, bool published, string[] roles)
    {
        var item = new ContentItem
        {
            ContentItemId = "minutes",
            ContentItemVersionId = versionId,
            ContentType = "Minutes",
            DisplayText = "Board minutes March",
            Published = published,
            Latest = true,
        };

        item.Alter<RolePickerPart>(part => part.RoleNames = roles);

        return item;
    }

    private static ContentTypePartDefinition Part(string name, bool restricted, bool required = false, bool allowMultiple = false, string[] excluded = null)
        => new(name, new ContentPartDefinition(nameof(RolePickerPart), [], new JsonObject { ["ContentPartSettings"] = new JsonObject { ["Reusable"] = true } }), new JsonObject
        {
            [nameof(RolePickerPartContentAccessControlSettings)] = new JsonObject { ["IsContentRestricted"] = restricted },
            [nameof(RolePickerPartSettings)] = new JsonObject
            {
                ["Required"] = required,
                ["AllowSelectMultiple"] = allowMultiple,
                ["ExcludedRoles"] = new JsonArray((excluded ?? []).Select(role => (JsonNode)role).ToArray()),
            },
        });

    private static ContentTypeDefinition CreateDefinition(params ContentTypePartDefinition[] parts)
        => new("Minutes", "Minutes", parts, new JsonObject());

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("minutes", It.IsAny<VersionOptions>()))
            .ReturnsAsync((string _, VersionOptions options) =>
            {
                if (options.IsPublished)
                {
                    return _published;
                }

                if (options.IsDraftRequired)
                {
                    _draft = Version("v3", published: false, _latest.Get<RolePickerPart>(nameof(RolePickerPart)).RoleNames);

                    return _draft;
                }

                return _latest;
            });
        contentManager.Setup(manager => manager.UpdateAsync(It.IsAny<ContentItem>())).Callback(() => _actions.Add("update")).Returns(Task.CompletedTask);
        contentManager.Setup(manager => manager.ValidateAsync(It.IsAny<ContentItem>())).Callback(() => _actions.Add("validate")).ReturnsAsync(new ContentValidateResult());
        contentManager.Setup(manager => manager.PublishAsync(It.IsAny<ContentItem>())).Callback(() => _actions.Add("publish")).ReturnsAsync(true);
        contentManager.Setup(manager => manager.SaveDraftAsync(It.IsAny<ContentItem>())).Callback(() => _actions.Add("saveDraft")).Returns(Task.CompletedTask);
        services.AddSingleton(contentManager.Object);

        var definitionManager = new Mock<IContentDefinitionManager>();
        definitionManager.Setup(manager => manager.GetTypeDefinitionAsync("Minutes")).ReturnsAsync(() => _definition);
        services.AddSingleton(definitionManager.Object);

        var roleManager = new Mock<RoleManager<IRole>>(Mock.Of<IRoleStore<IRole>>(), null, null, null, null);
        roleManager.SetupGet(manager => manager.Roles).Returns(new[] { "Administrator", "Anonymous", "Board", "Editor" }.Select(name => (IRole)new TestRole(name)).AsQueryable());
        services.AddSingleton(roleManager.Object);

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
        => AgentToolTestContext.InvokeAsync(toolName == GetContentItemAccessRolesTool.TheName ? new GetContentItemAccessRolesTool() : new SetContentItemAccessRolesTool(), services, arguments);

    private sealed class TestRole : IRole
    {
        public TestRole(string name)
        {
            RoleName = name;
        }

        public string RoleName { get; }

        public string RoleDescription => null;
    }
}
