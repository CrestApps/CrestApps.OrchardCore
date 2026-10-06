import React from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import Head from '@docusaurus/Head';
import {useDoc} from '@docusaurus/plugin-content-docs/client';
import Icon from '@site/src/components/SiteIcon';
import {MANUALS, manualOfDoc, useCounterpartDocs} from './manuals';
import styles from './styles.module.css';

const PROMPTS = {
  user: 'Need configuration, code or deployment details?',
  technical: 'Looking for step-by-step instructions?',
};

/**
 * The strip above every doc's title: which manual the reader is in, and a link to the
 * matching page in the other manual when the page names one.
 */
export default function ManualBar() {
  const {metadata, frontMatter} = useDoc();
  const manual = manualOfDoc(metadata.id);
  const other = MANUALS[manual.otherKey];
  const counterparts = useCounterpartDocs(manual, frontMatter);

  return (
    <>
      <Head>
        <html data-manual={manual.key} />
      </Head>
      <div className={clsx(styles.bar, styles[`bar_${manual.key}`])}>
        <span className={clsx(styles.badge, styles[`badge_${manual.key}`])}>
          <Icon name={manual.icon} />
          {manual.label}
        </span>
        {counterparts.length > 0 && (
          <span className={styles.barLinks}>
            {PROMPTS[manual.key]} In the {other.label}:{' '}
            {counterparts.map((doc, index) => (
              <React.Fragment key={doc.id}>
                {index > 0 && <span className={styles.separator} aria-hidden="true"> · </span>}
                <Link className={clsx(styles.link, styles[`link_${other.key}`])} to={doc.path}>
                  {doc.title}
                </Link>
              </React.Fragment>
            ))}
          </span>
        )}
      </div>
    </>
  );
}
