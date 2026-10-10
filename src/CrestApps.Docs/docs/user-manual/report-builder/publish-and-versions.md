---
sidebar_label: Publish and Versions
title: Drafts, Publishing and Versions
description: How the builder saves your work as a draft, publishes it, keeps versions you can restore, and protects your changes when two people edit a report.
technical_manual:
  - modules/report-builder/drafts-versions-and-real-time
---

The builder saves your work as you go, so you never lose a change. What you save is a **draft**: the people who run the report keep seeing the published report until you click **Publish**. Each time you publish a change, the builder keeps a **version** you can go back to.

Watch the short video, then read how it works below.

<video controls preload="metadata" width="100%" poster="/img/docs/report-builder-publish-and-versions.jpg" aria-label="Video: drafts saving as you work, publishing, and restoring an earlier version">
  <source src="/img/docs/report-builder-publish-and-versions.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/report-builder-publish-and-versions.vtt" srcLang="en" label="English" default />
</video>

| | |
| --- | --- |
| **Menu** | Reports > Report Builder > *a report* > Edit |
| **Permission** | Build reports and manage own custom reports and views (your own reports); Manage all custom reports and views (anyone's) |
| **Feature** | Report Builder |

<AskYourAdmin />

## Drafts save as you work

The builder saves your changes as you work, from the first change to a new report. The words next to the title say **Saving…**, then **Draft saved**, and you can close or refresh the page at any time. If a save fails, they say **Not saved**, and the builder tries again with your next change.

Saved changes are a **draft**: people who run the report keep seeing the published version until you click **Publish**.

- A new report you have not published yet is listed on the **Report Builder** page under **Not published yet**. Click **Edit** to keep working on it, or **Delete** to throw it away. Only you, and the people who manage every report, can see it. It cannot run until it is published. Use the status filter at the top of the list to show only published reports or only the ones not published yet.
- In the builder, a bar above the tabs says when the report is saved as a draft and was never published, with a **Delete draft** button.

## Publish

Click **Publish** at the top, or press Ctrl+S.

- The builder checks the title and the sharing settings first. A report without a title is not published.
- It then checks the design. A report with problems, such as a join without matching columns, is still published, and the builder lists the problems. Until they are fixed, the report shows them instead of running.
- When the report changed since the last version, a new version is kept, and the builder says **Published as version** and its number. When nothing changed, it says so and keeps no new version.

After publishing, **Run report** at the top opens the report as people see it.

## Unpublished changes

When a published report has a draft, a bar above the tabs shows that it has **Unpublished changes**, who made them and when. People who run the report still see the published version.

- Click **Publish** at the top to make the changes live.
- Click **Discard changes** to go back to the published version.

## Versions

Each time you publish a change, the builder keeps a **version**.

1. Click **Versions** at the top to list them, with who published each one and when. A version restored from an earlier one says so.
2. Click **Preview** to see a version.
3. Click **Restore** to copy it into the draft. Check it, then publish it to make it the report people run.

Restoring never changes what people run until you publish. Old versions are removed after a while; your administrator decides how many are kept.

## Work on a report with others

Two people can open the same report, but only one change can win, so the builder protects your work:

- When someone else changed the report since you opened it, your next change is **not saved** and a yellow bar says who changed it. Click **Reload their changes** to see their version (your unsaved changes are lost), or **Keep mine** to save your version over theirs.
- When someone else is saving the same report at that moment, the builder asks you to try again in a moment.
- When real-time updates are turned on for your site, the builder also shows the initials of the others who have the report open (point at them to see their names), warns that your changes can conflict, and tells you as soon as someone saves, publishes, discards changes, restores a version or deletes the report, with a **Reload** button.

## Views save directly

[Reusable views](views.md) have no drafts or versions: click **Save** to save a view, and reports that read it use the change at once.

Next: [Reusable views](views.md).
