using CrestApps.OrchardCore.AI.Chat.Interactions.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Interactions;

public sealed class ProfileBuilderSimulatorTests
{
    [Fact]
    public void Normalize_KeepsTheSettingsWithinTheirLimits()
    {
        var settings = ProfileBuilderSimulator.Normalize(new ProfileBuilderSimulationSettings
        {
            Scenarios = [" Dana wants a refund. ", "", "B", "C", "D", "E", "F"],
            Criteria = ["  Confirms the order number. ", " "],
            CustomerDeploymentName = " ",
            JudgeDeploymentName = " gpt-4o ",
            MaxTurns = 99,
            Rounds = 0,
            PassingScore = 120,
        });

        Assert.Equal(["Dana wants a refund.", "B", "C", "D", "E"], settings.Scenarios);
        Assert.Equal(["Confirms the order number."], settings.Criteria);
        Assert.Null(settings.CustomerDeploymentName);
        Assert.Equal("gpt-4o", settings.JudgeDeploymentName);
        Assert.Equal(20, settings.MaxTurns);
        Assert.Equal(1, settings.Rounds);
        Assert.Equal(100, settings.PassingScore);
        Assert.Empty(ProfileBuilderSimulator.Normalize(null).Scenarios);
    }

    [Fact]
    public void FeedbackComment_GivesTheCoachWhatTheJudgeFound()
    {
        var comment = ProfileBuilderSimulator.BuildFeedbackComment(
        [
            new ProfileBuilderSimulation
            {
                Scenario = "Dana is angry\nthat order 1001 is late.",
                Score = 40,
                Summary = "The assistant promised a refund it cannot give.",
                Problems = ["PolicyViolation: Promised a refund (\"I'll refund you now\")", "Criterion not met: Confirms the order number. It never asked."],
            },
        ], 70);

        Assert.StartsWith("Simulated customers tested these instructions and one conversation failed (a conversation passes at 70", comment);
        Assert.Contains("Customer: Dana is angry that order 1001 is late.", comment);
        Assert.Contains("Score: 40/100", comment);
        Assert.Contains("Judge: The assistant promised a refund it cannot give.", comment);
        Assert.Contains("- Criterion not met: Confirms the order number. It never asked.", comment);
    }

    [Fact]
    public void FindReply_PrefersTheAssistantLineAProblemQuotes()
    {
        var simulation = new ProfileBuilderSimulation
        {
            Transcript =
            [
                new() { Role = "user", Text = "My order is late." },
                new() { Role = "assistant", Text = "Sorry! I'll refund you now." },
                new() { Role = "user", Text = "Thanks." },
                new() { Role = "assistant", Text = "Anything else?" },
            ],
            Problems = ["Tone: Fine", "PolicyViolation: Promised a refund (\"I'll refund you now\")"],
        };

        var (customer, reply, index) = ProfileBuilderSimulator.FindReply(simulation);

        Assert.Equal("My order is late.", customer);
        Assert.Equal("Sorry! I'll refund you now.", reply);
        Assert.Equal(1, index);

        // Without a quote that matches, the feedback is about the last reply.
        simulation.Problems = ["Unresolved: The customer's need was left open."];
        Assert.Equal("Anything else?", ProfileBuilderSimulator.FindReply(simulation).Reply);

        simulation.Transcript = [new() { Role = "user", Text = "Hello?" }];
        Assert.Equal(-1, ProfileBuilderSimulator.FindReply(simulation).Index);
    }
}
