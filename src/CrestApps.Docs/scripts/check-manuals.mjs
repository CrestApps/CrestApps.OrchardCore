// Checks the rules that keep the User Manual and the Technical Manual apart and linked together.
// Run with `npm run check:manuals`. See the "Writing for the two manuals" section of README.md.

import {readdirSync, readFileSync, statSync} from 'node:fs';
import {join, relative, sep} from 'node:path';
import {fileURLToPath} from 'node:url';

const siteDir = fileURLToPath(new URL('..', import.meta.url));
const docsDir = join(siteDir, 'docs');
const USER_MANUAL_PREFIX = 'user-manual/';

// sidebars.js is an ES module in a package without "type": "module"; import its source as one.
const sidebarsSource = readFileSync(join(siteDir, 'sidebars.js'), 'utf8');
const {default: sidebars} = await import(`data:text/javascript;base64,${Buffer.from(sidebarsSource).toString('base64')}`);

const errors = [];
const fail = (file, message) => errors.push(`${file}: ${message}`);

function collectSidebarDocIds(items, ids = []) {
    for (const item of items) {
        if (typeof item === 'string') {
            ids.push(item);
        } else if (item.type === 'doc') {
            ids.push(item.id);
        } else if (item.type === 'category') {
            if (item.link?.type === 'doc') {
                ids.push(item.link.id);
            }

            collectSidebarDocIds(item.items, ids);
        }
    }

    return ids;
}

function listDocs(dir) {
    return readdirSync(dir).flatMap((name) => {
        const path = join(dir, name);

        if (statSync(path).isDirectory()) {
            return listDocs(path);
        }

        return /\.mdx?$/.test(name) ? [path] : [];
    });
}

// A small reader for the front matter keys this check needs: scalars, `[a, b]` lists and `- a` lists.
function readFrontMatter(text) {
    const match = /^---\r?\n([\s\S]*?)\r?\n---/.exec(text);
    const result = {};

    if (!match) {
        return result;
    }

    let listKey = null;

    for (const line of match[1].split(/\r?\n/)) {
        const listItem = /^\s+-\s+(.+)$/.exec(line);

        if (listItem && listKey) {
            result[listKey].push(listItem[1].trim().replace(/^['"]|['"]$/g, ''));
            continue;
        }

        const pair = /^([A-Za-z_][\w-]*):\s*(.*)$/.exec(line);

        if (!pair) {
            listKey = null;
            continue;
        }

        const [, key, rawValue] = pair;
        const value = rawValue.trim();

        if (value === '') {
            result[key] = [];
            listKey = key;
        } else if (value.startsWith('[') && value.endsWith(']')) {
            result[key] = value.slice(1, -1).split(',').map((v) => v.trim().replace(/^['"]|['"]$/g, '')).filter(Boolean);
            listKey = null;
        } else {
            result[key] = value.replace(/^['"]|['"]$/g, '');
            listKey = null;
        }
    }

    return result;
}

const userSidebarIds = collectSidebarDocIds(sidebars.userManualSidebar ?? []);
const technicalSidebarIds = collectSidebarDocIds(sidebars.technicalSidebar ?? []);
const sidebarOf = new Map();

for (const [name, ids] of [['userManualSidebar', userSidebarIds], ['technicalSidebar', technicalSidebarIds]]) {
    for (const id of ids) {
        if (sidebarOf.has(id)) {
            fail('sidebars.js', `"${id}" is listed more than once (${sidebarOf.get(id)} and ${name}).`);
        }

        sidebarOf.set(id, name);
    }
}

for (const name of Object.keys(sidebars)) {
    if (name !== 'userManualSidebar' && name !== 'technicalSidebar') {
        fail('sidebars.js', `unexpected sidebar "${name}"; every page belongs to userManualSidebar or technicalSidebar.`);
    }
}

for (const id of userSidebarIds) {
    if (!id.startsWith(USER_MANUAL_PREFIX)) {
        fail('sidebars.js', `"${id}" is in userManualSidebar but is not under docs/user-manual/.`);
    }
}

for (const id of technicalSidebarIds) {
    if (id.startsWith(USER_MANUAL_PREFIX)) {
        fail('sidebars.js', `"${id}" is in technicalSidebar but lives under docs/user-manual/.`);
    }
}

const docs = new Map();

for (const path of listDocs(docsDir)) {
    const id = relative(docsDir, path).split(sep).join('/').replace(/\.mdx?$/, '');
    const text = readFileSync(path, 'utf8');
    docs.set(id, {file: relative(siteDir, path).split(sep).join('/'), text, frontMatter: readFrontMatter(text)});
}

for (const id of sidebarOf.keys()) {
    if (!docs.has(id)) {
        fail('sidebars.js', `"${id}" is listed but docs/${id}.md does not exist.`);
    }
}

for (const [id, {file, text, frontMatter}] of docs) {
    const isUserManual = id.startsWith(USER_MANUAL_PREFIX);

    if (!sidebarOf.has(id) && frontMatter.unlisted !== 'true') {
        fail(file, 'is not listed in sidebars.js. Add it to userManualSidebar or technicalSidebar.');
    }

    const ownKey = isUserManual ? 'technical_manual' : 'user_manual';
    const wrongKey = isUserManual ? 'user_manual' : 'technical_manual';

    if (frontMatter[wrongKey] !== undefined) {
        fail(file, `uses "${wrongKey}" front matter; a ${isUserManual ? 'User' : 'Technical'} Manual page links to the other manual with "${ownKey}".`);
    }

    const counterparts = frontMatter[ownKey] === undefined ? [] : [].concat(frontMatter[ownKey]);

    for (const counterpart of counterparts) {
        if (!docs.has(counterpart)) {
            fail(file, `"${ownKey}" names "${counterpart}", which does not exist.`);
        } else if (counterpart.startsWith(USER_MANUAL_PREFIX) === isUserManual) {
            fail(file, `"${ownKey}" names "${counterpart}", which is in the same manual. Link to it in the text instead.`);
        }
    }

    if (!isUserManual) {
        continue;
    }

    const body = text.replace(/^---[\s\S]*?\n---/, '');
    const hasAccessTable = /^\|\s*\*\*(Menu|Page)\*\*\s*\|/m.test(body) && /^\|\s*\*\*Permissions?\*\*\s*\|/m.test(body);

    if (/^\s*(```|~~~)/m.test(body)) {
        fail(file, 'contains a code block. Code, configuration files and recipes belong in the Technical Manual.');
    }

    const featureId = /\b(?:CrestApps\.OrchardCore|OrchardCore)\.[A-Z][\w.]*/.exec(body.replace(/\]\([^)]*\)/g, ''));

    if (featureId) {
        fail(file, `mentions the technical name "${featureId[0]}". Use the feature name shown in Tools > Features, and keep IDs in the Technical Manual.`);
    }

    if (hasAccessTable && !body.includes('<AskYourAdmin')) {
        fail(file, 'has a Menu / Permission table but no <AskYourAdmin /> note under it.');
    }

    if (hasAccessTable && counterparts.length === 0) {
        fail(file, 'describes a feature but has no "technical_manual" front matter pointing to its Technical Manual page.');
    }
}

if (errors.length > 0) {
    console.error(`Manual checks failed (${errors.length}):\n`);
    errors.forEach((error) => console.error(`  - ${error}`));
    process.exit(1);
}

console.log(`Manual checks passed: ${userSidebarIds.length} User Manual pages, ${technicalSidebarIds.length} Technical Manual pages.`);
