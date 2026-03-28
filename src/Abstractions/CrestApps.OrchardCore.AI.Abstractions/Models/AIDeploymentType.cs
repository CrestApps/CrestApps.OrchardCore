namespace CrestApps.OrchardCore.AI.Models;

[Flags]
public enum AIDeploymentType
{
    None = 0,
    Chat = 1 << 0,
    Utility = 1 << 1,
    Embedding = 1 << 2,
    Image = 1 << 3,
    SpeechToText = 1 << 4,
    TextToSpeech = 1 << 5,
}
