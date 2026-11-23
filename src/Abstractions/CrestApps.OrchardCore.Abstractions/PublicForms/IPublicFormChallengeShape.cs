namespace CrestApps.OrchardCore.PublicForms;

/// <summary>
/// What an <see cref="IPublicFormChallenge"/> shows on the form, such as the ReCaptcha widget and its script. A
/// challenge that shows something implements it too: the <c>PublicFormChallenge</c> shape, which every public form
/// renders, renders the shape of each configured challenge.
/// </summary>
public interface IPublicFormChallengeShape
{
    /// <summary>
    /// Gets the type of the shape that shows the challenge on the form.
    /// </summary>
    string ShapeType { get; }
}
