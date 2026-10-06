import React from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import Home from '@theme-original/DocBreadcrumbs/Items/Home';
import {useDoc} from '@docusaurus/plugin-content-docs/client';
import Icon from '@site/src/components/Icon';
import {manualOfDoc, useManualHomePath} from '@site/src/components/Manual/manuals';
import styles from './styles.module.css';

/**
 * Adds the manual the page belongs to right after the home crumb, so the trail reads
 * Home > User Manual > Section > Page (or Home > Technical Manual > ...).
 */
export default function HomeWrapper(props) {
  const {metadata} = useDoc();
  const manual = manualOfDoc(metadata.id);
  const manualPath = useManualHomePath(manual);

  return (
    <>
      <Home {...props} />
      <li className={clsx('breadcrumbs__item', styles.manualItem, styles[`manualItem_${manual.key}`])}>
        <Link className="breadcrumbs__link" to={manualPath}>
          <Icon name={manual.icon} className={styles.manualIcon} />
          {manual.label}
        </Link>
      </li>
    </>
  );
}
