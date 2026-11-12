using CrestApps.OrchardCore.Subscriptions.Drivers;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// SEC-C17r: the subscription sign-up step checks the site's password policy when the password is entered, not after
/// the payment, and answers the same for a taken user name and a taken e-mail address, so the form doesn't tell which
/// accounts exist.
/// </summary>
public sealed class SubscriptionRegisterUserFormTests
{
    [Fact]
    public async Task UpdateAsync_APasswordThePolicyRefuses_IsRefusedAtTheStep()
    {
        // Arrange
        var (driver, _) = CreateDriver(existingUserName: null, existingEmail: null);
        var form = new SubscriptionRegisterUserForm();
        var context = Context(new CreateUserViewModel { UserName = "pat", Email = "pat@example.test", Password = "short", ConfirmPassword = "short" });

        // Act
        await driver.UpdateAsync(form, context);

        // Assert
        Assert.False(context.Updater.ModelState.IsValid);
        Assert.Null(form.Password);
    }

    [Fact]
    public async Task UpdateAsync_ATakenUserNameAndATakenEmail_GetTheSameAnswer()
    {
        // Arrange
        var (takenName, _) = CreateDriver(existingUserName: "pat", existingEmail: null);
        var (takenEmail, _) = CreateDriver(existingUserName: null, existingEmail: "pat@example.test");
        var nameContext = Context(new CreateUserViewModel { UserName = "pat", Email = "new@example.test", Password = "Password1!", ConfirmPassword = "Password1!" });
        var emailContext = Context(new CreateUserViewModel { UserName = "newname", Email = "pat@example.test", Password = "Password1!", ConfirmPassword = "Password1!" });

        // Act
        await takenName.UpdateAsync(new SubscriptionRegisterUserForm(), nameContext);
        await takenEmail.UpdateAsync(new SubscriptionRegisterUserForm(), emailContext);

        // Assert
        var nameErrors = nameContext.Updater.ModelState.Values.SelectMany(entry => entry.Errors).Select(error => error.ErrorMessage).ToList();
        var emailErrors = emailContext.Updater.ModelState.Values.SelectMany(entry => entry.Errors).Select(error => error.ErrorMessage).ToList();

        Assert.NotEmpty(nameErrors);
        Assert.Equal(nameErrors, emailErrors);
        Assert.Equal(nameContext.Updater.ModelState.Keys, emailContext.Updater.ModelState.Keys);
    }

    private static (SubscriptionRegisterUserFormDisplayDriver Driver, Mock<UserManager<IUser>> UserManager) CreateDriver(string existingUserName, string existingEmail)
    {
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        var validator = new Mock<IPasswordValidator<IUser>>();

        validator.Setup(value => value.ValidateAsync(It.IsAny<UserManager<IUser>>(), It.IsAny<IUser>(), It.IsAny<string>()))
            .ReturnsAsync((UserManager<IUser> _, IUser _, string password) => password.Length >= 8
                ? IdentityResult.Success
                : IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort", Description = "Passwords must be at least 8 characters." }));
        userManager.Object.PasswordValidators.Add(validator.Object);
        userManager.Setup(manager => manager.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => name == existingUserName ? new User { UserName = name } : null);
        userManager.Setup(manager => manager.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((string email) => email == existingEmail ? new User { Email = email } : null);

        var options = Options.Create(new IdentityOptions());

        options.Value.User.RequireUniqueEmail = true;

        return (new SubscriptionRegisterUserFormDisplayDriver(userManager.Object, options, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<SubscriptionRegisterUserFormDisplayDriver>()), userManager);
    }

    private static UpdateEditorContext Context(CreateUserViewModel posted)
        => new(
            Mock.Of<IShape>(),
            UserRegistrationCheckoutFlowDisplayDriver.UserRegistrationFormGroupId,
            false,
            string.Empty,
            Mock.Of<IShapeFactory>(),
            null,
            new PostedFormUpdateModel(posted));
}
