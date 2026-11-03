using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.FileStorage;
using OrchardCore.Infrastructure;
using OrchardCore.Media;
using OrchardCore.Media.Fields;
using OrchardCore.Media.Settings;
using OrchardCore.Security;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Agent.Contents;

public sealed class AttachDocumentToContentItemToolTests
{
    private const string InteractionId = "interaction-1";
    private const string SessionId = "session-1";
    private const string ContentItemId = "item-1";

    [Fact]
    public async Task InvokeAsync_WithMissingContentItemId_ShouldReturnErrorMessage()
    {
        var fixture = new Fixture();

        var result = await fixture.InvokeAsync(new Dictionary<string, object>());

        Assert.Contains("contentItemId", result);
    }

    [Fact]
    public async Task InvokeAsync_WithoutConversation_ShouldReportNoDocuments()
    {
        var fixture = new Fixture();

        var result = await fixture.InvokeAsync(new Dictionary<string, object>
        {
            ["contentItemId"] = ContentItemId,
        }, withConversation: false);

        Assert.Contains("No documents", result);
        fixture.VerifyNothingWritten();
    }

    [Fact]
    public async Task InvokeAsync_WithDocumentOutsideTheConversation_ShouldRefuse()
    {
        var fixture = new Fixture();

        var result = await fixture.InvokeAsync(new Dictionary<string, object>
        {
            ["contentItemId"] = ContentItemId,
            ["documentId"] = "document-of-another-user",
        });

        Assert.Contains("No document with the id 'document-of-another-user'", result);
        fixture.VerifyNothingWritten();
    }

    [Fact]
    public async Task InvokeAsync_WithSeveralMediaFieldsAndNoneNamed_ShouldListTheFields()
    {
        var fixture = new Fixture();
        fixture.AddMediaField("Images", new MediaFieldSettings());

        var result = await fixture.InvokeAsync(new Dictionary<string, object>
        {
            ["contentItemId"] = ContentItemId,
        });

        Assert.Contains("Article.Attachments", result);
        Assert.Contains("Article.Images", result);
        fixture.VerifyNothingWritten();
    }

    [Fact]
    public async Task InvokeAsync_WithExtensionTheFieldDoesNotAccept_ShouldRefuse()
    {
        var fixture = new Fixture(new MediaFieldSettings { AllowedExtensions = [".jpg"] });

        var result = await fixture.InvokeAsync(new Dictionary<string, object>
        {
            ["contentItemId"] = ContentItemId,
        });

        Assert.Contains("only accepts .jpg files", result);
        fixture.VerifyNothingWritten();
    }

    [Fact]
    public async Task InvokeAsync_WhenTheUserMayNotEditTheItem_ShouldRefuse()
    {
        var fixture = new Fixture();
        fixture.User = new ClaimsPrincipal(new ClaimsIdentity("Test"));

        var result = await fixture.InvokeAsync(new Dictionary<string, object>
        {
            ["contentItemId"] = ContentItemId,
        });

        Assert.Contains("not allowed", result);
        fixture.VerifyNothingWritten();
    }

