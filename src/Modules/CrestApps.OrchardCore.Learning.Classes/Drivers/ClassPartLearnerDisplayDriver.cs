using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Learning.Classes.Services;
using CrestApps.OrchardCore.Learning.Classes.ViewModels;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Classes.Drivers;

/// <summary>
/// The class panel on the class item in the learner player: the upcoming sessions with their places and registration
/// window, the learner's place, and the forms to register, accept an offer and give up a place.
/// </summary>
internal sealed class ClassPartLearnerDisplayDriver : ContentPartDisplayDriver<ClassPart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ClassLearnerResolver _learnerResolver;
    private readonly IClassRegistrationService _registrations;
    private readonly ISchedulingSessionService _sessions;
    private readonly IClock _clock;

    public ClassPartLearnerDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        ClassLearnerResolver learnerResolver,
        IClassRegistrationService registrations,
        ISchedulingSessionService sessions,
        IClock clock)
    {
        _httpContextAccessor = httpContextAccessor;
        _learnerResolver = learnerResolver;
        _registrations = registrations;
        _sessions = sessions;
        _clock = clock;
    }

    public override IDisplayResult Display(ClassPart part, BuildPartDisplayContext context)
        => Initialize<ClassPanelViewModel>("ClassPart_Learner", async model =>
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var learner = await _learnerResolver.ResolveAsync(userId, part.ContentItem);
            var now = _clock.UtcNow;

            model.ClassItemId = part.ContentItem.ContentItemId;
            model.Part = part;
            model.SignedIn = !string.IsNullOrEmpty(userId);
            model.CourseId = learner?.CourseId;
            model.Enrolled = learner?.Enrollment is not null;
            model.EnrollmentActive = learner?.Enrollment?.Status == EnrollmentStatus.Active;

            if (learner?.Enrollment is not null)
            {
                model.Registration = await _registrations.FindAsync(learner.Enrollment.ItemId, model.ClassItemId);
            }

            foreach (var session in await _registrations.ListSessionsAsync(model.ClassItemId))
            {
                if (DateTime.SpecifyKind(session.EndUtc, DateTimeKind.Utc) <= now && session.ItemId != model.Registration?.Session?.ItemId)
                {
                    continue;
                }

                var placesLeft = Math.Max(session.Capacity - await _sessions.CountTakenAsync(session.ItemId), 0);
                var window = Window(session, now);

                model.Sessions.Add(new ClassPanelSession
                {
                    Session = session,
                    PlacesLeft = placesLeft,
                    Window = window,
                    CanRegister = model.EnrollmentActive &&
                        model.Registration is null &&
                        window == ClassRegistrationWindow.Open &&
                        (placesLeft > 0 || session.WaitlistMode != SchedulingWaitlistMode.None),
                });
            }
        }).Location("Detail", "Content:5");

    private static ClassRegistrationWindow Window(SchedulingSession session, DateTime now)
    {
        if (session.RegistrationOpensUtc is { } opens && now < DateTime.SpecifyKind(opens, DateTimeKind.Utc))
        {
            return ClassRegistrationWindow.NotYetOpen;
        }

        var closes = session.RegistrationClosesUtc ?? session.StartUtc;

        return now >= DateTime.SpecifyKind(closes, DateTimeKind.Utc)
            ? ClassRegistrationWindow.Closed
            : ClassRegistrationWindow.Open;
    }
}
