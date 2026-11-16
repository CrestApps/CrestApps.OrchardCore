using CrestApps.OrchardCore.Users.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// HT1 (fix-verification review): on SQL Server's default collation <c>AE</c> equals <c>Æ</c>, so Orchard Core's lookup
/// <c>NormalizedEmail = @p</c> answered <c>victim@æther.com</c> with the account of <c>victim@aether.com</c>, and the
/// code was mailed to the address that was typed: the attacker's domain. These run the controller over Orchard Core's
/// user store on SQLite, with the user index's email column comparing the way SQL Server's does.
/// </summary>
public sealed class EmailSignInLookAlikeAddressTests : IAsyncLifetime
{
    private const string VictimEmail = "victim@aether.com";
    private const string LookAlikeEmail = "victim@æther.com";

    private EmailSignInHarness _harness;

    public async ValueTask InitializeAsync()
    {
        _harness = await EmailSignInHarness.CreateAsync();
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "victim", Email = VictimEmail, EmailConfirmed = true, IsEnabled = true }, withLogin: false);
    }

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task TheDatabase_AnswersTheLookAlikeWithTheVictimsAccount()
    {
        // The premise: the lookup itself is fooled, as it is on SQL Server.
        var found = await _harness.InRequestAsync(services => services.GetRequiredService<UserManager<IUser>>().FindByEmailAsync(LookAlikeEmail));

        Assert.Equal("user-1", Assert.IsType<User>(found).UserId);
    }

    [Fact]
    public async Task ALookAlikeAddress_GetsNoCode_AndNoCodeSignsItIn()
    {
        // Act: the attacker asks for a code for the look-alike, then posts every code that was sent.
        var asked = await _harness.InRequestAsync(services => _harness.CreateController(services).Index(new EmailSignInViewModel { Email = LookAlikeEmail }));
        var codes = _harness.Sender.Sent.Select(sent => sent.Code).DefaultIfEmpty("000000").ToList();
        var posted = new List<IActionResult>();

        foreach (var code in codes)
        {
            posted.Add(await _harness.InRequestAsync(services => _harness.CreateController(services).Code(new EmailSignInViewModel { Email = LookAlikeEmail, Code = code })));
        }

        // Assert: the page answers as for any address, nothing is mailed, and nobody is signed in.
        Assert.Equal(nameof(EmailSignInController.Code), Assert.IsType<ViewResult>(asked).ViewName);
        Assert.Empty(_harness.Sender.Sent);
        Assert.All(posted, result => Assert.IsType<ViewResult>(result));
    }

    [Fact]
    public async Task TheCode_IsMailedToTheAccountsOwnAddress_NotToWhatWasTyped()
    {
        // Act: the same address with other case and spaces is the account's own address.
        await _harness.InRequestAsync(services => _harness.CreateController(services).Index(new EmailSignInViewModel { Email = " Victim@AETHER.com " }));

        // Assert
        var sent = Assert.Single(_harness.Sender.Sent);
        Assert.Equal(VictimEmail, sent.Email);
        Assert.Equal("victim", sent.UserName);

        var result = await _harness.InRequestAsync(services => _harness.CreateController(services).Code(new EmailSignInViewModel { Email = VictimEmail, Code = sent.Code }));
        Assert.IsType<LocalRedirectResult>(result);
    }
}
