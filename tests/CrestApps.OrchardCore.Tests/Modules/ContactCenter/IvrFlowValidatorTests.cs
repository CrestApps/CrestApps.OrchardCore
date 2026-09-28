using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class IvrFlowValidatorTests
{
    [Fact]
    public void Validate_AcceptsARunnableFlow()
    {
        // Arrange
        var flow = new IvrFlow
        {
            RootNodeId = "main",
            Nodes =
            [
                new IvrNode
                {
                    NodeId = "main",
                    Prompt = "Press 1 for sales, 2 for support, 9 to hear this again.",
                    Options =
                    [
                        new IvrOption { Digit = "1", Action = new IvrAction { Kind = IvrActionKind.RouteToQueue, TargetId = "sales" } },
                        new IvrOption { Digit = "2", Action = new IvrAction { Kind = IvrActionKind.SubMenu, TargetId = "support" } },
                        new IvrOption { Digit = "9", Action = new IvrAction { Kind = IvrActionKind.Repeat } },
                    ],
                },
                new IvrNode
                {
                    NodeId = "support",
                    PromptMediaId = "media-1",
                    Options =
                    [
                        new IvrOption { Digit = "0", Action = new IvrAction { Kind = IvrActionKind.Voicemail } },
                    ],
                },
            ],
            FallbackAction = new IvrAction { Kind = IvrActionKind.RouteToQueue, TargetId = "general" },
        };

        // Act
        var errors = IvrFlowValidator.Validate(flow);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_RejectsARootThatIsNotAmongTheMenus()
    {
        // Arrange
        var flow = new IvrFlow
        {
            RootNodeId = "missing",
            Nodes = [ValidNode("main")],
        };

        // Act
        var errors = IvrFlowValidator.Validate(flow);

        // Assert
        var error = Assert.Single(errors);
        Assert.Equal(IvrFlowValidationErrorKind.RootNodeNotFound, error.Kind);
        Assert.Equal("missing", error.TargetId);
    }

    [Fact]
    public void Validate_RejectsAFlowWithNoRoot()
    {
        // Arrange
        var flow = new IvrFlow { Nodes = [ValidNode("main")] };

        // Act
        var errors = IvrFlowValidator.Validate(flow);

        // Assert
        Assert.Contains(errors, error => error.Kind == IvrFlowValidationErrorKind.RootNodeMissing);
    }

    [Fact]
    public void Validate_RejectsDuplicateMenuIdentifiers()
    {
        // Arrange
        var flow = new IvrFlow
        {
            RootNodeId = "main",
            Nodes = [ValidNode("main"), ValidNode("main")],
        };

        // Act
        var errors = IvrFlowValidator.Validate(flow);

        // Assert
        var error = Assert.Single(errors, candidate => candidate.Kind == IvrFlowValidationErrorKind.NodeIdDuplicate);
        Assert.Equal("main", error.NodeId);
    }

    [Fact]
    public void Validate_RejectsAMenuTheCallerWouldHearNothingOn()
    {
        // Arrange
        var node = ValidNode("main");
        node.Prompt = null;
        node.PromptMediaId = null;

        var flow = new IvrFlow { RootNodeId = "main", Nodes = [node] };

        // Act
        var errors = IvrFlowValidator.Validate(flow);

        // Assert
        var error = Assert.Single(errors);
        Assert.Equal(IvrFlowValidationErrorKind.NodePromptMissing, error.Kind);
        Assert.Equal("main", error.NodeId);
    }

    [Fact]
    public void Validate_RejectsAMenuWithNoKeys()
    {
        // Arrange
        var flow = new IvrFlow
        {
            RootNodeId = "main",
            Nodes = [new IvrNode { NodeId = "main", Prompt = "Hello" }],
        };

        // Act
        var errors = IvrFlowValidator.Validate(flow);

        // Assert
        var error = Assert.Single(errors);
        Assert.Equal(IvrFlowValidationErrorKind.NodeHasNoOptions, error.Kind);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("a")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_RejectsAKeyThatIsNotATelephoneKey(string digit)
    {
        // Arrange
        var flow = new IvrFlow
        {
            RootNodeId = "main",
            Nodes =
            [
                new IvrNode
                {
                    NodeId = "main",
                    Prompt = "Hello",
                    Options = [new IvrOption { Digit = digit, Action = new IvrAction { Kind = IvrActionKind.Voicemail } }],
                },
            ],
        };

        // Act
        var errors = IvrFlowValidator.Validate(flow);

        // Assert
        var error = Assert.Single(errors);
        Assert.Equal(IvrFlowValidationErrorKind.OptionDigitInvalid, error.Kind);
    }

    [Fact]
    public void Validate_RejectsTwoOptionsOnTheSameKey()
    {
        // Arrange
        var flow = new IvrFlow
        {
            RootNodeId = "main",
            Nodes =
            [
                new IvrNode
                {
                    NodeId = "main",
                    Prompt = "Hello",
                    Options =
                    [
                        new IvrOption { Digit = "1", Action = new IvrAction { Kind = IvrActionKind.Voicemail } },
                        new IvrOption { Digit = "1", Action = new IvrAction { Kind = IvrActionKind.RouteToQueue, TargetId = "q" } },
                    ],
                },
            ],
        };

        // Act
        var errors = IvrFlowValidator.Validate(flow);

        // Assert
        var error = Assert.Single(errors);
        Assert.Equal(IvrFlowValidationErrorKind.OptionDigitDuplicate, error.Kind);
        Assert.Equal("1", error.Digit);
    }

    [Fact]
    public void Validate_RejectsAnOptionThatDoesNothing()
    {
        // Arrange
        var flow = new IvrFlow
        {
            RootNodeId = "main",
            Nodes =
            [
                new IvrNode
                {
                    NodeId = "main",
                    Prompt = "Hello",
                    Options = [new IvrOption { Digit = "1" }],
                },
            ],
        };

        // Act
        var errors = IvrFlowValidator.Validate(flow);

        // Assert
        var error = Assert.Single(errors);
        Assert.Equal(IvrFlowValidationErrorKind.OptionActionMissing, error.Kind);
    }

    [Theory]
    [InlineData(IvrActionKind.RouteToQueue)]
    [InlineData(IvrActionKind.RouteToAgent)]
    [InlineData(IvrActionKind.ExternalTransfer)]
    [InlineData(IvrActionKind.SubMenu)]
    public void Validate_RejectsAnActionThatNeedsADestinationButNamesNone(IvrActionKind kind)
    {
        // Arrange
        var flow = new IvrFlow
        {
            RootNodeId = "main",
            Nodes =
            [
                new IvrNode
                {
                    NodeId = "main",
                    Prompt = "Hello",
                    Options = [new IvrOption { Digit = "1", Action = new IvrAction { Kind = kind } }],
                },
            ],
        };

        // Act
        var errors = IvrFlowValidator.Validate(flow);

        // Assert
        var error = Assert.Single(errors);
        Assert.Equal(IvrFlowValidationErrorKind.ActionTargetMissing, error.Kind);
        Assert.Equal("main", error.NodeId);
        Assert.Equal("1", error.Digit);
    }

    [Fact]
    public void Validate_RejectsASubMenuThatDoesNotExist()
    {
        // Arrange
        var flow = new IvrFlow
        {
            RootNodeId = "main",
            Nodes =
            [
                new IvrNode
                {
                    NodeId = "main",
                    Prompt = "Hello",
                    Options = [new IvrOption { Digit = "1", Action = new IvrAction { Kind = IvrActionKind.SubMenu, TargetId = "nowhere" } }],
                },
            ],
        };

        // Act
        var errors = IvrFlowValidator.Validate(flow);

        // Assert
        var error = Assert.Single(errors);
        Assert.Equal(IvrFlowValidationErrorKind.SubMenuNotFound, error.Kind);
        Assert.Equal("nowhere", error.TargetId);
    }

    [Fact]
    public void Validate_RejectsAFallbackThatNeedsADestinationButNamesNone()
    {
        // Arrange
        var flow = new IvrFlow
        {
            RootNodeId = "main",
            Nodes = [ValidNode("main")],
            FallbackAction = new IvrAction { Kind = IvrActionKind.RouteToQueue },
        };

        // Act
        var errors = IvrFlowValidator.Validate(flow);

        // Assert
        var error = Assert.Single(errors);
        Assert.Equal(IvrFlowValidationErrorKind.ActionTargetMissing, error.Kind);
        Assert.Null(error.NodeId);
    }

    [Fact]
    public void Validate_RejectsARetryCountThatAllowsNoAttempt()
    {
        // Arrange
        var flow = new IvrFlow
        {
            RootNodeId = "main",
            MaxRetries = 0,
            Nodes = [ValidNode("main")],
        };

        // Act
        var errors = IvrFlowValidator.Validate(flow);

        // Assert
        var error = Assert.Single(errors);
        Assert.Equal(IvrFlowValidationErrorKind.MaxRetriesInvalid, error.Kind);
    }

    private static IvrNode ValidNode(string nodeId)
        => new()
        {
            NodeId = nodeId,
            Prompt = "Press 1.",
            Options = [new IvrOption { Digit = "1", Action = new IvrAction { Kind = IvrActionKind.Voicemail } }],
        };
}
