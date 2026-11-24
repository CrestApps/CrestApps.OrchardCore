using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

/// <summary>
/// The SCORM 1.2 run-time API over a learner's data: the eight functions a SCO calls, with the session states (not
/// initialized, running, finished) and the last error. The script the package frame runs implements the same rules; the
/// server uses this class to replay what the frame sends, so a value the browser accepted is never trusted.
/// </summary>
public sealed class Scorm12Api
{
    private const string True = "true";
    private const string False = "false";

    private State _state;
    private string _lastError = Scorm12Errors.NoError;

    /// <summary>
    /// Initializes a new instance of the <see cref="Scorm12Api"/> class.
    /// </summary>
    /// <param name="cmi">The data.</param>
    public Scorm12Api(Scorm12Cmi cmi)
    {
        ArgumentNullException.ThrowIfNull(cmi);

        Cmi = cmi;
    }

    private enum State
    {
        NotInitialized,
        Running,
        Finished,
    }

    /// <summary>Gets the data.</summary>
    public Scorm12Cmi Cmi { get; }

    /// <summary>Gets whether <c>LMSInitialize</c> succeeded and <c>LMSFinish</c> has not been called.</summary>
    public bool IsRunning => _state == State.Running;

    /// <summary>Gets whether <c>LMSFinish</c> succeeded.</summary>
    public bool IsFinished => _state == State.Finished;

    /// <summary><c>LMSInitialize("")</c>: starts the session. The parameter must be empty; a second call is a general exception.</summary>
    public string LMSInitialize(string parameter)
    {
        if (!string.IsNullOrEmpty(parameter))
        {
            return Fail(Scorm12Errors.InvalidArgument);
        }

        if (_state != State.NotInitialized)
        {
            return Fail(Scorm12Errors.GeneralException);
        }

        _state = State.Running;

        return Succeed();
    }

    /// <summary><c>LMSFinish("")</c>: ends the session. The parameter must be empty.</summary>
    public string LMSFinish(string parameter)
    {
        if (!string.IsNullOrEmpty(parameter))
        {
            return Fail(Scorm12Errors.InvalidArgument);
        }

        if (_state != State.Running)
        {
            return Fail(Scorm12Errors.NotInitialized);
        }

        _state = State.Finished;

        return Succeed();
    }

    /// <summary><c>LMSGetValue(element)</c>: an empty string and an error when the element cannot be read.</summary>
    public string LMSGetValue(string element)
    {
        if (_state != State.Running)
        {
            Fail(Scorm12Errors.NotInitialized);

            return string.Empty;
        }

        var read = Scorm12DataModel.Get(Cmi, element ?? string.Empty);

        _lastError = read.Error;

        return read.Value;
    }

    /// <summary><c>LMSSetValue(element, value)</c>.</summary>
    public string LMSSetValue(string element, string value)
    {
        if (_state != State.Running)
        {
            return Fail(Scorm12Errors.NotInitialized);
        }

        var error = Scorm12DataModel.Set(Cmi, element ?? string.Empty, value);

        return error == Scorm12Errors.NoError ? Succeed() : Fail(error);
    }

    /// <summary><c>LMSCommit("")</c>. The parameter must be empty.</summary>
    public string LMSCommit(string parameter)
    {
        if (!string.IsNullOrEmpty(parameter))
        {
            return Fail(Scorm12Errors.InvalidArgument);
        }

        return _state != State.Running ? Fail(Scorm12Errors.NotInitialized) : Succeed();
    }

    /// <summary><c>LMSGetLastError()</c>: the code of the last call; reading it changes nothing.</summary>
    public string LMSGetLastError()
        => _lastError;

    /// <summary><c>LMSGetErrorString(code)</c>.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "One of the eight functions of the run-time API instance.")]
    public string LMSGetErrorString(string code)
        => Scorm12Errors.GetText(code);

    /// <summary><c>LMSGetDiagnostic(code)</c>: the text of the code, or of the last error when the code is empty.</summary>
    public string LMSGetDiagnostic(string code)
        => Scorm12Errors.GetText(string.IsNullOrEmpty(code) ? _lastError : code);

    private string Succeed()
    {
        _lastError = Scorm12Errors.NoError;

        return True;
    }

    private string Fail(string error)
    {
        _lastError = error;

        return False;
    }
}
