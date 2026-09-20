using System.Text.Json;
using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// Pins that an activity's subject can stop being a content item without the stored document changing.
/// </summary>
/// <remarks>
/// <para>
/// The subject is serialized inside the activity document, so changing the property's type changes what
/// is written unless the two render to the same text. It is also the last thing keeping
/// <c>OmnichannelActivity</c> tied to the content model, which is what keeps the model and the eighteen
/// files typed on it out of the framework.
/// </para>
/// <para>
/// The pre-extraction tenant snapshot does not cover this: its manifest carries four Contact Center
/// document types and no activity at all. These tests are the evidence instead, and they measure the
/// two claims the swap rests on - that a content item and the node it serializes to produce identical
/// text in the same slot, and that a host can convert between them without losing anything.
/// </para>
/// </remarks>
public sealed class ActivitySubjectCarrierTests
{
    [Fact]
    public void ASubjectWrittenAsANode_IsTheSameTextAsTheContentItemItCameFrom()
    {
        // Arrange
        var subject = CreateSubject();

        var asContentItem = new ContentItemCarrier { Subject = subject };
        var asNode = new NodeCarrier { Subject = ToNode(subject) };

        // Act
        var contentItemText = JsonSerializer.Serialize(asContentItem, JOptions.Default);
        var nodeText = JsonSerializer.Serialize(asNode, JOptions.Default);

        // Assert
        Assert.Equal(contentItemText, nodeText);
    }

    [Fact]
    public void AnAbsentSubject_IsTheSameTextEitherWay()
    {
        // A null subject is the common case for an activity whose subject type declares no fields.
        var contentItemText = JsonSerializer.Serialize(new ContentItemCarrier(), JOptions.Default);
        var nodeText = JsonSerializer.Serialize(new NodeCarrier(), JOptions.Default);

        Assert.Equal(contentItemText, nodeText);
    }

    [Fact]
    public void ASubjectStoredAsANode_ReadsBackAsTheSameContentItem()
    {
        // Arrange
        var subject = CreateSubject();
        var node = ToNode(subject);

        // Act
        var restored = node.Deserialize<ContentItem>(JOptions.Default);

        // Assert
        Assert.NotNull(restored);
        Assert.Equal(subject.ContentType, restored.ContentType);
        Assert.Equal(subject.ContentItemId, restored.ContentItemId);
        Assert.Equal(subject.DisplayText, restored.DisplayText);
        Assert.Equal(
            JsonSerializer.Serialize(subject, JOptions.Default),
            JsonSerializer.Serialize(restored, JOptions.Default));
    }

    /// <summary>
    /// Pins that the field read the subject writer performs still works against the node.
    /// </summary>
    /// <remarks>
    /// <c>ContentItemActivitySubjectWriter</c> already reads through <c>(JsonObject)activity.Subject.Content</c>,
    /// so what it needs from the carrier is the part-and-field shape, not the content item wrapper.
    /// </remarks>
    [Fact]
    public void TheFieldsTheSubjectWriterReads_AreInTheSamePlaceOnTheNode()
    {
        // Arrange
        var node = ToNode(CreateSubject());

        // Act
        var fromNode = node["SupportCasePart"]?["Summary"]?["Text"]?.ToString();

        // Assert
        Assert.Equal("Cannot sign in", fromNode);
    }

    private static JsonObject ToNode(ContentItem subject)
        => JsonSerializer.SerializeToNode(subject, JOptions.Default).AsObject();

    private static ContentItem CreateSubject()
    {
        var subject = new ContentItem
        {
            ContentType = "SupportCase",
            ContentItemId = "4c3v1p9wq2k8j6h5g0d7s1a2b3",
            DisplayText = "Cannot sign in",
        };

        subject.Content.SupportCasePart = new JsonObject
        {
            ["Summary"] = new JsonObject { ["Text"] = "Cannot sign in" },
            ["Severity"] = new JsonObject { ["Text"] = "High" },
        };

        return subject;
    }

    private sealed class ContentItemCarrier
    {
        public string SubjectContentType { get; set; } = "SupportCase";

        public ContentItem Subject { get; set; }
    }

    private sealed class NodeCarrier
    {
        public string SubjectContentType { get; set; } = "SupportCase";

        public JsonObject Subject { get; set; }
    }
}
