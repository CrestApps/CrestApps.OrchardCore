// @ts-check

/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  docsSidebar: [
    'intro',
    'getting-started',
    {
      type: 'category',
      label: 'AI Suite',
      collapsed: false,
      items: [
        'ai/ai-services',
        'ai/ai-chat',
        'ai/ai-chat-interactions',
        'ai/ai-chat-interactions-core',
        'ai/ai-copilot',
        'ai/ai-agent',
        {
          type: 'category',
          label: 'Data Sources',
          items: [
            'ai/data-sources/index',
            'ai/data-sources/elasticsearch',
            'ai/data-sources/azure-ai',
          ],
        },
        {
          type: 'category',
          label: 'Documents',
          items: [
            'ai/documents/index',
            'ai/documents/pdf',
            'ai/documents/openxml',
            'ai/documents/azure-ai',
            'ai/documents/elasticsearch',
          ],
        },
        {
          type: 'category',
          label: 'Model Context Protocol (MCP)',
          items: [
            'ai/mcp/index',
            'ai/mcp/ftp',
            'ai/mcp/sftp',
          ],
        },
      ],
    },
    {
      type: 'category',
      label: 'AI Providers',
      items: [
        'providers/openai',
        'providers/azure-openai',
        'providers/azure-ai-inference',
        'providers/ollama',
      ],
    },
    {
      type: 'category',
      label: 'Omnichannel Suite',
      items: [
        'omnichannel/index',
        'omnichannel/management',
        'omnichannel/sms',
        'omnichannel/event-grid',
      ],
    },
    {
      type: 'category',
      label: 'Standard Modules',
      items: [
        'modules/users',
        'modules/signalr',
        'modules/roles',
        'modules/content-access-control',
        'modules/resources',
        'modules/recipes',
      ],
    },
    {
      type: 'category',
      label: 'Samples',
      items: [
        'samples/mcp-client',
      ],
    },
  ],
};

export default sidebars;
