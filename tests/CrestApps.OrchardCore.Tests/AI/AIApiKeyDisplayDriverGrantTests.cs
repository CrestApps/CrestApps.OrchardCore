using System.Security.Claims;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.AI.Drivers;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Tests.AI;

/// <summary>
/// B2 and B3: the API key editor grants a key only what its editor may grant. A browser key lets the public chat with
/// its profiles, so its issuer must be able to chat with each one; and an edit that widens a server key (more areas,
/// turned back on, a later expiry) is held to the editor's authority over every role the key acts with. These tests post
/// the editor through <see cref="AIApiKeyDisplayDriver.UpdateAsync"/>.
/// </summary>
public sealed class AIApiKeyDisplayDriverGrantTests
{
    private static readonly AIProfile _openProfile = new() { ItemId = "open", Name = "open", DisplayText = "Open" };
    private static readonly AIProfile _restrictedProfile = new() { ItemId = "restricted", Name = "restricted", DisplayText = "Restricted" };

    [Fact]
    public async Task BrowserKey_AProfileTheIssuerCanQuery_IsAccepted()
    {
        var updater = await PostBrowserAsync(new AIApiKey(), isNew: true, ["open"]);

        Assert.True(updater.ModelState.IsValid);
    }

    [Fact]
    public async Task BrowserKey_AProfileTheIssuerCannotQuery_IsAModelError()
    {
        var updater = await PostBrowserAsync(new AIApiKey(), isNew: true, ["open", "restricted"]);

        Assert.False(updater.ModelState.IsValid);
        Assert.Contains(updater.ModelState, entry => entry.Key.EndsWith(nameof(AIApiKeyViewModel.AllowedProfileIds), StringComparison.Ordinal));
    }

    [Fact]
    public async Task BrowserKey_AddingARestrictedProfileOnEdit_IsAModelError()
    {
        var updater = await PostBrowserAsync(ExistingBrowserKey(["open"]), isNew: false, ["open", "restricted"]);

        Assert.False(updater.ModelState.IsValid);
    }

    [Fact]
    public async Task BrowserKey_AnUnknownProfile_IsAModelError()
    {
        var updater = await PostBrowserAsync(new AIApiKey(), isNew: true, ["missing"]);

        Assert.False(updater.ModelState.IsValid);
    }

    [Fact]
    public async Task BrowserKey_KeepingARestrictedProfileUnchanged_IsAccepted()
    {
        var updater = await PostBrowserAsync(ExistingBrowserKey(["restricted"]), isNew: false, ["restricted"]);

        Assert.True(updater.ModelState.IsValid);
    }

    [Fact]
    public async Task BrowserKey_TurningBackOnAKeyWithARestrictedProfile_IsAModelError()
    {
        var key = ExistingBrowserKey(["restricted"]);
        key.Enabled = false;

        var updater = await PostBrowserAsync(key, isNew: false, ["restricted"]);

        Assert.False(updater.ModelState.IsValid);
    }

    [Fact]
    public async Task ServerKey_AddingAScopeToAKeyWithARoleTheEditorDoesNotHold_IsAModelError()
    {
        var updater = await PostServerAsync(ExistingServerKey(), [AIApiKeys.Scopes.Chat, AIApiKeys.Scopes.All.First(scope => scope != AIApiKeys.Scopes.Chat)], enabled: true);

        Assert.False(updater.ModelState.IsValid);
        Assert.Contains(updater.ModelState, entry => entry.Key.EndsWith(nameof(AIApiKeyViewModel.Roles), StringComparison.Ordinal));
    }

    [Fact]
    public async Task ServerKey_TurningBackOnAKeyWithARoleTheEditorDoesNotHold_IsAModelError()
    {
        var key = ExistingServerKey();
        key.Enabled = false;

        var updater = await PostServerAsync(key, [AIApiKeys.Scopes.Chat], enabled: true);

        Assert.False(updater.ModelState.IsValid);
    }

    [Fact]
    public async Task ServerKey_KeepingAPrivilegedKeyUnchanged_IsAccepted()
    {
        var updater = await PostServerAsync(ExistingServerKey(), [AIApiKeys.Scopes.Chat], enabled: true);

        Assert.True(updater.ModelState.IsValid);
    }

