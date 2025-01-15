namespace CrestApps.OrchardCore.OpenAI;

public interface IOpenAIMarkdownService
{
    string ToHtml(string markdown);
}
