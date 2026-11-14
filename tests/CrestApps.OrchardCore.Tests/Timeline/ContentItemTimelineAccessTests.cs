using System.Reflection;
using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;
using OrchardCore.Security;
using OrchardCore.Security.AuthorizationHandlers;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Timeline;

/// <summary>
/// A content item's timeline (an account, a contact, a lead), through Orchard's own content permission handlers: staff
/// are the people who may edit every item of the type, so an owner who may edit only their own items doesn't read the
/// internal entries; public entries exist only on a type a module opted in; and a visitor who isn't signed in reads
/// nothing, even on a site where anyone may view content.
/// </summary>
public sealed class ContentItemTimelineAccessTests
{
    private const string OwnerId = "owner";

    private static readonly RecordRef _account = new(TimelineConstants.ContentItemKind, "account-1");

    [Fact]
    public async Task InternalEntries_AreForThoseWhoMayEditEveryItemOfTheType_NotForAnOwnerWhoMayEditOnlyTheirs()
    {
        // Arrange
        var access = Create();

        // Act
        var viewer = await access.CanViewAsync(User("viewer", CommonPermissions.ViewContent), _account, TimelineVisibility.Internal, TestContext.Current.CancellationToken);
        var owner = await access.CanViewAsync(User(OwnerId, CommonPermissions.ViewContent, CommonPermissions.EditOwnContent), _account, TimelineVisibility.Internal, TestContext.Current.CancellationToken);
        var ownerAdds = await access.CanAddAsync(User(OwnerId, CommonPermissions.ViewContent, CommonPermissions.EditOwnContent), _account, TimelineConstants.Kinds.Note, TimelineVisibility.Internal, TestContext.Current.CancellationToken);
        var editor = await access.CanViewAsync(User("editor", CommonPermissions.EditContent), _account, TimelineVisibility.Internal, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(viewer);
        Assert.False(owner);
        Assert.False(ownerAdds);
        Assert.True(editor);
    }

    [Fact]
    public async Task PublicEntries_ExistOnlyOnATypeThatOptedIn()
    {
        // Arrange
        var closed = Create();
        var open = Create(publicTypes: "Account");
        var editor = User("editor", CommonPermissions.EditContent);
        var viewer = User("viewer", CommonPermissions.ViewContent);

        // Act
        var viewerReadsClosed = await closed.CanViewAsync(viewer, _account, TimelineVisibility.Public, TestContext.Current.CancellationToken);
        var editorCommentsClosed = await closed.CanAddAsync(editor, _account, TimelineConstants.Kinds.Comment, TimelineVisibility.Public, TestContext.Current.CancellationToken);
        var viewerReadsOpen = await open.CanViewAsync(viewer, _account, TimelineVisibility.Public, TestContext.Current.CancellationToken);
        var viewerCommentsOpen = await open.CanAddAsync(viewer, _account, TimelineConstants.Kinds.Comment, TimelineVisibility.Public, TestContext.Current.CancellationToken);
        var editorCommentsOpen = await open.CanAddAsync(editor, _account, TimelineConstants.Kinds.Comment, TimelineVisibility.Public, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(viewerReadsClosed);
        Assert.False(editorCommentsClosed);
        Assert.True(viewerReadsOpen);
        Assert.False(viewerCommentsOpen);
        Assert.True(editorCommentsOpen);
    }

    [Fact]
    public async Task AVisitorWhoIsNotSignedIn_ReadsNothing_EvenWhereAnyoneMayViewContent()
    {
        // Arrange
        var access = Create(publicTypes: "Account");
        var visitor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(Permission.ClaimType, CommonPermissions.ViewContent.Name)]));

        // Act
        var publicEntries = await access.CanViewAsync(visitor, _account, TimelineVisibility.Public, TestContext.Current.CancellationToken);
        var internalEntries = await access.CanViewAsync(visitor, _account, TimelineVisibility.Internal, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(publicEntries);
        Assert.False(internalEntries);
    }

