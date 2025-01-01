namespace CrestApps.OrchardCore.OpenAI.Models;

public class OpenAIChatCompletionMessage
{
    public string Role { get; set; }

    public string Content { get; set; }

    public string Name { get; set; }

    public static OpenAIChatCompletionMessage CreateMessage(string content, string role)
    {
        return new OpenAIChatCompletionMessage()
        {
            Role = role,
            Content = content,
        };
    }

    public static OpenAIChatCompletionMessage CreateFunctionMessage(string content, string name)
    {
        return new OpenAIChatCompletionMessage()
        {
            Role = "function",
            Content = content,
            Name = name,
        };
    }
}
