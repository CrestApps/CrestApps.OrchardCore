// @ts-check

/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
    docsSidebar: [
        'intro',
        'getting-started',
        'feature-reference',
        {
            type: 'category',
            label: 'Artificial Intelligence Suite',
            collapsed: false,
            items: [
                {
                    type: 'doc',
                    id: 'ai/index',
                },
                'ai/overview',
                'ai/chat',
                'ai/chat-analytics',
                'ai/chat-interactions',
                'ai/chat-notifications',
                'ai/copilot',
                'ai/claude',
                'ai/agent',
                'ai/prompt-templates',
                'ai/profile-templates',
                'ai/memory',
                'ai/memory-azure-ai',
                'ai/memory-elasticsearch',
                'ai/workflows',
                {
                    type: 'category',
                    label: 'A2A (Agent-to-Agent)',
                    items: [
                        'ai/a2a/index',
                        'ai/a2a/client',
                        'ai/a2a/host',
                    ],
                },
                {
                    type: 'category',
                    label: 'AI Providers',
                    items: [
                        'ai/providers/index',
                        'ai/providers/azure-ai-inference',
                        'ai/providers/azure-openai',
                        'ai/providers/ollama',
                        'ai/providers/openai',
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
                        'ai/documents/azure-blob-storage',
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
                'omnichannel/azure-communication-services',
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
                'samples/a2a-client',
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
