using CrestApps.OrchardCore.Learning.Lti;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;

namespace CrestApps.OrchardCore.Tests.Learning.Lti;

/// <summary>
/// Custom parameters (LTI 1.3 Core 5.4.6 and Appendix C): the course item's win over the tool's, a supported variable is
/// replaced, and an unsupported or withheld variable is sent as written.
/// </summary>
public sealed class LtiCustomParametersTests
{
    [Fact]
    public void Resolve_MergesTheToolsAndTheLinksParameters_TheLinkWinning()
    {
        // Act
        var custom = LtiCustomParameters.Resolve("level=easy\nlang=en\n\n=nameless\n", "level=hard\nchapter=3", new Dictionary<string, string>());

        // Assert
        Assert.Equal("hard", custom["level"]?.GetValue<string>());
        Assert.Equal("en", custom["lang"]?.GetValue<string>());
        Assert.Equal("3", custom["chapter"]?.GetValue<string>());
        Assert.Equal(3, custom.Count);
    }

    [Fact]
    public void Resolve_KeepsEqualSignsInValues()
        => Assert.Equal("a=b=c", LtiCustomParameters.Resolve("formula=a=b=c", null, new Dictionary<string, string>())["formula"]?.GetValue<string>());

    [Fact]
    public void Resolve_ReplacesSupportedVariables_AndLeavesTheOthersAsWritten()
    {
        // Arrange
        var variables = LtiCustomParameters.VariablesOf(new LtiLaunchContext
        {
            Subject = "subject-1",
            CourseId = "course-1",
            CourseTitle = "Forklift Safety",
            ItemId = "item-1",
            ItemTitle = "Simulator",
            Roles = [LtiConstants.Roles.Learner],
        });

        // Act
        var custom = LtiCustomParameters.Resolve(
            "user=$User.id\ncourse=$Context.id\ntitle=$Context.title\nlink=$ResourceLink.id\nlinktitle=$ResourceLink.title\nrole=$Membership.role\nname=$Person.name.full\nemail=$Person.email.primary\nunknown=$Canvas.course.id",
            null,
            variables);

        // Assert
        Assert.Equal("subject-1", custom["user"]?.GetValue<string>());
        Assert.Equal("course-1", custom["course"]?.GetValue<string>());
        Assert.Equal("Forklift Safety", custom["title"]?.GetValue<string>());
        Assert.Equal("item-1", custom["link"]?.GetValue<string>());
        Assert.Equal("Simulator", custom["linktitle"]?.GetValue<string>());
        Assert.Equal(LtiConstants.Roles.Learner, custom["role"]?.GetValue<string>());

        // The privacy level withheld the name and the e-mail address, and the last variable is not supported.
        Assert.Equal("$Person.name.full", custom["name"]?.GetValue<string>());
        Assert.Equal("$Person.email.primary", custom["email"]?.GetValue<string>());
        Assert.Equal("$Canvas.course.id", custom["unknown"]?.GetValue<string>());
    }

    [Fact]
    public void VariablesOf_NeverHoldsTheUserId()
    {
        // Act
        var variables = LtiCustomParameters.VariablesOf(new LtiLaunchContext { UserId = "real-user-id", Subject = "subject-1" });

        // Assert
        Assert.DoesNotContain("real-user-id", variables.Values);
        Assert.Equal("subject-1", variables[LtiCustomParameters.UserId]);
    }

    [Fact]
    public void Resolve_WithoutParameters_IsEmpty()
        => Assert.Empty(LtiCustomParameters.Resolve(null, "  ", new Dictionary<string, string>()));
}
