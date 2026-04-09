// @ts-check

/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  docsSidebar: [
    'intro',
    'getting-started',
    {
      type: 'category',
      label: 'Orchard Core',
      collapsed: false,
      items: [
        {
          type: 'category',
          label: 'Artificial Intelligence Suite',
          collapsed: false,
          items: [
            'orchardcore/ai/index',
            'orchardcore/ai/overview',
            'orchardcore/ai/chat',
            'orchardcore/ai/chat-analytics',
            'orchardcore/ai/chat-interactions',
            'orchardcore/ai/profiles-code',
            'orchardcore/ai/tools',
            'orchardcore/ai/workflows',
            'orchardcore/ai/consuming-ai-services',
            'orchardcore/ai/copilot',
            'orchardcore/ai/agent',
            'orchardcore/ai/prompt-templates',
            'orchardcore/ai/profile-templates',
            'orchardcore/ai/memory',
            'orchardcore/ai/chat-notifications',
            'orchardcore/ai/response-handlers',
            'orchardcore/ai/migration-typed-deployments',
            {
              type: 'category',
              label: 'A2A (Agent-to-Agent)',
              items: [
                'orchardcore/ai/a2a/index',
                'orchardcore/ai/a2a/client',
                'orchardcore/ai/a2a/host',
              ],
            },
            {
              type: 'category',
              label: 'AI Providers',
              items: [
                'orchardcore/ai/providers/index',
                'orchardcore/ai/providers/azure-ai-inference',
                'orchardcore/ai/providers/azure-openai',
                'orchardcore/ai/providers/ollama',
                'orchardcore/ai/providers/openai',
              ],
            },
            {
              type: 'category',
              label: 'Data Sources',
              items: [
                'orchardcore/ai/data-sources/index',
                'orchardcore/ai/data-sources/azure-ai',
                'orchardcore/ai/data-sources/elasticsearch',
              ],
            },
            {
              type: 'category',
              label: 'Documents',
              items: [
                'orchardcore/ai/documents/index',
                'orchardcore/ai/documents/azure-ai',
                'orchardcore/ai/documents/elasticsearch',
                'orchardcore/ai/documents/openxml',
                'orchardcore/ai/documents/pdf',
              ],
            },
            {
              type: 'category',
              label: 'Model Context Protocol (MCP)',
              items: [
                'orchardcore/ai/mcp/index',
                'orchardcore/ai/mcp/client',
                'orchardcore/ai/mcp/server',
                'orchardcore/ai/mcp/ftp',
                'orchardcore/ai/mcp/sftp',
              ],
            },
          ],
        },
        {
          type: 'category',
          label: 'Omnichannel Communications',
          items: [
            'orchardcore/omnichannel/index',
            'orchardcore/omnichannel/event-grid',
            'orchardcore/omnichannel/management',
            'orchardcore/omnichannel/sms',
          ],
        },
        {
          type: 'category',
          label: 'Standard Modules',
          items: [
            'orchardcore/modules/index',
            'orchardcore/modules/content-access-control',
            'orchardcore/modules/recipes',
            'orchardcore/modules/resources',
            'orchardcore/modules/roles',
            'orchardcore/modules/signalr',
            'orchardcore/modules/users',
          ],
        },
        {
          type: 'category',
          label: 'Samples',
          items: [
            'orchardcore/samples/index',
            'orchardcore/samples/mcp-client',
            'orchardcore/samples/a2a-client',
          ],
        },
      ],
    },
    {
      type: 'category',
      label: 'Changelog',
      items: [
        'changelog/index',
        'changelog/v2.0.1',
        'changelog/v2.0.0',
      ],
    },
  ],
};

export default sidebars;
