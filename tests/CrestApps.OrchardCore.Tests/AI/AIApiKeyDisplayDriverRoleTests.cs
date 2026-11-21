using System.Security.Claims;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.AI.Drivers;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Tests.AI;

/// <summary>
/// SEC-A9: the API key editor gives a key only the roles its editor holds. These tests post the editor through
/// <see cref="AIApiKeyDisplayDriver.UpdateAsync"/>, so the rule is checked where it is wired, not only in the helper.
/// </summary>
public sealed class AIApiKeyDisplayDriverRoleTests
{
    [Fact]
    public async Task UpdateAsync_ARoleTheEditorDoesNotHold_IsAModelError()
    {
        var updater = await PostAsync(roles: ["Administrator"], canManageRoles: false);

        Assert.False(updater.ModelState.IsValid);
        Assert.Contains(updater.ModelState, entry => entry.Key.EndsWith(nameof(AIApiKeyViewModel.Roles), StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpdateAsync_ARoleTheEditorHolds_IsAccepted()
    {
        var updater = await PostAsync(roles: ["Editor"], canManageRoles: false);

        Assert.True(updater.ModelState.IsValid);
    }

    [Fact]
    public async Task UpdateAsync_AnEditorWhoManagesRoles_MayGiveAnyRole()
    {
        var updater = await PostAsync(roles: ["Administrator"], canManageRoles: true);

        Assert.True(updater.ModelState.IsValid);
    }

    private static async Task<PostedUpdater> PostAsync(string[] roles, bool canManageRoles)
    {
        var editor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "editor-1"), new Claim(ClaimTypes.Role, "Editor")],
            "Test",
            ClaimTypes.Name,
            ClaimTypes.Role));

        var roleService = new Mock<IRoleService>();
        roleService.Setup(service => service.GetRolesAsync()).ReturnsAsync([new Role { RoleName = "Administrator" }, new Role { RoleName = "Editor" }, new Role { RoleName = "Author" }]);

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(canManageRoles ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var driver = new AIApiKeyDisplayDriver(
            roleService.Object,
            Mock.Of<IAIProfileManager>(),
            Mock.Of<ILocalClock>(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = editor } },
            authorization.Object,
            new PassThroughStringLocalizer<AIApiKeyDisplayDriver>());

        var updater = new PostedUpdater(new AIApiKeyViewModel
        {
            Name = "Server key",
            Kind = AIApiKeyKind.Server,
            Scopes = [AIApiKeys.Scopes.Chat],
            Roles = roles,
        });

        var context = new UpdateEditorContext(Mock.Of<IShape>(), string.Empty, true, string.Empty, Mock.Of<IShapeFactory>(), null, updater);

        await driver.UpdateAsync(new AIApiKey(), context);

        return updater;
    }

    private sealed class PostedUpdater : IUpdateModel
    {
        private readonly AIApiKeyViewModel _posted;

        public PostedUpdater(AIApiKeyViewModel posted)
        {
            _posted = posted;
        }

        public Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary ModelState { get; } = new();

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
