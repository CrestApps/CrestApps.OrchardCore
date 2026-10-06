import React, {useState} from 'react';
import {useHistory} from '@docusaurus/router';
import useBaseUrl from '@docusaurus/useBaseUrl';
import styles from './styles.module.css';

/** The search context the search plugin builds for the User Manual (see docusaurus.config.js). */
export const USER_MANUAL_SEARCH_CONTEXT = 'docs/user-manual';

/**
 * A search box that searches only the User Manual. It opens the site's search page with the
 * User Manual filter selected, where the reader can switch the filter to "Everywhere".
 */
export default function ManualSearch({placeholder = 'Search the User Manual, for example "transfer a call"'}) {
  const [query, setQuery] = useState('');
  const history = useHistory();
  const searchUrl = useBaseUrl('/search/');

  const onSubmit = (event) => {
    event.preventDefault();
    const params = new URLSearchParams({q: query.trim(), ctx: USER_MANUAL_SEARCH_CONTEXT});
    history.push(`${searchUrl}?${params.toString()}`);
  };

  return (
    <form className={styles.search} role="search" onSubmit={onSubmit}>
      <input
        type="search"
        className={styles.searchInput}
        aria-label="Search the User Manual"
        placeholder={placeholder}
        value={query}
        onChange={(event) => setQuery(event.target.value)}
      />
      <button type="submit" className="button button--primary">
        Search
      </button>
    </form>
  );
}
