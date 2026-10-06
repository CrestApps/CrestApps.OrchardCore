import React from 'react';
import Admonition from '@theme/Admonition';

/**
 * The standard note every User Manual page shows under its Menu / Permission / Feature table.
 * Most people who use the app are not administrators, so it tells them who can change what they see.
 *
 * Usage: <AskYourAdmin /> for the standard wording, or <AskYourAdmin>An extra sentence.</AskYourAdmin>.
 */
export default function AskYourAdmin({children}) {
  return (
    <Admonition type="tip" title="Can't find it?">
      <p>
        What you see depends on your role. If the menu, a button or a setting on this page is missing,
        ask your administrator to give you the permission listed above or to turn on the feature. Only
        administrators can turn features on and change site settings.
      </p>
      {children}
    </Admonition>
  );
}
