// @ts-check
// Note: type annotations allow type checking and IDEs autocompletion

const {themes} = require('prism-react-renderer');
const lightCodeTheme = themes.github;
const darkCodeTheme = themes.dracula;

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'DistroNexus',
  tagline: 'The Ultimate Windows Subsystem for Linux Manager',
  favicon: 'img/logo.png',

  // Set the production url of your site here
  url: 'https://lazyworkshop-create.github.io',
  // Set the /<baseUrl>/ pathname under which your site is served
  // For GitHub Pages deployment, it is often '/<projectName>/'
  baseUrl: '/DistroNexus/',

  // GitHub pages deployment config.
  // If you aren't using GitHub pages, you don't need these.
  organizationName: 'DistroNexus', // Usually your GitHub org/user name.
  projectName: 'DistroNexus', // Usually your repo name.

  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',

  // Even if you don't use internalization, you can use this field to set useful
  // metadata like html lang. For example, if your site is Chinese, you may want
  // to replace "en" with "zh-Hans".
  i18n: {
    defaultLocale: 'en',
    locales: ['en', 'zh-Hans'],
  },

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          sidebarPath: require.resolve('./sidebars.js'),
          // Please change this to your repo.
          editUrl:
            'https://github.com/DistroNexus/DistroNexus/tree/main/website/',
        },
        blog: {
          showReadingTime: true,
          // Please change this to your repo.
          editUrl:
            'https://github.com/DistroNexus/DistroNexus/tree/main/website/',
        },
        theme: {
          customCss: require.resolve('./src/css/custom.css'),
        },
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      // Replace with your project's social card
      image: 'img/docusaurus-social-card.jpg',
      navbar: {
        title: 'DistroNexus',
        logo: {
          alt: 'DistroNexus Logo',
          src: 'img/logo.png',
        },
        items: [
          {
            type: 'docSidebar',
            sidebarId: 'tutorialSidebar',
            position: 'left',
            label: 'Documentation',
          },
          {to: '/blog', label: 'Release Notes', position: 'left'},
          {
            type: 'localeDropdown',
            position: 'right',
          },
          {
            href: 'https://github.com/DistroNexus/DistroNexus',
            label: 'GitHub',
            position: 'right',
          },
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            title: 'Docs',
            items: [
              {
                label: 'Get Started',
                to: '/docs/intro',
              },
              {
                label: 'Usage',
                to: '/docs/usage',
              },
            ],
          },
          {
            title: 'More',
            items: [
              {
                label: 'Release Notes',
                to: '/blog',
              },
              {
                label: 'GitHub',
                href: 'https://github.com/DistroNexus/DistroNexus',
              },
            ],
          },
        ],
        copyright: `Copyright © ${new Date().getFullYear()} DistroNexus. Built with Docusaurus.`,
      },
      prism: {
        theme: lightCodeTheme,
        darkTheme: darkCodeTheme,
        additionalLanguages: ['powershell', 'json', 'bash'],
      },
    }),
};

module.exports = config;
