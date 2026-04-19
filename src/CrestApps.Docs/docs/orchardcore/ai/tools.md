---
title: AI Tools
description: Shared tool registration and orchestration concepts are documented in CrestApps.Core.
---

# AI Tools

The shared tool system now belongs primarily to the **CrestApps.Core** documentation:

- [Tools](https://core.crestapps.com/docs/core/tools)
- [Agents](https://core.crestapps.com/docs/core/agents)
- [MCP](https://core.crestapps.com/docs/mcp/index)
- [A2A](https://core.crestapps.com/docs/a2a/index)

Within Orchard Core, tools become useful through the modules that register or expose them:

- [AI Services](overview)
- [AI Agents](agent)
- [AI Documents](./documents/)
- [AI Data Sources](./data-sources/)
- [MCP](./mcp/)
- [A2A](./a2a/)

## Invocation Context (AIInvocationScope)

Older Orchard docs and changelog entries may still refer to `AIInvocationScope` as the shared per-request context for references, tool state, and other invocation-scoped data.

For the current framework-level explanation, see the shared Core documentation:

- [Tools](https://core.crestapps.com/docs/core/tools)
