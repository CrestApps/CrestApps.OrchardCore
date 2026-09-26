using CrestApps.Core;
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Documents.Models;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Documents.Handlers;
using CrestApps.OrchardCore.AI.Documents.Services;
using CrestApps.OrchardCore.AI.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Documents;

public sealed class ProfileDocumentsTemplateApplicationHandlerTests
{
    private const string _templateId = "template-1";
    private const string _templateDocumentId = "template-document";
    private const string _profileId = "profile-1";

    [Fact]
    public async Task AppliedAsync_ShouldReplaceTheTemplateDocumentsWithCopiesScopedToTheProfile()
    {
        // Arrange
        var createdDocuments = new List<AIDocument>();
        var createdChunks = new List<AIDocumentChunk>();
        var handler = CreateHandler(createdDocuments, createdChunks);

        var template = CreateTemplateWithDocument();
        var profile = new AIProfile { ItemId = _profileId };

        // Applying the template copies its documents metadata verbatim, references to its documents included.
        AIProfileTemplateApplicator.Apply(profile, template);

        // Act
        await handler.AppliedAsync(new AIProfileTemplateAppliedContext(profile, template));

        // Assert
        var documentsMetadata = profile.GetOrCreate<DocumentsMetadata>();
        var profileDocument = Assert.Single(documentsMetadata.Documents);
        Assert.NotEqual(_templateDocumentId, profileDocument.DocumentId);
        Assert.Equal("faq.md", profileDocument.FileName);
        Assert.Equal(5, documentsMetadata.DocumentTopN);

        var createdDocument = Assert.Single(createdDocuments);
        Assert.Equal(profileDocument.DocumentId, createdDocument.ItemId);
        Assert.Equal(_profileId, createdDocument.ReferenceId);
        Assert.Equal(AIConstants.DocumentReferenceTypes.Profile, createdDocument.ReferenceType);

        var createdChunk = Assert.Single(createdChunks);
        Assert.Equal(createdDocument.ItemId, createdChunk.AIDocumentId);
        Assert.Equal(_profileId, createdChunk.ReferenceId);
        Assert.Equal(AIConstants.DocumentReferenceTypes.Profile, createdChunk.ReferenceType);
        Assert.Equal("What are your hours?", createdChunk.Content);
    }

    [Fact]
    public async Task AppliedAsync_ShouldLeaveTheTemplateDocumentsUntouched()
    {
        // Arrange
        var handler = CreateHandler([], []);
        var template = CreateTemplateWithDocument();
        var profile = new AIProfile { ItemId = _profileId };

        AIProfileTemplateApplicator.Apply(profile, template);

        // Act
        await handler.AppliedAsync(new AIProfileTemplateAppliedContext(profile, template));

        // Assert
        var templateDocument = Assert.Single(template.GetOrCreate<DocumentsMetadata>().Documents);
        Assert.Equal(_templateDocumentId, templateDocument.DocumentId);
    }

    [Fact]
    public async Task AppliedAsync_WhenTheTemplateHasNoDocuments_ShouldNotTouchTheStores()
    {
        // Arrange
        var documentStore = new Mock<IAIDocumentStore>(MockBehavior.Strict);
        var chunkStore = new Mock<IAIDocumentChunkStore>(MockBehavior.Strict);
        var handler = new ProfileDocumentsTemplateApplicationHandler(new ProfileTemplateDocumentCloner(documentStore.Object, chunkStore.Object));

        var template = new AIProfileTemplate { ItemId = _templateId, Name = "no-documents" };
        var profile = new AIProfile { ItemId = _profileId };

        AIProfileTemplateApplicator.Apply(profile, template);

        // Act
        await handler.AppliedAsync(new AIProfileTemplateAppliedContext(profile, template));

        // Assert
        Assert.False(profile.Has<DocumentsMetadata>());
    }

    private static AIProfileTemplate CreateTemplateWithDocument()
    {
        var template = new AIProfileTemplate
        {
            ItemId = _templateId,
            Name = "docs-assistant",
        };

        template.Put(new DocumentsMetadata
        {
            DocumentTopN = 5,
            Documents =
            [
                new ChatDocumentInfo
                {
                    DocumentId = _templateDocumentId,
                    FileName = "faq.md",
                    ContentType = "text/markdown",
                    FileSize = 42,
                },
            ],
        });

        return template;
    }

    private static ProfileDocumentsTemplateApplicationHandler CreateHandler(List<AIDocument> createdDocuments, List<AIDocumentChunk> createdChunks)
    {
        var templateDocument = new AIDocument
        {
            ItemId = _templateDocumentId,
            ReferenceId = _templateId,
            ReferenceType = AIConstants.DocumentReferenceTypes.ProfileTemplate,
            FileName = "faq.md",
            ContentType = "text/markdown",
            FileSize = 42,
        };

        var templateChunk = new AIDocumentChunk
        {
            ItemId = "template-chunk",
            AIDocumentId = _templateDocumentId,
            ReferenceId = _templateId,
            ReferenceType = AIConstants.DocumentReferenceTypes.ProfileTemplate,
            Content = "What are your hours?",
            Embedding = [0.1f, 0.2f],
            Index = 0,
        };

        var documentStore = new Mock<IAIDocumentStore>();
        documentStore
            .Setup(store => store.FindByIdAsync(_templateDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templateDocument);
        documentStore
            .Setup(store => store.CreateAsync(It.IsAny<AIDocument>(), It.IsAny<CancellationToken>()))
            .Callback<AIDocument, CancellationToken>((document, _) => createdDocuments.Add(document))
            .Returns(ValueTask.CompletedTask);

        var chunkStore = new Mock<IAIDocumentChunkStore>();
        chunkStore
            .Setup(store => store.GetChunksByAIDocumentIdAsync(_templateDocumentId))
            .ReturnsAsync(new List<AIDocumentChunk> { templateChunk });
        chunkStore
            .Setup(store => store.CreateAsync(It.IsAny<AIDocumentChunk>(), It.IsAny<CancellationToken>()))
            .Callback<AIDocumentChunk, CancellationToken>((chunk, _) => createdChunks.Add(chunk))
            .Returns(ValueTask.CompletedTask);

        return new ProfileDocumentsTemplateApplicationHandler(new ProfileTemplateDocumentCloner(documentStore.Object, chunkStore.Object));
    }
}
