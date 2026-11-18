---
sidebar_label: Learning Classes
title: Learning Classes
description: "The class course item of Learning: dated sessions in Scheduling, registration of enrolled learners with a waitlist and offers, a roster to mark attendance, and attendance that completes the class."
---

**Learning Classes** (`CrestApps.OrchardCore.Learning.Classes`, with `Learning.Classes.Abstractions` and `Learning.Classes.Core`) adds the **Class** course item to [Learning](./learning): "attend a class". A class has dated sessions, and an enrolled learner registers for one of them. The sessions, their places, the waitlist and its offers belong to [Scheduling](./scheduling); Learning keeps no seat counts, holds or waitlist of its own.

| Feature | Id | Depends on |
| --- | --- | --- |
| Learning Classes | `CrestApps.OrchardCore.Learning.Classes` | Learning, [Scheduling](./scheduling) |

## The class item

A class is a content item of the **Class** type (`LearningClass`, defined in code with `TitlePart`, `ClassPart` and `CourseItemPart`). Its course item kind is `Class`, and its completion mode is **Attended** by default (Marked done is the other choice). Add it to a course outline like any other course item.

`ClassPart` holds:

| Property | Meaning |
| --- | --- |
| `AppointmentTypeId` | The Scheduling appointment type whose rules (confirmation, cancel notice) the registrations follow. Sessions cannot be saved without one. |
| `ResourceIds` | The Scheduling resources (instructors, rooms) new sessions use. The first one's calendar holds the session's event. |
| `Capacity`, `Location`, `MeetingUrl`, `WaitlistMode` | The defaults of new sessions. |
| `MinimumMinutes` | The minutes a partial attendance needs to complete the class. Empty means only full attendance completes it. |

The class editor validates a capacity of at least one, minimum minutes of at least one when set, and a meeting link that starts with `https://` or `http://`. Once the class is saved, its editor links to its **Sessions** page.

## Sessions

A session is a Scheduling `SchedulingSession` with the source `Learning.Class` (`ClassesConstants.SessionSource`) and the class item's content item id as its source id. It has one or more meetings, its own capacity, a location or a meeting link, an optional registration window (it opens at once and closes when the first meeting starts when they are empty), what happens when it is full, and how many hours an offered place is held. One registration covers every meeting. When the first resource has a calendar, the session is one calendar event on it with its attendees, so reminders, invitations and room conflicts come from [Calendars](./calendars). Whatever the resources, each meeting of a session also shows on the own (personal) calendar of every learner with a confirmed or attended place, through the `LearningClassSessions` calendar event source (`ICalendarEventSource`): read-only events of the type `LearningClassSession`, read from the registrations rather than copied into events.

The **Sessions** page of a class (`/Admin/learning/classes/{classItemId}/sessions`) lists its sessions with their dates in the user's time zone, the places taken of the capacity, how many people wait and the status. **Add session** opens the session editor, filled from the class's defaults; its meetings are a date, a start and an end in local time, converted to UTC with `ILocalClock`, and **Add a meeting** adds a row. **Roster** opens the roster, and **Cancel the session** asks for a reason, then cancels every registration and empties the waitlist. With the Orchard Core **Notifications** feature on, each learner who had a place gets a **Class session cancelled** notification (`ClassesConstants.SessionCancelledNotificationType`) with the reason, through the notification center and the delivery methods they chose; a place a learner gives up, or one an ending enrollment gives up, belongs to a session that is still on, so nobody is told then.

## Registration and the waitlist

On the class item in the learner player, a learner sees the upcoming sessions with their dates, location, places left and registration window, and their own place: registered, waiting, offered with the time the offer runs out, attended or marked absent. The meeting link shows only to a learner with a place.

A learner with an active enrollment in the class's course registers for one session (`IClassRegistrationService.RegisterAsync`). The registration is a Scheduling appointment whose source id is the enrollment and the class item, so a learner holds one place per class. When the session is full and it has a waitlist, the learner waits; when a place frees up, Scheduling offers it to the next person (held for the offer hours, then passed on) or tells them, as the session's waitlist mode says. The learner accepts the offer, or gives up a place or leaves the waitlist, from the same panel. An enrollment that ends without being completed gives its places up, so the next person is offered them.

With the Orchard Core **Notifications** feature on, `ClassNotificationObserver` tells the learner, with a link to the class in the player: **Class place free** (`ClassesConstants.NotificationTypes.PlaceOffered`) when a place frees up in a session they wait for, held for them or free to take; and **Class place given** (`ClassesConstants.NotificationTypes.Registered`) when someone other than the learner, such as staff or a workflow, gives them a place. A learner who registers or accepts an offer themselves is not told.

The panel's forms post to `/learning/classes/{classItemId}/register`, `/accept` and `/cancel`. They act on the signed-in learner's own newest open enrollment in the class's course, found from the class item; only the class item and the session come from the form.

## Attendance

The **Roster** page of a session lists the registered learners with their status, and the waitlist below. Staff mark each learner **Attended**, **Partly attended** with the minutes, **Absent** or **Not marked**, and save; `ISchedulingSessionService.RecordAttendanceAsync` records it, and saving the same roster again changes nothing. After the change is saved, an attendance that counts (attended, or partly attended for at least the class's minimum minutes) completes the class item for the learner through `IAppointmentObserver`.

## Staff classes on the shift plan

When [Workforce](./workforce.md) is enabled, a class shows on the shift plan of a learner who is a staff member. Once their place in a session is confirmed (at registration, or when they accept an offer), each meeting of the session becomes a **Training** segment on their published shift that covers it, clipped to the shift and marked with the source `Learning.Class` and the registration's id; the schedule's draft gets it too, so a republish keeps it, and the change is logged on the schedule. When the place is cancelled, by the learner, by staff, by the session being cancelled or by the enrollment ending, those segments are taken off again.

Only staff members are affected (`IStaffDirectory.FindByUserIdAsync`). A meeting that no published shift covers is skipped, because the staff member isn't working then; nothing is added to the schedule for it. The Training segment uses the first enabled activity code of the Training kind. Changing a session's meetings after places are confirmed does not yet move the segments already placed.

The bridge is `ClassShiftSegmentObserver`, an `IAppointmentObserver` registered by a startup that requires the Workforce feature; it calls `IShiftService.PlaceSegmentAsync` and `RemoveSegmentsAsync` from `Workforce.Abstractions`.

## Permissions

| Permission | Meaning |
| --- | --- |
| `ManageLearningClasses` (**Manage learning classes**) | Add, change and cancel the sessions of every class, see their rosters and mark attendance. Implied by **Manage courses**. |

The instructors and assistants on the staff of a class's course manage the sessions of its classes when they also have **Manage the courses you teach**. Learners need **Access learning** to register.

## Pages

| Page | Address | Permission |
| --- | --- | --- |
| Sessions of a class | `/Admin/learning/classes/{classItemId}/sessions` | `ManageLearningClasses`, or course staff |
| Session editor | `/Admin/learning/classes/{classItemId}/sessions/new`, `/Admin/learning/class-sessions/{id}/edit` | `ManageLearningClasses`, or course staff |
| Cancel a session | `/Admin/learning/class-sessions/{id}/cancel` | `ManageLearningClasses`, or course staff |
| Roster and attendance | `/Admin/learning/class-sessions/{id}/roster` | `ManageLearningClasses`, or course staff |

## Not in this version

- **Course offerings** (`CourseOffering`, a dated run of a course with its own staff and enrollment window) are not built yet.
- **Time Tracker** entries for attendance come in a later slice.
- Nothing creates online meeting links; paste one on the class or the session.
