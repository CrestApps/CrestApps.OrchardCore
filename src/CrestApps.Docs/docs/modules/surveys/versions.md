---
sidebar_label: Versions
title: Survey Versions
description: "Draft and immutable published versions, responses pinned to their version, the publish checks and diff, restore and compare, and bank questions copied into versions."
user_manual:
  - user-manual/surveys/publish-and-versions
---

## Draft and versions

The designer edits only the draft. **Publish** (`ISurveyPublisher.PublishAsync`) makes a new `SurveyVersion`: numbered 1, 2, 3..., immutable, with the publisher, the time, notes and a SHA-256 of the definition. The publish saves the survey with a concurrency check, so when two people publish the same survey at once, one commit fails (`ConcurrencyException`) and its version is rolled back: no two versions share a number. A version that a response uses is never deleted.

A response keeps the version it started on, through every page, resume and reopen. Results join versions by stable question ids and name each column after the newest version.

## Publishing

1. **Check** the draft (`ISurveyDefinitionValidator`): unique ids and names, at least one question to answer, registered question types, rules and branches (no forward references, no cycles, existing sources and choices), formulas and patterns that compile, pools that can draw enough, complete scoring when scoring is on, valid endings and quotas, translations of existing items, each question type's own checks and every `ISurveyDefinitionValidationContributor`. Errors block publishing; warnings show.
2. **Copy bank questions** into the version (below).
3. **Compare** with the published version (`ISurveyDefinitionDiffer`): added, removed, changed and moved pages, questions, choices, rows, endings and variables. Changes that affect existing answers are flagged: a question type changed, a question with answers removed, a choice removed or retired, a scoring key changed, or a question made required. The publish page asks for confirmation when one is flagged.
4. **Create** version N+1, clear the "unpublished changes" flag, open the survey on its first publish and create its default web link.
5. Raise `ISurveyObserver.PublishedAsync` (workflow event, audit trail, webhook).

**Restore** copies an old version's definition into the draft. It needs Manage as well as Publish on the survey, and like **Discard draft** it refuses a draft another author changed after the page was shown. **Compare** shows the changes between two versions or the draft.

## Bank questions

When a pool draws from question banks, publishing copies the matching questions (not retired, with the tags and difficulty asked for) into the version's pool, with their bank question id as question id and the bank revision in `Properties["bankRevision"]`. A version is therefore self-contained. Each draw adds at most 500 candidates by default (2,000 at most). When a bank question changes after publishing, the survey must be published again to use the change.

Importing a file into a bank again (QTI, CSV, Excel, Moodle) updates each question it imported before (by `BankQuestion.SourceIdentifier`) and keeps the ids of its choices and rows (`SurveyDefinitionCloner.KeepIds`), so earlier answers keep reading and a regrade with the new key still scores them. A choice matches by its text first, then by its value, then by its place; a row by its text, then its name, then its place. A value or name that only numbers the item (`1`, `2`, `r1`: what a spreadsheet, or a QTI file without identifiers, gives) is no identifier and is never matched on, so choices put in another order, or a new one added at the top, keep their ids. A choice the file no longer has stays, retired, so answers given to it still read. A retired choice or row comes back only when its text returns: it never takes part in the value or place matches, so a new choice never takes its id (and its answers). A choice or row matched by its place only (its text changed and neither side has an identifier, as when a typo is fixed in a spreadsheet) keeps the earlier id and answers, and the import summary (`QuestionBankImportSummary.Notices`) lists it so the author can check it means the same. **Check the file** lists these too before anything is saved: it runs the same matching as a dry run (`IQuestionBankManager.CheckImportAsync`), which also counts the questions the import would add and update and reports the ones it would refuse.

## The versions list

The **Versions** page is paged and reads a page of `SurveyVersionIndex` (number, date, publisher and notes, the notes cut to 500 characters), so it loads no version and its definition. Publishing numbers the new version from the index, and **Regrade versions** lists the versions from it too. On a site upgraded from a version without the publisher and notes columns, the `Surveys.SurveyVersionsReindex` resumable step fills them; until it reaches a version, the list reads that version itself.
