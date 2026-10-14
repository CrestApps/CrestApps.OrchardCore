using CrestApps.OrchardCore.TenantHierarchy.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// Describes one child tenant in the tenant picker.
/// </summary>
public class TenantSwitchItem
{
    /// <summary>
    /// Gets or sets the child tenant.
    /// </summary>
    public ChildTenantInfo Info { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user marked it as a favorite.
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Gets or sets the position in the recent list, or <see langword="null"/> when it is not recent.
    /// </summary>
    public int? RecentRank { get; set; }

    /// <summary>
    /// Gets the initials shown in the avatar of the child tenant.
    /// </summary>
    public string Initials
    {
        get
        {
            var words = (Info?.Entry?.DisplayName ?? "?")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return words.Length switch
            {
                0 => "?",
                1 => words[0].Substring(0, Math.Min(2, words[0].Length)).ToUpperInvariant(),
                _ => string.Concat(words[0][0], words[1][0]).ToUpperInvariant(),
            };
        }
    }
}
