using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// The SCORM 2004 run-time API (RTE section 3.1): the eight methods and the state of the API, with the error codes of
/// each state.
/// </summary>
public sealed class Scorm2004ApiTests
{
    [Fact]
    public void Initialize_Twice_Is103()
    {
        var api = new Scorm2004Api(new Scorm2004Cmi());

        Assert.Equal("true", api.Initialize(string.Empty));
        Assert.Equal(Scorm2004Errors.NoError, api.GetLastError());
        Assert.True(api.IsRunning);
        Assert.Equal("false", api.Initialize(string.Empty));
        Assert.Equal(Scorm2004Errors.AlreadyInitialized, api.GetLastError());
    }

    [Fact]
    public void Initialize_AfterTerminate_Is104()
    {
        var api = Started();

        api.Terminate(string.Empty);

        Assert.Equal("false", api.Initialize(string.Empty));
        Assert.Equal(Scorm2004Errors.ContentInstanceTerminated, api.GetLastError());
    }

    [Theory]
    [InlineData("x")]
    [InlineData("null")]
    public void ParameterThatIsNotEmpty_Is201(string parameter)
    {
        var api = new Scorm2004Api(new Scorm2004Cmi());

        Assert.Equal("false", api.Initialize(parameter));
        Assert.Equal(Scorm2004Errors.GeneralArgumentError, api.GetLastError());

        api.Initialize(string.Empty);

        Assert.Equal("false", api.Commit(parameter));
        Assert.Equal(Scorm2004Errors.GeneralArgumentError, api.GetLastError());
        Assert.Equal("false", api.Terminate(parameter));
        Assert.Equal(Scorm2004Errors.GeneralArgumentError, api.GetLastError());
        Assert.True(api.IsRunning);
    }

    [Fact]
    public void BeforeInitialize_EachMethodHasItsOwnCode()
    {
        var api = new Scorm2004Api(new Scorm2004Cmi());

        Assert.Equal(string.Empty, api.GetValue("cmi.completion_status"));
        Assert.Equal(Scorm2004Errors.RetrieveDataBeforeInitialization, api.GetLastError());
        Assert.Equal("false", api.SetValue("cmi.location", "a"));
        Assert.Equal(Scorm2004Errors.StoreDataBeforeInitialization, api.GetLastError());
        Assert.Equal("false", api.Commit(string.Empty));
        Assert.Equal(Scorm2004Errors.CommitBeforeInitialization, api.GetLastError());
        Assert.Equal("false", api.Terminate(string.Empty));
        Assert.Equal(Scorm2004Errors.TerminationBeforeInitialization, api.GetLastError());
    }

    [Fact]
    public void AfterTerminate_EachMethodHasItsOwnCode()
    {
        var api = Started();

        Assert.Equal("true", api.Terminate(string.Empty));
        Assert.True(api.IsTerminated);

        Assert.Equal(string.Empty, api.GetValue("cmi.completion_status"));
        Assert.Equal(Scorm2004Errors.RetrieveDataAfterTermination, api.GetLastError());
        Assert.Equal("false", api.SetValue("cmi.location", "a"));
        Assert.Equal(Scorm2004Errors.StoreDataAfterTermination, api.GetLastError());
        Assert.Equal("false", api.Commit(string.Empty));
        Assert.Equal(Scorm2004Errors.CommitAfterTermination, api.GetLastError());
        Assert.Equal("false", api.Terminate(string.Empty));
        Assert.Equal(Scorm2004Errors.TerminationAfterTermination, api.GetLastError());
    }

    [Fact]
    public void Running_GetSetAndCommit_Succeed()
    {
        var api = Started();

        Assert.Equal("true", api.SetValue("cmi.location", "slide-2"));
        Assert.Equal(Scorm2004Errors.NoError, api.GetLastError());
        Assert.Equal("slide-2", api.GetValue("cmi.location"));
        Assert.Equal("true", api.Commit(string.Empty));
        Assert.Equal("slide-2", api.Cmi.Location);
    }

    [Fact]
    public void EmptyElement_Get_Is301_AndSet_Is351()
    {
        var api = Started();

        Assert.Equal(string.Empty, api.GetValue(string.Empty));
        Assert.Equal(Scorm2004Errors.GeneralGetFailure, api.GetLastError());
        Assert.Equal(string.Empty, api.GetValue(null));
        Assert.Equal(Scorm2004Errors.GeneralGetFailure, api.GetLastError());
        Assert.Equal("false", api.SetValue(string.Empty, "x"));
        Assert.Equal(Scorm2004Errors.GeneralSetFailure, api.GetLastError());
    }

    [Fact]
    public void DataModelErrors_AreTheLastError_AndGetReturnsEmpty()
    {
        var api = Started();

        Assert.Equal(string.Empty, api.GetValue("cmi.exit"));
        Assert.Equal(Scorm2004Errors.WriteOnly, api.GetLastError());
        Assert.Equal("false", api.SetValue("cmi.learner_id", "x"));
        Assert.Equal(Scorm2004Errors.ReadOnly, api.GetLastError());
        Assert.Equal("false", api.SetValue("cmi.score.scaled", "2"));
        Assert.Equal(Scorm2004Errors.ValueOutOfRange, api.GetLastError());
    }

    [Fact]
    public void ErrorMethods_DoNotChangeTheLastError()
    {
        var api = Started();

        api.GetValue("cmi.location");

        Assert.Equal(Scorm2004Errors.ValueNotInitialized, api.GetLastError());
        Assert.Equal("Data Model Element Value Not Initialized", api.GetErrorString("403"));
        Assert.Equal("Data Model Element Value Not Initialized", api.GetDiagnostic(string.Empty));
        Assert.Equal("Already Initialized", api.GetDiagnostic("103"));
        Assert.Equal(string.Empty, api.GetErrorString("999"));
        Assert.Equal(Scorm2004Errors.ValueNotInitialized, api.GetLastError());
    }

    [Theory]
    [InlineData("0", "No Error")]
    [InlineData("101", "General Exception")]
    [InlineData("112", "Termination Before Initialization")]
    [InlineData("123", "Retrieve Data After Termination")]
    [InlineData("132", "Store Data Before Initialization")]
    [InlineData("143", "Commit After Termination")]
    [InlineData("201", "General Argument Error")]
    [InlineData("351", "General Set Failure")]
    [InlineData("391", "General Commit Failure")]
    [InlineData("401", "Undefined Data Model Element")]
    [InlineData("402", "Unimplemented Data Model Element")]
    [InlineData("406", "Data Model Element Type Mismatch")]
    [InlineData("408", "Data Model Dependency Not Established")]
    public void ErrorString_NamesEveryCode(string code, string text)
    {
        Assert.Equal(text, new Scorm2004Api(new Scorm2004Cmi()).GetErrorString(code));
    }

    private static Scorm2004Api Started()
    {
        var api = new Scorm2004Api(new Scorm2004Cmi());

        Assert.Equal("true", api.Initialize(string.Empty));

        return api;
    }
}
