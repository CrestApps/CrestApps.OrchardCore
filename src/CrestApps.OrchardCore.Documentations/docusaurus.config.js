// @ts-check

import {themes as prismThemes} from 'prism-react-renderer';

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'CrestApps Orchard Core',
  tagline: 'Open-source modules to enhance Orchard Core CMS',
  favicon: 'img/favicon.ico',
  titleDelimiter: '|',

  future: {
    v4: true,
  },

  url: 'https://orchardcore.crestapps.com',
  baseUrl: '/',

  organizationName: 'CrestApps',
  projectName: 'CrestApps.OrchardCore',

  onBrokenLinks: 'warn',

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  themes: [
    [
      '@easyops-cn/docusaurus-search-local',
      /** @type {import("@easyops-cn/docusaurus-search-local").PluginOptions} */
      ({
        hashed: true,
        language: ['en'],
        highlightSearchTermsOnTargetPage: true,
        explicitSearchResultPath: true,
      }),
    ],
  ],

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          sidebarPath: './sidebars.js',
          editUrl:
            'https://github.com/CrestApps/CrestApps.OrchardCore/tree/main/src/CrestApps.OrchardCore.Documentations/',
          lastVersion: 'current',
          versions: {
            current: {
              label: 'Latest',
              path: '',
            },
          },
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      image: 'img/logo.png',
      colorMode: {
        defaultMode: 'light',
        respectPrefersColorScheme: true,
      },
      navbar: {
        title: 'CrestApps Orchard Core',
        logo: {
          alt: 'CrestApps Logo',
          src: 'img/logo.svg',
        },
        items: [
          {
            type: 'docSidebar',
            sidebarId: 'docsSidebar',
            position: 'left',
            label: 'Docs',
          },
          {
            type: 'docsVersionDropdown',
            position: 'right',
            dropdownActiveClassDisabled: true,
          },
          {
            href: 'https://github.com/CrestApps/CrestApps.OrchardCore',
            label: 'GitHub',
            position: 'right',
          },
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            title: 'Documentation',
            items: [
              {
                label: 'Getting Started',
                to: '/docs/getting-started',
              },
              {
                label: 'AI Suite',
                to: '/docs/ai',
              },
              {
                label: 'AI Providers',
                to: '/docs/providers',
              },
              {
                label: 'Consuming AI Services',
                to: '/docs/ai/consuming-ai-services',
              },
            ],
          },
          {
            title: 'Community',
            items: [
              {
                label: 'Issues',
                href: 'https://github.com/CrestApps/CrestApps.OrchardCore/issues',
              },
            ],
          },
          {
            title: 'More',
            items: [
              {
                label: 'GitHub',
                href: 'https://github.com/CrestApps/CrestApps.OrchardCore',
              },
              {
                label: 'NuGet Packages',
                href: 'https://www.nuget.org/profiles/malhayek',
              },
              {
                label: 'CrestApps',
                href: 'https://crestapps.com',
              },
            ],
          },
        ],
        copyright: `Copyright © ${new Date().getFullYear()} CrestApps.`,
      },
      prism: {
        theme: prismThemes.github,
        darkTheme: prismThemes.dracula,
        additionalLanguages: ['csharp', 'json', 'bash'],
      },
    }),
};

export default config;
