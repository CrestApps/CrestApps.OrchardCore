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

A session is a Scheduling `SchedulingSession` with the source `Learning.Class` (`ClassesConstants.SessionSource`) and the class item's content item id as its source id. It has one or more meetings, its own capacity, a location or a meeting link, an optional registration window (it opens at once and closes when the first meeting starts when they are empty), what happens when it is full, and how many hours an offered place is held. One registration covers every meeting. The session is one calendar event with its attendees, so reminders, invitations and room conflicts come from [Calendars](./calendars).

The **Sessions** page of a class (`/Admin/learning/classes/{classItemId}/sessions`) lists its sessions with their dates in the user's time zone, the places taken of the capacity, how many people wait and the status. **Add session** opens the session editor, filled from the class's defaults; its meetings are a date, a start and an end in local time, converted to UTC with `ILocalClock`, and **Add a meeting** adds a row. **Roster** opens the roster, and **Cancel the session** asks for a reason, then cancels every registration and empties the waitlist.

## Registration and the waitlist

On the class item in the learner player, a learner sees the upcoming sessions with their dates, location, places left and registration window, and their own place: registered, waiting, offered with the time the offer runs out, attended or marked absent. The meeting link shows only to a learner with a place.

A learner with an active enrollment in the class's course registers for one session (`IClassRegistrationService.RegisterAsync`). The registration is a Scheduling appointment whose source id is the enrollment and the class item, so a learner holds one place per class. When the session is full and it has a waitlist, the learner waits; when a place frees up, Scheduling offers it to the next person (held for the offer hours, then passed on) or tells them, as the session's waitlist mode says. The learner accepts the offer, or gives up a place or leaves the waitlist, from the same panel. An enrollment that ends without being completed gives its places up, so the next person is offered them.

The panel's forms post to `/learning/classes/{classItemId}/register`, `/accept` and `/cancel`. They act on the signed-in learner's own newest open enrollment in the class's course, found from the class item; only the class item and the session come from the form.

## Attendance

The **Roster** page of a session lists the registered learners with their status, and the waitlist below. Staff mark each learner **Attended**, **Partly attended** with the minutes, **Absent** or **Not marked**, and save; `ISchedulingSessionService.RecordAttendanceAsync` records it, and saving the same roster again changes nothing. After the change is saved, an attendance that counts (attended, or partly attended for at least the class's minimum minutes) completes the class item for the learner through `IAppointmentObserver`.

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
- **Workforce shift segments** for staff classes and **Time Tracker** entries for attendance come in a later slice.
- Nothing creates online meeting links; paste one on the class or the session.
