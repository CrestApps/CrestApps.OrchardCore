using CrestApps.Core.Omnichannel.Services;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.Core.Omnichannel.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// Verifies that model-provided subject values are written into the subject's real text-field structure
/// (its <c>Text</c> property), rather than merging a free-form content item whose invented shape the
/// field editors could not read.
/// </summary>
public sealed class ContentItemOmnichannelSubjectAccessorTests
{
    private static readonly List<SubjectFieldDefinition> _fields =
    [
        new() { Name = "LeadGeneration.VehicleInterest" },
        new() { Name = "LeadGeneration.Budget" },
        new() { Name = "LeadGeneration.Timeline" },
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

        var changed = ContentItemOmnichannelSubjectAccessor.ApplyFields(subject, values, _fields);

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

        var changed = ContentItemOmnichannelSubjectAccessor.ApplyFields(subject, values, _fields);

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

        var changed = ContentItemOmnichannelSubjectAccessor.ApplyFields(subject, values, _fields);

        Assert.False(changed);
        Assert.Null(Text(subject, "LeadGeneration", "Budget"));
    }

    [Fact]
    public void ApplySubjectFields_ReturnsFalse_ForNullOrEmptyInputs()
    {
        var subject = new ContentItem { ContentType = "LeadGeneration" };

        Assert.False(ContentItemOmnichannelSubjectAccessor.ApplyFields(subject, null, _fields));
        Assert.False(ContentItemOmnichannelSubjectAccessor.ApplyFields(subject, new Dictionary<string, string>(), _fields));
        Assert.False(ContentItemOmnichannelSubjectAccessor.ApplyFields(subject, new Dictionary<string, string> { ["LeadGeneration.Budget"] = "x" }, []));
    }
}
