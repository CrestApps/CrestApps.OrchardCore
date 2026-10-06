import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import ManualSearch from '@site/src/components/Manual/ManualSearch';
import Icon from '@site/src/components/SiteIcon';
import styles from './index.module.css';

const MANUALS = [
  {
    key: 'user',
    label: 'User Manual',
    icon: 'book-open',
    title: 'I use the app',
    description:
      'For agents, supervisors, managers and administrators who work in the browser. Step-by-step instructions, screencasts, use cases and training paths. No code.',
    to: '/docs/user-manual',
    cta: 'Open the User Manual',
    links: [
      {label: 'Find your way around', to: '/docs/user-manual/getting-started/finding-your-way'},
      {label: 'Training paths by role', to: '/docs/user-manual/getting-started/training-paths'},
      {label: 'Use cases', to: '/docs/user-manual/use-cases'},
      {label: 'Glossary', to: '/docs/user-manual/glossary'},
    ],
  },
  {
    key: 'technical',
    label: 'Technical Manual',
    icon: 'wrench',
    title: 'I install, configure or extend it',
    description:
      'For developers and IT. Packages and feature IDs, appsettings.json and environment variables, recipes, architecture, operations and extension points.',
    to: '/docs/intro',
    cta: 'Open the Technical Manual',
    links: [
      {label: 'Getting started', to: '/docs/getting-started'},
      {label: 'Configuration reference', to: '/docs/configuration'},
      {label: 'Feature IDs', to: '/docs/feature-reference'},
      {label: 'Release notes', to: '/docs/changelog'},
    ],
  },
];

const AREAS = [
  {
    title: 'AI Assistant',
    inHero: true,
    icon: 'hexagon-nodes',
    text: 'AI chat, AI profiles, website chat, knowledge from your documents, tools and agents.',
    user: '/docs/user-manual/ai',
    technical: '/docs/ai',
  },
  {
    title: 'CRM',
    inHero: true,
    icon: 'contact',
    text: 'Contacts, leads, accounts and opportunities, subjects, dispositions and campaigns.',
    user: '/docs/user-manual/contacts',
    technical: '/docs/omnichannel',
  },
  {
    title: 'Contact Center',
    inHero: true,
    icon: 'headset',
    text: 'Queues, entry points and IVR menus, the dialer, the agent workspace and the live dashboard.',
    user: '/docs/user-manual/agent-workspace',
    technical: '/docs/contact-center',
  },
  {
    title: 'Messaging',
    inHero: true,
    icon: 'messages-square',
    text: 'A shared inbox for text conversations, templates, broadcasts and automatic follow-ups.',
    user: '/docs/user-manual/messaging',
    technical: '/docs/omnichannel/messaging-workspace',
  },
  {
    title: 'Phone',
    inHero: true,
    icon: 'phone',
    text: 'The soft phone, the browser extension and Windows app, extensions and voicemail.',
    user: '/docs/user-manual/soft-phone',
    technical: '/docs/telephony',
  },
  {
    title: 'Site Administration',
    icon: 'shield-check',
    text: 'Users and roles, import and export, Do Not Call lists, phone number checks and time zones.',
    user: '/docs/user-manual/administration/users',
    technical: '/docs/modules',
  },
];

function HomepageHeader() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <header className={clsx('hero hero--primary', styles.heroBanner)}>
      <div className="container">
        <Heading as="h1" className="hero__title">
          {siteConfig.title}
        </Heading>
        <p className={clsx('hero__subtitle', styles.heroDefinition)}>
          Open-source modules that turn an Orchard Core site into a business application: an AI assistant
          suite, a CRM, a contact center with phones and a dialer, and omnichannel messaging, all managed from
          the site's admin.
        </p>
        <ul className={styles.heroFacts}>
          {AREAS.filter((area) => area.inHero).map((area) => (
            <li key={area.title}>
              <Link to={area.user}>
                <Icon name={area.icon} />
                {area.title}
              </Link>
            </li>
          ))}
        </ul>
      </div>
    </header>
  );
}

function ManualCard({manual}) {
  return (
    <div className={clsx('col col--6', styles.cardColumn)}>
      <section className={clsx(styles.card, styles[`card_${manual.key}`])} aria-labelledby={`manual-${manual.key}`}>
        <div className={styles.cardHeader}>
          <span className={clsx(styles.cardIcon, styles[`cardIcon_${manual.key}`])}>
            <Icon name={manual.icon} />
          </span>
          <span className={clsx(styles.badge, styles[`badge_${manual.key}`])}>{manual.label}</span>
        </div>
        <Heading as="h3" id={`manual-${manual.key}`} className={styles.cardTitle}>
          {manual.title}
        </Heading>
        <p className={styles.cardText}>{manual.description}</p>
        {manual.key === 'user' && <ManualSearch placeholder='e.g. "transfer a call" or "create a queue"' />}
        <ul className={styles.cardLinks}>
          {manual.links.map((link) => (
            <li key={link.to}>
              <Link to={link.to}>{link.label}</Link>
            </li>
          ))}
        </ul>
        <Link className={clsx('button button--lg', styles[`cardButton_${manual.key}`])} to={manual.to}>
          {manual.cta}
        </Link>
      </section>
    </div>
  );
}

function ProductAreas() {
  return (
    <section className={styles.areas}>
      <div className="container">
        <Heading as="h2" className={styles.areasTitle}>
          Browse by product area
        </Heading>
        <div className="row">
          {AREAS.map((area) => (
            <div key={area.title} className={clsx('col col--4', styles.areaColumn)}>
              <div className={styles.area}>
                <Heading as="h3" className={styles.areaTitle}>
                  <span className={styles.areaIcon}>
                    <Icon name={area.icon} />
                  </span>
                  {area.title}
                </Heading>
                <p>{area.text}</p>
                <div className={styles.areaLinks}>
                  <Link className={styles.userLink} to={area.user}>
                    How to use it
                  </Link>
                  <Link className={styles.technicalLink} to={area.technical}>
                    How it works
                  </Link>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

export default function Home() {
  return (
    <Layout
      title="Documentation"
      description="The User Manual and the Technical Manual for the CrestApps Orchard Core modules: AI, CRM, Contact Center, Messaging and Phone.">
      <HomepageHeader />
      <main>
        <div className={clsx('container', styles.cards)}>
          <Heading as="h2" className={styles.cardsTitle}>
            Two manuals for two kinds of reader. Pick the one that fits you.
          </Heading>
          <div className="row">
            {MANUALS.map((manual) => (
              <ManualCard key={manual.key} manual={manual} />
            ))}
          </div>
        </div>
        <ProductAreas />
      </main>
    </Layout>
  );
}
