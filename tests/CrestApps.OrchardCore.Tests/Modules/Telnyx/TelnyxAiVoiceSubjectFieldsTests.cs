using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Telnyx.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.Telnyx;

/// <summary>
/// Verifies the automated AI voice conclusion writes model-provided subject values into the subject's real
/// TextField structure (its <c>Text</c> property), rather than merging a free-form content item whose invented
/// shape the field editors could not read.
/// </summary>
public sealed class TelnyxAiVoiceSubjectFieldsTests
{
    private static readonly List<(string Part, string Field)> _fields =
    [
        ("LeadGeneration", "VehicleInterest"),
        ("LeadGeneration", "Budget"),
        ("LeadGeneration", "Timeline"),
    ];

    private static string Text(ContentItem item, string part, string field)
        => (((JsonObject)item.Content)[part]?[field]?["Text"])?.ToString();

    [Fact]
    public void ApplySubjectFields_WritesTextFieldStructure_ForKnownFields()
    {
        var subject = new ContentItem { ContentType = "LeadGeneration" };
        var values = new Dictionary<string, string>
        {
            ["LeadGeneration.VehicleInterest"] = "used work van",
            ["LeadGeneration.Budget"] = "$30,000",
            ["LeadGeneration.Timeline"] = "this month",
        };

        var changed = TelnyxAiVoiceConversationHandler.ApplySubjectFields(subject, values, _fields);

        Assert.True(changed);

        // Each value must land on the field's Text property, which is the exact structure the TextField editor reads.
        Assert.Equal("used work van", Text(subject, "LeadGeneration", "VehicleInterest"));
        Assert.Equal("$30,000", Text(subject, "LeadGeneration", "Budget"));
        Assert.Equal("this month", Text(subject, "LeadGeneration", "Timeline"));
    }

    [Fact]
    public void ApplySubjectFields_AcceptsBareFieldNameKeys()
    {
        var subject = new ContentItem { ContentType = "LeadGeneration" };
        var values = new Dictionary<string, string> { ["VehicleInterest"] = "sedan" };

        var changed = TelnyxAiVoiceConversationHandler.ApplySubjectFields(subject, values, _fields);

        Assert.True(changed);
        Assert.Equal("sedan", Text(subject, "LeadGeneration", "VehicleInterest"));
    }

    [Fact]
    public void ApplySubjectFields_IgnoresUnknownKeysAndEmptyValues()
    {
        var subject = new ContentItem { ContentType = "LeadGeneration" };
        var values = new Dictionary<string, string>
        {
            ["LeadGeneration.NotAField"] = "ignored",
            ["LeadGeneration.Budget"] = "   ",
        };

        var changed = TelnyxAiVoiceConversationHandler.ApplySubjectFields(subject, values, _fields);

        Assert.False(changed);
        Assert.Null(Text(subject, "LeadGeneration", "Budget"));
    }

    [Fact]
    public void ApplySubjectFields_ReturnsFalse_ForNullOrEmptyInputs()
    {
        var subject = new ContentItem { ContentType = "LeadGeneration" };

        Assert.False(TelnyxAiVoiceConversationHandler.ApplySubjectFields(subject, null, _fields));
        Assert.False(TelnyxAiVoiceConversationHandler.ApplySubjectFields(subject, new Dictionary<string, string>(), _fields));
        Assert.False(TelnyxAiVoiceConversationHandler.ApplySubjectFields(subject, new Dictionary<string, string> { ["LeadGeneration.Budget"] = "x" }, []));
    }
}
