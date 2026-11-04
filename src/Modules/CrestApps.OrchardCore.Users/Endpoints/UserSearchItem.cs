namespace CrestApps.OrchardCore.Users.Endpoints;

/// <summary>
/// One user in the response of the user search endpoint.
/// </summary>
internal sealed class UserSearchItem
{
    public string Value { get; set; }

    public string Text { get; set; }

    public string SecondaryText { get; set; }

    public bool IsEnabled { get; set; }
}
