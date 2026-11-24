---
sidebar_label: Learning statements
title: Learning statements
description: "See the learning statements that courses and learning tools store on your site: who did what and when, with the score and result, and statements that were voided."
technical_manual:
  - modules/learning-xapi
---

Some learning content reports what learners do as learning statements, such as "Ana completed Safety basics with 92%". cmi5 courses and other learning tools that use xAPI send them to your site, which keeps them in its learning record store. **Learning Statements** lists them.

| | |
| --- | --- |
| **Menu** | **Learning** → **Learning Statements** |
| **Permission** | Read all learning statements of the Learning Record Store |
| **Feature** | Learning Record Store |

<AskYourAdmin />

## Find a statement

Go to **Learning** → **Learning Statements**. The newest statements come first, each with who did it, what they did, the result and when it was stored.

- Search by actor: a user name, an e-mail address or an account name.
- Filter by **Verb** (what was done, such as completed or passed), **Activity IRI** (the web address that names the activity), **Registration** (one attempt at a course) and the days it was stored, from **Stored from** to **Stored to**. The days are in your time zone. **Clear** removes the filters.

The result column shows whether the learner completed the activity, whether they succeeded and their score, when the statement has them.

## Open a statement

Select **View** to open a statement. Its page shows the statement id, the verb, the object, the registration, who stored it, its timestamp and when it was stored, and any attachments. The times at the top are in your time zone. **Statement as stored** shows the full statement; the times inside it are in UTC, as the standard keeps them.

## Voided statements

A statement cannot be changed or deleted. To cancel one, a tool sends a new statement that voids it. A voided statement is marked **Voided** in the list, and its page links to the statement that voided it. Reports and tools that ask for statements no longer get it.

## Who can send statements

Learning tools connect to the learning record store as applications your administrator sets up, never with someone's sign-in. Courses launched from your site, such as cmi5 packages, get their own short-lived access. See the Technical Manual for how to connect a tool.
