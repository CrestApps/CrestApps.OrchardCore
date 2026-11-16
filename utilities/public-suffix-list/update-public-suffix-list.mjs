// Refreshes the copy of the Public Suffix List (https://publicsuffix.org) the Subscriptions module ships, which the
// tenant purchase validator reads so a sold site can't claim a name nobody owns (co.uk, github.io, *.ck). The list is
// published under the Mozilla Public License 2.0 and is kept byte for byte, notice included. Run it on purpose, when
// the copy should be refreshed, and review the diff like code:
//
//   node utilities/public-suffix-list/update-public-suffix-list.mjs
//
// The list asks to be pulled from publicsuffix.org only, not from a mirror or its repository.

import { writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const SOURCE = 'https://publicsuffix.org/list/public_suffix_list.dat';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const output = join(root, 'src', 'Core', 'CrestApps.OrchardCore.Subscriptions.Core', 'Data', 'public_suffix_list.dat');

// A list missing any of these is not the whole list, and is not written.
const requiredMarkers = [
    'This Source Code Form is subject to the terms of the Mozilla Public',
    'https://mozilla.org/MPL/2.0/',
    '// ===BEGIN ICANN DOMAINS===',
    '// ===END ICANN DOMAINS===',
    '// ===BEGIN PRIVATE DOMAINS===',
    '// ===END PRIVATE DOMAINS===',
];

async function fetchList() {
    for (let attempt = 1; ; attempt++) {
        const response = await fetch(SOURCE);

        if (response.ok) {
            return response.text();
        }

        if (attempt >= 3) {
            throw new Error(`${SOURCE} answered ${response.status} ${response.statusText}.`);
        }

        await new Promise((resolve) => setTimeout(resolve, attempt * 2000));
    }
}

const text = (await fetchList()).replace(/\r\n/g, '\n');

for (const marker of requiredMarkers) {
    if (!text.includes(marker)) {
        throw new Error(`The downloaded list has no "${marker}"; it was not written.`);
    }
}

const rules = text.split('\n').filter((line) => line.trim() !== '' && !line.startsWith('//'));

if (rules.length < 9000) {
    throw new Error(`The downloaded list has only ${rules.length} rules; it was not written.`);
}

await writeFile(output, text, 'utf8');

const version = /^\/\/ VERSION: (.+)$/m.exec(text)?.[1] ?? 'unknown';

console.log(`Wrote ${rules.length} rules (version ${version}) to ${output}.`);
