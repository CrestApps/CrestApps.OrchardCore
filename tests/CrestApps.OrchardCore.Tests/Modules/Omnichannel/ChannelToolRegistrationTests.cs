using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell.Configuration;
using VoiceStartup = CrestApps.OrchardCore.Omnichannel.Voice.Startup;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// The tools only a call or an automated conversation can use are given only to a session that names them.
/// </summary>
/// <remarks>
/// They were registered without being selectable, to be attached by the call or the conversation itself. A tool
/// registered that way is a system tool, which every chat is given: the admin chat ended its conversation with two
/// goodbyes, and a call or an SMS conversation was offered the transfer while nobody could take it.
/// </remarks>
public sealed class ChannelToolRegistrationTests
{
    private static readonly string[] _channelTools =
    [
        EndCallTool.ToolName,
        PressKeysTool.ToolName,
        CollectSecureDigitsTool.ToolName,
        OmnichannelHandoffHelper.TransferToAgentToolName,
    ];

    [Fact]
    public async Task AChat_IsNotGivenTheToolsOfACallOrAHandOff()
    {
        // Arrange
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();

        // Act
        var entries = await registry.GetAllAsync(new AICompletionContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(entries, entry => _channelTools.Contains(entry.Name));
    }

    [Theory]
    [InlineData(EndCallTool.ToolName)]
    [InlineData(PressKeysTool.ToolName)]
    [InlineData(CollectSecureDigitsTool.ToolName)]
    [InlineData(OmnichannelHandoffHelper.TransferToAgentToolName)]
    public async Task ASessionThatNamesOne_IsGivenIt(string toolName)
    {
        // Arrange
        await using var services = BuildServices();
        await using var scope = services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();

        // Act
        var entries = await registry.GetAllAsync(new AICompletionContext { ToolNames = [toolName] }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(toolName, Assert.Single(entries, entry => _channelTools.Contains(entry.Name)).Name);
    }

    [Theory]
    [InlineData(EndCallTool.ToolName)]
    [InlineData(PressKeysTool.ToolName)]
    [InlineData(CollectSecureDigitsTool.ToolName)]
    [InlineData(OmnichannelHandoffHelper.TransferToAgentToolName)]
    public void TheTool_IsNotOfferedForAProfileToSelect(string toolName)
    {
        // Arrange
        using var services = BuildServices();

        // Act
        var definition = services.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools[toolName];

        // Assert
        Assert.False(definition.IsSystemTool);
        Assert.False(definition.IsSelectable());
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAIOrchestration();

        // The registry also offers agents, which these tests have none of.
        services.AddScoped(_ => Mock.Of<IAIProfileManager>());

        var shellConfiguration = new Mock<IShellConfiguration>();
        shellConfiguration
            .Setup(configuration => configuration.GetSection(It.IsAny<string>()))
            .Returns((string key) => new ConfigurationBuilder().Build().GetSection(key));

        new VoiceStartup(new PassThroughStringLocalizer<VoiceStartup>()).ConfigureServices(services);
        new AISubjectFlowStartup(new PassThroughStringLocalizer<AISubjectFlowStartup>(), shellConfiguration.Object).ConfigureServices(services);

        return services.BuildServiceProvider();
    }

    private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
