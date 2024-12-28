namespace CrestApps.OrchardCore.OpenAI.Models;

public class ChatCompletionMessage
{
    public string Id { get; set; }

    public string Role { get; set; }

    public string Prompt { get; set; }

    public static ChatCompletionMessage CreateMessage(string prompt, string role)
    {
        return new ChatCompletionMessage()
        {
            Role = role,
            Prompt = prompt,
        };
    }
}
