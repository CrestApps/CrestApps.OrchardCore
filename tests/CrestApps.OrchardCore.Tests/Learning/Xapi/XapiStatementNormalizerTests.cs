using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi.Core;

namespace CrestApps.OrchardCore.Tests.Learning.Xapi;

public sealed class XapiStatementNormalizerTests
{
    [Fact]
    public void Statements_DifferingOnlyInWhatTheLrsSets_AreEquivalent()
    {
        var id = XapiSamples.NewId();
        var sent = XapiSamples.Full(id);
        var stored = XapiSamples.Full(id.ToUpperInvariant());

        stored["stored"] = "2026-11-02T12:00:00.000Z";
        stored["authority"] = XapiSamples.Agent("mailto:lrs@example.com");
        stored["version"] = "1.0.0";
        stored["timestamp"] = "2026-11-02T12:00:00.000Z";
        stored["actor"].AsObject().Remove("objectType");
        stored["context"]["contextActivities"]["parent"] = new JsonArray(new JsonObject { ["id"] = "http://www.example.com/meetings/series/267" });
        stored["context"]["registration"] = XapiSamples.Registration.ToUpperInvariant();
        stored["result"]["score"]["raw"] = JsonNode.Parse("95.0");

        Assert.True(XapiStatementNormalizer.AreEquivalent(sent, stored));
    }

    [Fact]
    public void Statements_WithAnotherResult_AreNotEquivalent()
    {
        var id = XapiSamples.NewId();
        var first = XapiSamples.Full(id);
        var second = XapiSamples.Full(id);

        second["result"]["success"] = false;

        Assert.False(XapiStatementNormalizer.AreEquivalent(first, second));
    }

    [Fact]
    public void Normalize_PutsContextActivitiesInArrays_AndIdsInLowerCase()
    {
        var statement = XapiSamples.Full(XapiSamples.NewId().ToUpperInvariant());

        XapiStatementNormalizer.Normalize(statement);

        Assert.IsType<JsonArray>(statement["context"]["contextActivities"]["parent"]);
        Assert.Equal(statement["id"].GetValue<string>().ToLowerInvariant(), statement["id"].GetValue<string>());
    }

    [Fact]
    public void IdsFormat_KeepsOnlyWhatIdentifies()
    {
        var statement = XapiSamples.Full();

        var formatted = XapiStatementNormalizer.Format(statement, "ids", []);

        Assert.Equal("mailto:learner@example.com", formatted["actor"]["mbox"].GetValue<string>());
        Assert.Null(formatted["actor"]["name"]);
        Assert.Null(formatted["object"]["definition"]);
        Assert.Equal(XapiSamples.Activity, formatted["object"]["id"].GetValue<string>());
        Assert.Null(formatted["verb"]["display"]);
        Assert.Null(formatted["context"]["instructor"]["name"]);

        // The statement given is not changed.
        Assert.NotNull(statement["object"]["definition"]);
    }

    [Fact]
    public void CanonicalFormat_KeepsOneLanguage_TheBestTheClientAccepts()
    {
        var statement = XapiSamples.Simple();

        var french = XapiStatementNormalizer.Format(statement, "canonical", ["fr-FR", "en-US"]);
        var other = XapiStatementNormalizer.Format(statement, "canonical", ["de-DE"]);

        Assert.Equal(["fr-FR"], french["verb"]["display"].AsObject().Select(pair => pair.Key));
        Assert.Equal("Réunion", french["object"]["definition"]["name"]["fr-FR"].GetValue<string>());
        Assert.Single(other["verb"]["display"].AsObject());
    }

    [Fact]
    public void Keys_HoldDirectAndRelatedAgentsAndActivities()
    {
        var statement = XapiSamples.Full();

        statement["authority"] = XapiSamples.Agent("mailto:lrs@example.com");

        var keys = XapiStatementNormalizer.BuildKeys(statement);

        Assert.Contains(keys.Agents, key => key.Value == "mbox:mailto:learner@example.com" && key.Direct);
        Assert.Contains(keys.Agents, key => key.Value == "mbox:mailto:instructor@example.com" && !key.Direct);
        Assert.Contains(keys.Agents, key => key.Value == "mbox:mailto:teampb@example.com" && !key.Direct);
        Assert.Contains(keys.Agents, key => key.Value == "mbox:mailto:lrs@example.com" && !key.Direct);
        Assert.Contains(keys.Activities, key => key.Value == XapiSamples.Activity && key.Direct);
        Assert.Contains(keys.Activities, key => key.Value == "http://www.example.com/meetings/series/267" && !key.Direct);
        Assert.Equal([XapiSamples.Completed], keys.Verbs);
        Assert.Equal([XapiSamples.Registration], keys.Registrations);
    }

    [Fact]
    public void AgentKeys_AreTheIdentifier()
    {
        Assert.Equal("mbox:mailto:a@example.com", XapiAgents.Key(JsonNode.Parse("""{ "mbox": "mailto:a@example.com", "name": "A" }""")));
        Assert.Equal("account:https://site.example.com|u1", XapiAgents.Key(JsonNode.Parse("""{ "account": { "homePage": "https://site.example.com", "name": "u1" } }""")));
        Assert.Equal(XapiAgents.AccountKey("https://site.example.com", "u1"), XapiAgents.Key(XapiAgents.Account("https://site.example.com", "u1", "Learner")));
        Assert.Null(XapiAgents.Key(JsonNode.Parse("""{ "objectType": "Group", "member": [] }""")));
        Assert.Equal(64, XapiAgents.Hash("mbox:mailto:a@example.com").Length);
    }
}
