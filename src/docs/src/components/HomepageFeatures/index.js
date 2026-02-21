import clsx from 'clsx';
import Heading from '@theme/Heading';
import styles from './styles.module.css';

const FeatureList = [
  {
    title: 'AI-Powered Modules',
    emoji: '🤖',
    description: (
      <>
        Integrate ChatGPT, Azure OpenAI, Ollama, and more into your Orchard Core
        site with chat interfaces, AI agents, RAG data sources, and document
        processing.
      </>
    ),
  },
  {
    title: 'Omnichannel Communication',
    emoji: '📡',
    description: (
      <>
        Unified orchestration layer for SMS, Email, and Phone channels with a
        built-in mini-CRM and AI-driven automation support.
      </>
    ),
  },
  {
    title: 'Modular & Extensible',
    emoji: '🧩',
    description: (
      <>
        Install only the modules you need. Enhanced user management, role-based
        access control, SignalR integration, recipes, and shared resources — all
        independently configurable.
      </>
    ),
  },
];

function Feature({emoji, title, description}) {
  return (
    <div className={clsx('col col--4')}>
      <div className="text--center" style={{fontSize: '3rem'}}>
        {emoji}
      </div>
      <div className="text--center padding-horiz--md">
        <Heading as="h3">{title}</Heading>
        <p>{description}</p>
      </div>
    </div>
  );
}

export default function HomepageFeatures() {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
