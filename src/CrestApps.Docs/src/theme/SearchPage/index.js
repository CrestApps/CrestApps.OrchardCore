import React, {useEffect, useState} from 'react';
import {createPortal} from 'react-dom';
import SearchPage from '@theme-original/SearchPage';
import {useHistory, useLocation} from '@docusaurus/router';
import {USER_MANUAL_SEARCH_CONTEXT} from '@site/src/components/Manual/ManualSearch';
import styles from './styles.module.css';

/**
 * Replaces the search page's "Everywhere / User Manual" drop-down with a "User Manual only" checkbox.
 * The search plugin reads the scope from the `ctx` query parameter, so the checkbox only rewrites it.
 */
export default function SearchPageWrapper(props) {
  const history = useHistory();
  const location = useLocation();
  const [slot, setSlot] = useState(null);
  const userManualOnly = new URLSearchParams(location.search).get('ctx') === USER_MANUAL_SEARCH_CONTEXT;

  useEffect(() => {
    const select = document.getElementById('context-selector');

    if (select?.parentElement) {
      select.parentElement.classList.add(styles.slot);
      setSlot(select.parentElement);
    }
  }, []);

  const onChange = (event) => {
    const params = new URLSearchParams(location.search);

    if (event.target.checked) {
      params.set('ctx', USER_MANUAL_SEARCH_CONTEXT);
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
          <label className={styles.scope}>
            <input type="checkbox" checked={userManualOnly} onChange={onChange} />
            <span>Search only the User Manual</span>
          </label>,
          slot,
        )}
    </>
  );
}
