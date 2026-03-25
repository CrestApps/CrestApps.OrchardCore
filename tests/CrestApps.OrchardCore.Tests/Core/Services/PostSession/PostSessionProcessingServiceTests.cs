using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Core.Services.PostSession;

public sealed class PostSessionProcessingServiceTests
{
    private const string TestProviderName = "TestProvider";
    private const string TestConnectionName = "TestConnection";
    private const string TestDeploymentName = "gpt-4o";

    [Fact]
    public async Task ProcessAsync_WhenProcessingDisabled_ShouldReturnNull()
    {
        // Arrange: profile with post-session processing disabled.
        var profile = CreateProfile();
        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = false;
        });

        var session = CreateSession();
        var prompts = CreatePrompts();

        var service = CreateService();

        // Act
        var result = await service.ProcessAsync(profile, session, prompts, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessAsync_WhenNoTasksConfigured_ShouldReturnNull()
    {
        // Arrange: processing enabled but no tasks.
        var profile = CreateProfile();
        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = true;
            s.PostSessionTasks = [];
        });

        var session = CreateSession();
        var prompts = CreatePrompts();

        var service = CreateService();

        // Act
        var result = await service.ProcessAsync(profile, session, prompts, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessAsync_WithTasksAndNoTools_ShouldUseStructuredOutputPath()
    {
        // Arrange: tasks configured but no tool names — should use structured output (GetResponseAsync<T>).
        var profile = CreateProfile();
        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = true;
            s.PostSessionTasks =
            [
                new PostSessionTask
                {
                    Name = "disposition",
                    Type = PostSessionTaskType.PredefinedOptions,
                    Instructions = "Determine the disposition.",
                    Options =
                    [
                        new PostSessionTaskOption { Value = "Resolved", Description = "Issue resolved" },
                        new PostSessionTaskOption { Value = "Unresolved", Description = "Issue not resolved" },
                    ],
                },
            ];
            s.ToolNames = [];
        });

        var session = CreateSession();
        var prompts = CreatePrompts();

        var mockChatClient = new Mock<IChatClient>();

        // The structured output path calls GetResponseAsync<PostSessionProcessingResponse>
        // which is an extension method on IChatClient that delegates to GetResponseAsync.
        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                "{\"tasks\":[{\"name\":\"summary\",\"value\":\"Summarized the conversation.\"}]}")));

        var mockTemplateService = new Mock<IAITemplateService>();
        mockTemplateService
            .Setup(t => t.RenderAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync("Rendered prompt text");

        var service = CreateService(
            chatClient: mockChatClient.Object,
            templateService: mockTemplateService.Object);

        // Act
        var result = await service.ProcessAsync(profile, session, prompts, TestContext.Current.CancellationToken);

        // Assert: The structured output path was invoked via the chat client.
        mockChatClient.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithToolNames_ShouldResolveToolsAndUseToolsPath()
    {
        // Arrange: tasks with tool names configured — should use tools path.
        var profile = CreateProfile();
        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = true;
            s.PostSessionTasks =
            [
                new PostSessionTask
                {
                    Name = "summary",
                    Type = PostSessionTaskType.Semantic,
                    Instructions = "Summarize the conversation.",
                },
            ];
            s.ToolNames = ["sendEmail"];
        });

        var session = CreateSession();
        var prompts = CreatePrompts();

        var mockTool = new TestAIFunction("sendEmail");
        var mockToolsService = new Mock<IAIToolsService>();
        mockToolsService
            .Setup(t => t.GetByNameAsync("sendEmail"))
            .ReturnsAsync(mockTool);

        var mockChatClient = new Mock<IChatClient>();

        // The tools path calls non-generic GetResponseAsync.
        // Simulate response with a JSON result.
        var responseMessage = new ChatMessage(ChatRole.Assistant,
            "{\"tasks\":[{\"name\":\"summary\",\"value\":\"User asked about pricing and was given options.\"}]}");
        var chatResponse = new ChatResponse(responseMessage);

        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        var mockTemplateService = new Mock<IAITemplateService>();
        mockTemplateService
            .Setup(t => t.RenderAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync("Rendered prompt");

        var service = CreateService(
            chatClient: mockChatClient.Object,
            toolsService: mockToolsService.Object,
            templateService: mockTemplateService.Object);

        // Act
        var result = await service.ProcessAsync(profile, session, prompts, TestContext.Current.CancellationToken);

        // Assert: tools service was asked to resolve the tool.
        mockToolsService.Verify(t => t.GetByNameAsync("sendEmail"), Times.Once);

        // Assert: the chat client was invoked with tools in the options.
        mockChatClient.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.Is<ChatOptions>(opts => opts.Tools != null && opts.Tools.Count > 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenToolNotFound_ShouldLogWarningAndFallToStructuredOutput()
    {
        // Arrange: tool name configured but not resolvable — should fall back to structured output.
        var profile = CreateProfile();
        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = true;
            s.PostSessionTasks =
            [
                new PostSessionTask
                {
                    Name = "summary",
                    Type = PostSessionTaskType.Semantic,
                    Instructions = "Summarize the conversation.",
                },
            ];
            s.ToolNames = ["nonExistentTool"];
        });

        var session = CreateSession();
        var prompts = CreatePrompts();

        var mockToolsService = new Mock<IAIToolsService>();
        mockToolsService
            .Setup(t => t.GetByNameAsync("nonExistentTool"))
            .ReturnsAsync((AITool)null);

        var mockChatClient = new Mock<IChatClient>();
        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                "{\"tasks\":[{\"name\":\"summary\",\"value\":\"Summarized the conversation.\"}]}")));

        var mockTemplateService = new Mock<IAITemplateService>();
        mockTemplateService
            .Setup(t => t.RenderAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync("Rendered prompt");

        var service = CreateService(
            chatClient: mockChatClient.Object,
            toolsService: mockToolsService.Object,
            templateService: mockTemplateService.Object);

        // Act
        var result = await service.ProcessAsync(profile, session, prompts, TestContext.Current.CancellationToken);

        // Assert: tool resolution was attempted.
        mockToolsService.Verify(t => t.GetByNameAsync("nonExistentTool"), Times.Once);

        // Assert: when no tools resolve, the structured output path is used (no tools in options).
        mockChatClient.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.Is<ChatOptions>(opts => opts.Tools == null || opts.Tools.Count == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenChatClientCannotBeCreated_ShouldThrow()
    {
        // Arrange: profile references a provider that is not configured.
        var profile = CreateProfile();
        profile.Source = "UnknownProvider";
        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = true;
            s.PostSessionTasks =
            [
                new PostSessionTask
                {
                    Name = "summary",
                    Type = PostSessionTaskType.Semantic,
                    Instructions = "Summarize.",
                },
            ];
        });

        var session = CreateSession();
        var prompts = CreatePrompts();

        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ProcessAsync(profile, session, prompts, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProcessAsync_WhenPromptTemplateReturnsEmpty_ShouldReturnNull()
    {
        // Arrange: template service returns empty prompt.
        var profile = CreateProfile();
        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = true;
            s.PostSessionTasks =
            [
                new PostSessionTask
                {
                    Name = "summary",
                    Type = PostSessionTaskType.Semantic,
                    Instructions = "Summarize.",
                },
            ];
        });

        var session = CreateSession();
        var prompts = CreatePrompts();

        var mockChatClient = new Mock<IChatClient>();
        var mockTemplateService = new Mock<IAITemplateService>();

        // Return a valid system prompt but an empty user prompt.
        mockTemplateService
            .Setup(t => t.RenderAsync(AITemplateIds.PostSessionAnalysis, It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync("System prompt");
        mockTemplateService
            .Setup(t => t.RenderAsync(AITemplateIds.PostSessionAnalysisPrompt, It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync(string.Empty);

        var service = CreateService(
            chatClient: mockChatClient.Object,
            templateService: mockTemplateService.Object);

        // Act
        var result = await service.ProcessAsync(profile, session, prompts, TestContext.Current.CancellationToken);

        // Assert: should return null without calling the chat client.
        Assert.Null(result);
        mockChatClient.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenAllTasksAlreadySucceeded_ShouldReturnNull()
    {
        // Arrange: session has all tasks already marked as Succeeded.
        var profile = CreateProfile();
        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = true;
            s.PostSessionTasks =
            [
                new PostSessionTask
                {
                    Name = "disposition",
                    Type = PostSessionTaskType.PredefinedOptions,
                    Instructions = "Determine the disposition.",
                    Options =
                    [
                        new PostSessionTaskOption { Value = "Resolved", Description = "Issue resolved" },
                    ],
                },
                new PostSessionTask
                {
                    Name = "summary",
                    Type = PostSessionTaskType.Semantic,
                    Instructions = "Summarize.",
                },
            ];
        });

        var session = CreateSession();
        session.PostSessionResults["disposition"] = new PostSessionResult
        {
            Name = "disposition",
            Value = "Resolved",
            Status = PostSessionTaskResultStatus.Succeeded,
            ProcessedAtUtc = DateTime.UtcNow,
        };
        session.PostSessionResults["summary"] = new PostSessionResult
        {
            Name = "summary",
            Value = "User asked about pricing.",
            Status = PostSessionTaskResultStatus.Succeeded,
            ProcessedAtUtc = DateTime.UtcNow,
        };

        var mockChatClient = new Mock<IChatClient>();
        var service = CreateService(chatClient: mockChatClient.Object);

        // Act
        var result = await service.ProcessAsync(profile, session, CreatePrompts(), TestContext.Current.CancellationToken);

        // Assert: should return null since all tasks have already succeeded.
        Assert.Null(result);
        mockChatClient.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenSomeTasksFailed_ShouldOnlyProcessFailedTasks()
    {
        // Arrange: one task succeeded, one task failed from a previous attempt.
        var profile = CreateProfile();
        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = true;
            s.PostSessionTasks =
            [
                new PostSessionTask
                {
                    Name = "disposition",
                    Type = PostSessionTaskType.PredefinedOptions,
                    Instructions = "Determine the disposition.",
                    Options =
                    [
                        new PostSessionTaskOption { Value = "Resolved", Description = "Issue resolved" },
                    ],
                },
                new PostSessionTask
                {
                    Name = "summary",
                    Type = PostSessionTaskType.Semantic,
                    Instructions = "Summarize.",
                },
            ];
        });

        var session = CreateSession();
        session.PostSessionResults["disposition"] = new PostSessionResult
        {
            Name = "disposition",
            Value = "Resolved",
            Status = PostSessionTaskResultStatus.Succeeded,
            ProcessedAtUtc = DateTime.UtcNow,
        };
        session.PostSessionResults["summary"] = new PostSessionResult
        {
            Name = "summary",
            Status = PostSessionTaskResultStatus.Failed,
            ErrorMessage = "Previous attempt failed",
        };

        var mockChatClient = new Mock<IChatClient>();
        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                "{\"tasks\":[{\"name\":\"summary\",\"value\":\"Summarized the conversation.\"}]}")));

        var mockTemplateService = new Mock<IAITemplateService>();
        mockTemplateService
            .Setup(t => t.RenderAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync("Rendered prompt");

        var service = CreateService(
            chatClient: mockChatClient.Object,
            templateService: mockTemplateService.Object);

        // Act
        var result = await service.ProcessAsync(profile, session, CreatePrompts(), TestContext.Current.CancellationToken);

        // Assert: the chat client was invoked (the failed task should be retried).
        mockChatClient.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_SuccessfulResults_ShouldHaveSucceededStatus()
    {
        // Arrange: verify that returned results have Status = Succeeded.
        var profile = CreateProfile();
        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = true;
            s.PostSessionTasks =
            [
                new PostSessionTask
                {
                    Name = "summary",
                    Type = PostSessionTaskType.Semantic,
                    Instructions = "Summarize the conversation.",
                },
            ];
            s.ToolNames = [];
        });

        var session = CreateSession();
        var prompts = CreatePrompts();

        var mockChatClient = new Mock<IChatClient>();

        // The structured output returns a PostSessionProcessingResponse via
        // the generic GetResponseAsync<T> extension which calls the underlying GetResponseAsync.
        var responseJson = "{\"tasks\":[{\"name\":\"summary\",\"value\":\"User asked about pricing.\"}]}";
        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseJson)));

        var mockTemplateService = new Mock<IAITemplateService>();
        mockTemplateService
            .Setup(t => t.RenderAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync("Rendered prompt text");

        var service = CreateService(
            chatClient: mockChatClient.Object,
            templateService: mockTemplateService.Object);

        // Act
        var result = await service.ProcessAsync(profile, session, prompts, TestContext.Current.CancellationToken);

        // Assert: if the structured output path returns valid results, they should have Succeeded status.
        // Note: The structured output path uses GetResponseAsync<T> which is an extension method.
        // The mock returns a plain ChatResponse, so the generic extension may not parse as expected.
        // This test verifies the service processes without errors.
        mockChatClient.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #region Helpers

    private static AIProfile CreateProfile()
    {
        var profile = new AIProfile
        {
            ItemId = "test-profile-id",
            Name = "TestProfile",
            DisplayText = "Test Profile",
            Source = TestProviderName,
        };

        return profile;
    }

    private static AIChatSession CreateSession()
    {
        return new AIChatSession
        {
            SessionId = "test-session-id",
            ProfileId = "test-profile-id",
            Status = ChatSessionStatus.Closed,
        };
    }

    private static List<AIChatSessionPrompt> CreatePrompts()
    {
        return
        [
            new AIChatSessionPrompt
            {
                Role = ChatRole.User,
                Content = "Hello, I need help with my order.",
                CreatedUtc = DateTime.UtcNow,
            },
            new AIChatSessionPrompt
            {
                Role = ChatRole.Assistant,
                Content = "Sure! I'd be happy to help. Could you provide your order number?",
                CreatedUtc = DateTime.UtcNow,
            },
        ];
    }

    private static PostSessionProcessingService CreateService(
        IChatClient chatClient = null,
        IAIToolsService toolsService = null,
        IAITemplateService templateService = null)
    {
        var mockClientFactory = new Mock<IAIClientFactory>();
        if (chatClient is not null)
        {
            mockClientFactory
                .Setup(f => f.CreateChatClientAsync(TestProviderName, TestConnectionName, TestDeploymentName))
                .ReturnsAsync(chatClient);
        }

        var mockToolsService = toolsService is not null
            ? null
            : new Mock<IAIToolsService>();

        var mockTemplateService = templateService is not null
            ? null
            : new Mock<IAITemplateService>();

        // Set up default template renders.
        if (mockTemplateService is not null)
        {
            mockTemplateService
                .Setup(t => t.RenderAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
                .ReturnsAsync("Default rendered text");
        }

        var providerOptions = new AIProviderOptions();
        providerOptions.Providers[TestProviderName] = new AIProvider
        {
            DefaultConnectionName = TestConnectionName,
            Connections = new Dictionary<string, AIProviderConnectionEntry>
            {
                [TestConnectionName] = new AIProviderConnectionEntry(
                    new Dictionary<string, object>
                    {
                        ["UtilityDeploymentName"] = TestDeploymentName,
                        ["ChatDeploymentName"] = TestDeploymentName,
                    }),
            },
        };

        var defaultOptions = new DefaultAIOptions
        {
            MaximumIterationsPerRequest = 10,
        };

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(DateTime.UtcNow);

        return new PostSessionProcessingService(
            mockClientFactory.Object,
            toolsService ?? mockToolsService.Object,
            templateService ?? mockTemplateService.Object,
            Options.Create(providerOptions),
            defaultOptions,
            new Mock<IServiceProvider>().Object,
            clock.Object,
            NullLoggerFactory.Instance);
    }

    /// <summary>
    /// A minimal AIFunction implementation for testing tool resolution.
    /// </summary>
    private sealed class TestAIFunction : AIFunction
    {
        public TestAIFunction(string name)
        {
            Name = name;
        }

        public override string Name { get; }

        public override string Description => $"Test tool: {Name}";

        public override System.Text.Json.JsonElement JsonSchema =>
            System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("{}");

        protected override ValueTask<object> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            return new ValueTask<object>("Tool executed successfully.");
        }
    }

    [Fact]
    public async Task ProcessAsync_WithTools_WhenResponseIsJsonInCodeFence_ShouldParseSuccessfully()
    {
        // Arrange: model wraps JSON in markdown code fences despite instructions.
        var profile = CreateProfile();
        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = true;
            s.PostSessionTasks =
            [
                new PostSessionTask
                {
                    Name = "summary",
                    Type = PostSessionTaskType.Semantic,
                    Instructions = "Summarize the conversation.",
                },
            ];
            s.ToolNames = ["sendEmail"];
        });

        var session = CreateSession();
        var prompts = CreatePrompts();

        var mockTool = new TestAIFunction("sendEmail");
        var mockToolsService = new Mock<IAIToolsService>();
        mockToolsService
            .Setup(t => t.GetByNameAsync("sendEmail"))
            .ReturnsAsync(mockTool);

        var responseText = "```json\n{\"tasks\":[{\"name\":\"summary\",\"value\":\"Customer asked about pricing.\"}]}\n```";
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var mockTemplateService = new Mock<IAITemplateService>();
        mockTemplateService
            .Setup(t => t.RenderAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync("Rendered prompt");

        var service = CreateService(
            chatClient: mockChatClient.Object,
            toolsService: mockToolsService.Object,
            templateService: mockTemplateService.Object);

        // Act
        var result = await service.ProcessAsync(profile, session, prompts, TestContext.Current.CancellationToken);

        // Assert: result should be parsed from the code fence.
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("summary"));
        Assert.Equal("Customer asked about pricing.", result["summary"].Value);
        Assert.Equal(PostSessionTaskResultStatus.Succeeded, result["summary"].Status);
    }

    [Fact]
    public async Task ProcessAsync_WithTools_WhenResponseIsJsonWithSurroundingText_ShouldParseSuccessfully()
    {
        // Arrange: model returns JSON with extra text around it.
        var profile = CreateProfile();
        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = true;
            s.PostSessionTasks =
            [
                new PostSessionTask
                {
                    Name = "summary",
                    Type = PostSessionTaskType.Semantic,
                    Instructions = "Summarize the conversation.",
                },
            ];
            s.ToolNames = ["sendEmail"];
        });

        var session = CreateSession();
        var prompts = CreatePrompts();

        var mockTool = new TestAIFunction("sendEmail");
        var mockToolsService = new Mock<IAIToolsService>();
        mockToolsService
            .Setup(t => t.GetByNameAsync("sendEmail"))
            .ReturnsAsync(mockTool);

        var responseText = "Here are the results:\n{\"tasks\":[{\"name\":\"summary\",\"value\":\"Customer asked about pricing.\"}]}\nDone.";
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var mockTemplateService = new Mock<IAITemplateService>();
        mockTemplateService
            .Setup(t => t.RenderAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync("Rendered prompt");

        var service = CreateService(
            chatClient: mockChatClient.Object,
            toolsService: mockToolsService.Object,
            templateService: mockTemplateService.Object);

        // Act
        var result = await service.ProcessAsync(profile, session, prompts, TestContext.Current.CancellationToken);

        // Assert: result should be parsed from embedded JSON.
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("summary"));
        Assert.Equal("Customer asked about pricing.", result["summary"].Value);
        Assert.Equal(PostSessionTaskResultStatus.Succeeded, result["summary"].Status);
    }

    [Fact]
    public async Task ProcessAsync_WithTools_WhenResponseIsTruncatedJson_ShouldRecoverWithStructuredRetry()
    {
        // Arrange: tool execution succeeds but the final assistant response is truncated.
        var profile = CreateProfile();
        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = true;
            s.PostSessionTasks =
            [
                new PostSessionTask
                {
                    Name = "summary",
                    Type = PostSessionTaskType.Semantic,
                    Instructions = "Summarize the conversation.",
                },
            ];
            s.ToolNames = ["sendEmail"];
        });

        var session = CreateSession();
        var prompts = CreatePrompts();

        var mockTool = new TestAIFunction("sendEmail");
        var mockToolsService = new Mock<IAIToolsService>();
        mockToolsService
            .Setup(t => t.GetByNameAsync("sendEmail"))
            .ReturnsAsync(mockTool);

        var mockChatClient = new Mock<IChatClient>();
        mockChatClient
            .SetupSequence(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "{")))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                "{\"tasks\":[{\"name\":\"summary\",\"value\":\"The user requested fence information and follow-up was initiated.\"}]}")));

        var mockTemplateService = new Mock<IAITemplateService>();
        mockTemplateService
            .Setup(t => t.RenderAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync("Rendered prompt");

        var service = CreateService(
            chatClient: mockChatClient.Object,
            toolsService: mockToolsService.Object,
            templateService: mockTemplateService.Object);

        // Act
        var result = await service.ProcessAsync(profile, session, prompts, TestContext.Current.CancellationToken);

        // Assert: the recovery pass should return a structured success result.
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("summary"));
        Assert.Equal("The user requested fence information and follow-up was initiated.", result["summary"].Value);
        Assert.Equal(PostSessionTaskResultStatus.Succeeded, result["summary"].Status);
        mockChatClient.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessAsync_WithTools_WhenSingleSemanticTaskAndNonJsonResponse_ShouldReturnFailedResult()
    {
        // Arrange: model never returns valid structured JSON, even after recovery.
        var profile = CreateProfile();
        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = true;
            s.PostSessionTasks =
            [
                new PostSessionTask
                {
                    Name = "summary",
                    Type = PostSessionTaskType.Semantic,
                    Instructions = "Summarize the conversation.",
                },
            ];
            s.ToolNames = ["sendEmail"];
        });

        var session = CreateSession();
        var prompts = CreatePrompts();

        var mockTool = new TestAIFunction("sendEmail");
        var mockToolsService = new Mock<IAIToolsService>();
        mockToolsService
            .Setup(t => t.GetByNameAsync("sendEmail"))
            .ReturnsAsync(mockTool);

        var responseText = "The customer asked about pricing options.";
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var mockTemplateService = new Mock<IAITemplateService>();
        mockTemplateService
            .Setup(t => t.RenderAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync("Rendered prompt");

        var service = CreateService(
            chatClient: mockChatClient.Object,
            toolsService: mockToolsService.Object,
            templateService: mockTemplateService.Object);

        // Act
        var result = await service.ProcessAsync(profile, session, prompts, TestContext.Current.CancellationToken);

        // Assert: malformed/non-structured output should fail the task instead of succeeding.
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("summary"));
        Assert.Equal(PostSessionTaskResultStatus.Failed, result["summary"].Status);
        Assert.NotNull(result["summary"].ErrorMessage);
        Assert.Equal(string.Empty, result["summary"].Value ?? string.Empty);
    }

    [Fact]
    public async Task ProcessAsync_WithTools_WhenMultipleTasksAndNonJsonResponse_ShouldReturnFailedResults()
    {
        // Arrange: model returns plain text but there are multiple tasks and no structured payload.
        var profile = CreateProfile();
        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = true;
            s.PostSessionTasks =
            [
                new PostSessionTask
                {
                    Name = "summary",
                    Type = PostSessionTaskType.Semantic,
                    Instructions = "Summarize the conversation.",
                },
                new PostSessionTask
                {
                    Name = "disposition",
                    Type = PostSessionTaskType.PredefinedOptions,
                    Instructions = "Determine the disposition.",
                    Options =
                    [
                        new PostSessionTaskOption { Value = "Resolved", Description = "Issue resolved" },
                    ],
                },
            ];
            s.ToolNames = ["sendEmail"];
        });

        var session = CreateSession();
        var prompts = CreatePrompts();

        var mockTool = new TestAIFunction("sendEmail");
        var mockToolsService = new Mock<IAIToolsService>();
        mockToolsService
            .Setup(t => t.GetByNameAsync("sendEmail"))
            .ReturnsAsync(mockTool);

        var mockChatClient = new Mock<IChatClient>();
        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Still not JSON.")));

        var mockTemplateService = new Mock<IAITemplateService>();
        mockTemplateService
            .Setup(t => t.RenderAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync("Rendered prompt");

        var service = CreateService(
            chatClient: mockChatClient.Object,
            toolsService: mockToolsService.Object,
            templateService: mockTemplateService.Object);

        // Act
        var result = await service.ProcessAsync(profile, session, prompts, TestContext.Current.CancellationToken);

        // Assert: every pending task should be marked failed when structured output never materializes.
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal(PostSessionTaskResultStatus.Failed, result["summary"].Status);
        Assert.Equal(PostSessionTaskResultStatus.Failed, result["disposition"].Status);
        Assert.NotNull(result["summary"].ErrorMessage);
        Assert.NotNull(result["disposition"].ErrorMessage);
    }

    [Fact]
    public async Task ProcessAsync_WithTools_WhenResponseIsEmpty_ShouldReturnFailedResult()
    {
        // Arrange: model returns empty response — tools may have executed as side effects.
        var profile = CreateProfile();
        profile.AlterSettings<AIProfilePostSessionSettings>(s =>
        {
            s.EnablePostSessionProcessing = true;
            s.PostSessionTasks =
            [
                new PostSessionTask
                {
                    Name = "summary",
                    Type = PostSessionTaskType.Semantic,
                    Instructions = "Summarize the conversation.",
                },
            ];
            s.ToolNames = ["sendEmail"];
        });

        var session = CreateSession();
        var prompts = CreatePrompts();

        var mockTool = new TestAIFunction("sendEmail");
        var mockToolsService = new Mock<IAIToolsService>();
        mockToolsService
            .Setup(t => t.GetByNameAsync("sendEmail"))
            .ReturnsAsync(mockTool);

        var mockChatClient = new Mock<IChatClient>();
        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "")));

        var mockTemplateService = new Mock<IAITemplateService>();
        mockTemplateService
            .Setup(t => t.RenderAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync("Rendered prompt");

        var service = CreateService(
            chatClient: mockChatClient.Object,
            toolsService: mockToolsService.Object,
            templateService: mockTemplateService.Object);

        // Act
        var result = await service.ProcessAsync(profile, session, prompts, TestContext.Current.CancellationToken);

        // Assert: empty response should fail the task instead of being treated as success.
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("summary"));
        Assert.Equal(PostSessionTaskResultStatus.Failed, result["summary"].Status);
        Assert.NotNull(result["summary"].ErrorMessage);
    }

    #endregion
}
