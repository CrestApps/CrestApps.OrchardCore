/** The search context the search plugin builds for the User Manual (see docusaurus.config.js). */
export const USER_MANUAL_SEARCH_CONTEXT = 'docs/user-manual';

/** The search context of the Technical Manual: every doc except the User Manual (see docusaurus.config.js). */
export const TECHNICAL_MANUAL_SEARCH_CONTEXT = 'docs';

/** Both manuals, the default. */
export const BOTH_MANUALS = {user: true, technical: true};

/**
 * Gets the search context of a choice of manuals: both search everything (the empty context), one searches only it.
 * @param {{user: boolean, technical: boolean}} scope The manuals to search.
 * @returns {string} The search context.
 */
export function searchContextOf(scope) {
  if (scope.user && !scope.technical) {
    return USER_MANUAL_SEARCH_CONTEXT;
  }

  if (scope.technical && !scope.user) {
    return TECHNICAL_MANUAL_SEARCH_CONTEXT;
  }

  return '';
}

/**
 * Gets the manuals a search context searches.
 * @param {string | null} context The search context, such as the search page's `ctx` parameter.
 * @returns {{user: boolean, technical: boolean}} The manuals.
 */
export function scopeOf(context) {
  if (context === USER_MANUAL_SEARCH_CONTEXT) {
    return {user: true, technical: false};
  }

  if (context === TECHNICAL_MANUAL_SEARCH_CONTEXT) {
    return {user: false, technical: true};
  }

  return BOTH_MANUALS;
}

/**
 * Ticks or clears one manual. At least one stays ticked: clearing the only ticked one changes nothing.
 * @param {{user: boolean, technical: boolean}} scope The current choice.
 * @param {'user' | 'technical'} manual The manual whose checkbox changed.
 * @param {boolean} checked Whether it is now ticked.
 * @returns {{user: boolean, technical: boolean}} The new choice.
 */
export function toggleManual(scope, manual, checked) {
  const next = {...scope, [manual]: checked};

  return next.user || next.technical ? next : scope;
}
