using CrestApps.OrchardCore.Users.Services;
using OrchardCore.Localization;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;

namespace CrestApps.OrchardCore.Tests.Users;

/// <summary>
/// The site's notification preferences edit the same <see cref="UserNotificationPreferencesPart"/> the Notifications
/// module's profile editor does, with its rules: a method turned off is off for every type, and a type stores only the
/// choices that differ from its defaults.
/// </summary>
public sealed class UserNotificationPreferencesFormTests
{
    private static readonly string[] _methods = ["Email", "SMS"];

    [Fact]
    public void Read_ShowsTheTypeDefaults_ForAPersonWhoChoseNothing()
    {
        var enrolled = Type("LearningEnrolled");
        var graded = Type("LearningAttemptGraded", disabled: "SMS");

        var form = UserNotificationPreferencesForm.Read(null, _methods, [enrolled, graded]);

        Assert.Equal(_methods, form.EnabledMethods);
        Assert.Equal(_methods, form.TypeMethods["LearningEnrolled"]);
        Assert.Equal(["Email"], form.TypeMethods["LearningAttemptGraded"]);
    }

    [Fact]
    public void Apply_TurnsAMethodOffForEveryType_AndStoresOnlyTheTypeChoicesThatDifferFromTheDefaults()
    {
        // Arrange
        var part = new UserNotificationPreferencesPart();
        var enrolled = Type("LearningEnrolled");
        var graded = Type("LearningAttemptGraded", disabled: "SMS");

        // Act: SMS off for everything; e-mail off for enrolment notices; graded notices as they come.
        UserNotificationPreferencesForm.Apply(
            part,
            _methods,
            [enrolled, graded],
            enabledMethods: ["Email"],
            typeMethods: new Dictionary<string, string[]>
            {
                ["LearningEnrolled"] = ["SMS"],
                ["LearningAttemptGraded"] = ["Email"],
            });

        // Assert
        Assert.Equal(["SMS"], part.Optout);
        Assert.False(NotificationTypePreferences.IsMethodEnabledForType(part, enrolled, "Email"));
        Assert.True(NotificationTypePreferences.IsMethodEnabledForType(part, graded, "Email"));
        Assert.False(part.Types.ContainsKey("LearningAttemptGraded"));

        var read = UserNotificationPreferencesForm.Read(part, _methods, [enrolled, graded]);
        Assert.Equal(["Email"], read.EnabledMethods);
        Assert.Equal(["SMS"], read.TypeMethods["LearningEnrolled"]);
    }

    [Fact]
    public void Apply_WithATypeLeftOut_TurnsEveryMethodOffForIt()
    {
        var part = new UserNotificationPreferencesPart();
        var enrolled = Type("LearningEnrolled");

        UserNotificationPreferencesForm.Apply(part, _methods, [enrolled], enabledMethods: _methods, typeMethods: null);

        Assert.All(_methods, method => Assert.False(NotificationTypePreferences.IsMethodEnabledForType(part, enrolled, method)));
    }

    private static NotificationTypeDescriptor Type(string name, params string[] disabled)
    {
        var type = new NotificationTypeDescriptor(name, LocalizationSource.Create<UserNotificationPreferencesFormTests>("Test notification type"));

        foreach (var method in disabled)
        {
            type.DisabledMethods.Add(method);
        }

        return type;
    }
}
