namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// Who the buyer of an agent checkout is.
/// </summary>
public sealed class AgentBuyer
{
    /// <summary>
    /// Gets or sets the buyer's first name.
    /// </summary>
    public string FirstName { get; set; }

    /// <summary>
    /// Gets or sets the buyer's last name.
    /// </summary>
    public string LastName { get; set; }

    /// <summary>
    /// Gets or sets the buyer's full name, when the protocol gives one name.
    /// </summary>
    public string FullName { get; set; }

    /// <summary>
    /// Gets or sets the buyer's e-mail address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the buyer's phone number.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Returns the buyer's full name, from <see cref="FullName"/> or the first and last names.
    /// </summary>
    public string GetFullName()
    {
        if (!string.IsNullOrWhiteSpace(FullName))
        {
            return FullName.Trim();
        }

        var name = $"{FirstName} {LastName}".Trim();

        return name.Length == 0 ? null : name;
    }
}
