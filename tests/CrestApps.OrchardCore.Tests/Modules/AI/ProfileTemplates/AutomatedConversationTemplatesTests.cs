using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Templates.Parsing;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Services;
using Fluid;

namespace CrestApps.OrchardCore.Tests.Modules.AI.ProfileTemplates;

/// <summary>
/// Parses the text-messaging and phone-call starting points shipped with the SMS and automated voice modules
/// the way the module template provider does, then builds a profile from each, so a front-matter mistake shows
/// up here rather than as a card that creates a profile the automated conversations refuse to offer.
/// </summary>
public sealed class AutomatedConversationTemplatesTests
{
    private const string SmsModule = "CrestApps.OrchardCore.Omnichannel.Sms";
    private const string VoiceModule = "CrestApps.OrchardCore.Omnichannel.Voice";

    public static TheoryData<string, string, string> Templates => new()
    {
        { SmsModule, "sms-qualify-leads", "Text messaging" },
        { SmsModule, "sms-customer-care", "Text messaging" },
        { VoiceModule, "voice-front-desk", "Phone calls" },
        { VoiceModule, "voice-qualify-leads", "Phone calls" },
        { VoiceModule, "voice-appointment-confirmation", "Phone calls" },
    };

    public static TheoryData<string, string> TemplateIds => new()
    {
        { SmsModule, "sms-qualify-leads" },
        { SmsModule, "sms-customer-care" },
        { VoiceModule, "voice-front-desk" },
        { VoiceModule, "voice-qualify-leads" },
        { VoiceModule, "voice-appointment-confirmation" },
    };

