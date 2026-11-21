using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Tests.Learning.Xapi;

/// <summary>
/// Statements written from the examples and the property tables of xAPI 1.0.3, Part Two.
/// </summary>
internal static class XapiSamples
{
    public const string Completed = "http://adlnet.gov/expapi/verbs/completed";
    public const string Attempted = "http://adlnet.gov/expapi/verbs/attempted";
    public const string Experienced = "http://adlnet.gov/expapi/verbs/experienced";
    public const string Activity = "http://www.example.com/meetings/occurances/34534";
    public const string Course = "http://www.example.com/courses/xapi-101";
    public const string Registration = "ec531277-b57b-4c15-8d91-d292c5b2b8f7";

    public static JsonObject Agent(string mbox = "mailto:learner@example.com", string name = "Learner")
        => new()
        {
            ["objectType"] = "Agent",
            ["name"] = name,
            ["mbox"] = mbox,
        };

    /// <summary>
    /// The simplest statement of the specification: an actor, a verb and an activity.
    /// </summary>
    public static JsonObject Simple(string id = null, string mbox = "mailto:learner@example.com", string verb = Completed, string activity = Activity)
    {
        var statement = new JsonObject
        {
            ["actor"] = Agent(mbox),
            ["verb"] = new JsonObject
            {
                ["id"] = verb,
                ["display"] = new JsonObject { ["en-US"] = "completed", ["fr-FR"] = "a terminé" },
            },
            ["object"] = new JsonObject
            {
                ["objectType"] = "Activity",
                ["id"] = activity,
                ["definition"] = new JsonObject
                {
                    ["name"] = new JsonObject { ["en-US"] = "Example meeting", ["fr-FR"] = "Réunion" },
                    ["type"] = "http://adlnet.gov/expapi/activities/meeting",
                },
            },
        };

        if (id is not null)
        {
            statement["id"] = id;
        }

        return statement;
    }

    /// <summary>
    /// The long example of the specification: result, context with an instructor, a team, context activities, a
    /// language and extensions.
    /// </summary>
    public static JsonObject Full(string id = null)
    {
        var statement = Simple(id);

        statement["result"] = new JsonObject
        {
            ["completion"] = true,
            ["success"] = true,
            ["duration"] = "PT1H0M0S",
            ["response"] = "We agreed on some example actions.",
            ["score"] = new JsonObject { ["scaled"] = 0.95, ["raw"] = 95, ["min"] = 0, ["max"] = 100 },
            ["extensions"] = new JsonObject { ["http://example.com/profiles/meetings/resultextensions/minuteslocation"] = "X:\\meetings\\minutes\\examplemeeting.one" },
        };
        statement["context"] = new JsonObject
        {
            ["registration"] = Registration,
            ["instructor"] = Agent("mailto:instructor@example.com", "Instructor"),
            ["team"] = new JsonObject
            {
                ["objectType"] = "Group",
                ["name"] = "Team PB",
                ["mbox"] = "mailto:teampb@example.com",
            },
            ["contextActivities"] = new JsonObject
            {
                ["parent"] = new JsonObject { ["id"] = "http://www.example.com/meetings/series/267", ["objectType"] = "Activity" },
                ["category"] = new JsonArray(new JsonObject { ["id"] = "http://www.example.com/meetings/categories/teammeeting" }),
                ["other"] = new JsonArray(new JsonObject { ["id"] = "http://www.example.com/meetings/occurances/34257" }),
            },
            ["language"] = "en",
            ["revision"] = "rev1",
            ["platform"] = "Example virtual meeting software",
            ["extensions"] = new JsonObject { ["http://example.com/profiles/meetings/contextextensions/topic"] = "Example topic" },
        };
        statement["timestamp"] = "2013-05-18T05:32:34.804+00:00";

        return statement;
    }

    /// <summary>
    /// A statement whose object is a sub-statement, as in the "planned" example.
    /// </summary>
    public static JsonObject WithSubStatement()
        => new()
        {
            ["actor"] = Agent(),
            ["verb"] = new JsonObject { ["id"] = "http://example.com/planned", ["display"] = new JsonObject { ["en-US"] = "planned" } },
            ["object"] = new JsonObject
            {
                ["objectType"] = "SubStatement",
                ["actor"] = Agent(),
                ["verb"] = new JsonObject { ["id"] = "http://example.com/visited" },
                ["object"] = new JsonObject { ["id"] = "http://example.com/website", ["definition"] = new JsonObject { ["name"] = new JsonObject { ["en-US"] = "Some Awesome Website" } } },
                ["context"] = new JsonObject
                {
                    ["contextActivities"] = new JsonObject { ["grouping"] = new JsonObject { ["id"] = "http://example.com/plans" } },
                },
            },
        };

    /// <summary>
    /// An anonymous group as the actor.
    /// </summary>
    public static JsonObject WithAnonymousGroup()
        => new()
        {
            ["actor"] = new JsonObject
            {
                ["objectType"] = "Group",
                ["name"] = "Example group",
                ["member"] = new JsonArray(Agent("mailto:one@example.com", "One"), new JsonObject { ["account"] = new JsonObject { ["homePage"] = "http://www.example.com", ["name"] = "13936749" } }),
            },
            ["verb"] = new JsonObject { ["id"] = Attempted },
            ["object"] = new JsonObject { ["id"] = Activity },
        };

    /// <summary>
    /// A choice interaction activity, as in the interaction examples.
    /// </summary>
    public static JsonObject WithChoiceInteraction()
    {
        var statement = Simple();

        statement["verb"] = new JsonObject { ["id"] = "http://adlnet.gov/expapi/verbs/answered" };
        statement["object"] = new JsonObject
        {
            ["id"] = "http://www.example.com/tincan/activities/multipart",
            ["definition"] = new JsonObject
            {
                ["type"] = "http://adlnet.gov/expapi/activities/cmi.interaction",
                ["interactionType"] = "choice",
                ["correctResponsesPattern"] = new JsonArray("golf[,]tetris"),
                ["choices"] = new JsonArray(
                    new JsonObject { ["id"] = "golf", ["description"] = new JsonObject { ["en-US"] = "Golf Example" } },
                    new JsonObject { ["id"] = "tetris", ["description"] = new JsonObject { ["en-US"] = "Tetris Example" } }),
            },
        };

        return statement;
    }

    public static JsonObject Voiding(string targetId, string id = null)
    {
        var statement = new JsonObject
        {
            ["actor"] = Agent("mailto:admin@example.com", "Admin"),
            ["verb"] = new JsonObject { ["id"] = "http://adlnet.gov/expapi/verbs/voided", ["display"] = new JsonObject { ["en-US"] = "voided" } },
            ["object"] = new JsonObject { ["objectType"] = "StatementRef", ["id"] = targetId },
        };

        if (id is not null)
        {
            statement["id"] = id;
        }

        return statement;
    }

    public static string NewId()
        => Guid.NewGuid().ToString();
}
