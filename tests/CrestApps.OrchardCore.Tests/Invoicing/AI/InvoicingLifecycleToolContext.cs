using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// Runs the invoice life-cycle AI tools on the invoicing harness as a chat does: the caller comes from
/// <see cref="IUserAccessor"/> only (there is no HTTP request), the permissions are those the test grants, and the preview
/// tokens and idempotency keys live in one distributed cache for the whole test, so a preview and the tool that acts can
/// run in different chat messages.
/// </summary>
internal sealed class InvoicingLifecycleToolContext
{
    /// <summary>
    /// The user name of the default caller.
    /// </summary>
    public const string CallerName = "pat";

    /// <summary>
    /// Gets the distributed cache the preview tokens and idempotency keys are kept in.
    /// </summary>
    public IDistributedCache Cache { get; } = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    /// <summary>
    /// Invokes a tool in its own scope (one chat message) and returns its answer.
    /// </summary>
    /// <param name="harness">The invoicing harness.</param>
    /// <param name="tool">The tool.</param>
    /// <param name="values">The arguments.</param>
    /// <param name="allow">Which permissions the caller holds; all of them when not given.</param>
    /// <param name="signedIn">Whether somebody is signed in.</param>
    /// <param name="userId">The caller's user id.</param>
    /// <param name="userName">The caller's user name.</param>
    /// <param name="services">More services the invocation sees, such as a clock.</param>
    public Task<string> InvokeAsync(
        InvoicingTestHarness harness,
        AIFunction tool,
        Dictionary<string, object> values,
        Func<Permission, bool> allow = null,
        bool signedIn = true,
        string userId = "user-1",
        string userName = CallerName,
        IReadOnlyDictionary<Type, object> services = null)
        => harness.InScopeAsync(scope => InvokeInScopeAsync(scope, new InvoicingToolScope(), tool, values, allow, signedIn, userId, userName, services));

    /// <summary>
    /// Invokes several tools in one scope, as the tool calls of one chat message, and returns their answers.
    /// </summary>
    /// <param name="harness">The invoicing harness.</param>
    /// <param name="before">Work done in the message's scope before the tools run, such as an unsaved change.</param>
    /// <param name="calls">The tools and their arguments, in order.</param>
    public Task<List<string>> InvokeInOneMessageAsync(
        InvoicingTestHarness harness,
        Func<IServiceProvider, Task> before,
        params (AIFunction Tool, Dictionary<string, object> Values)[] calls)
        => harness.InScopeAsync(async scope =>
        {
            var toolScope = new InvoicingToolScope();
            var answers = new List<string>();

            if (before is not null)
            {
                await before(scope);
            }

            foreach (var (tool, values) in calls)
            {
                answers.Add(await InvokeInScopeAsync(scope, toolScope, tool, values, null, true, "user-1", CallerName, null));
            }

            return answers;
        });

    /// <summary>
    /// Grants every permission but one.
    /// </summary>
    /// <param name="denied">The permission the caller lacks.</param>
    public static Func<Permission, bool> AllBut(Permission denied)
        => permission => !string.Equals(permission.Name, denied.Name, StringComparison.Ordinal);

    /// <summary>
    /// Grants only the given permissions.
    /// </summary>
    /// <param name="granted">The permissions the caller holds.</param>
    public static Func<Permission, bool> Only(params Permission[] granted)
        => permission => granted.Any(item => string.Equals(item.Name, permission.Name, StringComparison.Ordinal));

    /// <summary>
    /// A clock that reads a later time than the harness's, for a token that has expired.
    /// </summary>
    /// <param name="later">How much later.</param>
    public static IReadOnlyDictionary<Type, object> Later(TimeSpan later)
        => new Dictionary<Type, object> { [typeof(IClock)] = Mock.Of<IClock>(clock => clock.UtcNow == InvoicingTestHarness.Now.Add(later)) };

    /// <summary>
    /// Issues a draft invoice of two hours of consulting at 50 USD, billed to Fabrikam, so it owes 100 USD on the ledger.
    /// </summary>
    /// <param name="harness">The invoicing harness.</param>
    /// <param name="configure">Changes made to the draft before it is issued.</param>
    public static async Task<Invoice> IssueInvoiceAsync(InvoicingTestHarness harness, Action<Invoice> configure = null)
    {
        var draft = await harness.CreateDraftAsync(configure);
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }

    /// <summary>
    /// Reads a tool's JSON answer, failing with the answer when it is a sentence instead.
    /// </summary>
    /// <param name="answer">The answer.</param>
    public static JsonElement Parse(string answer)
    {
        Assert.False(string.IsNullOrEmpty(answer));
        Assert.True(answer.StartsWith('{'), answer);

        using var document = JsonDocument.Parse(answer);

        return document.RootElement.Clone();
    }

    private async Task<string> InvokeInScopeAsync(
        IServiceProvider scope,
        InvoicingToolScope toolScope,
        AIFunction tool,
        Dictionary<string, object> values,
        Func<Permission, bool> allow,
        bool signedIn,
        string userId,
        string userName,
        IReadOnlyDictionary<Type, object> services)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
            {
                var permission = requirements.OfType<PermissionRequirement>().Select(requirement => requirement.Permission).FirstOrDefault();

                return user?.Identity?.IsAuthenticated == true && permission is not null && (allow?.Invoke(permission) ?? true)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed();
            });

        var caller = signedIn
            ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, userName)], "test"))
            : new ClaimsPrincipal(new ClaimsIdentity());

        var overrides = new Dictionary<Type, object>
        {
            [typeof(IAuthorizationService)] = authorization.Object,
            [typeof(IUserAccessor)] = new CallerAccessor(caller),
            [typeof(IDistributedCache)] = Cache,
            [typeof(InvoicingToolScope)] = toolScope,
        };

        foreach (var (type, service) in services ?? new Dictionary<Type, object>())
        {
            overrides[type] = service;
        }

        var result = await tool.InvokeAsync(new AIFunctionArguments(values) { Services = new Overlay(scope, overrides) }, TestContext.Current.CancellationToken);

        return result?.ToString();
    }

    private sealed class CallerAccessor(ClaimsPrincipal user) : IUserAccessor
    {
        public ClaimsPrincipal User { get; set; } = user;
    }

    private sealed class Overlay(IServiceProvider inner, IReadOnlyDictionary<Type, object> overrides) : IServiceProvider
    {
        public object GetService(Type serviceType)
            => overrides.TryGetValue(serviceType, out var value) ? value : inner.GetService(serviceType);
    }
}
