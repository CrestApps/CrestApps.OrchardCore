using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi.Core.Validation;

namespace CrestApps.OrchardCore.Tests.Learning.Xapi;

public sealed class XapiStatementValidatorTests
{
    public static TheoryData<string> ValidSamples => ["simple", "full", "substatement", "group", "interaction", "voiding"];

    [Theory]
    [MemberData(nameof(ValidSamples))]
    public void SpecificationSamples_AreValid(string sample)
    {
        var statement = sample switch
        {
            "simple" => XapiSamples.Simple(XapiSamples.NewId()),
            "full" => XapiSamples.Full(),
            "substatement" => XapiSamples.WithSubStatement(),
            "group" => XapiSamples.WithAnonymousGroup(),
            "interaction" => XapiSamples.WithChoiceInteraction(),
            _ => XapiSamples.Voiding(XapiSamples.NewId()),
        };

        Assert.Null(XapiStatementValidator.Validate(statement));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("verb")]
    [InlineData("object")]
    public void RequiredProperties_AreRequired(string property)
    {
        var statement = XapiSamples.Simple();

        statement.Remove(property);

        Assert.NotNull(XapiStatementValidator.Validate(statement));
    }

    [Fact]
    public void UnknownProperties_AndNullValues_AreRefused()
    {
        var unknown = XapiSamples.Simple();
        var nulled = XapiSamples.Simple();
        var wrongCase = XapiSamples.Simple();

        unknown["foo"] = "bar";
        nulled["result"] = null;
        wrongCase["Actor"] = wrongCase["actor"].DeepClone();
        wrongCase.Remove("actor");

        Assert.NotNull(XapiStatementValidator.Validate(unknown));
        Assert.NotNull(XapiStatementValidator.Validate(nulled));
        Assert.NotNull(XapiStatementValidator.Validate(wrongCase));
    }

    [Fact]
    public void StatementId_MustBeAUuid()
        => Assert.NotNull(XapiStatementValidator.Validate(XapiSamples.Simple("not-a-uuid")));

    [Theory]
    [InlineData("""{ "mbox": "mailto:a@example.com", "openid": "http://openid.example.com/a" }""")]
    [InlineData("""{ "name": "No identifier" }""")]
    [InlineData("""{ "mbox": "a@example.com" }""")]
    [InlineData("""{ "mbox_sha1sum": "not-a-hash" }""")]
    [InlineData("""{ "account": { "homePage": "http://www.example.com" } }""")]
    [InlineData("""{ "account": { "homePage": "not an iri", "name": "1" } }""")]
    [InlineData("""{ "account": { "homePage": "http://www.example.com", "name": "1", "extra": true } }""")]
    [InlineData("""{ "objectType": "Agent", "mbox": "mailto:a@example.com", "member": [] }""")]
    [InlineData("""{ "objectType": "agent", "mbox": "mailto:a@example.com" }""")]
    [InlineData("""{ "objectType": "Group", "name": "Anonymous without members" }""")]
    [InlineData("""{ "objectType": "Group", "member": [ { "objectType": "Group", "mbox": "mailto:g@example.com" } ] }""")]
    [InlineData("""{ "mbox": "mailto:a@example.com", "name": 5 }""")]
    public void Agents_NeedExactlyOneIdentifier_AndGroupsHoldAgents(string actor)
    {
        var statement = XapiSamples.Simple();

        statement["actor"] = JsonNode.Parse(actor);

        Assert.NotNull(XapiStatementValidator.Validate(statement));
    }

    [Fact]
    public void Agents_WithEachIdentifier_AreValid()
    {
        foreach (var actor in new[]
        {
            """{ "mbox_sha1sum": "ebd31e95054c018b10727ccffd2ef2ec3a016ee9" }""",
            """{ "openid": "http://openid.example.org/12345" }""",
            """{ "account": { "homePage": "http://www.example.com", "name": "1625378" } }""",
            """{ "objectType": "Group", "mbox": "mailto:team@example.com", "member": [ { "mbox": "mailto:a@example.com" } ] }""",
        })
        {
            var statement = XapiSamples.Simple();

            statement["actor"] = JsonNode.Parse(actor);

            Assert.Null(XapiStatementValidator.Validate(statement));
        }
    }

