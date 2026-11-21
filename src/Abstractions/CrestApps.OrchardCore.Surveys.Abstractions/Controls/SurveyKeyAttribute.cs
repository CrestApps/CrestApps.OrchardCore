namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// Marks a property of a question type's settings that is part of the answer key, so the respondent projection leaves it
/// out.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class SurveyKeyAttribute : Attribute
{
}
