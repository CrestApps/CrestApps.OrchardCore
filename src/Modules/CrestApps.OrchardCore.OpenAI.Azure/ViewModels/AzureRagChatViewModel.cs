using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.OpenAI.Azure.ViewModels;

/// <summary>
/// View model for Azure RAG (Retrieval-Augmented Generation) query parameters.
/// Used to configure query-time parameters on AIProfile.
/// </summary>
public class AzureRagChatViewModel
{
    [Range(1, 5)]
    public int? Strictness { get; set; }

    [Range(3, 20)]
    public int? TopNDocuments { get; set; }

    public string Filter { get; set; }
}
