using System.Text;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Makes a test case from a real conversation, so a conversation that went wrong can be replayed until it goes right.
/// </summary>
public static class AITestCaseFactory
{
    /// <summary>
    /// The most customer lines quoted in the scenario.
    /// </summary>
    public const int MaxQuotedLines = 12;

    /// <summary>
    /// Makes an unsaved test case that plays the customer of a conversation again.
    /// </summary>
    /// <param name="session">The conversation.</param>
    /// <param name="prompts">The conversation's messages, oldest first.</param>
    /// <returns>The test case, ready to review and save.</returns>
    public static AITestCase FromConversation(AIChatSession session, IEnumerable<AIChatSessionPrompt> prompts)
    {
        ArgumentNullException.ThrowIfNull(session);

        var customerLines = (prompts ?? [])
            .Where(prompt => prompt.Role == ChatRole.User && !prompt.IsGeneratedPrompt && !string.IsNullOrWhiteSpace(prompt.Content))
            .Select(prompt => prompt.Content.Trim())
            .Take(MaxQuotedLines)
            .ToList();

        var scenario = new StringBuilder();
        scenario.AppendLine("You are the customer of a real conversation, played again to test the AI. Want what they wanted and behave the way they did; say things in your own words, and react to what the AI says rather than repeating these lines in order.");
        scenario.AppendLine();
        scenario.AppendLine("What the customer said:");

        foreach (var line in customerLines)
        {
            scenario.Append("- ").AppendLine(line);
        }

        var testCase = new AITestCase
        {
            Name = string.IsNullOrWhiteSpace(session.Title) ? $"Conversation {session.SessionId}" : session.Title.Trim(),
            ProfileId = session.ProfileId,
            Scenario = scenario.ToString().TrimEnd(),
            Criteria = "The customer's need is met, or they are told clearly what happens next.",
            SourceSessionId = session.SessionId,
        };

        foreach (var (name, value) in session.GetConversationVariables())
        {
            testCase.Variables[name] = value;
        }

        if (session.TryGet<AIProfileVersionCopy>(out var copy) && copy.Version > 0)
        {
            testCase.ProfileId = copy.SourceProfileId;
            testCase.ProfileVersion = copy.Version;
        }

        return testCase;
    }
}
