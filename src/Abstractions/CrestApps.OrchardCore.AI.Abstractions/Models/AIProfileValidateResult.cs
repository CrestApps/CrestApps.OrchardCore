using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.AI.Models;

public class AIProfileValidateResult
{
    private readonly List<ValidationResult> _errors = [];

    public IReadOnlyList<ValidationResult> Errors
        => _errors;

    /// <summary>
    /// Success may be altered by a handler during the validating async event.
    /// </summary>
    public bool Succeeded { get; set; } = true;

    public void Fail(ValidationResult error)
    {
        Succeeded = false;

        _errors.Add(error);
    }
}
