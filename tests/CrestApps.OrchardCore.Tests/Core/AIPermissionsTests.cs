using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.Tests.Core;

public sealed class AIPermissionsTests
{
    [Fact]
    public void AccessAnyAITool_IsSecurityCritical()
    {
        // Assert
        Assert.True(AIPermissions.AccessAnyAITool.IsSecurityCritical);
    }

    [Fact]
    public void AccessAnyAITool_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("AccessAnyAITool", AIPermissions.AccessAnyAITool.Name);
        Assert.Equal("Access any AI tool", AIPermissions.AccessAnyAITool.Description);
    }

    [Fact]
    public void AccessAITool_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("AccessAITool", AIPermissions.AccessAITool.Name);
        Assert.Equal("Access AI tool", AIPermissions.AccessAITool.Description);
    }

    [Fact]
    public void CreateAIToolPermission_CreatesPermissionWithCorrectName()
    {
        // Arrange
        var toolName = "TestTool";

        // Act
        var permission = AIPermissions.CreateAIToolPermission(toolName);

        // Assert
        Assert.Equal("AccessAITool_TestTool", permission.Name);
        Assert.Equal("Access AI tool - TestTool", permission.Description);
    }

    [Fact]
    public void CreateAIToolPermission_IsImpliedByAccessAnyAITool()
    {
        // Arrange
        var toolName = "TestTool";

        // Act
        var permission = AIPermissions.CreateAIToolPermission(toolName);

        // Assert
        Assert.NotNull(permission.ImpliedBy);
        Assert.Contains(AIPermissions.AccessAnyAITool, permission.ImpliedBy);
    }

    [Theory]
    [InlineData("")]
    public void CreateAIToolPermission_ThrowsOnEmptyToolName(string toolName)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => AIPermissions.CreateAIToolPermission(toolName));
    }

    [Fact]
    public void CreateAIToolPermission_ThrowsOnNullToolName()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => AIPermissions.CreateAIToolPermission(null));
    }

    [Fact]
    public void CreateProfilePermission_CreatesPermissionWithCorrectName()
    {
        // Arrange
        var profileName = "TestProfile";

        // Act
        var permission = AIPermissions.CreateProfilePermission(profileName);

        // Assert
        Assert.Equal("QueryAIProfile_TestProfile", permission.Name);
        Assert.Equal("Query AI profile - TestProfile", permission.Description);
    }

    [Fact]
    public void CreateProfilePermission_IsImpliedByQueryAnyAIProfile()
    {
        // Arrange
        var profileName = "TestProfile";

        // Act
        var permission = AIPermissions.CreateProfilePermission(profileName);

        // Assert
        Assert.NotNull(permission.ImpliedBy);
        Assert.Contains(AIPermissions.QueryAnyAIProfile, permission.ImpliedBy);
    }

    [Theory]
    [InlineData("")]
    public void CreateProfilePermission_ThrowsOnEmptyProfileName(string profileName)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => AIPermissions.CreateProfilePermission(profileName));
    }

    [Fact]
    public void CreateProfilePermission_ThrowsOnNullProfileName()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => AIPermissions.CreateProfilePermission(null));
    }
}
