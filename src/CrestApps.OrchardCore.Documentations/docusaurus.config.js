// @ts-check

import { themes as prismThemes } from 'prism-react-renderer';

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'CrestApps Orchard Core',
  tagline: 'Open-source modules to enhance Orchard Core CMS',
  favicon: 'img/favicon.ico',
  titleDelimiter: '|',

  future: {
    v4: true,
  },

  url: 'https://crestapps.crestapps.com',
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
      ({
        docs: {
          sidebarPath: './sidebars.js',
          editUrl: 'https://github.com/CrestApps/CrestApps.OrchardCore/tree/main/src/CrestApps.OrchardCore.Documentations/',
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
          {
            href: 'https://core.crestapps.com',
            label: 'Core Docs',
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
              { label: 'Getting Started', to: '/docs/getting-started' },
              { label: 'Framework', href: 'https://core.crestapps.com' },
              { label: 'AI Suite', to: '/docs/ai' },
              { label: 'AI Providers', to: '/docs/ai/providers' },
            ],
          },
          {
            title: 'Community',
            items: [
              { label: 'Issues', href: 'https://github.com/CrestApps/CrestApps.OrchardCore/issues' },
            ],
          },
          {
            title: 'More',
            items: [
              { label: 'GitHub', href: 'https://github.com/CrestApps/CrestApps.OrchardCore' },
              { label: 'NuGet Packages', href: 'https://www.nuget.org/profiles/malhayek' },
              { label: 'CrestApps', href: 'https://crestapps.com' },
            ],
          },
        ],
        copyright: `Copyright © ${new Date().getFullYear()} CrestApps.Core.`,
      },
      prism: {
        theme: prismThemes.github,
        darkTheme: prismThemes.dracula,
        additionalLanguages: ['csharp', 'json', 'bash'],
      },
    }),
};

export default config;
