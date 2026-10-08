using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The name the soft phone shows beside an extension: the person's display name as the site shows users, else their
/// username, else the extension itself -- resolved on the server, all users in one lookup.
/// </summary>
public sealed class ExtensionDisplayNameTests
{
    [Theory]
    [InlineData("Front desk", "jdoe", "Jane Doe", "2", "Front desk")]
    [InlineData("jdoe", "jdoe", "Jane Doe", "2", "Jane Doe")]
    [InlineData("JDOE", "jdoe", "Jane Doe", "2", "Jane Doe")]
    [InlineData(null, "jdoe", "Jane Doe", "2", "Jane Doe")]
    [InlineData(null, "jdoe", "  ", "2", "jdoe")]
    [InlineData(null, "jdoe", null, "2", "jdoe")]
    [InlineData(null, null, null, " 2 ", "2")]
    [InlineData("", "", "", "2", "2")]
    public void Choose_PrefersANameSetOnTheExtension_ThenTheDisplayName_ThenTheUsername_ThenTheNumber(
        string extensionDisplayName,
        string userName,
        string userDisplayName,
        string number,
        string expected)
    {
        // Act
        var name = TelephonyExtensionNames.Choose(extensionDisplayName, userName, userDisplayName, number);

        // Assert
        Assert.Equal(expected, name);
    }

    [Fact]
    public async Task Directory_NamesEveryExtension_LoadingAllItsUsersInOneLookup()
    {
        // Arrange
        var store = new Mock<ITelephonyExtensionStore>();
        store
            .Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TelephonyExtension { Number = "2", UserId = "u-2", UserName = "jdoe", DisplayName = "jdoe" },
                new TelephonyExtension { Number = "3", UserId = "u-3", UserName = "bsmith", DisplayName = "bsmith" },
                new TelephonyExtension { Number = "4", UserId = "u-4", UserName = "front", DisplayName = "Front desk" },
                new TelephonyExtension { Number = "5", UserId = null, UserName = null },
                new TelephonyExtension { Number = " ", UserId = "u-6", UserName = "nobody" },
            ]);
        var names = new RecordingUserDisplayNames(new Dictionary<string, string>
        {
            ["u-2"] = "Jane Doe",
            ["u-4"] = "Fran Front",
        });
        var directory = new TelephonyExtensionDirectory(store.Object, names);

        // Act
        var entries = await directory.ListAsync(TestContext.Current.CancellationToken);

        // Assert - extensions that ring nobody are left out; the rest are named, sorted by name.
        Assert.Equal(["3", "4", "2"], entries.Select(entry => entry.Extension));
        Assert.Equal(["bsmith", "Front desk", "Jane Doe"], entries.Select(entry => entry.DisplayName));
        Assert.Equal(1, names.Lookups);
        Assert.Equal(["u-2", "u-3", "u-4"], names.LastUserIds.Order());
    }

    [Fact]
    public async Task Resolver_NamesTheExtensionByTheUsersDisplayName()
    {
        // Arrange
        var store = new Mock<ITelephonyExtensionStore>();
        store
            .Setup(s => s.FindByNumberAsync("2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelephonyExtension { Number = "2", UserId = "u-2", UserName = "jdoe", DisplayName = "jdoe" });
        var resolver = new DisplayNameTelephonyExtensionResolver(
            store.Object,
            new RecordingUserDisplayNames(new Dictionary<string, string> { ["u-2"] = "Jane Doe" }));

        // Act
        var resolution = await resolver.ResolveAsync(" 2 ", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(resolution.Found);
        Assert.Equal("2", resolution.Number);
        Assert.Equal("u-2", resolution.UserId);
        Assert.Equal("jdoe", resolution.UserName);
        Assert.Equal("Jane Doe", resolution.DisplayName);
    }

    [Fact]
    public async Task Resolver_FallsBackToTheUsername_WhenTheUserHasNoDisplayName()
    {
        // Arrange
        var store = new Mock<ITelephonyExtensionStore>();
        store
            .Setup(s => s.FindByNumberAsync("2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelephonyExtension { Number = "2", UserId = "u-2", UserName = "jdoe" });
        var resolver = new DisplayNameTelephonyExtensionResolver(store.Object, new RecordingUserDisplayNames(new Dictionary<string, string>()));

        // Act
        var resolution = await resolver.ResolveAsync("2", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("jdoe", resolution.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("9")]
    public async Task Resolver_IsNotFound_ForNothingOrAnUnknownExtension(string number)
    {
        // Arrange
        var store = new Mock<ITelephonyExtensionStore>();
        var names = new RecordingUserDisplayNames(new Dictionary<string, string>());
        var resolver = new DisplayNameTelephonyExtensionResolver(store.Object, names);

        // Act
        var resolution = await resolver.ResolveAsync(number, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(resolution.Found);
        Assert.Equal(0, names.Lookups);
    }

    [Fact]
    public async Task Directory_NamesAUser_OrNothingWhenTheUserIsUnknown()
    {
        // Arrange
        var directory = new TelephonyExtensionDirectory(
            new Mock<ITelephonyExtensionStore>().Object,
            new RecordingUserDisplayNames(new Dictionary<string, string> { ["u-2"] = "Jane Doe" }));

        // Act & Assert
        Assert.Equal("Jane Doe", await directory.GetUserNameAsync("u-2", TestContext.Current.CancellationToken));
        Assert.Null(await directory.GetUserNameAsync("u-9", TestContext.Current.CancellationToken));
        Assert.Null(await directory.GetUserNameAsync(null, TestContext.Current.CancellationToken));
    }

    private sealed class RecordingUserDisplayNames : ITelephonyUserDisplayNames
    {
        private readonly IReadOnlyDictionary<string, string> _names;

        public RecordingUserDisplayNames(IReadOnlyDictionary<string, string> names)
        {
            _names = names;
        }

        public int Lookups { get; private set; }

        public IReadOnlyList<string> LastUserIds { get; private set; } = [];

        public Task<IReadOnlyDictionary<string, string>> GetAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
        {
            Lookups++;
            LastUserIds = userIds.ToArray();

            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                LastUserIds.Where(_names.ContainsKey).ToDictionary(id => id, id => _names[id]));
        }
    }
}
