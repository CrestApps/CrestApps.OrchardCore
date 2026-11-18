---
sidebar_label: Attachments
title: Attachments
description: Attach files, such as photos and documents, to a note or comment on a record, open them, and know which files the site accepts.
technical_manual:
  - modules/attachments
---

When the **Attachments** feature is on, you can attach files to the notes and comments of a record, such as a work order or an order. The files are kept with the note: whoever may read the note may open its files.

| | |
| --- | --- |
| **Menu** | On the page of the record, in its **Notes** card |
| **Permission** | Whatever lets you add notes to that record |
| **Feature** | Attachments |

<AskYourAdmin />

## Attach files to a note

1. Open the record and find the **Notes** card.
2. Type your note, or leave the box empty to send only files.
3. Choose **Browse** (or the file box) and pick one or more files.
4. Select **Add note** or **Add comment**.

The note shows each file with its name and size. Select a file to open it: pictures open in a new tab, other files download.

## Files the site accepts

By default the site accepts pictures (JPEG, PNG, GIF, WebP), PDF, Word, Excel and PowerPoint documents, text, CSV and vCard files, up to 20 MB each and 10 at a time. Your administrator can change these limits.

The site checks what a file really is from its content, not from its name. A file that isn't one of the accepted kinds is refused, even if its name ends in `.png` or `.pdf`. When the site has a virus scanner, every file is scanned first, and an infected file is refused. When one file of several is refused, none of them is attached, and the note is not added, so you can fix it and try again.

## Remove a file

Redacting a note (see [Notes and Timeline](timeline.md)) removes its text and its files for good.
