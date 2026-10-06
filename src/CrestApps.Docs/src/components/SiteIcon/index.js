import React from 'react';
import clsx from 'clsx';
import useBaseUrl from '@docusaurus/useBaseUrl';
import styles from './styles.module.css';

/**
 * A Lucide icon from static/img/icons, drawn in the current text color.
 * Add an icon by copying its SVG from lucide-static into that folder.
 */
export default function Icon({name, className, size, label}) {
  const url = useBaseUrl(`/img/icons/${name}.svg`);

  return (
    <span
      className={clsx(styles.icon, className)}
      style={{'--icon-url': `url("${url}")`, ...(size ? {width: size, height: size} : {})}}
      role={label ? 'img' : undefined}
      aria-label={label}
      aria-hidden={label ? undefined : true}
    />
  );
}
