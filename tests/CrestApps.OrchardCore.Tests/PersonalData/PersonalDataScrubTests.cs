using System.Text.Json.Nodes;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Tests.PersonalData;

/// <summary>
/// Scrubbing replaces a person's details in free text without touching the references a record holds: ids, user ids
/// and status ids stay as they are, and a name only matches as a whole word.
/// </summary>
public sealed class PersonalDataScrubTests
{
    [Fact]
    public void ScrubJson_NeverTouchesIdsOrValuesUnderIdKeys_AndMatchesWholeWordsOnly()
    {
        // Arrange
        var subject = new PersonalDataSubject(email: "ann@example.com");
        subject.AddName("Ann");

        var content = new JsonObject
        {
            ["ContentItemId"] = "4annq2v8rk3ann0mn1xyz9abcd",
            ["OwnerId"] = "ann",
            ["Company"] = new JsonObject { ["ContentItemIds"] = new JsonArray("4ann000000000000000000000a", "ann") },
            ["Reference"] = "4annq2v8rk3ann0mn1xyz9abce",
            ["Guid"] = "6f2a9c1e-0a1b-4b5e-9d3f-1a2b3c4d5e6f",
            ["Notes"] = "Ann asked Annabel to call ann@example.com. Joanne agreed.",
        };

        // Act
        var changed = subject.ScrubJson(content, "[removed]");

        // Assert
        Assert.True(changed);
        Assert.Equal("4annq2v8rk3ann0mn1xyz9abcd", (string)content["ContentItemId"]);
        Assert.Equal("ann", (string)content["OwnerId"]);
        Assert.Equal("4ann000000000000000000000a", (string)content["Company"]["ContentItemIds"][0]);
        Assert.Equal("ann", (string)content["Company"]["ContentItemIds"][1]);
        Assert.Equal("4annq2v8rk3ann0mn1xyz9abce", (string)content["Reference"]);
        Assert.Equal("6f2a9c1e-0a1b-4b5e-9d3f-1a2b3c4d5e6f", (string)content["Guid"]);
        Assert.Equal("[removed] asked Annabel to call [removed]. Joanne agreed.", (string)content["Notes"]);
    }

    [Fact]
    public void AddName_IgnoresNamesShorterThanThreeCharacters()
    {
        var subject = new PersonalDataSubject(email: "ed@example.com");

        Assert.False(subject.AddName("Ed"));
        Assert.True(subject.AddName("Eda"));
        Assert.Equal("Edward and [removed] met [removed]", subject.Scrub("Edward and Eda met ed@example.com", "[removed]"));
    }

    [Fact]
    public void Scrub_ReplacesOnlyWholeWords()
    {
        var subject = new PersonalDataSubject(email: "lee@example.com");
        subject.AddName("Lee");

        Assert.Equal("[x] and Leeann wrote to [x]", subject.Scrub("Lee and Leeann wrote to lee@example.com", "[x]"));
        Assert.True(subject.IsMentionedIn("ask Lee"));
        Assert.False(subject.IsMentionedIn("ask Leeann"));
    }
}
