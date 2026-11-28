// @ts-check

/**
 * Gives a link that replaces the draft (Discard draft) the draft version the page now knows. The link carries the
 * version the page was shown with, and the server refuses a discard over a draft that changed since; after the page's
 * own panel saves, the old version would read as another author's change.
 * @param {string} href The link's address.
 * @param {string} version The draft version the last save returned.
 * @param {string} base The page's address, to resolve a relative link.
 * @returns {string} The address with its `draft` value replaced.
 */
export function withDraftStamp(href, version, base) {
    if (!href || !version) {
        return href;
    }

    const url = new URL(href, base);
    url.searchParams.set('draft', version);

    return url.toString();
}
