using System.Text.Encodings.Web;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Handlers;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

public sealed class AIConversationLayerTests
{
    [Fact]
    public async Task AttachAsync_OffersTheProfilesToolsAndTheChannelsSystemTools()
    {
        var registry = new Mock<IToolRegistry>();
        registry.Setup(x => x.GetAllAsync(It.IsAny<AICompletionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ToolRegistryEntry { Id = "lookup", Name = "lookup", Source = ToolRegistryEntrySource.Local }]);

        var context = new AICompletionContext();

        var entries = await AIConversationTools.AttachAsync(
            registry.Object,
            context,
            [AIConversationTools.SystemTool("endCall", "Ends the call.")],
            TestContext.Current.CancellationToken);

        Assert.Equal(["lookup", "endCall"], entries.Select(entry => entry.Name));
        Assert.Same(entries, context.AdditionalProperties[FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey]);
    }

    [Fact]
    public async Task AttachAsync_DoesNotOfferASystemToolTwice()
    {
        var registry = new Mock<IToolRegistry>();
        registry.Setup(x => x.GetAllAsync(It.IsAny<AICompletionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AIConversationTools.SystemTool("endCall", "Ends the call.")]);

        var entries = await AIConversationTools.AttachAsync(
            registry.Object,
            new AICompletionContext(),
            [AIConversationTools.SystemTool("endCall", "Ends the call.")],
            TestContext.Current.CancellationToken);

        Assert.Single(entries);
    }

    [Fact]
    public async Task AttachAsync_OffersOnlySystemTools_WhenTheProfileDisablesTools()
    {
        var registry = new Mock<IToolRegistry>(MockBehavior.Strict);

        var entries = await AIConversationTools.AttachAsync(
            registry.Object,
            new AICompletionContext { DisableTools = true },
            [AIConversationTools.SystemTool("endCall", "Ends the call.")],
            TestContext.Current.CancellationToken);

        Assert.Equal("endCall", Assert.Single(entries).Name);
    }

    [Fact]
    public void ConversationVariables_AreKeptOnTheSession_AndANullValueRemovesOne()
    {
        var session = new AIChatSession { SessionId = "s1" };

        Assert.True(session.SetConversationVariables([new("first_name", "Ada"), new("tier", "gold")]));
        Assert.False(session.SetConversationVariable("FIRST_NAME", "Ada"));
        Assert.True(session.SetConversationVariable("tier", null));

        var variables = session.GetConversationVariables();

        Assert.Equal("Ada", variables["first_name"]);
        Assert.False(variables.ContainsKey("tier"));
    }

    [Fact]
    public void ContextResolver_FillsConversationKeysAndVariables()
    {
        var session = new AIChatSession { SessionId = "s1" };
        session.SetConversationVariable("account_id", "A-42");

        using var scope = AIConversationScope.Begin(new AIConversationContext
        {
            Channel = AIConversationChannels.Phone,
            Session = session,
            CustomerAddress = "+17025550100",
            Contact = new ContentItem { ContentItemId = "contact1", DisplayText = "Ada Lovelace" },
        });

        var resolver = new AIConversationToolParameterContextResolver();

        Assert.True(resolver.TryResolve("conversation.customer_address", null, out var number));
        Assert.Equal("+17025550100", number);
        Assert.True(resolver.TryResolve("contact.name", null, out var name));
        Assert.Equal("Ada Lovelace", name);
        Assert.True(resolver.TryResolve("variables.account_id", null, out var account));
        Assert.Equal("A-42", account);
        Assert.False(resolver.TryResolve("variables.missing", null, out _));
    }

    [Fact]
    public void ContextResolver_ResolvesNothing_OutsideAConversation()
    {
        var resolver = new AIConversationToolParameterContextResolver();

        Assert.False(resolver.TryResolve("conversation.customer_address", null, out _));
    }

    [Fact]
    public void Scope_RestoresTheOuterConversation_WhenAnInnerOneEnds()
    {
        var outer = new AIConversationContext { Channel = AIConversationChannels.Sms };

        using (AIConversationScope.Begin(outer))
        {
            using (AIConversationScope.Begin(new AIConversationContext { Channel = AIConversationChannels.Phone }))
            {
                Assert.Equal(AIConversationChannels.Phone, AIConversationScope.Current.Channel);
            }

            Assert.Same(outer, AIConversationScope.Current);
        }

        Assert.Null(AIConversationScope.Current);
    }

    [Fact]
    public async Task TemplateProperties_LetASystemMessageReadVariablesAndTheConversation()
    {
        var session = new AIChatSession { SessionId = "s1" };
        session.SetConversationVariable("first_name", "Ada");

        var properties = AIConversationLiquidCompletionContextBuilderHandler.BuildProperties(
            new AIConversationContext { Channel = AIConversationChannels.Phone, Session = session },
            new AIProfile());

        var options = new TemplateOptions();
        options.MemberAccessStrategy.Register<AIConversationTemplateModel>();

        var parser = new FluidParser();
        Assert.True(parser.TryParse("Hi {{ Variables.first_name }} on {{ Conversation.Channel }}", out var template, out var error), error);

        var templateContext = new TemplateContext(options);

        foreach (var (name, value) in properties)
        {
            templateContext.SetValue(name, value);
        }

        Assert.Equal("Hi Ada on Phone", await template.RenderAsync(templateContext));
    }

    [Fact]
    public async Task Handler_RendersASystemMessageWithLiquid_AndLeavesPlainTextAlone()
    {
        var liquid = new Mock<ILiquidTemplateManager>();
        liquid.Setup(x => x.RenderStringAsync(It.IsAny<string>(), It.IsAny<TextEncoder>(), It.IsAny<object>(), It.IsAny<IEnumerable<KeyValuePair<string, FluidValue>>>()))
            .ReturnsAsync("rendered");

        var handler = new AIConversationLiquidCompletionContextBuilderHandler(liquid.Object, NullLogger<AIConversationLiquidCompletionContextBuilderHandler>.Instance);

        var plain = new AICompletionContext { SystemMessage = "Be kind." };
        await handler.BuiltAsync(new AICompletionContextBuiltContext(new AIProfile(), plain));
        Assert.Equal("Be kind.", plain.SystemMessage);

        var templated = new AICompletionContext { SystemMessage = "Greet {{ Variables.first_name }}." };
        await handler.BuiltAsync(new AICompletionContextBuiltContext(new AIProfile(), templated));
        Assert.Equal("rendered", templated.SystemMessage);
    }

    [Fact]
    public async Task Handler_KeepsTheMessageAsWritten_WhenTheTemplateFails()
    {
        var liquid = new Mock<ILiquidTemplateManager>();
        liquid.Setup(x => x.RenderStringAsync(It.IsAny<string>(), It.IsAny<TextEncoder>(), It.IsAny<object>(), It.IsAny<IEnumerable<KeyValuePair<string, FluidValue>>>()))
            .ThrowsAsync(new InvalidOperationException("bad template"));

        var handler = new AIConversationLiquidCompletionContextBuilderHandler(liquid.Object, NullLogger<AIConversationLiquidCompletionContextBuilderHandler>.Instance);

        var context = new AICompletionContext { SystemMessage = "Greet {{ broken" };
        await handler.BuiltAsync(new AICompletionContextBuiltContext(new AIProfile(), context));

        Assert.Equal("Greet {{ broken", context.SystemMessage);
    }
}