    [Theory]
    [InlineData("""{ "display": { "en-US": "completed" } }""")]
    [InlineData("""{ "id": "completed" }""")]
    [InlineData("""{ "id": "http://adlnet.gov/expapi/verbs/completed", "display": { "en_US": "completed" } }""")]
    [InlineData("""{ "id": "http://adlnet.gov/expapi/verbs/completed", "display": { "en-US": 1 } }""")]
    [InlineData("""{ "id": "http://adlnet.gov/expapi/verbs/completed", "description": "unknown" }""")]
    public void Verbs_NeedAnIriAndALanguageMap(string verb)
    {
        var statement = XapiSamples.Simple();

        statement["verb"] = JsonNode.Parse(verb);

        Assert.NotNull(XapiStatementValidator.Validate(statement));
    }

    [Theory]
    [InlineData("""{ "objectType": "Activity" }""")]
    [InlineData("""{ "objectType": "activity", "id": "http://example.com/a" }""")]
    [InlineData("""{ "id": "http://example.com/a", "definition": { "moreInfo": "not an irl" } }""")]
    [InlineData("""{ "id": "http://example.com/a", "definition": { "interactionType": "essay" } }""")]
    [InlineData("""{ "id": "http://example.com/a", "definition": { "interactionType": "choice", "choices": [ { "id": "a" }, { "id": "a" } ] } }""")]
    [InlineData("""{ "id": "http://example.com/a", "definition": { "extensions": { "not-an-iri": 1 } } }""")]
    [InlineData("""{ "id": "http://example.com/a", "definition": { "name": { "en-US": "x" }, "unknown": 1 } }""")]
    [InlineData("""{ "objectType": "StatementRef", "id": "not-a-uuid" }""")]
    [InlineData("""{ "objectType": "StatementRef", "id": "ec531277-b57b-4c15-8d91-d292c5b2b8f7", "definition": {} }""")]
    [InlineData("""{ "objectType": "Agent", "name": "No identifier" }""")]
    public void Objects_FollowTheirTypes(string value)
    {
        var statement = XapiSamples.Simple();

        statement["object"] = JsonNode.Parse(value);

        Assert.NotNull(XapiStatementValidator.Validate(statement));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("stored")]
    [InlineData("version")]
    [InlineData("authority")]
    [InlineData("nested")]
    public void SubStatements_HaveNoIdStoredVersionOrAuthority_AndNoSubStatement(string change)
    {
        var statement = XapiSamples.WithSubStatement();
        var sub = statement["object"].AsObject();

        switch (change)
        {
            case "id":
                sub["id"] = XapiSamples.NewId();
                break;
            case "stored":
                sub["stored"] = "2013-05-18T05:32:34.804Z";
                break;
            case "version":
                sub["version"] = "1.0.0";
                break;
            case "authority":
                sub["authority"] = XapiSamples.Agent();
                break;
            default:
                sub["object"] = XapiSamples.WithSubStatement()["object"].DeepClone();
                break;
        }

        Assert.NotNull(XapiStatementValidator.Validate(statement));
    }

    [Theory]
    [InlineData("""{ "score": { "scaled": 1.5 } }""")]
    [InlineData("""{ "score": { "raw": 120, "min": 0, "max": 100 } }""")]
    [InlineData("""{ "score": { "min": 10, "max": 5 } }""")]
    [InlineData("""{ "duration": "one hour" }""")]
    [InlineData("""{ "success": "yes" }""")]
    [InlineData("""{ "completion": 1 }""")]
    [InlineData("""{ "response": 42 }""")]
    public void Results_FollowTheirTypesAndRanges(string value)
    {
        var statement = XapiSamples.Simple();

        statement["result"] = JsonNode.Parse(value);

        Assert.NotNull(XapiStatementValidator.Validate(statement));
    }

