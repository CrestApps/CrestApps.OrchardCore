import React from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import {useDoc} from '@docusaurus/plugin-content-docs/client';
import Icon from '@site/src/components/SiteIcon';
import {MANUALS, manualOfDoc, useCounterpartDocs} from './manuals';
import styles from './styles.module.css';

const HEADINGS = {
  user: {
    title: 'Go deeper in the Technical Manual',
    text: 'For developers and IT: configuration files, environment variables, recipes, deployment and extending the feature.',
  },
  technical: {
    title: 'See it in the User Manual',
    text: 'Step-by-step instructions and screencasts for the people who use these screens every day.',
  },
};

/** The card at the end of a doc that lists every counterpart page in the other manual. */
export default function ManualCrossLinks() {
  const {metadata, frontMatter} = useDoc();
  const manual = manualOfDoc(metadata.id);
  const counterparts = useCounterpartDocs(manual, frontMatter);

  if (counterparts.length === 0) {
    return null;
  }

  const heading = HEADINGS[manual.key];
  const other = MANUALS[manual.otherKey];

  return (
    <aside className={clsx(styles.crossLinks, styles[`crossLinks_${other.key}`])} aria-label={heading.title}>
      <div className={styles.crossLinksHeader}>
        <span className={clsx(styles.badge, styles[`badge_${other.key}`])}>
          <Icon name={other.icon} />
          {other.label}
        </span>
        <strong>{heading.title}</strong>
      </div>
      <p className={styles.crossLinksText}>{heading.text}</p>
      <ul className={styles.crossLinksList}>
        {counterparts.map((doc) => (
          <li key={doc.id}>
            <Link className={clsx(styles.link, styles[`link_${other.key}`])} to={doc.path}>
              {doc.title}
            </Link>
            {doc.description && <span className={styles.crossLinksDescription}>{doc.description}</span>}
          </li>
        ))}
      </ul>
    </aside>
  );
}
