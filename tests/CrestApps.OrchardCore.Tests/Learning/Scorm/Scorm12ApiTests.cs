using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// The SCORM 1.2 run-time API functions and session states (RTE section 3.3).
/// </summary>
public sealed class Scorm12ApiTests
{
    [Fact]
    public void BeforeInitialize_EveryCallIs301()
    {
        var api = new Scorm12Api(new Scorm12Cmi());

        Assert.Equal(string.Empty, api.LMSGetValue("cmi.core.lesson_status"));
        Assert.Equal("301", api.LMSGetLastError());
        Assert.Equal("false", api.LMSSetValue("cmi.core.lesson_status", "completed"));
        Assert.Equal("301", api.LMSGetLastError());
        Assert.Equal("false", api.LMSCommit(string.Empty));
        Assert.Equal("301", api.LMSGetLastError());
        Assert.Equal("false", api.LMSFinish(string.Empty));
        Assert.Equal("301", api.LMSGetLastError());
        Assert.False(api.IsRunning);
    }

    [Fact]
    public void Initialize_WithAnArgument_Is201()
    {
        var api = new Scorm12Api(new Scorm12Cmi());

        Assert.Equal("false", api.LMSInitialize("x"));
        Assert.Equal("201", api.LMSGetLastError());
        Assert.False(api.IsRunning);
    }

    [Fact]
    public void Initialize_Twice_Is101()
    {
        var api = new Scorm12Api(new Scorm12Cmi());

        Assert.Equal("true", api.LMSInitialize(string.Empty));
        Assert.Equal("0", api.LMSGetLastError());
        Assert.Equal("false", api.LMSInitialize(string.Empty));
        Assert.Equal("101", api.LMSGetLastError());
        Assert.True(api.IsRunning);
    }

    [Fact]
    public void Running_GetsSetsAndCommits()
    {
        var cmi = new Scorm12Cmi();
        var api = new Scorm12Api(cmi);

        api.LMSInitialize(string.Empty);

        Assert.Equal("true", api.LMSSetValue("cmi.core.lesson_status", "incomplete"));
        Assert.Equal("0", api.LMSGetLastError());
        Assert.Equal("incomplete", api.LMSGetValue("cmi.core.lesson_status"));
        Assert.Equal("true", api.LMSCommit(string.Empty));
        Assert.Equal("incomplete", cmi.LessonStatus);
    }

    [Fact]
    public void ErrorsAreKeptUntilTheNextCall_AndHaveTexts()
    {
        var api = new Scorm12Api(new Scorm12Cmi());

        api.LMSInitialize(string.Empty);

        Assert.Equal("false", api.LMSSetValue("cmi.core.student_id", "me"));
        Assert.Equal("403", api.LMSGetLastError());
        Assert.Equal("403", api.LMSGetLastError());
        Assert.Equal("Element is read only", api.LMSGetErrorString("403"));
        Assert.Equal("Incorrect Data Type", api.LMSGetErrorString("405"));
        Assert.Equal("No error", api.LMSGetErrorString("0"));
        Assert.Equal(string.Empty, api.LMSGetErrorString("999"));
        Assert.Equal("Element is read only", api.LMSGetDiagnostic(string.Empty));
        Assert.Equal("Not initialized", api.LMSGetDiagnostic("301"));
        Assert.Equal("403", api.LMSGetLastError());

        api.LMSGetValue("cmi.core.lesson_status");

        Assert.Equal("0", api.LMSGetLastError());
    }

    [Fact]
    public void CommitAndFinish_WithAnArgument_Are201()
    {
        var api = new Scorm12Api(new Scorm12Cmi());

        api.LMSInitialize(string.Empty);

        Assert.Equal("false", api.LMSCommit("now"));
        Assert.Equal("201", api.LMSGetLastError());
        Assert.Equal("false", api.LMSFinish("now"));
        Assert.Equal("201", api.LMSGetLastError());
        Assert.True(api.IsRunning);
    }

    [Fact]
    public void AfterFinish_CallsAre301AndInitializeIs101()
    {
        var api = new Scorm12Api(new Scorm12Cmi());

        api.LMSInitialize(string.Empty);

        Assert.Equal("true", api.LMSFinish(string.Empty));
        Assert.True(api.IsFinished);
        Assert.False(api.IsRunning);
        Assert.Equal("false", api.LMSSetValue("cmi.core.lesson_status", "completed"));
        Assert.Equal("301", api.LMSGetLastError());
        Assert.Equal(string.Empty, api.LMSGetValue("cmi.core.lesson_status"));
        Assert.Equal("301", api.LMSGetLastError());
        Assert.Equal("false", api.LMSFinish(string.Empty));
        Assert.Equal("301", api.LMSGetLastError());
        Assert.Equal("false", api.LMSInitialize(string.Empty));
        Assert.Equal("101", api.LMSGetLastError());
    }

    [Fact]
    public void NullArguments_AreTreatedAsTheEmptyString()
    {
        var api = new Scorm12Api(new Scorm12Cmi());

        Assert.Equal("true", api.LMSInitialize(null));
        Assert.Equal(string.Empty, api.LMSGetValue(null));
        Assert.Equal("201", api.LMSGetLastError());
    }
}
