using CrestApps.OrchardCore.AI.Agent.Tenants;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Agent.Tenants;

/// <summary>
/// The tenant tools of the AI Agent must work in the Default tenant only, like the Tenants admin, so a parent or child
/// tenant can never list or read other tenants through them.
/// </summary>
public sealed class TenantToolsDefaultShellTests
{
    public static TheoryData<string> ToolNames => [GetTenantTool.TheName, ListTenantTool.TheName];

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task InvokeAsync_OutsideTheDefaultTenant_RefusesAndNeverReadsTheTenants(string toolName)
    {
        // Arrange
        var shellHost = new Mock<IShellHost>(MockBehavior.Strict);
        var arguments = CreateArguments("parent-tenant", shellHost.Object);

        // Act
        var result = await CreateTool(toolName).InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("only be used in the default tenant", Assert.IsType<string>(result), StringComparison.Ordinal);
        shellHost.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ListTenants_InTheDefaultTenant_ListsTheTenants()
    {
        // Arrange
        var shellHost = new Mock<IShellHost>();
        shellHost.Setup(host => host.GetAllSettings()).Returns([new ShellSettings { Name = "tenant1" }]);
        var arguments = CreateArguments(ShellSettings.DefaultShellName, shellHost.Object);

        // Act
        var result = await new ListTenantTool().InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("tenant1", Assert.IsType<string>(result), StringComparison.Ordinal);
    }

    private static AIFunction CreateTool(string toolName)
    {
        return toolName == GetTenantTool.TheName
            ? new GetTenantTool()
            : new ListTenantTool();
    }

    private static AIFunctionArguments CreateArguments(string tenantName, IShellHost shellHost)
    {
        var services = new ServiceCollection()
            .AddSingleton(new ShellSettings { Name = tenantName })
            .AddSingleton(shellHost)
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .BuildServiceProvider();

        return new AIFunctionArguments(new Dictionary<string, object> { ["name"] = "tenant1" })
        {
            Services = services,
        };
    }
}
