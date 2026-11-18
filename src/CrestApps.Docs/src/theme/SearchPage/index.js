import React, {useEffect, useState} from 'react';
import {createPortal} from 'react-dom';
import SearchPage from '@theme-original/SearchPage';
import {useHistory, useLocation} from '@docusaurus/router';
import {scopeOf, searchContextOf, toggleManual} from '@site/src/components/Manual/searchScope';
import styles from './styles.module.css';

/**
 * Replaces the search page's "Everywhere / User Manual / Technical Manual" drop-down with a "User Manual" and a
 * "Technical Manual" checkbox. Both ticked search both manuals; at least one stays ticked. The search plugin reads the
 * scope from the `ctx` query parameter, so the checkboxes only rewrite it.
 */
export default function SearchPageWrapper(props) {
  const history = useHistory();
  const location = useLocation();
  const [slot, setSlot] = useState(null);
  const scope = scopeOf(new URLSearchParams(location.search).get('ctx'));

  useEffect(() => {
    const select = document.getElementById('context-selector');

    if (select?.parentElement) {
      select.parentElement.classList.add(styles.slot);
      setSlot(select.parentElement);
    }
  }, []);

  const onToggle = (manual) => (event) => {
    const params = new URLSearchParams(location.search);
    const context = searchContextOf(toggleManual(scope, manual, event.target.checked));

    if (context) {
      params.set('ctx', context);
    } else {
      params.delete('ctx');
    }

    history.replace({search: params.toString()});
  };

  return (
    <>
      <SearchPage {...props} />
      {slot &&
        createPortal(
          <div className={styles.scopes} role="group" aria-label="Manuals to search">
            <label className={styles.scope}>
              <input type="checkbox" checked={scope.user} onChange={onToggle('user')} />
              <span>User Manual</span>
            </label>
            <label className={styles.scope}>
              <input type="checkbox" className={styles.technical} checked={scope.technical} onChange={onToggle('technical')} />
              <span>Technical Manual</span>
            </label>
          </div>,
          slot,
        )}
    </>
  );
}