    [Theory]
    [InlineData("revision-with-agent")]
    [InlineData("platform-with-statementref")]
    [InlineData("registration")]
    [InlineData("context-activities-key")]
    [InlineData("team")]
    [InlineData("language")]
    [InlineData("statement")]
    public void Contexts_FollowTheirRules(string change)
    {
        var statement = XapiSamples.Full();
        var context = statement["context"].AsObject();

        switch (change)
        {
            case "revision-with-agent":
                statement["object"] = XapiSamples.Agent("mailto:object@example.com");
                context.Remove("platform");
                break;
            case "platform-with-statementref":
                statement["object"] = new JsonObject { ["objectType"] = "StatementRef", ["id"] = XapiSamples.NewId() };
                context.Remove("revision");
                break;
            case "registration":
                context["registration"] = "registration-1";
                break;
            case "context-activities-key":
                context["contextActivities"] = new JsonObject { ["sibling"] = new JsonArray(new JsonObject { ["id"] = "http://example.com/a" }) };
                break;
            case "team":
                context["team"] = XapiSamples.Agent("mailto:team@example.com");
                break;
            case "language":
                context["language"] = "en_US";
                break;
            default:
                context["statement"] = new JsonObject { ["objectType"] = "StatementRef", ["id"] = "bad" };
                break;
        }

        Assert.NotNull(XapiStatementValidator.Validate(statement));
    }

    [Theory]
    [InlineData("timestamp", "2013-05-18T05:32:34-00:00")]
    [InlineData("timestamp", "yesterday")]
    [InlineData("version", "2.0.0")]
    [InlineData("version", "0.95")]
    [InlineData("stored", "now")]
    public void TopLevelValues_FollowTheirFormats(string property, string value)
    {
        var statement = XapiSamples.Simple();

        statement[property] = value;

        Assert.NotNull(XapiStatementValidator.Validate(statement));
    }

    [Fact]
    public void Version_OneDotZeroDotAnything_IsAccepted()
    {
        var statement = XapiSamples.Simple();

        statement["version"] = "1.0.9";

        Assert.Null(XapiStatementValidator.Validate(statement));
    }

    [Fact]
    public void VoidingStatement_MustReferToAStatement()
    {
        var statement = XapiSamples.Voiding(XapiSamples.NewId());

        statement["object"] = new JsonObject { ["id"] = XapiSamples.Activity };

        Assert.NotNull(XapiStatementValidator.Validate(statement));
    }

    [Theory]
    [InlineData("display")]
    [InlineData("contentType")]
    [InlineData("length")]
    [InlineData("sha2")]
    [InlineData("usageType")]
    public void Attachments_NeedTheirProperties(string missing)
    {
        var statement = XapiSamples.Simple();
        var attachment = new JsonObject
        {
            ["usageType"] = "http://example.com/attachment-usage/test",
            ["display"] = new JsonObject { ["en-US"] = "A test attachment" },
            ["contentType"] = "text/plain; charset=ascii",
            ["length"] = 27,
            ["sha2"] = "495395e777cd98da653df9615d09c0fd6bb2f8d4788394cd53c56a3bfdcd848a",
        };

        attachment.Remove(missing);
        statement["attachments"] = new JsonArray(attachment);

        Assert.NotNull(XapiStatementValidator.Validate(statement));
    }

    [Fact]
    public void ExtensionValues_MayBeAnyJson()
    {
        var statement = XapiSamples.Full();

        statement["result"]["extensions"] = new JsonObject { ["http://example.com/ext"] = null, ["http://example.com/other"] = new JsonArray(1, "two", new JsonObject()) };

        Assert.Null(XapiStatementValidator.Validate(statement));
    }

    [Theory]
    [InlineData("1.0", true)]
    [InlineData("1.0.0", true)]
    [InlineData("1.0.3", true)]
    [InlineData("1.0.9", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("0.95", false)]
    [InlineData("1.1", false)]
    [InlineData("2.0.0", false)]
    [InlineData("1.0.x", false)]
    public void VersionHeader_AcceptsOneDotZero(string header, bool expected)
        => Assert.Equal(expected, XapiStatementValidator.IsSupportedVersion(header));
}
