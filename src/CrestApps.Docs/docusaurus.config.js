// @ts-check

import {themes as prismThemes} from 'prism-react-renderer';

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'CrestApps Orchard Core',
  tagline: 'User Manual and Technical Manual for the CrestApps Orchard Core modules',
  favicon: 'img/favicon.ico',
  titleDelimiter: '|',

  future: {
    v4: true,
  },

  url: 'https://orchardcore.crestapps.com',
  baseUrl: '/',

  organizationName: 'CrestApps',
  projectName: 'CrestApps.OrchardCore',

  // The User Manual and the Technical Manual link to each other everywhere, so a broken link or anchor fails the build.
  onBrokenLinks: 'throw',
  onBrokenAnchors: 'throw',

  markdown: {
    hooks: {
      onBrokenMarkdownLinks: 'throw',
    },
  },

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
        // The User Manual gets its own search index so people who use the app can search only it.
        // The search box on a User Manual page searches the User Manual; everywhere else it searches
        // the whole site. The search page has a filter to switch between the two.
        searchContextByPaths: [
          {
            label: 'User Manual',
            path: 'docs/user-manual',
          },
        ],
        useAllContextsWithNoSearchContext: true,
      }),
    ],
  ],

  plugins: [
    [
      '@docusaurus/plugin-client-redirects',
      /** @type {import('@docusaurus/plugin-client-redirects').Options} */
      ({
        redirects: [
          {
            // The single-page agent and supervisor manual became the task-by-task User Manual.
            from: '/docs/contact-center/user-manual',
            to: '/docs/user-manual',
          },
        ],
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
            'https://github.com/CrestApps/CrestApps.OrchardCore/tree/main/src/CrestApps.Docs/',
          lastVersion: 'current',
          versions: {
            current: {
              label: 'Latest',
              path: '',
            },
            '2.1': {
              label: '2.1',
              path: '2.1',
            },
            '2.0': {
              label: '2.0',
              path: '2.0',
            },
            '1.2': {
              label: '1.2',
              path: '1.2',
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
            sidebarId: 'userManualSidebar',
            position: 'left',
            label: 'User Manual',
          },
          {
            type: 'docSidebar',
            sidebarId: 'technicalSidebar',
            position: 'left',
            label: 'Technical Manual',
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
            title: 'User Manual',
            items: [
              {
                label: 'Start here',
                to: '/docs/user-manual',
              },
              {
                label: 'Training paths',
                to: '/docs/user-manual/getting-started/training-paths',
              },
              {
                label: 'Use cases',
                to: '/docs/user-manual/use-cases',
              },
              {
                label: 'Glossary',
                to: '/docs/user-manual/glossary',
              },
            ],
          },
          {
            title: 'Technical Manual',
            items: [
              {
                label: 'Overview',
                to: '/docs/intro',
              },
              {
                label: 'Getting Started',
                to: '/docs/getting-started',
              },
              {
                label: 'Configuration',
                to: '/docs/configuration',
              },
              {
                label: 'Feature IDs',
                to: '/docs/feature-reference',
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
