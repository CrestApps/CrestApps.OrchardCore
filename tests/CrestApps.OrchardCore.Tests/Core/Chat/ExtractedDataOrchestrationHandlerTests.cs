using CrestApps.AI;
using CrestApps.AI.Chat.Handlers;
using CrestApps.AI.Models;
using CrestApps.Templates.Models;
using CrestApps.Templates.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Core.Chat;

public sealed class ExtractedDataOrchestrationHandlerTests
{
    [Fact]
    public async Task BuiltAsync_WithCollectedFields_AddsSessionStateInstructions()
    {
        var handler = new ExtractedDataOrchestrationHandler(
            new FakeTemplateService(),
            NullLogger<ExtractedDataOrchestrationHandler>.Instance);

        var profile = new AIProfile
        {
            ItemId = "profile-1",
        };
        profile.AlterSettings<AIProfileDataExtractionSettings>(settings =>
        {
            settings.EnableDataExtraction = true;
            settings.DataExtractionEntries =
            [
                new DataExtractionEntry { Name = "first_name", Description = "The customer's first name." },
                new DataExtractionEntry { Name = "last_name", Description = "The customer's last name." },
                new DataExtractionEntry { Name = "phone_number", Description = "The customer's phone number." },
            ];
        });

        var context = new OrchestrationContext
        {
            CompletionContext = new AICompletionContext(),
        };
        context.CompletionContext.AdditionalProperties["Session"] = new AIChatSession
        {
            ExtractedData =
            {
                ["first_name"] = new ExtractedFieldState
                {
                    Values = ["Mike"],
                },
            },
        };

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(profile, context));

        var systemMessage = context.SystemMessageBuilder.ToString();
        Assert.Contains("[Collected Session Data]", systemMessage);
        Assert.Contains("first_name=Mike", systemMessage);
        Assert.Contains("missing=last_name, phone_number", systemMessage);
    }

    [Fact]
    public async Task BuiltAsync_WithoutCollectedFields_DoesNothing()
    {
        var handler = new ExtractedDataOrchestrationHandler(
            new FakeTemplateService(),
            NullLogger<ExtractedDataOrchestrationHandler>.Instance);

        var profile = new AIProfile();
        profile.AlterSettings<AIProfileDataExtractionSettings>(settings =>
        {
            settings.EnableDataExtraction = true;
            settings.DataExtractionEntries =
            [
                new DataExtractionEntry { Name = "first_name" },
            ];
        });

        var context = new OrchestrationContext
        {
            CompletionContext = new AICompletionContext(),
        };
        context.CompletionContext.AdditionalProperties["Session"] = new AIChatSession();

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(profile, context));

        Assert.Equal(string.Empty, context.SystemMessageBuilder.ToString());
    }

    private sealed class FakeTemplateService : ITemplateService
    {
        public Task<IReadOnlyList<Template>> ListAsync()
            => Task.FromResult<IReadOnlyList<Template>>([]);

        public Task<Template> GetAsync(string id)
            => Task.FromResult<Template>(null);

        public Task<string> RenderAsync(string id, IDictionary<string, object> arguments = null)
        {
            if (id != AITemplateIds.ExtractedDataAvailability)
            {
                return Task.FromResult(string.Empty);
            }

            var collectedFields = ((IEnumerable<object>)arguments["collectedFields"]).ToList();
            var missingFields = ((IEnumerable<object>)arguments["missingFields"]).ToList();

            var collected = string.Join(", ", collectedFields.Select(field =>
                $"{GetStringProperty(field, "Name")}={string.Join("|", GetValues(field))}"));
            var missing = string.Join(", ", missingFields.Select(field => GetStringProperty(field, "Name")));

            return Task.FromResult($"[Collected Session Data]{Environment.NewLine}{collected}{Environment.NewLine}missing={missing}");
        }

        public Task<string> MergeAsync(IEnumerable<string> ids, IDictionary<string, object> arguments = null, string separator = "\n\n")
            => Task.FromResult(string.Join(separator, ids));

        private static string GetStringProperty(object value, string propertyName)
            => value.GetType().GetProperty(propertyName)?.GetValue(value)?.ToString();

        private static IEnumerable<string> GetValues(object value)
            => value.GetType().GetProperty("Values")?.GetValue(value) as IEnumerable<string> ?? [];
    }
}
