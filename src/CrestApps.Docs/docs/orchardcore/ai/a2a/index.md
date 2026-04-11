---
sidebar_label: Overview
sidebar_position: 1
title: Agent-to-Agent Protocol (A2A)
description: Overview of A2A client and host support for inter-agent communication using the Agent-to-Agent protocol.
---

# Agent-to-Agent Protocol (A2A)

The [Agent-to-Agent (A2A) protocol](https://github.com/a2aproject/A2A) is an open standard that enables seamless communication between AI agents. It allows agents to discover, communicate with, and delegate tasks to other agents, regardless of their underlying implementation or hosting environment.

## Features Overview

CrestApps provides both **client** (Agent Connections) and **host** A2A support:

| Feature | Feature ID | Description |
|---------|-----------|-------------|
| [A2A Client (Agent Connections)](client) | `CrestApps.OrchardCore.AI.A2A` | Connect to remote A2A hosts and use their agents |
| [A2A Host](host) | `CrestApps.OrchardCore.AI.A2A.Host` | Expose Agent AI Profiles via the A2A protocol |

## How It Works

The A2A protocol uses JSON-RPC 2.0 over HTTP for agent communication. Agents expose their capabilities through **agent cards** served at a well-known endpoint (`/.well-known/agent-card.json`). Client agents discover available agents through these cards and send messages using the A2A message format.

### Key Concepts

| Concept | Description |
|---------|-------------|
| **Agent Card** | A JSON document describing an agent's capabilities, skills, and metadata |
| **Skills** | Specific capabilities an agent can perform (e.g., "translate text", "summarize document") |
| **Task** | A unit of work sent to an agent, tracked through its lifecycle |
| **Message** | A communication between agents containing text parts and metadata |

## AI Functions

When the A2A client feature is enabled, the following system AI functions become available to all AI profiles:

| Function | Description |
|----------|-------------|
| `listAvailableAgents` | Lists all available agents (local and remote) with their names, descriptions, and capabilities |
| `findAgentForTask` | Uses keyword matching to find the most relevant agent for a given task description |
| `findToolsForTask` | Uses keyword matching to find the most relevant AI tools for a given task description |

## Agent Card Caching

To optimize performance, agent cards fetched from remote A2A hosts are cached for **15 minutes** per connection. The cache is automatically invalidated when a connection is updated or deleted, ensuring fresh data is loaded on the next request.
