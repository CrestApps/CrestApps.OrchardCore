import React, {useState} from 'react';
import {useHistory} from '@docusaurus/router';
import useBaseUrl from '@docusaurus/useBaseUrl';
import {USER_MANUAL_SEARCH_CONTEXT} from './searchScope';
import styles from './styles.module.css';

export {USER_MANUAL_SEARCH_CONTEXT};

/**
 * A search box that searches only the User Manual. It opens the site's search page with only the
 * User Manual ticked, where the reader can tick the Technical Manual too.
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
