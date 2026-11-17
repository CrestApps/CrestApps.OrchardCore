using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// People and permissions for authorization tests.
/// </summary>
internal static class TestSecurity
{
    /// <summary>
    /// Creates a signed-in person.
    /// </summary>
    public static ClaimsPrincipal User(string name, string id = null)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, name), new Claim(ClaimTypes.NameIdentifier, id ?? name + "-id")], "test"));

    /// <summary>
    /// Creates an authorization service that grants exactly the given permissions, to everyone.
    /// </summary>
    public static IAuthorizationService Granting(params Permission[] grants)
    {
        var names = grants.Select(permission => permission.Name).ToHashSet(StringComparer.Ordinal);
        var authorization = new Mock<IAuthorizationService>();

        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                user?.Identity?.IsAuthenticated == true && requirements.OfType<PermissionRequirement>().All(requirement => names.Contains(requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorization.Object;
    }
}

/// <summary>
/// An HTML localizer that returns each text as written, with its arguments.
/// </summary>
/// <typeparam name="T">The localized type.</typeparam>
internal sealed class EchoHtmlLocalizer<T> : IHtmlLocalizer<T>
{
    public LocalizedHtmlString this[string name]
        => new(name, name);

    public LocalizedHtmlString this[string name, params object[] arguments]
        => new(name, name, false, arguments);

    public LocalizedString GetString(string name)
        => new(name, name, false, null);

    public LocalizedString GetString(string name, params object[] arguments)
        => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments), false, null);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => [];
}

/// <summary>
/// Keeps the messages a page shows the person.
/// </summary>
internal sealed class RecordingNotifier : INotifier
{
    public List<NotifyEntry> Entries { get; } = [];

    public ValueTask AddAsync(NotifyType type, LocalizedHtmlString message)
    {
        Entries.Add(new NotifyEntry { Type = type, Message = message });

        return ValueTask.CompletedTask;
    }

    public IList<NotifyEntry> List()
        => Entries;

    public void Clear()
        => Entries.Clear();
}
