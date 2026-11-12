using System.Reflection;
using System.Security.Claims;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.AI.Endpoints.Api;
using CrestApps.OrchardCore.AI.Endpoints.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Api;

/// <summary>
/// SEC-A20 a: the completion API's template-prompt path started a session on the parent profile without checking that
/// the caller may use the parent profile.
/// </summary>
public sealed class ApiAICompletionTemplateAuthorizationTests
{
    [Fact]
    public async Task TemplatePrompt_WhenTheCallerMayNotUseTheParent_IsForbiddenAndStartsNoSession()
    {
        var template = new AIProfile { ItemId = "template", Type = AIProfileType.TemplatePrompt };
        var parent = new AIProfile { ItemId = "parent", Type = AIProfileType.Chat };

        var profiles = new Mock<INamedCatalogManager<AIProfile>>();
        profiles.Setup(p => p.FindByIdAsync("template", It.IsAny<CancellationToken>())).ReturnsAsync(template);
        profiles.Setup(p => p.FindByIdAsync("parent", It.IsAny<CancellationToken>())).ReturnsAsync(parent);

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object resource, IEnumerable<IAuthorizationRequirement> _) =>
                ReferenceEquals(resource, parent) ? AuthorizationResult.Failed() : AuthorizationResult.Success());

        var sessions = new Mock<IAIChatSessionManager>(MockBehavior.Strict);
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], AIApiKeys.Scheme)),
        });

        var method = typeof(ApiAICompletionEndpoint)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(typeof(ApiAICompletionTemplateAuthorizationTests));

        var arguments = method.GetParameters().Select<ParameterInfo, object>(parameter => parameter.ParameterType switch
        {
            var type when type == typeof(IAuthorizationService) => authorization.Object,
            var type when type == typeof(INamedCatalogManager<AIProfile>) => profiles.Object,
            var type when type == typeof(IAIChatSessionManager) => sessions.Object,
            var type when type == typeof(IHttpContextAccessor) => accessor.Object,
            var type when type == typeof(AICompletionRequest) => new AICompletionRequest { ProfileId = "template", SessionProfileId = "parent" },
            var type when type.Name.StartsWith("ILogger", StringComparison.Ordinal) => NullLogger<ApiAICompletionTemplateAuthorizationTests>.Instance,
            _ => null,
        }).ToArray();

        var result = await (Task<IResult>)method.Invoke(null, arguments);

        Assert.IsType<ForbidHttpResult>(result);
    }
}
