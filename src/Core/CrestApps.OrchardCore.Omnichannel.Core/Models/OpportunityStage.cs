using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// A step an opportunity moves through, such as <c>Prospecting</c>, <c>Proposal</c> or <c>Closed Won</c>. Each
/// opportunity type chooses the stages that apply to it in its <see cref="OpportunityPartSettings"/>.
/// </summary>
public sealed class OpportunityStage : CatalogItem, INameAwareModel, IModifiedUtcAwareModel, ICloneable<OpportunityStage>
{
    /// <summary>
    /// Gets or sets the unique stage name. It is fixed once the stage is created.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the position of the stage in the pipeline, lowest first.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the default chance, from 0 to 100, that an opportunity in this stage closes as won.
    /// </summary>
    public int Probability { get; set; }

    /// <summary>
    /// Gets or sets whether an opportunity in this stage is closed.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Gets or sets whether an opportunity in this stage is won. Only a closed stage can be won.
    /// </summary>
    public bool IsWon { get; set; }

    /// <summary>
    /// Gets or sets the created utc.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the modified utc.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the author.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the owner id.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Creates a copy of the current stage.
    /// </summary>
    public OpportunityStage Clone()
    {
        return new OpportunityStage
        {
            ItemId = ItemId,
            Name = Name,
            Description = Description,
            Order = Order,
            Probability = Probability,
            IsClosed = IsClosed,
            IsWon = IsWon,
            CreatedUtc = CreatedUtc,
            ModifiedUtc = ModifiedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties is null
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(Properties)),
        };
    }
}
