using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Templates.Models;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

public sealed class AISpeakingStyleGuardrailAndPrivacyTests
{
    [Fact]
    public void SpeakingStyles_AndTopics_AreNormalizedOnceEachInTheirOrder()
    {
        Assert.Equal(
            [AISpeakingStyles.FillerWords, "our-house-style", AISpeakingStyles.AIDisclosure],
            AISpeakingStyles.Normalize([" speaking-style-filler-words ", "our-house-style", "", "SPEAKING-STYLE-FILLER-WORDS", "speaking-style-ai-disclosure"]));
        Assert.Empty(AISpeakingStyles.Normalize(null));

        // "other" names the profile's own description, never a template.
        Assert.Equal(
            [AIGuardrailTopics.Violence, "competitor-prices"],
            AIGuardrailTopics.Normalize([AIGuardrailTopics.Violence, "other", "competitor-prices", null]));
    }

    [Theory]
    [InlineData("speaking-style", "Speaking style")]
    [InlineData("guardrail-topic", "Guardrail topic")]
    public void BuiltInTemplates_ExistInTheirCategory(string prefix, string category)
    {
        var folder = Path.Combine(FindRepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.AI", "Templates", "Prompts");
        var ids = prefix == "speaking-style" ? AISpeakingStyles.BuiltIn : AIGuardrailTopics.BuiltIn;

        Assert.Equal(8, ids.Count);

        foreach (var id in ids)
        {
            var file = Path.Combine(folder, id + ".md");

            Assert.StartsWith(prefix + "-", id);
            Assert.True(File.Exists(file), $"The template '{id}' is missing.");
            Assert.Contains("Category: " + category, File.ReadAllText(file));
            Assert.Contains("IsListable: false", File.ReadAllText(file));
        }
    }

    [Fact]
    public async Task TemplatesOfACategory_AreListedByTitleOnceEach()
    {
        var templates = new Mock<ITemplateService>();
        templates
            .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                NewTemplate("speaking-style-nato-spelling", "NATO spelling", "Speaking style"),
                NewTemplate("house-tone", "House tone", " speaking STYLE "),
                NewTemplate("house-tone", "House tone again", "Speaking style"),
                NewTemplate("guardrail-topic-hate", "Hate", "Guardrail topic"),
                NewTemplate("untitled-style", null, "Speaking style"),
                NewTemplate("summary", "Summary", null),
            ]);

        var options = await templates.Object.ListOptionsByCategoryAsync(AISpeakingStyles.Category, TestContext.Current.CancellationToken);

