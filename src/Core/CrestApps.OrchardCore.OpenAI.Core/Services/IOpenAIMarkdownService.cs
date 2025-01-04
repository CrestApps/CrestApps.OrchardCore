namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public interface IOpenAIMarkdownService
{
    string ToHtml(string markdown);
}