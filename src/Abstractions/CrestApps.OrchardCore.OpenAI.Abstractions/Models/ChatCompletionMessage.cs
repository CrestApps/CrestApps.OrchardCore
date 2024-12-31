namespace CrestApps.OrchardCore.OpenAI.Models;

public class ChatCompletionMessage
{
    public string Role { get; set; }

    public string Content { get; set; }

    public string Name { get; set; }

    public static ChatCompletionMessage CreateMessage(string content, string role)
    {
        return new ChatCompletionMessage()
        {
            Role = role,
            Content = content,
        };
    }

    public static ChatCompletionMessage CreateFunctionMessage(string content, string name)
    {
        return new ChatCompletionMessage()
        {
            Role = "function",
            Content = content,
            Name = name,
        };
    }
}
