using CrestApps.OrchardCore.Roles.Core.Models;
using CrestApps.OrchardCore.Roles.Drivers;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;

namespace CrestApps.OrchardCore.Tests.ContentAccessControl;

/// <summary>
/// "Role Picker" didn't say what a picker that restricts content does: on the item editor it now reads "Which roles can
/// see this content", and "Any role" while none is picked. Other role pickers keep their name.
/// </summary>
public sealed class RestrictingRolePickerLabelTests
{
    [Fact]
    public void ARestrictingPickerWithoutAName_SaysWhoCanSeeTheContent_AndAnyRoleWhenEmpty()
    {
        // Act
        var (label, placeholder, hint) = RolePickerPartDisplayDriver.Describe(Picker(restricts: true), new PassThroughLocalizer());

        // Assert
        Assert.Equal("Which roles can see this content", label);
        Assert.Equal("Any role", placeholder);
        Assert.Equal("Leave empty so everyone can see it.", hint);
    }

    [Fact]
    public void ARestrictingPicker_KeepsTheNameAndHintAnAdministratorGaveIt()
    {
        // Act
        var (label, placeholder, hint) = RolePickerPartDisplayDriver.Describe(Picker(restricts: true, name: "Readers", hint: "Pick the teams."), new PassThroughLocalizer());

        // Assert
        Assert.Equal("Readers", label);
        Assert.Equal("Any role", placeholder);
        Assert.Equal("Pick the teams.", hint);
    }

    [Fact]
    public void APickerThatDoesntRestrict_KeepsThePartsName()
    {
        // Act
        var (label, placeholder, hint) = RolePickerPartDisplayDriver.Describe(Picker(restricts: false), new PassThroughLocalizer());

        // Assert
        Assert.Equal("Role Picker", label);
        Assert.Equal("Role Picker", placeholder);
        Assert.Null(hint);
    }

    private static ContentTypePartDefinition Picker(bool restricts, string name = null, string hint = null)
    {
        var partDefinition = new ContentPartDefinitionBuilder().Named(nameof(RolePickerPart)).WithDisplayName("Role Picker").Build();
        var type = new ContentTypeDefinitionBuilder()
            .WithName("Handbook")
            .WithPart(nameof(RolePickerPart), partDefinition, part =>
            {
                part.WithSettings(new RolePickerPartSettings { AllowSelectMultiple = true, Hint = hint })
                    .WithSettings(new RolePickerPartContentAccessControlSettings { IsContentRestricted = restricts });

                if (name is not null)
                {
                    part.WithDisplayName(name);
                }
            })
            .Build();

        return type.Parts.Single();
    }

    private sealed class PassThroughLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