    [Fact]
    public async Task AnItemAPerItemHandlerDenies_StaysClosed_ToSomeoneWhoMayEditTheType()
    {
        // Arrange: content access control, a team rule or a contained-item rule refuses this one item.
        var access = Create(denyItem: "account-1");
        var editor = User("editor", CommonPermissions.EditContent);

        // Act
        var reads = await access.CanViewAsync(editor, _account, TimelineVisibility.Internal, TestContext.Current.CancellationToken);
        var adds = await access.CanAddAsync(editor, _account, TimelineConstants.Kinds.Note, TimelineVisibility.Internal, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(reads);
        Assert.False(adds);
    }

    [Fact]
    public async Task AMissingItem_HasNoTimeline()
    {
        // Arrange
        var access = Create(publicTypes: "Account");

        // Act
        var sees = await access.CanViewAsync(User("editor", CommonPermissions.EditContent), new RecordRef(TimelineConstants.ContentItemKind, "missing"), TimelineVisibility.Internal, TestContext.Current.CancellationToken);
        var adds = await access.CanAddAsync(User("editor", CommonPermissions.EditContent), new RecordRef(TimelineConstants.ContentItemKind, "missing"), TimelineConstants.Kinds.Note, TimelineVisibility.Internal, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(sees);
        Assert.False(adds);
    }

    private static ContentItemTimelineAccess Create(params string[] publicTypes)
        => Create(denyItem: null, publicTypes);

    private static ContentItemTimelineAccess Create(string denyItem, params string[] publicTypes)
    {
        var contentManager = new Mock<IContentManager>();

        contentManager
            .Setup(manager => manager.GetAsync("account-1", It.IsAny<VersionOptions>()))
            .ReturnsAsync(new ContentItem { ContentItemId = "account-1", ContentType = "Account", Owner = OwnerId });

        var options = new TimelineOptions();

        foreach (var type in publicTypes)
        {
            options.PublicContentTypes.Add(type);
        }

        return new ContentItemTimelineAccess(contentManager.Object, RealAuthorization(denyItem), Options.Create(options));
    }

    // Orchard's authorization as a tenant runs it: the permission handler (claims, implied permissions) and the Contents
    // module's content type handler (per-type and own-item variations).
    private static IAuthorizationService RealAuthorization(string denyItem = null)
    {
        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.GetTypeDefinitionAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => new ContentTypeDefinition(name, name));

        var services = new ServiceCollection()
            .AddLogging()
            .AddOptions()
            .AddAuthorizationCore()
            .AddSingleton(definitions.Object)
            .AddSingleton<IPermissionGrantingService, DefaultPermissionGrantingService>()
            .AddScoped<IAuthorizationHandler, PermissionHandler>();

        var contentTypeHandler = Assembly.Load("OrchardCore.Contents").GetTypes().Single(type => type.Name == "ContentTypeAuthorizationHandler");
        services.AddScoped(typeof(IAuthorizationHandler), provider => ActivatorUtilities.CreateInstance(provider, contentTypeHandler));

        if (denyItem is not null)
        {
            services.AddScoped<IAuthorizationHandler>(_ => new DenyItemHandler(denyItem));
        }

        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IAuthorizationService>();
    }

    // A handler that decides per item, as content access control does: it fails every requirement on one item.
    private sealed class DenyItemHandler : IAuthorizationHandler
    {
        private readonly string _contentItemId;

        public DenyItemHandler(string contentItemId)
        {
            _contentItemId = contentItemId;
        }

        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            if (context.Resource is ContentItem { ContentItemId: { } id } && id == _contentItemId)
            {
                context.Fail();
            }

            return Task.CompletedTask;
        }
    }

    private static ClaimsPrincipal User(string userId, params Permission[] permissions)
        => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), .. permissions.Select(permission => new Claim(Permission.ClaimType, permission.Name))],
            "test"));
}
