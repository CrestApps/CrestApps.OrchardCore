---
sidebar_label: Playwright
sidebar_position: 11
title: AI Playwright Browser Automation
description: Configure CrestApps.OrchardCore.AI.Playwright for multi-step browser workflows and interactive page observation in Orchard Core admin.
---

# AI Playwright Browser Automation

The `CrestApps.OrchardCore.AI.Playwright` module gives an AI profile a dedicated browser that can work inside Orchard Core admin and stay available for follow-up tasks.

## What It Does

- Opens a dedicated Playwright browser window for the current AI profile
- Reuses that browser across follow-up tasks instead of closing it after every answer
- Keeps its own sign-in state and can use the saved Playwright login when the admin login page appears
- Lets the AI inspect the live page before answering questions such as "do you see the HtmlBody widget?"
- Lets the AI continue from the current page when you ask for another task
- Shows a visible AI target indicator in the browser before clicks and typing so users can see what the operator is about to act on

## Session Behavior

When Playwright is enabled on an AI profile:

- The browser stays open after a task completes
- The assistant is expected to ask whether you have another task for the same browser session
- If you continue, the next task reuses the same browser and current page context
- If you are done, the browser remains available until the inactivity timeout closes it naturally

The session timeout follows the AI profile session inactivity timeout when the profile provides one. Otherwise, Playwright uses a 30 minute inactivity timeout.

## Interactive Page Guidance

The Playwright operator supports live page observation, not just one-shot execution.

Examples:

- `Do you see the HtmlBody widget?`
- `What widgets do you see on this page?`
- `Can you edit it?`
- `Take a screenshot of the current page.`

When the user asks an observation question, the operator should inspect the live page first and answer from the current browser state instead of guessing from memory.

## Built-In Observation Tools

The Playwright operator now includes deterministic observation tools for live-page follow-up work:

| Tool | Purpose |
| --- | --- |
| `playwright_capture_state` | Capture the current URL, title, heading, toast, validation messages, and visible buttons |
| `playwright_get_page_content` | Return the visible text content of the current page |
| `playwright_find_element` | Find visible elements that match a text snippet, label, or widget name |
| `playwright_check_element_exists` | Check whether a requested widget, control, or text snippet is visible |
| `playwright_get_visible_widgets` | List visible widget-like cards, headings, and editor sections |
| `playwright_take_screenshot` | Save a screenshot of the current page and return the saved file path |
| `playwright_list_content_items` | List the visible Orchard content items from the current content items screen |
| `playwright_open_content_item_editor` | Open an existing Orchard content item editor by title using the current list first |

These work alongside the Orchard admin task tools such as opening content items, starting a new content item, setting the title, saving drafts, and publishing.

## Content Editing Reliability

The Orchard admin automation now uses content-aware editing behavior instead of generic page clicks:

- When the assistant needs to edit an existing content item from the content list, it scopes the action to the row that contains the requested title before clicking `Edit`
- If one visible content item is already the clear match, the assistant should open it directly instead of asking for extra confirmation
- When the assistant is already on the content items screen, follow-up requests should continue from that screen instead of restarting from admin home
- When you ask to list content items, the assistant should list the visible titles directly instead of asking whether it should list them
- When you ask to open an existing content item, the assistant can use the current list and a best-match title search before falling back to broader navigation
- When the assistant fills `HtmlBody` or other body-like fields, it checks whether the live editor is a textarea, rich text surface, contenteditable region, or iframe editor
- For body-like fields, the assistant appends new text by default unless you explicitly tell it to replace the existing content
- Field edits do not automatically save or publish. The assistant should pause after editing unless you explicitly ask it to save or publish.
- Publish and save results are only reported as successful after the browser observation shows a confirming state such as a toast, heading, or resulting editor URL
- If Orchard returns an application error such as `database is locked`, the assistant should report that exact app failure instead of pretending the browser action succeeded
- If a retry is needed, the assistant should summarize the final verified state instead of sending conflicting success and failure messages

## Configuration

In the AI Profile editor, keep the Playwright configuration simple:

- Enable Playwright browser automation
- Provide the Playwright login username and password if the browser should sign in automatically when it reaches Orchard admin login
- Optionally enable publish-by-default if you want task completion to publish unless you explicitly ask for a draft

The browser interaction style is fixed to the dedicated Playwright browser workflow. End users do not need to choose browser modes.

## Multi-Step Workflow Example

1. `Create a new SitePage titled "Launch Draft".`
2. The assistant completes the task and asks whether there is another task for the same browser session.
3. You reply: `Yes. Do you see the HtmlBody widget?`
4. The assistant inspects the current page and answers from the live editor state.
5. You reply: `Can you edit it?`
6. The assistant confirms whether the widget is visible and editable, then asks what you want to add.

## Notes

- The operator should prefer Orchard-specific tools before using raw browser fallback tools.
- Observation questions should use the inspection tools before answering.
- The browser is not closed automatically after every successful task anymore.
- The browser window now shows a visible indicator for click and typing attempts to make the live automation easier to follow.
