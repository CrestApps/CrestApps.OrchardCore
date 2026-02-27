---
sidebar_label: AI Workflows
sidebar_position: 11
title: AI Chat with Workflows
description: How to use AI completion tasks in Orchard Core Workflows.
---

# AI Chat with Workflows

When combined with the **Workflows** feature, the **AI Services** module introduces new activities that allow workflows to interact directly with AI chat services.

## AI Completion using Profile Task

This activity lets you request AI completions using an existing **AI Profile**, and store the response in a workflow property.
To use it, search for the **AI Completion using Profile** task in your workflow and specify a unique **Result Property Name**.
The generated response will be saved in this property.

For example, if the **Result Property Name** is `AI-CrestApps-Step1`, you can access the response later using:

```liquid
{{ Workflow.Output["AI-CrestApps-Step1"].Content }}
```

To prevent naming conflicts with other workflow tasks, it's recommended to prefix your **Result Property Name** with `AI-`.

## AI Completion using Direct Config Task

This activity allows you to request AI completions by defining the configuration directly within the workflow, without relying on a predefined AI Profile.
To use it, search for the **AI Completion using Direct Config** task in your workflow and specify a unique **Result Property Name**.
The generated response will be saved in this property.

For example, if the **Result Property Name** is `AI-CrestApps-Step1`, you can access the response later using:

```liquid
{{ Workflow.Output["AI-CrestApps-Step1"].Content }}
```

As with other AI tasks, it's recommended to prefix your **Result Property Name** with `AI-` to avoid conflicts.
