using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.Core.SourceAccess;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Checks;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Admin;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using ApprovalsAIToolsStartup = CrestApps.OrchardCore.Approvals.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Approvals.AI;

/// <summary>
/// Builds the services of an approvals AI tool call over <see cref="ApprovalsTestContext"/>: the real approval rules on
/// requests held in memory, the caller published through <see cref="IUserAccessor"/> (there is no HTTP request, so no
/// link is built), the permissions every signed-in person is granted, and a site clock five hours behind UTC.
/// </summary>
internal static class ApprovalsAIToolHarness
{
    /// <summary>
    /// The approval type whose records only some people may act on.
    /// </summary>
    public const string PayRun = "PayRun";

    /// <summary>
    /// The access scope of the pay runs Pat and Kim may act on.
    /// </summary>
    public const string GroupA = "groupA";

    /// <summary>
    /// The permission of the admin, which every approvals tool needs.
    /// </summary>
    public static Permission[] Admin => [AdminPermissions.AccessAdminPanel];

    /// <summary>
    /// The access to pay runs: Pat and Kim may act on group A's; anyone else on none. Other kinds are open to all.
    /// </summary>
    /// <returns>The source access service.</returns>
    public static SourceAccessService PayRunAccess()
        => new([new FixedSourceAccess(PayRun, new Dictionary<string, SourceAccessScope>
        {
            ["pat"] = SourceAccessScope.Of([GroupA]),
            ["kim"] = SourceAccessScope.Of([GroupA]),
        })]);

    /// <summary>
    /// Builds the services of a tool call.
    /// </summary>
    /// <param name="context">The approvals.</param>
    /// <param name="user">The caller, or <see langword="null"/> for nobody.</param>
    /// <param name="grants">The permissions granted to every signed-in person.</param>
    /// <returns>The services.</returns>
    public static ServiceProvider Services(ApprovalsTestContext context, ClaimsPrincipal user, params Permission[] grants)
    {
        var services = new ServiceCollection();
        var authorization = TestSecurity.Granting(grants);
        var clock = new Mock<IClock>();

        clock.SetupGet(entry => entry.UtcNow).Returns(() => context.Now);

        services.AddSingleton<IUserAccessor>(new FixedUserAccessor(user));
        services.AddSingleton(authorization);
        services.AddSingleton<IApprovalRequestManager>(context.Manager);
        services.AddSingleton<IApprovalRequestStore>(context.Store);
        services.AddSingleton<IApprovalService>(context.Service);
        services.AddSingleton<IApprovalTypeCatalog>(context.Types);
        services.AddSingleton(context.Access);
        services.AddSingleton(new ApprovalInbox(context.Store, context.Service, context.Access));
        services.AddSingleton(new ApprovalRequestAccess(context.Service, authorization, context.Access));
        services.AddSingleton<ILocalClock>(new FixedOffsetLocalClock(() => context.Now, TimeSpan.FromHours(-5)));
        services.AddSingleton(clock.Object);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Builds the services of a tool call by a signed-in person with the admin permission and any others given.
    /// </summary>
    /// <param name="context">The approvals.</param>
    /// <param name="userName">The caller's user name; the user id is the name plus "-id".</param>
    /// <param name="grants">The permissions besides the admin's.</param>
    /// <returns>The services.</returns>
    public static ServiceProvider As(ApprovalsTestContext context, string userName, params Permission[] grants)
        => Services(context, TestSecurity.User(userName), [.. Admin, .. grants]);

    /// <summary>
    /// Creates a person who is not signed in.
    /// </summary>
    /// <returns>The person.</returns>
    public static ClaimsPrincipal Anonymous()
        => new(new ClaimsIdentity());

    /// <summary>
    /// Calls a tool.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <param name="services">The services of the call.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The tool's answer.</returns>
    public static Task<string> InvokeAsync(AIFunction tool, IServiceProvider services, Dictionary<string, object> arguments)
        => AgentToolTestContext.InvokeAsync(tool, services, arguments);

    /// <summary>
    /// Reads the JSON a tool answered.
    /// </summary>
    /// <param name="result">The answer.</param>
    /// <returns>The JSON.</returns>
    public static JsonElement Json(string result)
        => AgentToolTestContext.ReadJson(result);

    /// <summary>
    /// Gets the tools the Approvals AI startup registers.
    /// </summary>
    /// <returns>The tools by name.</returns>
    public static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools()
    {
        var services = new ServiceCollection();

        new ApprovalsAIToolsStartup(new CrestApps.OrchardCore.Tests.Modules.Reports.Designer.PassThroughStringLocalizer<ApprovalsAIToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
    }

    private sealed class FixedUserAccessor : IUserAccessor
    {
        public FixedUserAccessor(ClaimsPrincipal user)
        {
            User = user;
        }

        public ClaimsPrincipal User { get; set; }
    }
}
