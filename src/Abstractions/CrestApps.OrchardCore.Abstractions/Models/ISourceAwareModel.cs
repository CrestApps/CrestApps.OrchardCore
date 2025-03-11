namespace CrestApps.OrchardCore.Models;

public interface ISourceAwareModel
{
    string Source { get; set; }
}
