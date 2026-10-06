import React, {useEffect, useState} from 'react';
import SearchBar from '@theme-original/SearchBar';
import {useLocation} from '@docusaurus/router';
import useBaseUrl from '@docusaurus/useBaseUrl';
import {useActivePlugin, useActiveVersion} from '@docusaurus/plugin-content-docs/client';
import {USER_MANUAL_SEARCH_CONTEXT} from '@site/src/components/Manual/ManualSearch';
import styles from './styles.module.css';

const STORAGE_KEY = 'crestapps.search.userManualOnly';

function readStoredChoice() {
  try {
    const value = window.localStorage.getItem(STORAGE_KEY);

    return value === null ? null : value === 'true';
  } catch {
    return null;
  }
}

function storeChoice(value) {
  try {
    window.localStorage.setItem(STORAGE_KEY, String(value));
  } catch {
    // Storage can be blocked; the checkbox still works for this page.
  }
}

/**
 * Adds a "User Manual only" checkbox to the navbar search.
 *
 * Until the reader uses it, the checkbox is ticked on User Manual pages and clear everywhere else. Once
 * the reader ticks or clears it, that choice is remembered on every page. The search plugin is patched
 * (patches/@easyops-cn+docusaurus-search-local+*.patch) to take the chosen scope as searchContextOverride.
 */
export default function SearchBarWrapper(props) {
  const {pathname} = useLocation();
  const userManualPath = useBaseUrl('/docs/user-manual');
  const activePlugin = useActivePlugin();
  const activeVersion = useActiveVersion(activePlugin?.pluginId ?? 'default');
  const inUserManual = pathname === userManualPath || pathname.startsWith(`${userManualPath}/`);
  // Only the latest version has a User Manual search index.
  const canScope = !activeVersion || activeVersion.isLast;
  const [storedChoice, setStoredChoice] = useState(null);

  useEffect(() => {
    setStoredChoice(readStoredChoice());
  }, []);

  const userManualOnly = storedChoice ?? inUserManual;
  const onChange = (event) => {
    setStoredChoice(event.target.checked);
    storeChoice(event.target.checked);
  };

  return (
    <div className={styles.wrapper}>
      <SearchBar {...props} searchContextOverride={canScope ? (userManualOnly ? USER_MANUAL_SEARCH_CONTEXT : '') : undefined} />
      {canScope && (
        <label className={styles.scope} title="Search only the User Manual, the step-by-step guide for people who use the app">
          <input type="checkbox" checked={userManualOnly} onChange={onChange} />
          <span>User Manual only</span>
        </label>
      )}
    </div>
  );
}
