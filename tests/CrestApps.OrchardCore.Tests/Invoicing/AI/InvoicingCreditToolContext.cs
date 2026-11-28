using System.Security.Claims;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// Runs the credit note, estimate and recurring plan AI tools on the invoicing harness as a chat does: the caller comes
/// from <see cref="IUserAccessor"/>, the permissions are those the test grants, and the preview tokens live in one
/// distributed cache for the whole test, so a preview and the tool that acts can run in different chat messages.
/// </summary>
internal sealed class InvoicingCreditToolContext
{
    /// <summary>
    /// Gets the distributed cache the preview tokens are kept in.
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
    /// <param name="services">More services the invocation sees, such as a party directory.</param>
    public Task<string> InvokeAsync(
        InvoicingTestHarness harness,
        AIFunction tool,
        Dictionary<string, object> values,
        Func<Permission, bool> allow = null,
        bool signedIn = true,
        IReadOnlyDictionary<Type, object> services = null)
        => InMessageAsync(harness, invoke => invoke(tool, values), allow, signedIn, services);

    /// <summary>
    /// Runs several tool calls in one scope, as the calls of one chat message share one service scope and one database
    /// session.
    /// </summary>
    /// <typeparam name="T">What the calls come to.</typeparam>
    /// <param name="harness">The invoicing harness.</param>
    /// <param name="calls">The calls, made with the function it is given, which invokes a tool and returns its answer.</param>
    /// <param name="allow">Which permissions the caller holds; all of them when not given.</param>
    /// <param name="signedIn">Whether somebody is signed in.</param>
    /// <param name="services">More services the invocations see, such as a party directory.</param>
    public Task<T> InMessageAsync<T>(
        InvoicingTestHarness harness,
        Func<Func<AIFunction, Dictionary<string, object>, Task<string>>, Task<T>> calls,
        Func<Permission, bool> allow = null,
        bool signedIn = true,
        IReadOnlyDictionary<Type, object> services = null)
        => harness.InScopeAsync(scope =>
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
                ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1"), new Claim(ClaimTypes.Name, "pat")], "test"))
                : new ClaimsPrincipal(new ClaimsIdentity());

            var overrides = new Dictionary<Type, object>
            {
                [typeof(IAuthorizationService)] = authorization.Object,
                [typeof(IUserAccessor)] = new CallerAccessor(caller),
                [typeof(IDistributedCache)] = Cache,
                [typeof(InvoicingToolScope)] = new InvoicingToolScope(),
            };

            foreach (var (type, service) in services ?? new Dictionary<Type, object>())
            {
                overrides[type] = service;
            }

            var overlay = new Overlay(scope, overrides);

            async Task<string> InvokeToolAsync(AIFunction tool, Dictionary<string, object> values)
                => (await tool.InvokeAsync(new AIFunctionArguments(values) { Services = overlay }, TestContext.Current.CancellationToken))?.ToString();

            return calls(InvokeToolAsync);
        });

    /// <summary>
    /// Grants every permission but one.
    /// </summary>
    /// <param name="denied">The permission the caller lacks.</param>
    public static Func<Permission, bool> AllBut(Permission denied)
        => permission => !string.Equals(permission.Name, denied.Name, StringComparison.Ordinal);

    /// <summary>
    /// Issues a draft invoice of two hours of consulting at 50 USD, billed to Fabrikam, so it owes 100 USD on the ledger.
    /// </summary>
    /// <param name="harness">The invoicing harness.</param>
    public static async Task<Invoice> IssueInvoiceAsync(InvoicingTestHarness harness)
    {
        var draft = await harness.CreateDraftAsync();
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }

    /// <summary>
    /// Stores a draft estimate of two hours of consulting at 50 USD for Fabrikam.
    /// </summary>
    /// <param name="harness">The invoicing harness.</param>
    /// <param name="configure">Changes made to it before it is stored.</param>
    public static Task<Estimate> CreateEstimateAsync(InvoicingTestHarness harness, Action<Estimate> configure = null)
        => harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IEstimateManager>();
            var estimate = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

            estimate.Currency = "USD";
            estimate.BillTo = new DocumentParty { Contact = new ContactInfo { Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" } };
            estimate.Lines.Add(new DocumentLine { Id = "line-1", Name = "Consulting", Quantity = 2m, UnitPrice = 50m });

            configure?.Invoke(estimate);

            var validation = await manager.ValidateAsync(estimate, TestContext.Current.CancellationToken);

            Assert.True(validation.Succeeded, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));

            await manager.CreateAsync(estimate, TestContext.Current.CancellationToken);

            return estimate;
        });

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
