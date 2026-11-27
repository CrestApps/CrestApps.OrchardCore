using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;
using OrchardCore.Json;
using OrchardCore.Security;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Agent.Contents;

/// <summary>
/// Round 3 (High 1): the content tool loaded the item it was asked to update as a draft, which is itself a write, before
/// it checked the caller, and it loaded every nested item by the id the payload named the same way and merged the payload
/// into it. An author could leave a draft on someone else's item, or change it by naming it as a nested item.
/// </summary>
public sealed class CreateOrUpdateContentToolAuthorizationTests
{
    [Fact]
    public async Task InvokeAsync_ByAnAuthor_OnSomeoneElsesItem_IsRefused_BeforeADraftIsMade()
    {
        // Arrange
        var theirs = new ContentItem { ContentItemId = "theirs", ContentType = "Article", Owner = "someone-else" };
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("theirs", It.IsAny<VersionOptions>())).ReturnsAsync(theirs);
        using var services = BuildServices(contentManager.Object, Caller("author"));

        // Act
        var result = await InvokeAsync(services, """{ "ContentItemId": "theirs", "ContentType": "Article" }""");

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        contentManager.Verify(manager => manager.GetAsync("theirs", It.Is<VersionOptions>(options => options.IsDraftRequired)), Times.Never);
        contentManager.Verify(manager => manager.UpdateAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    [Fact]
    public async Task PrepareAsync_ANestedItemNamingAStoredItem_IsBuiltNew_AndTheStoredItemIsNotTouched()
    {
        // Arrange
        var page = new ContentTypeDefinition("Page", "Page", [new ContentTypePartDefinition("BagPart", new ContentPartDefinition("BagPart", [], []), [])], []);
        var emailAddress = new ContentTypeDefinition("EmailAddress", "EmailAddress");
        page.Parts.Single().ContentTypeDefinition = page;
        var victim = new ContentItem { ContentItemId = "victim", ContentType = "EmailAddress", Owner = "someone-else" };
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("victim", It.IsAny<VersionOptions>())).ReturnsAsync(victim);
        contentManager.Setup(manager => manager.NewAsync(It.IsAny<string>())).ReturnsAsync((string type) => new ContentItem { ContentType = type });
        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.GetTypeDefinitionAsync("EmailAddress")).ReturnsAsync(emailAddress);
        var service = new ContentItemPreparationService(contentManager.Object, definitions.Object, [], Options.Create(new DocumentJsonSerializerOptions()));
        var payload = JsonNode.Parse("""
            {
              "ContentType": "Page",
              "BagPart": { "ContentItems": [ { "ContentItemId": "victim", "ContentType": "EmailAddress", "EmailPart": { "Address": "attacker@example.com" } } ] }
            }
            """).AsObject();

        // Act
        await service.PrepareAsync(page, payload, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        contentManager.Verify(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()), Times.Never);
        contentManager.Verify(manager => manager.NewAsync("EmailAddress"), Times.Once);
        Assert.DoesNotContain("attacker@example.com", JsonSerializer.Serialize(victim, new DocumentJsonSerializerOptions().SerializerOptions), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_ANewItem_IsOwnedByTheCallerTheAuthorizationCheckRead()
    {
        // Arrange (round 3, Low): the hub publishes its caller through the user accessor, which the authorization check
        // reads first, while the owner was read from the request's user, which a hub invocation may not have.
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.NewAsync("Article")).ReturnsAsync(() => new ContentItem { ContentType = "Article" });
        contentManager.Setup(manager => manager.ValidateAsync(It.IsAny<ContentItem>())).ReturnsAsync(new ContentValidateResult());
        ContentItem created = null;
        contentManager
            .Setup(manager => manager.CreateAsync(It.IsAny<ContentItem>(), It.IsAny<VersionOptions>()))
            .Callback<ContentItem, VersionOptions>((item, _) => created = item)
            .ReturnsAsync(true);
        using var services = BuildServices(contentManager.Object, Caller("hub-user"));

        // Act
        var result = await InvokeAsync(services, """{ "ContentType": "Article" }""");

        // Assert
        Assert.NotEqual(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal("hub-user", created?.Owner);
    }

    private static ClaimsPrincipal Caller(string userId)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"));

    private static async Task<string> InvokeAsync(IServiceProvider services, string contentItem)
    {
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["contentItem"] = contentItem,
            ["isDraft"] = false,
        })
        {
            Services = services,
        };

        return (await new CreateOrUpdateContentTool().InvokeAsync(arguments, TestContext.Current.CancellationToken))?.ToString();
    }

    // A hub invocation: the caller is published through the user accessor and there is no request.
    private static ServiceProvider BuildServices(IContentManager contentManager, ClaimsPrincipal caller)
    {
        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.GetTypeDefinitionAsync("Article")).ReturnsAsync(new ContentTypeDefinition("Article", "Article"));

        return new ServiceCollection()
            .AddOptions()
            .AddAgentToolPermissions()
            .AddSingleton<IAuthorizationService>(new OwnContentAuthorizationService())
            .AddSingleton<IUserAccessor>(new FixedUserAccessor(caller))
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor())
            .AddSingleton<ILogger<CreateOrUpdateContentTool>>(NullLogger<CreateOrUpdateContentTool>.Instance)
            .AddSingleton<IContentItemPayloadAssistanceService>(new ValidPayloadAssistanceService())
            .AddSingleton(contentManager)
            .AddSingleton(definitions.Object)
            .AddSingleton(Mock.Of<YSession>())
            .AddScoped<ContentItemPreparationService>()
            .BuildServiceProvider();
    }

    // Grants the tool's own permission to anyone signed in, and editing and publishing only on the caller's own items, as
    // Orchard Core does for an author with EditOwnContent and PublishOwnContent.
    private sealed class OwnContentAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            var permission = requirements.OfType<PermissionRequirement>().Single().Permission;
            var isContentPermission = permission.Name == CommonPermissions.EditContent.Name || permission.Name == CommonPermissions.PublishContent.Name;
            var granted = !isContentPermission ||
                (resource is ContentItem item && (string.IsNullOrEmpty(item.Owner) || item.Owner == user.FindFirstValue(ClaimTypes.NameIdentifier)));

            return Task.FromResult(granted ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }

    private sealed class ValidPayloadAssistanceService : IContentItemPayloadAssistanceService
    {
        public ValueTask<ContentItemPayloadValidationResult> ValidateAsync(ContentTypeDefinition contentDefinition, JsonNode inputNode, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new ContentItemPayloadValidationResult(true, [], [], null));

        public ValueTask<string> GetGuidanceAsync(ContentTypeDefinition contentDefinition, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(string.Empty);
    }

    private sealed class FixedUserAccessor : IUserAccessor
    {
        public FixedUserAccessor(ClaimsPrincipal user)
            => User = user;

        public ClaimsPrincipal User { get; set; }
    }
}
