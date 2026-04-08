using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Copilot.Models;
using CrestApps.Core.AI.Copilot.Services;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Mvc.Web.Areas.AI.ViewModels;

namespace CrestApps.OrchardCore.Tests.Framework.Chat.Copilot;

public sealed class MvcAITemplateViewModelCopilotTests
{
    [Fact]
    public void FromTemplate_WhenCopilotMetadataExists_ShouldPopulateCopilotFields()
    {
        var template = new AIProfileTemplate
        {
            Source = AITemplateSources.Profile,
        };

        template.Put(new CopilotSessionMetadata
        {
            CopilotModel = "gpt-4.1",
            IsAllowAll = true,
        });

        var model = AITemplateViewModel.FromTemplate(template);

        Assert.Equal("gpt-4.1", model.CopilotModel);
        Assert.True(model.CopilotIsAllowAll);
    }

    [Fact]
    public void ApplyTo_WhenOrchestratorIsCopilot_ShouldPersistCopilotMetadata()
    {
        var model = new AITemplateViewModel
        {
            Name = "support-template",
            Source = AITemplateSources.Profile,
            OrchestratorName = CopilotOrchestrator.OrchestratorName,
            CopilotModel = "claude-3.7-sonnet",
            CopilotIsAllowAll = true,
        };

        var template = new AIProfileTemplate();

        model.ApplyTo(template);

        Assert.True(template.TryGet<CopilotSessionMetadata>(out var metadata));
        Assert.Equal("claude-3.7-sonnet", metadata.CopilotModel);
        Assert.True(metadata.IsAllowAll);
    }

    [Fact]
    public void ApplyTo_WhenOrchestratorIsNotCopilot_ShouldRemoveExistingCopilotMetadata()
    {
        var model = new AITemplateViewModel
        {
            Name = "support-template",
            Source = AITemplateSources.Profile,
            OrchestratorName = "default",
        };

        var template = new AIProfileTemplate();
        template.Put(new CopilotSessionMetadata
        {
            CopilotModel = "gpt-4.1",
            IsAllowAll = true,
        });

        model.ApplyTo(template);

        Assert.False(template.TryGet<CopilotSessionMetadata>(out _));
    }

    [Fact]
    public void FromTemplate_WhenLegacyMvcMemorySettingsExist_ShouldFallbackToLegacyValue()
    {
        var template = new AIProfileTemplate
        {
            Source = AITemplateSources.Profile,
        };
        template.Put(new CrestApps.Core.Mvc.Web.Models.MemorySettings
        {
            EnableUserMemory = true,
        });

        var model = AITemplateViewModel.FromTemplate(template);

        Assert.True(model.EnableUserMemory);
    }

    [Fact]
    public void ApplyTo_WhenProfileSource_ShouldPersistSharedMemorySettings()
    {
        var model = new AITemplateViewModel
        {
            Name = "support-template",
            Source = AITemplateSources.Profile,
            EnableUserMemory = true,
        };

        var template = new AIProfileTemplate();

        model.ApplyTo(template);

        Assert.True(template.As<MemoryMetadata>().EnableUserMemory ?? false);
    }
}
