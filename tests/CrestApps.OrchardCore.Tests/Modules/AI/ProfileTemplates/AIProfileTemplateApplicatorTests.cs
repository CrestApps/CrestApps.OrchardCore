using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI.ProfileTemplates;

public sealed class AIProfileTemplateApplicatorTests
{
    [Fact]
    public void Apply_WhenTemplateHasScenarioMetadata_ShouldNotCopyItOntoTheProfile()
    {
        // Arrange
        var template = new AIProfileTemplate
        {
            Name = "website-assistant",
            DisplayText = "Website assistant",
        };

        template.Put(new ProfileScenarioMetadata
        {
            Featured = true,
            Icon = "fa-solid fa-comments",
            Order = 1,
            RequiresFeatures = ["CrestApps.OrchardCore.AI.Chat"],
        });

        template.Put(new ProfileTemplateMetadata
        {
            ProfileType = AIProfileType.Chat,
        });

        var profile = new AIProfile();

        // Act
        AIProfileTemplateApplicator.Apply(profile, template);

        // Assert
        Assert.False(profile.Has<ProfileScenarioMetadata>());
        Assert.False(profile.Settings.ContainsKey(nameof(ProfileScenarioMetadata)));
        Assert.Equal(AIProfileType.Chat, profile.Type);
    }

    [Fact]
    public void Apply_WhenTemplateHasAnInitialPrompt_ShouldSetItOnTheProfileWithoutCopyingTheDefaults()
    {
        // Arrange
        var template = new AIProfileTemplate
        {
            Name = "sms-qualify-leads",
            DisplayText = "Qualify leads by text",
        };

        template.Put(new ProfileTemplateMetadata
        {
            ProfileType = AIProfileType.Chat,
            SystemMessage = "You qualify leads by text.",
        });

        template.Put(new ProfileTemplateDefaultsMetadata
        {
            InitialPrompt = "Hi {{ Contact.DisplayText }}, do you have a minute?",
        });

        var profile = new AIProfile();

        // Act
        AIProfileTemplateApplicator.Apply(profile, template);

        // Assert
        var metadata = profile.GetOrCreate<AIProfileMetadata>();
        Assert.Equal("Hi {{ Contact.DisplayText }}, do you have a minute?", metadata.InitialPrompt);
        Assert.Equal("You qualify leads by text.", metadata.SystemMessage);
        Assert.False(profile.Has<ProfileTemplateDefaultsMetadata>());
        Assert.False(profile.Settings.ContainsKey(nameof(ProfileTemplateDefaultsMetadata)));
    }

    [Fact]
    public void Apply_WhenTemplateHasNoInitialPrompt_ShouldLeaveTheProfilesInitialPromptEmpty()
    {
        // Arrange
        var template = new AIProfileTemplate
        {
            Name = "website-assistant",
        };

        template.Put(new ProfileTemplateMetadata
        {
            ProfileType = AIProfileType.Chat,
        });

        var profile = new AIProfile();

        // Act
        AIProfileTemplateApplicator.Apply(profile, template);

        // Assert
        Assert.Null(profile.GetOrCreate<AIProfileMetadata>().InitialPrompt);
    }

    [Fact]
    public void Apply_WhenAgentTemplateAllowsToolInvocation_ShouldLetTheAgentRunItsTools()
    {
        // Arrange
        var template = new AIProfileTemplate
        {
            Name = "content-agent",
            DisplayText = "Content Agent",
        };

        template.Put(new ProfileTemplateMetadata
        {
            ProfileType = AIProfileType.Agent,
            AgentAvailability = AgentAvailability.OnDemand,
            AllowToolInvocation = true,
            ToolNames = ["searchForContentItems"],
        });

        var profile = new AIProfile();

        // Act
        AIProfileTemplateApplicator.Apply(profile, template);

        // Assert
        var agentMetadata = profile.GetOrCreate<AgentMetadata>();
        Assert.True(agentMetadata.AllowToolInvocation);
        Assert.Equal(AgentAvailability.OnDemand, agentMetadata.Availability);
    }

    [Fact]
    public void Apply_WhenTemplateOnlyAllowsToolInvocation_ShouldKeepTheProfilesAvailability()
    {
        // Arrange
        var template = new AIProfileTemplate
        {
            Name = "agent",
            DisplayText = "Agent",
        };

        template.Put(new ProfileTemplateMetadata
        {
            ProfileType = AIProfileType.Agent,
            AllowToolInvocation = true,
        });

        var profile = new AIProfile();
        profile.Put(new AgentMetadata
        {
            Availability = AgentAvailability.AlwaysAvailable,
        });

        // Act
        AIProfileTemplateApplicator.Apply(profile, template);

        // Assert
        var agentMetadata = profile.GetOrCreate<AgentMetadata>();
        Assert.True(agentMetadata.AllowToolInvocation);
        Assert.Equal(AgentAvailability.AlwaysAvailable, agentMetadata.Availability);
    }

    [Fact]
    public void Apply_WhenTemplateSaysNothingAboutToolInvocation_ShouldKeepTheProfilesSetting()
    {
        // Arrange
        var template = new AIProfileTemplate
        {
            Name = "agent",
            DisplayText = "Agent",
        };

        template.Put(new ProfileTemplateMetadata
        {
            ProfileType = AIProfileType.Agent,
            AgentAvailability = AgentAvailability.OnDemand,
        });

        var profile = new AIProfile();
        profile.Put(new AgentMetadata
        {
            AllowToolInvocation = true,
        });

        // Act
        AIProfileTemplateApplicator.Apply(profile, template);

        // Assert
        Assert.True(profile.GetOrCreate<AgentMetadata>().AllowToolInvocation);
    }
}