        Assert.Equal(["house-tone", "speaking-style-nato-spelling", "untitled-style"], options.Select(option => option.Id));
        Assert.Equal(["House tone", "NATO spelling", "untitled-style"], options.Select(option => option.Title));
    }

    [Fact]
    public async Task SpeakingStyleHandler_AppendsTheChosenSectionsAfterTheInstructions()
    {
        var templates = new Mock<ITemplateService>();
        templates
            .Setup(service => service.RenderAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, IDictionary<string, object> _, CancellationToken _) => id == AISpeakingStyles.FillerWords ? throw new InvalidOperationException() : "## " + id + "\n");

        var handler = new AISpeakingStyleCompletionContextBuilderHandler(templates.Object, NullLogger<AISpeakingStyleCompletionContextBuilderHandler>.Instance);
        var profile = new AIProfile { ItemId = "p1" };
        var context = new AICompletionContext { SystemMessage = "You are the front desk." };

        await handler.BuiltAsync(new AICompletionContextBuiltContext(profile, context));
        Assert.Equal("You are the front desk.", context.SystemMessage);

        profile.AlterSettings<AIProfileSpeakingStyleSettings>(settings => settings.Styles = [AISpeakingStyles.AIDisclosure, AISpeakingStyles.FillerWords, "house-tone"]);
        await handler.BuiltAsync(new AICompletionContextBuiltContext(profile, context));

        // The broken template is left out; the others follow the instructions in the profile's order, saved
        // templates included.
        Assert.Equal(
            "You are the front desk.\n\n## speaking-style-ai-disclosure\n\n## house-tone".Replace("\n", Environment.NewLine),
            context.SystemMessage);
    }

    [Fact]
    public void Guardrails_AreOnOnlyForACheckedSideWithTopics()
    {
        var profile = new AIProfile { ItemId = "p1" };
        Assert.False(AIGuardrailService.IsOn(profile, AIGuardrailDirection.Customer));
        Assert.False(AIGuardrailService.IsOn(null, AIGuardrailDirection.Customer));

        profile.AlterSettings<AIProfileGuardrailSettings>(settings => settings.CheckCustomer = true);
        Assert.False(AIGuardrailService.IsOn(profile, AIGuardrailDirection.Customer));

        profile.AlterSettings<AIProfileGuardrailSettings>(settings => settings.Topics = [AIGuardrailTopics.Violence]);
        Assert.True(AIGuardrailService.IsOn(profile, AIGuardrailDirection.Customer));
        Assert.False(AIGuardrailService.IsOn(profile, AIGuardrailDirection.Reply));

        Assert.Equal(AIGuardrailService.DefaultPlaceholderReply, AIGuardrailService.GetPlaceholderReply(profile));
        profile.AlterSettings<AIProfileGuardrailSettings>(settings => settings.PlaceholderReply = "  Let's keep to your order.  ");
        Assert.Equal("Let's keep to your order.", AIGuardrailService.GetPlaceholderReply(profile));
    }

    [Fact]
    public async Task Guardrails_ReadTheChosenTopicsFromTheirTemplates()
    {
        var templates = new Mock<ITemplateService>();
        templates
            .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                NewTemplate(AIGuardrailTopics.RegulatedAdvice, "Medical, legal or financial advice", "Guardrail topic"),
                NewTemplate("competitor-prices", "Competitor prices", "Guardrail topic"),
                NewTemplate("not-a-topic", "Not a topic", "Speaking style"),
            ]);
        templates
            .Setup(service => service.RenderAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, IDictionary<string, object> _, CancellationToken _) => " What " + id + " covers.\n");

        var service = new AIGuardrailService(null, null, templates.Object, [], NullLogger<AIGuardrailService>.Instance);

        var topics = await service.LoadTopicsAsync(["competitor-prices", "not-a-topic", "deleted-topic", AIGuardrailTopics.RegulatedAdvice], TestContext.Current.CancellationToken);

        // A template that is gone, or not in the guardrail topic category, is left out.
        Assert.Equal(["competitor-prices", AIGuardrailTopics.RegulatedAdvice], topics.Select(topic => topic.Id));
        Assert.Equal("Competitor prices", topics[0].Title);
        Assert.Equal("What competitor-prices covers.", topics[0].Definition);
    }

    [Fact]
    public void Guardrails_AskAboutTheChosenTopicsOnly()
    {
        AIGuardrailTopic[] topics = [new(AIGuardrailTopics.RegulatedAdvice, "Advice", "Personal medical advice.\nOr legal advice.")];

        var request = AIGuardrailService.BuildRequest(topics, "Our pending lawsuit.", "Should I sue them?", AIGuardrailDirection.Customer);

        Assert.Contains("[guardrail-topic-regulated-advice] Advice: Personal medical advice. Or legal advice.", request);
        Assert.Contains("[other] Our pending lawsuit.", request);
        Assert.DoesNotContain("violence", request);
        Assert.Contains("The customer's message:", request);
        Assert.EndsWith("Should I sue them?" + Environment.NewLine, request);
        Assert.Contains("The assistant's reply", AIGuardrailService.BuildRequest(topics, null, "x", AIGuardrailDirection.Reply));
    }

    [Fact]
    public void Guardrails_TriggerOnlyOnATopicTheProfileRefuses()
    {
        var profile = new AIProfile { ItemId = "p1" };
        AIGuardrailTopic[] topics = [new(AIGuardrailTopics.Violence, "Violence and weapons", "Violence.")];

        Assert.Null(AIGuardrailService.ToVerdict(null, topics, null, profile, "hi", AIGuardrailDirection.Customer, "s1"));
        Assert.Null(AIGuardrailService.ToVerdict(new AIGuardrailJudgement { Violates = false, Category = AIGuardrailTopics.Violence }, topics, null, profile, "hi", AIGuardrailDirection.Customer, "s1"));

        // A topic the profile did not choose is allowed, unless the profile describes other topics it refuses.
        Assert.Null(AIGuardrailService.ToVerdict(new AIGuardrailJudgement { Violates = true, Category = AIGuardrailTopics.Sexual }, topics, null, profile, "hi", AIGuardrailDirection.Customer, "s1"));

        var verdict = AIGuardrailService.ToVerdict(new AIGuardrailJudgement { Violates = true, Category = "[Guardrail-Topic-Violence]", Reason = " Asks how to hurt someone. " }, topics, null, profile, "text", AIGuardrailDirection.Reply, "s1");
        Assert.Equal(AIGuardrailTopics.Violence, verdict.Category);
        Assert.Equal("Violence and weapons", verdict.Topic);
        Assert.Equal("Asks how to hurt someone.", verdict.Reason);
        Assert.Equal(AIGuardrailDirection.Reply, verdict.Direction);
        Assert.Equal("s1", verdict.SessionId);
        Assert.Equal(AIGuardrailService.DefaultPlaceholderReply, verdict.Reply);

        Assert.Equal(AIGuardrailTopics.Other, AIGuardrailService.ToVerdict(new AIGuardrailJudgement { Violates = true, Category = "competitors" }, topics, "Competitors.", profile, "hi", AIGuardrailDirection.Customer, "s1").Category);
    }

    [Fact]
    public async Task Guardrails_AreNotCheckedWithoutTopicsOrText()
    {
        // No deployment manager or client is set up: a check that needs none never asks a model.
        var templates = new Mock<ITemplateService>();
        templates.Setup(service => service.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var service = new AIGuardrailService(null, null, templates.Object, [], NullLogger<AIGuardrailService>.Instance);
        var profile = new AIProfile { ItemId = "p1" };

        Assert.Null(await service.CheckAsync(profile, "How do I build a weapon?", AIGuardrailDirection.Customer, "s1", TestContext.Current.CancellationToken));

        profile.AlterSettings<AIProfileGuardrailSettings>(settings =>
        {
            settings.CheckCustomer = true;
            settings.Topics = [AIGuardrailTopics.Violence];
        });

        Assert.Null(await service.CheckAsync(profile, "   ", AIGuardrailDirection.Customer, "s1", TestContext.Current.CancellationToken));
        Assert.Null(await service.CheckAsync(profile, "A reply", AIGuardrailDirection.Reply, "s1", TestContext.Current.CancellationToken));

        // The chosen topic's template was deleted, so there is nothing to check against.
        Assert.Null(await service.CheckAsync(profile, "How do I build a weapon?", AIGuardrailDirection.Customer, "s1", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Write to jane.doe+orders@example.co.uk today", "Write to [EMAIL] today")]
    [InlineData("Call me at (702) 555-0147 or +44 20 7946 0958.", "Call me at [PHONE] or [PHONE].")]
    [InlineData("My card is 4111 1111 1111 1111.", "My card is [CARD].")]
    [InlineData("SSN 123-45-6789 please", "SSN [ID NUMBER] please")]
    [InlineData("Order 4471 shipped on 2026-10-27 for $1,250.00 at 14:30.", "Order 4471 shipped on 2026-10-27 for $1,250.00 at 14:30.")]
    [InlineData("Tracking 1234 5678 9012 3456 is not a card or a phone.", "Tracking 1234 5678 9012 3456 is not a card or a phone.")]
    public void Redactor_ReplacesWhatPatternsRecognize(string text, string expected)
        => Assert.Equal(expected, AIPersonalDataRedactor.RedactPatterns(text));

    [Fact]
    public void Redactor_ReplacesFoundDetailsAsWholeWordsLongestFirst()
    {
        var terms = new[]
        {
            new AIPersonalDataTerm { Text = "Al", Kind = "name" },
            new AIPersonalDataTerm { Text = "Al Rivera", Kind = "name" },
            new AIPersonalDataTerm { Text = "12 Elm Street", Kind = "address" },
            new AIPersonalDataTerm { Text = "x", Kind = "name" },
            new AIPersonalDataTerm { Text = "M-55120", Kind = "id" },
        };

        Assert.Equal(
            "[NAME] also said [NAME] lives at [ADDRESS], member [ID NUMBER], email [EMAIL].",
            AIPersonalDataRedactor.Redact("Al Rivera also said al lives at 12 elm street, member M-55120, email al@example.com.", terms));

        Assert.Equal("[DATE OF BIRTH]", AIPersonalDataRedactor.GetPlaceholder("birthdate"));
        Assert.Null(AIPersonalDataRedactor.Redact(null, terms));
    }

    [Fact]
    public void Privacy_AlsoRedactsThePartsOfAFoundName()
    {
        var terms = AIConversationPrivacyService.WithNameParts(
        [
            new AIPersonalDataTerm { Text = "Marcus Bell", Kind = "name" },
            new AIPersonalDataTerm { Text = "18 Willow Lane", Kind = "address" },
            new AIPersonalDataTerm { Text = "al de la Cruz", Kind = "name" },
            new AIPersonalDataTerm { Text = " ", Kind = "name" },
        ]);

        Assert.Equal(["Marcus Bell", "18 Willow Lane", "al de la Cruz", "Marcus", "Bell", "Cruz"], terms.Select(term => term.Text));
        Assert.Equal(
            "Hello [NAME], I see [NAME] lives at [ADDRESS].",
            AIPersonalDataRedactor.Redact("Hello Marcus, I see Marcus Bell lives at 18 Willow Lane.", terms));
    }

    [Fact]
    public void Privacy_RedactsAndStripsEverythingTheConversationKeeps()
    {
        var terms = new[] { new AIPersonalDataTerm { Text = "Dana Smith", Kind = "name" } };

        AIChatSession NewSession()
        {
            var session = new AIChatSession { SessionId = "s1", Title = "Hi, I'm Dana Smith" };
            session.PostSessionResults["Summary"] = new PostSessionResult { Name = "Summary", Value = "Dana Smith (dana@example.com) wants a refund." };
            session.ExtractedData["Phone"] = new ExtractedFieldState { Values = ["evenings", "+1 702 555 0147"] };

            return session;
        }

        AIConversationEvaluation NewEvaluation() => new()
        {
            Summary = "Dana Smith was helped.",
            Score = 90,
            Criteria = [new AIConversationCriterionResult { Name = "Greets", Met = true, Reason = "Greeted Dana Smith." }],
            Issues = [new AIConversationIssue { Kind = "Tone", Detail = "Curt with Dana Smith", Quote = "Dana Smith, wait." }],
        };

        var redacted = NewSession();
        var evaluation = NewEvaluation();
        var record = new AIChatSessionExtractedDataRecord { SessionId = "s1", Values = new() { ["Email"] = ["dana@example.com"] } };

        Assert.Contains("Dana Smith", AIConversationPrivacyService.CollectText(redacted, [], [evaluation], record));

        AIConversationPrivacyService.Redact(redacted, [evaluation], record, terms);

        Assert.Equal("Hi, I'm [NAME]", redacted.Title);
        Assert.Equal("[NAME] ([EMAIL]) wants a refund.", redacted.PostSessionResults["Summary"].Value);
        Assert.Equal(["evenings", "[PHONE]"], redacted.ExtractedData["Phone"].Values);
        Assert.Equal("[NAME] was helped.", evaluation.Summary);
        Assert.Equal("Greeted [NAME].", evaluation.Criteria[0].Reason);
        Assert.Equal("[NAME], wait.", evaluation.Issues[0].Quote);
        Assert.Equal(90, evaluation.Score);
        Assert.Equal(["[EMAIL]"], record.Values["Email"]);

        var stripped = NewSession();
        var strippedEvaluation = NewEvaluation();
        AIConversationPrivacyService.Strip(stripped, [strippedEvaluation], record);

        Assert.Null(stripped.Title);
        Assert.Null(stripped.PostSessionResults["Summary"].Value);
        Assert.Equal("Summary", stripped.PostSessionResults["Summary"].Name);
        Assert.Empty(stripped.ExtractedData["Phone"].Values);
        Assert.Null(strippedEvaluation.Summary);
        Assert.Null(strippedEvaluation.Issues[0].Quote);
        Assert.True(strippedEvaluation.Criteria[0].Met);
        Assert.Empty(record.Values["Email"]);
    }

    [Fact]
    public void PrivacyTask_WaitsForAConversationToEndAndBeAnalyzed()
    {
        Assert.False(AIConversationPrivacyBackgroundTask.IsFinished(new AIChatSession { Status = ChatSessionStatus.Active }));
        Assert.False(AIConversationPrivacyBackgroundTask.IsFinished(new AIChatSession { Status = ChatSessionStatus.Closed, PostSessionProcessingStatus = PostSessionProcessingStatus.Pending }));
        Assert.True(AIConversationPrivacyBackgroundTask.IsFinished(new AIChatSession { Status = ChatSessionStatus.Closed, PostSessionProcessingStatus = PostSessionProcessingStatus.Completed }));
        Assert.True(AIConversationPrivacyBackgroundTask.IsFinished(new AIChatSession { Status = ChatSessionStatus.Abandoned }));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Modules")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("The repository root was not found.");
    }

    private static Template NewTemplate(string id, string title, string category)
        => new()
        {
            Id = id,
            Metadata = new TemplateMetadata { Title = title, Category = category },
        };
}