    [Fact]
    public async Task ServerKey_NarrowingAPrivilegedKey_IsAccepted()
    {
        var updater = await PostServerAsync(ExistingServerKey(), [AIApiKeys.Scopes.Chat], enabled: false);

        Assert.True(updater.ModelState.IsValid);
    }

    private static AIApiKey ExistingBrowserKey(List<string> profiles) => new()
    {
        Kind = AIApiKeyKind.Browser,
        Enabled = true,
        AllowedOrigins = ["https://www.example.com"],
        AllowedProfileIds = profiles,
        Scopes = [AIApiKeys.Scopes.Chat],
    };

    // A chat-only key a more powerful user gave the Administrator role.
    private static AIApiKey ExistingServerKey() => new()
    {
        Kind = AIApiKeyKind.Server,
        Enabled = true,
        Scopes = [AIApiKeys.Scopes.Chat],
        Roles = ["Administrator"],
    };

    private static Task<PostedUpdater> PostBrowserAsync(AIApiKey key, bool isNew, string[] profiles)
        => PostAsync(key, isNew, new AIApiKeyViewModel
        {
            Name = "Browser key",
            Kind = AIApiKeyKind.Browser,
            Enabled = true,
            AllowedOrigins = "https://www.example.com",
            AllowedProfileIds = profiles,
        });

    private static Task<PostedUpdater> PostServerAsync(AIApiKey key, string[] scopes, bool enabled)
        => PostAsync(key, isNew: false, new AIApiKeyViewModel
        {
            Name = "Server key",
            Kind = AIApiKeyKind.Server,
            Enabled = enabled,
            Scopes = scopes,
            Roles = ["Administrator"],
        });

    // The editor holds only the Editor role and the key-management permission: not ManageRoles, and they may chat only
    // with the "open" profile.
    private static async Task<PostedUpdater> PostAsync(AIApiKey key, bool isNew, AIApiKeyViewModel posted)
    {
        var editor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "editor-1"), new Claim(ClaimTypes.Role, "Editor")],
            "Test",
            ClaimTypes.Name,
            ClaimTypes.Role));

        var roleService = new Mock<IRoleService>();
        roleService.Setup(service => service.GetRolesAsync()).ReturnsAsync([new Role { RoleName = "Administrator" }, new Role { RoleName = "Editor" }]);

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement =>
                    requirement.Permission.Name == AIPermissions.QueryAnyAIProfile.Name && resource is AIProfile { ItemId: "open" })
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        var profileManager = new Mock<IAIProfileManager>();
        profileManager
            .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(new[] { _openProfile, _restrictedProfile }.FirstOrDefault(profile => profile.ItemId == id)));

        var driver = new AIApiKeyDisplayDriver(
            roleService.Object,
            profileManager.Object,
            Mock.Of<ILocalClock>(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = editor } },
            authorization.Object,
            new PassThroughStringLocalizer<AIApiKeyDisplayDriver>());

        var updater = new PostedUpdater(posted);
        var context = new UpdateEditorContext(Mock.Of<IShape>(), string.Empty, isNew, string.Empty, Mock.Of<IShapeFactory>(), null, updater);

        await driver.UpdateAsync(key, context);

        return updater;
    }

    private sealed class PostedUpdater : IUpdateModel
    {
        private readonly AIApiKeyViewModel _posted;

        public PostedUpdater(AIApiKeyViewModel posted)
        {
            _posted = posted;
        }

        public ModelStateDictionary ModelState { get; } = new();

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model) where TModel : class => TryUpdateModelAsync(model, string.Empty);

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix) where TModel : class
        {
            if (model is AIApiKeyViewModel target)
            {
                foreach (var property in typeof(AIApiKeyViewModel).GetProperties().Where(property => property.CanRead && property.CanWrite))
                {
                    property.SetValue(target, property.GetValue(_posted));
                }
            }

            return Task.FromResult(true);
        }

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix, params System.Linq.Expressions.Expression<Func<TModel, object>>[] includeExpressions) where TModel : class
            => TryUpdateModelAsync(model, prefix);

        public bool TryValidateModel(object model) => true;

        public bool TryValidateModel(object model, string prefix) => true;
    }
}
