// @ts-check

/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  docsSidebar: [
    'intro',
    'getting-started',
    {
      type: 'category',
      label: 'Artificial Intelligence Suite',
      collapsed: false,
      items: [
        'ai/index',
        'ai/ai',
        'ai/ai-chat',
        'ai/ai-chat-interactions',
        'ai/ai-profiles-code',
        'ai/ai-tools',
        'ai/ai-workflows',
        'ai/consuming-ai-services',
        'ai/ai-copilot',
        'ai/ai-agent',
        {
          type: 'category',
          label: 'AI Providers',
          items: [
            'providers/index',
            'providers/azure-ai-inference',
            'providers/azure-openai',
            'providers/ollama',
            'providers/openai',
          ],
        },
        {
          type: 'category',
          label: 'Data Sources',
          items: [
            'ai/data-sources/index',
            'ai/data-sources/azure-ai',
            'ai/data-sources/elasticsearch',
          ],
        },
        {
          type: 'category',
          label: 'Documents',
          items: [
            'ai/documents/index',
            'ai/documents/azure-ai',
            'ai/documents/elasticsearch',
            'ai/documents/openxml',
            'ai/documents/pdf',
          ],
        },
        {
          type: 'category',
          label: 'Model Context Protocol (MCP)',
          items: [
            'ai/mcp/index',
            'ai/mcp/client',
            'ai/mcp/server',
            'ai/mcp/ftp',
            'ai/mcp/sftp',
          ],
        },
      ],
    },
    {
      type: 'category',
      label: 'Omnichannel Communications',
      items: [
        'omnichannel/index',
        'omnichannel/event-grid',
        'omnichannel/management',
        'omnichannel/sms',
      ],
    },
    {
      type: 'category',
      label: 'Standard Modules',
      items: [
        'modules/index',
        'modules/content-access-control',
        'modules/recipes',
        'modules/resources',
        'modules/roles',
        'modules/signalr',
        'modules/users',
      ],
    },
    {
      type: 'category',
      label: 'Samples',
      items: [
        'samples/index',
        'samples/mcp-client',
      ],
    },
    {
      type: 'category',
      label: 'Changelog',
      items: [
        'changelog/index',
        'changelog/v2.0.0',
      ],
    },
  ],
};

export default sidebars;