import React, {useEffect, useState} from 'react';
import SearchBar from '@theme-original/SearchBar';
import {useActivePlugin, useActiveVersion} from '@docusaurus/plugin-content-docs/client';
import {BOTH_MANUALS, searchContextOf, toggleManual} from '@site/src/components/Manual/searchScope';
import styles from './styles.module.css';

const STORAGE_KEY = 'crestapps.search.manuals';

// Below this width the navbar has no room for the checkboxes: the search box searches both manuals, and the search
// page has the checkboxes.
const NARROW_QUERY = '(max-width: 996px)';

function readStoredScope() {
  try {
    const value = JSON.parse(window.localStorage.getItem(STORAGE_KEY));

    return value && typeof value.user === 'boolean' && typeof value.technical === 'boolean' && (value.user || value.technical)
      ? {user: value.user, technical: value.technical}
      : null;
  } catch {
    return null;
  }
}

function storeScope(scope) {
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(scope));
  } catch {
    // Storage can be blocked; the checkboxes still work for this page.
  }
}

/**
 * Adds a "User Manual" and a "Technical Manual" checkbox to the navbar search. Both are ticked by default, so the search
 * covers both manuals; clearing one searches only the other, and at least one stays ticked. The reader's choice is
 * remembered on every page. The search plugin is patched (patches/@easyops-cn+docusaurus-search-local+*.patch) to take
 * the chosen scope as searchContextOverride.
 */
export default function SearchBarWrapper(props) {
  const activePlugin = useActivePlugin();
  const activeVersion = useActiveVersion(activePlugin?.pluginId ?? 'default');
  // Only the latest version has an index per manual.
  const canScope = !activeVersion || activeVersion.isLast;
  const [scope, setScope] = useState(BOTH_MANUALS);
  const [narrow, setNarrow] = useState(false);

  useEffect(() => {
    setScope(readStoredScope() ?? BOTH_MANUALS);

    const query = window.matchMedia(NARROW_QUERY);
    const onChange = () => setNarrow(query.matches);

    onChange();
    query.addEventListener('change', onChange);

    return () => query.removeEventListener('change', onChange);
  }, []);

  const onToggle = (manual) => (event) => {
    const next = toggleManual(scope, manual, event.target.checked);

    setScope(next);
    storeScope(next);
  };

  return (
    <div className={styles.wrapper}>
      <SearchBar {...props} searchContextOverride={canScope ? (narrow ? '' : searchContextOf(scope)) : undefined} />
      {canScope && (
        <div className={styles.scopes} role="group" aria-label="Manuals to search">
          <label className={styles.scope} title="Search the User Manual, the step-by-step guide for people who use the app">
            <input type="checkbox" checked={scope.user} onChange={onToggle('user')} />
            <span>User Manual</span>
          </label>
          <label className={styles.scope} title="Search the Technical Manual, for developers and IT">
            <input type="checkbox" className={styles.technical} checked={scope.technical} onChange={onToggle('technical')} />
            <span>Technical Manual</span>
          </label>
        </div>
      )}
    </div>
  );
}
