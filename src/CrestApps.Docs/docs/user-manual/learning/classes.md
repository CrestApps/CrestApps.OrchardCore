---
sidebar_label: Classes
title: Classes
description: "Add a class to a course: dated sessions with places and a registration window, learners who register or wait for a place, and a roster to mark who attended."
technical_manual:
  - modules/learning-classes
---

A class is a lesson that learners attend, in a room or online. It has dated sessions, and each learner registers for one of them. When they attend, the class counts as done.

| | |
| --- | --- |
| **Menu** | A course's outline (add a **Class** item); **Sessions** on the class's editor |
| **Permission** | Manage learning classes (given with Manage courses); Manage the courses you teach for a course's instructors and assistants; Access learning for learners to register |
| **Feature** | Learning Classes (turns on Scheduling) |

<AskYourAdmin />

## Add a class

1. In the course's list of lessons, create a **Class** and give it a title.
2. On the **Class** card, choose the **Appointment type** the sessions follow, such as whether a place must be confirmed and how late it can be cancelled. Sessions cannot be saved without one. Appointment types are set up in [Scheduling](../calendars/scheduling.md#describe-an-appointment-type).
3. Choose the **Instructors and rooms** new sessions use. The first one's calendar holds each session.
4. Under **Defaults of new sessions**, set the **Capacity** (how many learners a session takes), the **Location** or the **Meeting link**, and **When a session is full**:
   - **Offer the place to the next person, held for a while**: when a place frees up, it is held for the next person waiting until the offer runs out, then passed on.
   - **Tell the next person, who registers themselves**: the next person is told a place is free.
   - **No waitlist: a full session refuses**.
5. Under **Attendance**, set **Minimum minutes** if a learner who attends part of a session should still complete the class. Leave it empty so only full attendance completes it.
6. Save. The class counts as done when the learner attends, unless you choose **When the learner marks it done** on its **Course item** card.

## Add sessions

Save the class first, then select **Sessions** on its editor. The **Sessions** page lists each session with its dates, the places taken and how many people wait.

1. Select **Add session**. The editor is filled from the class's defaults.
2. Give the session a **Name** if you like, such as the class name with its month. Leave it empty to use the class name.
3. Under **Meetings**, enter the date, start and end of each meeting, in your time zone. **Add a meeting** adds a row. One registration covers every meeting. Clear a meeting's date to remove it.
4. Set the **Capacity**, the **Location** or the **Meeting link**. Only learners with a place see the link.
5. Under **Registration**, set when it **Opens** and **Closes**. Empty opens it at once and closes it when the first meeting starts.
6. Choose **When it is full** and, for offers, **Offer held for (hours)**.
7. **Save**.

Learners with a place see each meeting on their own calendar. When you move a session to other times, they are told.

To cancel a session, choose **Cancel the session** in its actions, give the **Reason** and confirm. Every place is cancelled and the waitlist is emptied. Learners who had a place are told, with the reason.

## How learners register

A learner enrolled in the course opens the class in the course player. It lists the upcoming sessions with their dates, location, places left and when registration opens or closes.

- **Register** takes a place. When the appointment type needs confirmation, the learner waits until someone confirms it.
- When a session is full, **Join the waitlist** puts the learner in line. When a place frees up, they are offered it, with a time to **Accept the place** before it passes to the next person, or told it is free, as the session says.
- **Give up my place** and **Leave the waitlist** do what they say. The place goes to the next person waiting.

A learner holds one place per class. When their enrollment ends before they complete the course, their places are given up.

## Take attendance

On the **Sessions** page, select **Roster** for a session. It lists the registered learners with their status, and the waitlist below.

1. Mark each learner **Attended**, **Partly attended** with the minutes, **Absent** or **Not marked**.
2. Select **Save attendance**.

A learner who attended, or attended part of a session for at least the class's minimum minutes, completes the class. A learner who did not attend enough can register for another session.

## Classes for staff

When your site plans shifts, a staff member's confirmed class shows as a **Training** segment on the published shift that covers each meeting. It is taken off again when the place is cancelled.
