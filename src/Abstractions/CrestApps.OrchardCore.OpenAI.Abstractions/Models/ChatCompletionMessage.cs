namespace CrestApps.OrchardCore.OpenAI.Models;

public class ChatCompletionMessage
{
    public string Role { get; set; }

    public string Content { get; set; }

    public static ChatCompletionMessage CreateMessage(string content, string role)
    {
        return new ChatCompletionMessage()
        {
            Role = role,
            Content = content,
        };
    }
}
