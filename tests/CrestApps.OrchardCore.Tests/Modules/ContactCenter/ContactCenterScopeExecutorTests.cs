using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Builders;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterScopeExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WithoutAmbientShellScope_ResolvesContextFromChildScope()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddScoped<TestScopeContext>()
            .BuildServiceProvider();
        var executor = new ContactCenterScopeExecutor(services);
        TestScopeContext resolvedContext = null;

        // Act
        await executor.ExecuteAsync<TestScopeContext>(context =>
        {
            resolvedContext = context;

            return Task.CompletedTask;
        });

        // Assert
        Assert.NotNull(resolvedContext);
        Assert.True(resolvedContext.IsDisposed);
    }

    [Fact]
    public void ScheduleAfterCommit_WithoutAmbientShellScope_ReturnsFalseWithoutExecuting()
    {
        // Arrange
        var executor = new ContactCenterScopeExecutor(new ServiceCollection().BuildServiceProvider());
        var executed = false;

        // Act
        var scheduled = executor.ScheduleAfterCommit(() =>
        {
            executed = true;

            return Task.CompletedTask;
        });

        // Assert
        Assert.False(scheduled);
        Assert.False(executed);
    }

    [Fact]
    public async Task ScheduleAfterCommit_WithAmbientShellScope_RegistersDeferredOperation()
    {
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var executor = new ContactCenterScopeExecutor(services);
        var shellScope = new ShellScope(new ShellContext
        {
            ServiceProvider = services,
        });
        var executed = false;
        var scheduled = false;

        // Act
        await shellScope.UsingServiceScopeAsync(_ =>
        {
            scheduled = executor.ScheduleAfterCommit(() =>
            {
                executed = true;

                return Task.CompletedTask;
            });

            Assert.False(executed);

            return Task.CompletedTask;
        });

        // Assert
        Assert.True(scheduled);
        Assert.False(executed);
    }

    private sealed class TestScopeContext : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
