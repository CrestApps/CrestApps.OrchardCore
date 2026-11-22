using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Surveys.Extras;

/// <summary>
/// A CAPTCHA that can't be shown fails closed, a personal invitation needs no CAPTCHA, and an e-mail invitation is for
/// exactly one address.
/// </summary>
public sealed class SurveyAccessHardeningTests
{
    [Fact]
    public async Task CaptchaRule_RequiredWithoutTheReCaptchaFeature_RefusesGuestsWithAClearReason()
    {
        // Arrange: no CAPTCHA service is registered, as when the reCAPTCHA feature is off.
        var rule = new CaptchaRule(new ServiceCollection().BuildServiceProvider());

        // Act
        var decision = await rule.CheckStartAsync(Context(invitation: null));

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(SurveyErrorCode.CaptchaUnavailable, decision.Error);
    }

    [Fact]
    public async Task CaptchaRule_PersonalInvitation_IsNotAskedForACaptcha()
    {
        // Arrange
        var rule = new CaptchaRule(new ServiceCollection().BuildServiceProvider());

        // Act
        var decision = await rule.CheckStartAsync(Context(new SurveyInvitation { ItemId = "i1" }));

        // Assert
        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task CaptchaRule_SignedInRespondent_IsNotAskedForACaptcha()
    {
        // Arrange
        var rule = new CaptchaRule(new ServiceCollection().BuildServiceProvider());
        var context = Context(invitation: null);
        context.Identity.UserId = "u1";

        // Act
        var decision = await rule.CheckStartAsync(context);

        // Assert
        Assert.True(decision.IsAllowed);
    }

    [Theory]
    [InlineData("ann@example.com", true)]
    [InlineData("ann.lee+surveys@example.co.uk", true)]
    [InlineData("a@x.com;b@y.com", false)]
    [InlineData("a@x.com, b@y.com", false)]
    [InlineData("Ann <ann@example.com>", false)]
    [InlineData("not an address", false)]
    [InlineData("ann@", false)]
    public void IsSingleEmailAddress_AcceptsOnePlainAddressOnly(string email, bool expected)
    {
        // Arrange

        // Act
        var valid = SurveyInvitationService.IsSingleEmailAddress(email);

        // Assert
        Assert.Equal(expected, valid);
    }

    private static SurveyAccessContext Context(SurveyInvitation invitation)
        => new()
        {
            Survey = new Survey { ItemId = "s1", Access = new SurveyAccessSettings { RequireCaptcha = true } },
            Invitation = invitation,
            Identity = new SurveyRespondentIdentity(),
        };
}
