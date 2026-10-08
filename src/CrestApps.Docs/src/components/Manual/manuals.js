import {useDocsData, useDocsVersion} from '@docusaurus/plugin-content-docs/client';

/**
 * The site has two manuals that live in one docs instance:
 * - the User Manual, every doc under `user-manual/`, for people who use the app in the browser;
 * - the Technical Manual, every other doc, for developers and IT.
 *
 * A page names its counterpart pages in the other manual through front matter:
 * `technical_manual: [ids]` on a User Manual page, `user_manual: [ids]` on a Technical Manual page.
 */
export const USER_MANUAL_PREFIX = 'user-manual/';

export const MANUALS = {
  user: {
    key: 'user',
    label: 'User Manual',
    icon: 'book-open',
    homeDocId: 'user-manual/index',
    homePath: '/docs/user-manual',
    otherKey: 'technical',
    frontMatterKey: 'technical_manual',
  },
  technical: {
    key: 'technical',
    label: 'Technical Manual',
    icon: 'wrench',
    homeDocId: 'intro',
    homePath: '/docs/intro',
    otherKey: 'user',
    frontMatterKey: 'user_manual',
  },
};

export function manualOfDoc(docId) {
  return docId.startsWith(USER_MANUAL_PREFIX) ? MANUALS.user : MANUALS.technical;
}

function asList(value) {
  if (!value) {
    return [];
  }

  return Array.isArray(value) ? value : [value];
}

/**
 * Resolves the counterpart pages a doc names in its front matter to {id, title, description, path},
 * within the version being viewed. Ids that this version does not have (an older version that has
 * no User Manual yet, for example) are skipped.
 */
export function useCounterpartDocs(manual, frontMatter) {
  const version = useDocsVersion();
  const docsData = useDocsData('default');
  const globalVersion = docsData.versions.find((v) => v.name === version.version);

  return asList(frontMatter[manual.frontMatterKey])
    .map((id) => {
      const doc = version.docs[id];
      const globalDoc = globalVersion?.docs.find((d) => d.id === id);

      if (!doc || !globalDoc) {
        return null;
      }

      return {id, title: doc.title, description: doc.description, path: globalDoc.path};
    })
    .filter(Boolean);
}

/**
 * The first page of a manual in the version being viewed, or the latest version's first page when this
 * version does not have that manual (older versions have no User Manual).
 */
export function useManualHomePath(manual) {
  const version = useDocsVersion();
  const docsData = useDocsData('default');
  const globalVersion = docsData.versions.find((v) => v.name === version.version);
  const homeDoc = globalVersion?.docs.find((d) => d.id === manual.homeDocId);

  return homeDoc?.path ?? manual.homePath;
}
