namespace CrestApps.AI;

public interface IAIMemorySafetyService
{
    bool TryValidate(string name, string description, string content, out string errorMessage);
}
