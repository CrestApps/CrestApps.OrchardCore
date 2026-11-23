---
sidebar_label: External tools
title: External tools (LTI)
description: "Register an LTI 1.3 tool, add its activities to a course or pick them in the tool, and let learners launch them and get their scores back."
technical_manual:
  - modules/learning-lti
---

An external tool is a learning application on another site, such as a simulator or a publisher's exercises, that works with your courses through LTI 1.3. Learners open its activities from the course, and the tool sends their scores back to the course's grades.

| | |
| --- | --- |
| **Menu** | **Learning** → **External Tools**; the course editor (Content from external tools) |
| **Permission** | Manage external tools to register tools; Manage the courses you teach (or Manage courses) to add activities and pick them in a tool |
| **Feature** | External Tools (turns on the OpenID Authorization Server, OpenID Token Validation and Secrets if they are off) |

<AskYourAdmin />

Your administrator sets up the site's sign-in server once, so that tools can sign in on their own to send scores back. The [Technical Manual](../../modules/learning-lti.md) has the settings.

## Register a tool

The tool's provider gives you their details, and you give them the site's.

1. Go to **Learning** → **External Tools** and click **Add external tool**.
2. Enter a **Name**, then the details from the provider: **Client ID**, **Deployment ID**, **Login initiation URL**, **Redirect URIs** (one a line), **Target link URI**, the **Deep Linking URL** if the tool lets you pick content, and the **Tool's JWKS URL** (or paste its key set). The client ID cannot be one that another OpenID application of the site already uses (one you made under **Security** → **OpenID Connect**, for example); the editor refuses it.
3. Under **What the tool learns**, choose what the tool may know about a person: an anonymous id only, the id and the name, or the id, the name and the e-mail address.
4. Tick **May read the course roster** and **May post grades** if the tool needs them.
5. Click **Save**.
6. Copy the details under **Give the tool these platform details** (issuer, authorization endpoint, platform JWKS URL, token endpoint, Deep Linking return URL, client ID and deployment ID) and send them to the provider.

The tool never gets a person's user id: it gets an id made for that tool only.

To stop a tool, untick **Can be launched**. Its activities stop opening and its requests are refused.

## Add an activity to a course

Add an **External tool** item to the course like any other course item. Choose the **Tool**, and if the provider gave you one, the **Target link URI** of the activity. Set the **Maximum score** if the activity counts toward the grade.

## Pick activities in the tool

If the tool supports it, you can choose activities inside the tool instead:

1. Save your changes to the course.
2. In the course editor, under **Content from external tools**, click **Pick from** and the tool's name.
3. Choose the activities in the tool and confirm.

You come back to the site, and each activity you chose is added as an External tool item at the end of the course's last section. Publish the course when you are ready.

## What learners see

An enrolled learner opens the activity and clicks **Launch**. The activity opens in the tool in a new window, already signed in. When the tool sends a final score, it becomes the item's score in the course, so the gradebook and the course's completion use it.

## Personal data

A person's data export lists the scores tools sent for them and the id each tool knows them by. Erasing a person deletes those scores here, but the tools keep their own records: ask each tool's provider to erase them, using the id from the export.

:::note
The site has not been through 1EdTech's LTI certification.
:::