    [Theory]
    [MemberData(nameof(Templates))]
    public void Template_ShouldParseAsAListedChatStartingPoint(string module, string id, string expectedCategory)
    {
        // Act
        var template = ParseTemplate(module, id);

        // Assert
        Assert.Equal(AITemplateSources.Profile, template.Source);
        Assert.True(template.IsListable);
        Assert.Equal(expectedCategory, template.Category);
        Assert.False(string.IsNullOrWhiteSpace(template.DisplayText));
        Assert.NotEqual(id.Replace('-', ' '), template.DisplayText);
        Assert.False(string.IsNullOrWhiteSpace(template.Description));

        var profileMetadata = template.GetOrCreate<ProfileTemplateMetadata>();
        Assert.Equal(AIProfileType.Chat, profileMetadata.ProfileType);
        Assert.False(string.IsNullOrWhiteSpace(profileMetadata.SystemMessage));
        Assert.NotNull(profileMetadata.Temperature);

        // The automated conversations speak through the telephony provider or a realtime-capable chat
        // deployment, never through the chat mode, and no deployment is named that only exists on one site.
        Assert.Null(profileMetadata.ChatMode);
        Assert.True(string.IsNullOrEmpty(profileMetadata.ChatDeploymentName));
        Assert.True(string.IsNullOrEmpty(profileMetadata.ConversationDeploymentName));
    }

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void Template_ShouldBeAnOrdinaryStartingPointThatNeedsItsChannelFeature(string module, string id)
    {
        // Act
        var template = ParseTemplate(module, id);

        // Assert
        Assert.True(template.TryGet<ProfileScenarioMetadata>(out var scenario));

        // Not featured, so the general-purpose featured scenarios stay first in the picker.
        Assert.False(scenario.Featured);
        Assert.StartsWith("fa-solid fa-", scenario.Icon);
        Assert.True(scenario.Order > 0);
        Assert.Equal([module], scenario.RequiresFeatures);
    }

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void Template_ShouldGiveTheProfileAnInitialPrompt(string module, string id)
    {
        // Arrange
        var template = ParseTemplate(module, id);
        var profile = new AIProfile();

        // Act
        AIProfileTemplateApplicator.Apply(profile, template);

        // Assert
        Assert.Equal(AIProfileType.Chat, profile.Type);

        // Automated SMS conversations and calls only offer chat profiles with an initial prompt, because it is
        // the opening text or greeting.
        var metadata = profile.GetOrCreate<AIProfileMetadata>();
        Assert.False(string.IsNullOrWhiteSpace(metadata.InitialPrompt));
        Assert.Equal(template.GetOrCreate<ProfileTemplateMetadata>().SystemMessage, metadata.SystemMessage);
        Assert.False(profile.Has<ProfileTemplateDefaultsMetadata>());
        Assert.False(profile.Has<ProfileScenarioMetadata>());
    }

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void SystemPrompt_ShouldLeaveHandoffAndEndingMechanicsToThePlatform(string module, string id)
    {
        // Act
        var systemMessage = ParseTemplate(module, id).GetOrCreate<ProfileTemplateMetadata>().SystemMessage;

        // Assert

        // The platform attaches the transfer and end-call tools and says how to use them. A prompt that names a
        // tool or a control marker itself would contradict that guidance whenever the tool is not attached.
        Assert.DoesNotContain("[[", systemMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("transferToLiveAgent", systemMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endCall", systemMessage, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("automated assistant", systemMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("If you have been given instructions for", systemMessage, StringComparison.Ordinal);
        Assert.Contains("## About the business", systemMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SmsModule, "sms-qualify-leads")]
    [InlineData(SmsModule, "sms-customer-care")]
    public void SmsOpening_ShouldTellTheCustomerHowToOptOut(string module, string id)
    {
        // Act
        var initialPrompt = ParseTemplate(module, id).GetOrCreate<ProfileTemplateDefaultsMetadata>().InitialPrompt;

        // Assert
        Assert.Contains("Reply STOP to opt out.", initialPrompt, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void InitialPrompt_ShouldGreetANamedContactByFirstName(string module, string id)
    {
        // Arrange
        var initialPrompt = ParseTemplate(module, id).GetOrCreate<ProfileTemplateDefaultsMetadata>().InitialPrompt;

        // Act
        var rendered = Render(initialPrompt, new Dictionary<string, object>
        {
            ["DisplayText"] = "Dana Example",
        });

        // Assert
        Assert.DoesNotContain("{", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Example", rendered, StringComparison.Ordinal);
        Assert.Contains("automated assistant", rendered, StringComparison.OrdinalIgnoreCase);

        if (initialPrompt.Contains("Contact.", StringComparison.Ordinal))
        {
            Assert.Contains("Dana", rendered, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void InitialPrompt_ShouldReadNaturallyWithoutAContactName(string module, string id)
    {
        // Arrange
        var initialPrompt = ParseTemplate(module, id).GetOrCreate<ProfileTemplateDefaultsMetadata>().InitialPrompt;

        // Act: a voice call may have no contact at all, and a contact may have no name.
        var withoutContact = Render(initialPrompt, contact: null);
        var withoutName = Render(initialPrompt, new Dictionary<string, object>
        {
            ["DisplayText"] = string.Empty,
        });

        // Assert

        foreach (var rendered in new[] { withoutContact, withoutName })
        {
            Assert.DoesNotContain("{", rendered, StringComparison.Ordinal);
            Assert.DoesNotContain("  ", rendered, StringComparison.Ordinal);
            Assert.DoesNotContain(" ,", rendered, StringComparison.Ordinal);
            Assert.DoesNotContain("is this ?", rendered, StringComparison.Ordinal);
            Assert.Equal(rendered.Trim(), rendered);
            Assert.True(char.IsUpper(rendered[0]));
        }
    }

    private static string Render(string source, Dictionary<string, object> contact)
    {
        var parser = new FluidParser();

        Assert.True(parser.TryParse(source, out var template, out var error), error);

        var context = new TemplateContext();

        if (contact is not null)
        {
            context.SetValue("Contact", contact);
        }

        return template.Render(context);
    }

    private static AIProfileTemplate ParseTemplate(string module, string id)
    {
        var path = Path.Combine(FindRepositoryRoot(), "src", "Modules", module, "Templates", "Profiles", id + ".md");
        var parseResult = new DefaultMarkdownTemplateParser().Parse(File.ReadAllText(path));
        var template = AIProfileTemplateParser.Parse(id, parseResult);

        ProfileScenarioMetadataReader.Apply(template, parseResult.Metadata);
        ProfileTemplateDefaultsReader.Apply(template, parseResult.Metadata);

        return template;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Unable to locate the repository root.");
    }
}
