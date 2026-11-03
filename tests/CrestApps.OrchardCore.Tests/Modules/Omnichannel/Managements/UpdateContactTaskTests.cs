using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// Bug: a workflow texted a customer who did not answer, then marked the contact as texted with Orchard Core's Update
/// Content Item task. That task validates the whole record, and a contact loaded without a field its type requires (a
/// last name) failed: the task cancelled the workflow's session, so the text went out and nothing was recorded, and the
/// next unanswered call texted the customer again. Reproduced on a copy of the dev tenant. Update Contact sets only the
/// fields it names and never validates the record.
/// </summary>
public sealed class UpdateContactTaskTests
{
    private const string MarkTexted = """{ "Customer": { "NoAnswerTextSent": { "Value": true } } }""";

    [Fact]
    public async Task SetsTheNamedField_WithoutValidatingTheRecord()
    {
        // Arrange
        var contact = CreateContact(published: true);
        var contentManager = CreateContentManager(contact);
        var task = CreateTask(contentManager.Object, "contact-1", MarkTexted);
        var workflow = CreateWorkflowContext();

        // Act
        var result = await task.ExecuteAsync(workflow, null!);

        // Assert
        Assert.Contains("Done", result.Outcomes);
        Assert.Equal("contact-1", workflow.LastResult);
        Assert.True((bool)ReadPart(contact)["NoAnswerTextSent"]!["Value"]!);
        Assert.Null(ReadPart(contact)["LastName"]!["Text"]);
        contentManager.Verify(manager => manager.UpdateAsync(contact), Times.Once);
        contentManager.Verify(manager => manager.ValidateAsync(It.IsAny<ContentItem>()), Times.Never);
        contentManager.Verify(manager => manager.PublishAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    [Fact]
    public async Task PublishesALatestDraft_WhenAsked()
    {
        // Arrange
        var contact = CreateContact(published: false);
        var contentManager = CreateContentManager(contact);
        var task = CreateTask(contentManager.Object, "contact-1", MarkTexted);

        // Act
        var result = await task.ExecuteAsync(CreateWorkflowContext(), null!);

        // Assert
        Assert.Contains("Done", result.Outcomes);
        contentManager.Verify(manager => manager.PublishAsync(contact), Times.Once);
    }

    [Theory]
    [InlineData("", MarkTexted)]
    [InlineData("missing", MarkTexted)]
    [InlineData("contact-1", "not json")]
    [InlineData("contact-1", "[1, 2]")]
    public async Task ChangesNothing_WhenTheContactOrTheFieldsCannotBeResolved(string contactId, string properties)
    {
        // Arrange
        var contact = CreateContact(published: true);
        var contentManager = CreateContentManager(contact);
        var task = CreateTask(contentManager.Object, contactId, properties);

        // Act
        var result = await task.ExecuteAsync(CreateWorkflowContext(), null!);

        // Assert
        Assert.Contains("Failed", result.Outcomes);
        contentManager.Verify(manager => manager.UpdateAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    private static ContentItem CreateContact(bool published)
    {
        var contact = new ContentItem
        {
            ContentItemId = "contact-1",
            ContentType = "Customer",
            Published = published,
            Latest = true,
        };

        // Loaded from a file without the last name the type requires.
        contact.Merge(JsonNode.Parse("""{ "Customer": { "FirstName": { "Text": "Jane" }, "LastName": { "Text": null }, "NoAnswerTextSent": { "Value": false } } }"""));

        return contact;
    }

    private static JsonObject ReadPart(ContentItem contact)
        => JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(contact))!["Customer"]!.AsObject();

    private static Mock<IContentManager> CreateContentManager(ContentItem contact)
    {
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(manager => manager.GetAsync("contact-1", It.IsAny<VersionOptions>()))
            .ReturnsAsync(contact);

        return contentManager;
    }

    // Each field's expression is its value, so the test reads as the values the task resolved.
    private static UpdateContactTask CreateTask(IContentManager contentManager, string contactId, string properties)
    {
        var evaluator = new Mock<IWorkflowExpressionEvaluator>();
        evaluator
            .Setup(service => service.EvaluateAsync(It.IsAny<WorkflowExpression<string>>(), It.IsAny<WorkflowExecutionContext>(), It.IsAny<TextEncoder>()))
            .Returns((WorkflowExpression<string> expression, WorkflowExecutionContext _, TextEncoder _) => Task.FromResult(expression.Expression));

        return new UpdateContactTask(
            contentManager,
            evaluator.Object,
            NullLogger<UpdateContactTask>.Instance,
            new PassThroughStringLocalizer<UpdateContactTask>())
        {
            ContactContentItemId = contactId,
            ContentProperties = properties,
        };
    }

    private static WorkflowExecutionContext CreateWorkflowContext()
        => new(
            new WorkflowType(),
            new Workflow(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            [],
            null,
            []);
}
