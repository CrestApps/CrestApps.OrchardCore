using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Chat.Interactions.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Interactions;

public sealed class ProfileBuilderProfileMapperTests
{
    [Fact]
    public void CopyToProfile_CarriesInstructionsParametersCapabilitiesAndVoice()
    {
        // Arrange
        var interaction = new ChatInteraction
        {
            ChatDeploymentName = "gpt-chat",
            UtilityDeploymentName = "gpt-utility",
            ConversationDeploymentName = "gpt-realtime",
            RealtimeVoiceName = "alloy",
            SystemMessage = "You qualify leads by SMS.",
            Temperature = 0.4f,
            TopP = 0.8f,
            MaxTokens = 300,
            PastMessagesCount = 12,
            ToolNames = ["lookupVehicle"],
            AgentNames = ["scheduler"],
            McpConnectionIds = ["mcp-1"],
        };
        interaction.Put(new AIToolInstanceMetadata { ToolInstanceNames = ["crm-search"] });

        var session = new ProfileBuilderSession { InitialPrompt = "  Greet the lead by first name.  " };
        var profile = new AIProfile();

        // Act
        ProfileBuilderProfileMapper.CopyToProfile(interaction, session, profile);

        // Assert
        Assert.Equal("gpt-chat", profile.ChatDeploymentName);
        Assert.Equal("gpt-utility", profile.UtilityDeploymentName);

        Assert.True(profile.TryGet<AIProfileMetadata>(out var metadata));
        Assert.Equal("You qualify leads by SMS.", metadata.SystemMessage);
        Assert.Equal("Greet the lead by first name.", metadata.InitialPrompt);
        Assert.Equal(0.4f, metadata.Temperature);
        Assert.Equal(0.8f, metadata.TopP);
        Assert.Equal(300, metadata.MaxTokens);
        Assert.Equal(12, metadata.PastMessagesCount);

        Assert.True(profile.TryGet<FunctionInvocationMetadata>(out var tools));
        Assert.Equal(["lookupVehicle"], tools.Names);
        Assert.True(profile.TryGet<AgentInvocationMetadata>(out var agents));
        Assert.Equal(["scheduler"], agents.Names);
        Assert.True(profile.TryGet<AIToolInstanceMetadata>(out var instances));
        Assert.Equal(["crm-search"], instances.ToolInstanceNames);

        var mcp = JsonSerializer.SerializeToNode(profile.Properties[ProfileBuilderProfileMapper.McpMetadataName]);
        Assert.Equal("mcp-1", mcp["ConnectionIds"][0].GetValue<string>());

        Assert.True(profile.TryGetSettings<ChatModeProfileSettings>(out var chatMode));
        Assert.Equal(ChatMode.Conversation, chatMode.ChatMode);
        Assert.Equal("gpt-realtime", chatMode.ConversationDeploymentName);
        Assert.Equal("alloy", chatMode.VoiceName);
    }

    [Fact]
    public void CopyFromProfile_ThenCopyToProfile_RoundTrips()
    {
        // Arrange
        var source = new AIProfile { ItemId = "profile-1", ChatDeploymentName = "gpt-chat" };
        source.Put(new AIProfileMetadata { SystemMessage = "Be brief.", InitialPrompt = "Say hi.", Temperature = 0.1f });
        source.Put(new FunctionInvocationMetadata { Names = ["tool-a"] });
        source.Put(ProfileBuilderProfileMapper.A2AMetadataName, new { ConnectionIds = new[] { "a2a-1" } });

        var interaction = new ChatInteraction();
        var session = new ProfileBuilderSession();

        // Act
        ProfileBuilderProfileMapper.CopyFromProfile(source, interaction, session);

        var target = new AIProfile();
        ProfileBuilderProfileMapper.CopyToProfile(interaction, session, target);

        // Assert
        Assert.Equal("profile-1", session.ProfileId);
        Assert.Equal("Be brief.", interaction.SystemMessage);
        Assert.Equal(["tool-a"], interaction.ToolNames);
        Assert.Equal(["a2a-1"], interaction.A2AConnectionIds);
        Assert.Equal("Say hi.", session.InitialPrompt);

        Assert.True(target.TryGet<AIProfileMetadata>(out var metadata));
        Assert.Equal("Be brief.", metadata.SystemMessage);
        Assert.Equal("Say hi.", metadata.InitialPrompt);
        Assert.Equal(0.1f, metadata.Temperature);
        Assert.Equal("gpt-chat", target.ChatDeploymentName);
    }

    [Fact]
    public void CopyToProfile_WithoutConnections_ClearsThoseTheProfileHad()
    {
        // Arrange
        var profile = new AIProfile();
        profile.Put(ProfileBuilderProfileMapper.McpMetadataName, new { ConnectionIds = new[] { "old" }, ToolNames = new Dictionary<string, string[]>() });

        // Act
        ProfileBuilderProfileMapper.CopyToProfile(new ChatInteraction(), new ProfileBuilderSession(), profile);

        // Assert
        var mcp = JsonSerializer.SerializeToNode(profile.Properties[ProfileBuilderProfileMapper.McpMetadataName]);
        Assert.Empty(mcp["ConnectionIds"].AsArray());
        Assert.NotNull(mcp["ToolNames"]);
    }
}
