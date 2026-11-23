using CrestApps.OrchardCore.PublicForms;

namespace CrestApps.OrchardCore.Tests.PublicForms;

/// <summary>
/// The trap field of public forms: every form posts it under one name, and only a filled one gives a robot away.
/// </summary>
public sealed class PublicFormTrapTests
{
    [Theory]
    [InlineData("http://spam.example")]
    [InlineData("x")]
    [InlineData(" filled ")]
    public void AFilledTrapField_GivesAPostAway(string value)
    {
        Assert.True(PublicFormTrap.IsTrapped(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void AnEmptyTrapField_LetsThePostThrough(string value)
    {
        Assert.False(PublicFormTrap.IsTrapped(value));
    }

    [Fact]
    public void TheTrapField_KeepsTheNameFormsAlreadyPost()
    {
        // The Ticket Form widget posted the trap as "website" before it moved to the shared shape.
        Assert.Equal("website", PublicFormTrap.FieldName);
    }
}
