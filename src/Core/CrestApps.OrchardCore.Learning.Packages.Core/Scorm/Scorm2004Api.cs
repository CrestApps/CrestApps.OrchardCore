using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

/// <summary>
/// The SCORM 2004 run-time API (<c>API_1484_11</c>, RTE section 3.1) over a learner's data: the eight methods a SCO calls,
/// with the states of the API (not initialized, running, terminated) and the last error. The script the package frame
/// runs implements the same rules; the server uses this class to replay what the frame sends, so a value the browser
/// accepted is never trusted.
/// </summary>
public sealed class Scorm2004Api
{
    private const string True = "true";
    private const string False = "false";

    private State _state;
    private string _lastError = Scorm2004Errors.NoError;

    /// <summary>
    /// Initializes a new instance of the <see cref="Scorm2004Api"/> class.
    /// </summary>
    /// <param name="cmi">The data.</param>
    public Scorm2004Api(Scorm2004Cmi cmi)
    {
        ArgumentNullException.ThrowIfNull(cmi);

        Cmi = cmi;
    }

    private enum State
    {
        NotInitialized,
        Running,
        Terminated,
    }

    /// <summary>Gets the data.</summary>
    public Scorm2004Cmi Cmi { get; }

    /// <summary>Gets whether <c>Initialize</c> succeeded and <c>Terminate</c> has not been called.</summary>
    public bool IsRunning => _state == State.Running;

    /// <summary>Gets whether <c>Terminate</c> succeeded.</summary>
    public bool IsTerminated => _state == State.Terminated;

    /// <summary><c>Initialize("")</c>: starts the session; 103 when it already started, 104 after it terminated.</summary>
    public string Initialize(string parameter)
    {
        if (!string.IsNullOrEmpty(parameter))
        {
            return Fail(Scorm2004Errors.GeneralArgumentError);
        }

        switch (_state)
        {
            case State.Running:
                return Fail(Scorm2004Errors.AlreadyInitialized);
            case State.Terminated:
                return Fail(Scorm2004Errors.ContentInstanceTerminated);
            default:
                _state = State.Running;

                return Succeed();
        }
    }

    /// <summary><c>Terminate("")</c>: ends the session, which commits its data; 112 before it started, 113 after it
    /// terminated.</summary>
    public string Terminate(string parameter)
    {
        if (!string.IsNullOrEmpty(parameter))
        {
            return Fail(Scorm2004Errors.GeneralArgumentError);
        }

        switch (_state)
        {
            case State.NotInitialized:
                return Fail(Scorm2004Errors.TerminationBeforeInitialization);
            case State.Terminated:
                return Fail(Scorm2004Errors.TerminationAfterTermination);
            default:
                _state = State.Terminated;

                return Succeed();
        }
    }

    /// <summary><c>GetValue(element)</c>: an empty string and an error when the element cannot be read; 122 before the
    /// session started, 123 after it terminated.</summary>
    public string GetValue(string element)
    {
        if (_state != State.Running)
        {
            Fail(_state == State.NotInitialized ? Scorm2004Errors.RetrieveDataBeforeInitialization : Scorm2004Errors.RetrieveDataAfterTermination);

            return string.Empty;
        }

        var read = Scorm2004DataModel.Get(Cmi, element ?? string.Empty);

        _lastError = read.Error;

        return read.Value;
    }

    /// <summary><c>SetValue(element, value)</c>; 132 before the session started, 133 after it terminated.</summary>
    public string SetValue(string element, string value)
    {
        if (_state != State.Running)
        {
            return Fail(_state == State.NotInitialized ? Scorm2004Errors.StoreDataBeforeInitialization : Scorm2004Errors.StoreDataAfterTermination);
        }

        var error = Scorm2004DataModel.Set(Cmi, element ?? string.Empty, value);

        return error == Scorm2004Errors.NoError ? Succeed() : Fail(error);
    }

    /// <summary><c>Commit("")</c>; 142 before the session started, 143 after it terminated.</summary>
    public string Commit(string parameter)
    {
        if (!string.IsNullOrEmpty(parameter))
        {
            return Fail(Scorm2004Errors.GeneralArgumentError);
        }

        return _state switch
        {
            State.NotInitialized => Fail(Scorm2004Errors.CommitBeforeInitialization),
            State.Terminated => Fail(Scorm2004Errors.CommitAfterTermination),
            _ => Succeed(),
        };
    }

    /// <summary><c>GetLastError()</c>: the code of the last call; reading it changes nothing.</summary>
    public string GetLastError()
        => _lastError;

    /// <summary><c>GetErrorString(code)</c>: the text of a code, or an empty string for an unknown one.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "One of the eight methods of the run-time API instance.")]
    public string GetErrorString(string code)
        => Scorm2004Errors.GetText(code);

    /// <summary><c>GetDiagnostic(code)</c>: the text of the code, or of the last error when the code is empty.</summary>
    public string GetDiagnostic(string code)
        => Scorm2004Errors.GetText(string.IsNullOrEmpty(code) ? _lastError : code);

    private string Succeed()
    {
        _lastError = Scorm2004Errors.NoError;

        return True;
    }

    private string Fail(string error)
    {
        _lastError = error;

        return False;
    }
}
