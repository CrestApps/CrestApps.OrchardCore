using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Surveys.Controls;

/// <summary>
/// A response with every answer shape round-trips through System.Text.Json, as YesSql stores it (phase 0 check C8).
/// </summary>
public sealed class SurveyAnswerStorageTests
{
    [Fact]
    public void Response_RoundTrip_KeepsEveryAnswerShape()
    {
        var response = new SurveyResponse
        {
            ItemId = "r1",
            Answers = new()
            {
                ["choice"] = new SurveyAnswer { Values = ["a", "b"], OtherText = "other" },
                ["number"] = new SurveyAnswer { Number = 12.5m },
                ["date"] = new SurveyAnswer { Date = new DateTime(2026, 1, 2, 3, 4, 5) },
                ["rows"] = new SurveyAnswer { Rows = new() { ["r1"] = ["x"] } },
                ["files"] = new SurveyAnswer { AttachmentIds = ["f1"] },
                ["extra"] = new SurveyAnswer { Extra = new JsonObject { ["DataUrl"] = "data:image/png;base64,AA==" } },
            },
        };

        var json = JsonSerializer.Serialize(response);
        var copy = JsonSerializer.Deserialize<SurveyResponse>(json);

        Assert.Equal(["a", "b"], copy.Answers["choice"].Values);
        Assert.Equal("other", copy.Answers["choice"].OtherText);
        Assert.Equal(12.5m, copy.Answers["number"].Number);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), copy.Answers["date"].Date);
        Assert.Equal(["x"], copy.Answers["rows"].Rows["r1"]);
        Assert.Equal(["f1"], copy.Answers["files"].AttachmentIds);
        Assert.Equal("data:image/png;base64,AA==", copy.Answers["extra"].Extra["DataUrl"]!.GetValue<string>());
    }
}
