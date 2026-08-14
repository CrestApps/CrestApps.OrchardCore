using System.Text.Json.Nodes;
using OrchardCore.Entities;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A minimal user that is both an <see cref="IUser"/> and an <see cref="IEntity"/> so the token store
/// can read and write provider tokens on it.
/// </summary>
internal sealed class FakeUser : IUser, IEntity
{
    public string UserName { get; set; } = "tester";

    public JsonObject Properties { get; set; } = [];
}