    [Fact]
    public async Task InvokeAsync_WhenTheUserMayEditTheItemAndManageMedia_ShouldAttach()
    {
        var fixture = new Fixture();
        fixture.User = new ClaimsPrincipal(new ClaimsIdentity("Test"));
        fixture.Grants = ["EditContent", "ManageMediaContent", "ManageMediaFolder"];

        var result = await fixture.InvokeAsync(new Dictionary<string, object>
        {
            ["contentItemId"] = ContentItemId,
        });

        Assert.Contains("was attached", result);
        fixture.MediaFileStore.Verify(store => store.CreateFileFromStreamAsync("mediafields/Article/item-1/handbook.pdf", It.IsAny<Stream>(), false), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenAFileHandlerRefusesTheFile_ShouldNotWriteIt()
    {
        var fixture = new Fixture();
        var handler = new Mock<IFileEventHandler>();
        handler
            .Setup(h => h.CreatingAsync(It.IsAny<FileCreatingContext>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FileCreatingResult.Failed(null, new ResultError { Message = new LocalizedString("Infected", "The file is infected.") }));
        fixture.FileEventHandlers.Add(handler.Object);

        var result = await fixture.InvokeAsync(new Dictionary<string, object>
        {
            ["contentItemId"] = ContentItemId,
        });

        Assert.Contains("The file is infected.", result);
        fixture.VerifyNothingWritten();
    }

    [Fact]
    public async Task InvokeAsync_InAProfileChat_ShouldAttachADocumentOfTheChatSession()
    {
        var fixture = new Fixture();

        var result = await fixture.InvokeAsync(new Dictionary<string, object>
        {
            ["contentItemId"] = ContentItemId,
            ["documentId"] = "session-document",
        }, inProfileChat: true);

        Assert.Contains("'notes.pdf' was attached", result);
        fixture.MediaFileStore.Verify(store => store.CreateFileFromStreamAsync("mediafields/Article/item-1/notes.pdf", It.IsAny<Stream>(), false), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithTheAttachedEditor_ShouldKeepTheFileNamesAlignedWithThePaths()
    {
        var fixture = new Fixture(new MediaFieldSettings(), editor: "Attached");
        fixture.SetAttachments(["mediafields/Article/item-1/0f3a.pdf"], ["Old"]);

        await fixture.InvokeAsync(new Dictionary<string, object>
        {
            ["contentItemId"] = ContentItemId,
        });

        var field = fixture.ContentItem.Get<ContentPart>("Article").Get<MediaField>("Attachments");

        Assert.Equal(2, field.Paths.Length);
        Assert.Equal([string.Empty, "handbook.pdf"], ((JsonArray)field.Content["AttachedFileNames"]).ToObject<string[]>());
    }

    [Fact]
    public async Task InvokeAsync_WithSingleFileFieldThatHasAFile_ShouldAskBeforeReplacing()
    {
        var fixture = new Fixture(new MediaFieldSettings { Multiple = false });
        fixture.SetAttachments(["mediafields/Article/item-1/old.pdf"], []);

        var result = await fixture.InvokeAsync(new Dictionary<string, object>
        {
            ["contentItemId"] = ContentItemId,
        });

        Assert.Contains("replaceExisting", result);
        fixture.VerifyNothingWritten();
    }

    [Fact]
    public async Task InvokeAsync_WithPublishedItem_ShouldAddTheFileAndPublish()
    {
        var fixture = new Fixture();
        fixture.SetAttachments(["mediafields/Article/item-1/old.pdf"], []);

        var result = await fixture.InvokeAsync(new Dictionary<string, object>
        {
            ["contentItemId"] = ContentItemId,
            ["mediaText"] = "The handbook",
        });

        Assert.Contains("was attached", result);

        var field = fixture.ContentItem.Get<ContentPart>("Article").Get<MediaField>("Attachments");

        Assert.Equal(["mediafields/Article/item-1/old.pdf", "mediafields/Article/item-1/handbook.pdf"], field.Paths);
        Assert.Equal([string.Empty, "The handbook"], field.MediaTexts);

        fixture.MediaFileStore.Verify(store => store.CreateFileFromStreamAsync("mediafields/Article/item-1/handbook.pdf", It.IsAny<Stream>(), false), Times.Once);
        fixture.ContentManager.Verify(manager => manager.PublishAsync(fixture.ContentItem), Times.Once);
        fixture.ContentManager.Verify(manager => manager.SaveDraftAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithItemThatHasAnUnpublishedDraft_ShouldKeepItADraft()
    {
        var fixture = new Fixture();
        fixture.ContentItem.Published = false;

        var result = await fixture.InvokeAsync(new Dictionary<string, object>
        {
            ["contentItemId"] = ContentItemId,
        });

        Assert.Contains("saved as a draft", result);
        fixture.ContentManager.Verify(manager => manager.SaveDraftAsync(fixture.ContentItem), Times.Once);
        fixture.ContentManager.Verify(manager => manager.PublishAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheFileNameIsTaken_ShouldNotOverwriteIt()
    {
        var fixture = new Fixture();
        fixture.ExistingMediaPaths.Add("mediafields/Article/item-1/handbook.pdf");

        await fixture.InvokeAsync(new Dictionary<string, object>
        {
            ["contentItemId"] = ContentItemId,
        });

        fixture.MediaFileStore.Verify(store => store.CreateFileFromStreamAsync("mediafields/Article/item-1/handbook-1.pdf", It.IsAny<Stream>(), false), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheItemIsNotValid_ShouldRemoveTheFileAndKeepTheField()
    {
        var fixture = new Fixture();
        fixture.SetAttachments(["mediafields/Article/item-1/old.pdf"], ["Old"]);
        fixture.ValidationResult.Fail(new System.ComponentModel.DataAnnotations.ValidationResult("The title is required."));

        var result = await fixture.InvokeAsync(new Dictionary<string, object>
        {
            ["contentItemId"] = ContentItemId,
        });

        Assert.Contains("The title is required.", result);
        Assert.Equal(["mediafields/Article/item-1/old.pdf"], fixture.ContentItem.Get<ContentPart>("Article").Get<MediaField>("Attachments").Paths);
        fixture.MediaFileStore.Verify(store => store.TryDeleteFileAsync("mediafields/Article/item-1/handbook.pdf"), Times.Once);
        fixture.ContentManager.Verify(manager => manager.PublishAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WhenSavingTheItemThrows_ShouldRemoveTheFile()
    {
        var fixture = new Fixture();
        fixture.ContentManager
            .Setup(manager => manager.GetAsync(ContentItemId, It.Is<VersionOptions>(options => options.IsDraftRequired)))
            .ThrowsAsync(new InvalidOperationException("An object with the same identity is already part of this transaction."));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.InvokeAsync(new Dictionary<string, object>
        {
            ["contentItemId"] = ContentItemId,
        }));

        fixture.MediaFileStore.Verify(store => store.TryDeleteFileAsync("mediafields/Article/item-1/handbook.pdf"), Times.Once);
    }

    private sealed class Fixture
    {
        private readonly List<ContentPartFieldDefinition> _fields = [];

        public Fixture(MediaFieldSettings attachmentsSettings = null, string editor = null)
        {
            AddMediaField("Attachments", attachmentsSettings ?? new MediaFieldSettings(), editor);

            ContentItem = new ContentItem
            {
                ContentItemId = ContentItemId,
                ContentType = "Article",
                Published = true,
                Latest = true,
            };

            ContentManager
                .Setup(manager => manager.GetAsync(ContentItemId, It.IsAny<VersionOptions>()))
                .ReturnsAsync(() => ContentItem);
            ContentManager
                .Setup(manager => manager.ValidateAsync(It.IsAny<ContentItem>()))
                .ReturnsAsync(() => ValidationResult);

            DocumentStore
                .Setup(store => store.GetDocumentsAsync(InteractionId, AIConstants.DocumentReferenceTypes.ChatInteraction))
                .ReturnsAsync(
                [
                    new AIDocument
                    {
                        ItemId = "document-1",
                        FileName = "handbook.pdf",
                        StoredFilePath = "chat-interaction/interaction-1/handbook.pdf",
                        FileSize = 1024,
                    },
                ]);

            DocumentStore
                .Setup(store => store.GetDocumentsAsync(SessionId, AIConstants.DocumentReferenceTypes.ChatSession))
                .ReturnsAsync(
                [
                    new AIDocument
                    {
                        ItemId = "session-document",
                        FileName = "notes.pdf",
                        StoredFilePath = "chat-session/session-1/notes.pdf",
                        FileSize = 2048,
                    },
                ]);

            DocumentFileStore
                .Setup(store => store.GetFileAsync(It.IsAny<string>()))
                .ReturnsAsync(() => new MemoryStream([1, 2, 3]));

            MediaFileStore
                .Setup(store => store.GetFileInfoAsync(It.IsAny<string>()))
                .ReturnsAsync((string path) => ExistingMediaPaths.Contains(path) ? Mock.Of<IFileStoreEntry>() : null);
            MediaFileStore
                .Setup(store => store.CreateFileFromStreamAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()))
                .ReturnsAsync((string path, Stream _, bool _) =>
                {
                    ExistingMediaPaths.Add(path);

                    return path;
                });
        }

        public ContentItem ContentItem { get; }

        public ContentValidateResult ValidationResult { get; } = new();

        public ClaimsPrincipal User { get; set; }

        public string[] Grants { get; set; } = [];

        public HashSet<string> ExistingMediaPaths { get; } = [];

        public List<IFileEventHandler> FileEventHandlers { get; } = [];

        public Mock<IContentManager> ContentManager { get; } = new();

        public Mock<IAIDocumentStore> DocumentStore { get; } = new();

        public Mock<IDocumentFileStore> DocumentFileStore { get; } = new();

        public Mock<IMediaFileStore> MediaFileStore { get; } = new();

        public void AddMediaField(string name, MediaFieldSettings settings, string editor = null)
        {
            _fields.Add(new ContentPartFieldDefinition(
                new ContentFieldDefinition(nameof(MediaField)),
                name,
                new JsonObject
                {
                    [nameof(MediaFieldSettings)] = JsonSerializer.SerializeToNode(settings),
                    [nameof(ContentPartFieldSettings)] = JsonSerializer.SerializeToNode(new ContentPartFieldSettings { Editor = editor }),
                }));
        }

        public void SetAttachments(string[] paths, string[] texts)
        {
            ContentItem.Alter<ContentPart>("Article", part => part.Alter<MediaField>("Attachments", field =>
            {
                field.Paths = paths;
                field.MediaTexts = texts;
            }));
        }

        public async Task<string> InvokeAsync(Dictionary<string, object> values, bool withConversation = true, bool inProfileChat = false)
        {
            using var scope = AIInvocationScope.Begin();

            if (inProfileChat)
            {
                AIInvocationScope.Current.ToolExecutionContext = new AIToolExecutionContext(new AIProfile
                {
                    ItemId = "profile-1",
                });
                AIInvocationScope.Current.Items[nameof(AIChatSession)] = new AIChatSession
                {
                    SessionId = SessionId,
                };
            }
            else if (withConversation)
            {
                AIInvocationScope.Current.ToolExecutionContext = new AIToolExecutionContext(new ChatInteraction
                {
                    ItemId = InteractionId,
                });
            }

            var arguments = new AIFunctionArguments(values)
            {
                Services = BuildServices(),
            };

            var result = await new AttachDocumentToContentItemTool().InvokeAsync(arguments, TestContext.Current.CancellationToken);

            return result?.ToString();
        }

        public void VerifyNothingWritten()
        {
            MediaFileStore.Verify(store => store.CreateFileFromStreamAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()), Times.Never);
            ContentManager.Verify(manager => manager.UpdateAsync(It.IsAny<ContentItem>()), Times.Never);
        }

        private ServiceProvider BuildServices()
        {
            var typeDefinition = new ContentTypeDefinition(
                "Article",
                "Article",
                [new ContentTypePartDefinition("Article", new ContentPartDefinition("Article", _fields, []), [])],
                []);

            var contentDefinitionManager = new Mock<IContentDefinitionManager>();
            contentDefinitionManager
                .Setup(manager => manager.GetTypeDefinitionAsync("Article"))
                .ReturnsAsync(typeDefinition);

            var authorizationService = new Mock<IAuthorizationService>();
            authorizationService
                .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                    requirements.OfType<PermissionRequirement>().All(requirement => Grants.Contains(requirement.Permission.Name))
                        ? AuthorizationResult.Success()
                        : AuthorizationResult.Failed());

            var userAccessor = new Mock<IUserAccessor>();
            userAccessor.Setup(accessor => accessor.User).Returns(() => User);

            var services = new ServiceCollection();
            services.AddSingleton<ILogger<AttachDocumentToContentItemTool>>(NullLogger<AttachDocumentToContentItemTool>.Instance);
            services.AddSingleton(ContentManager.Object);
            services.AddSingleton(contentDefinitionManager.Object);
            services.AddSingleton(DocumentStore.Object);
            services.AddSingleton(DocumentFileStore.Object);
            services.AddSingleton(MediaFileStore.Object);
            services.AddSingleton(authorizationService.Object);
            services.AddSingleton(userAccessor.Object);
            services.AddSingleton(Options.Create(new MediaOptions
            {
                AllowedFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".jpg" },
                MaxFileSize = 30_000_000,
            }));
            services.AddSingleton(new Mock<ISession>().Object);
            services.AddSingleton(new FileCreationService(FileEventHandlers));

            return services.BuildServiceProvider();
        }
    }
}
