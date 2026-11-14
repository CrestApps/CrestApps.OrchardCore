import { readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { describe, expect, it } from 'vitest';

// Script written inline in a Razor view cannot be linted, tested, cached or covered by a strict Content Security
// Policy, and it is where localized text gets pasted into JavaScript string literals: '@T["..."]' is HTML-encoded, so
// an apostrophe reaches the user as &#x27; and a quote can end the string. New script goes in the module's
// Assets/js/ as a resource, configured through data-* attributes or one JSON block.
//
// This is a ratchet, not a bar. The baseline next to this file records, per view, the inline script lines and the
// localized strings quoted inside script that were there when the rule was set. A view may go below its numbers (and
// then has to lower them, so the gain is kept), never above; a view that is not listed may have neither.
//
// After moving script out of views, lower the baseline with:
//     UPDATE_INLINE_SCRIPT_BASELINE=1 npx vitest run tests/js/lint/inline-scripts-ratchet.test.js
// The update only lowers numbers and drops views that reached zero; it never raises one.
const repositoryRoot = join(__dirname, '..', '..', '..');
const sourceRoot = join(repositoryRoot, 'src');
const baselinePath = join(__dirname, 'inline-scripts-ratchet.baseline.json');

// A <script> element with its attributes and body. Razor writes these as plain HTML, so a regular expression is enough.
const scriptElement = /<script\b([^>]*)>([\s\S]*?)<\/script\b[^>]*>/gi;

// A script loaded from a file or a registered resource has no inline body worth counting.
const externalScript = /\b(?:src|asp-src|asp-name)\s*=/i;

// A data block (JSON configuration, JSON-LD, a template) is the recommended way to hand values to a script.
const scriptType = /\btype\s*=\s*["']([^"']*)["']/i;
const javaScriptTypes = new Set(['', 'text/javascript', 'application/javascript', 'module']);

// A localized string written straight into a JavaScript string literal: '@T["..."]' or "@T["..."]".
const quotedLocalizedString = /['"]@T\[/g;

function views(directory, found = []) {
    for (const name of readdirSync(directory)) {
        if (name === 'node_modules' || name === 'bin' || name === 'obj' || name === 'wwwroot') {
            continue;
        }

        const path = join(directory, name);

        if (statSync(path).isDirectory()) {
            views(path, found);
        } else if (name.endsWith('.cshtml')) {
            found.push(path);
        }
    }

    return found;
}

function inlineScripts(text) {
    const bodies = [];

    for (const [, attributes, body] of text.matchAll(scriptElement)) {
        if (externalScript.test(attributes)) {
            continue;
        }

        const type = (scriptType.exec(attributes)?.[1] ?? '').trim().toLowerCase();

        if (!javaScriptTypes.has(type)) {
            continue;
        }

        bodies.push(body);
    }

    return bodies;
}

function measure() {
    const scriptLines = {};
    const localizedStrings = {};

    for (const file of views(sourceRoot)) {
        const bodies = inlineScripts(readFileSync(file, 'utf8'));

        if (bodies.length === 0) {
            continue;
        }

        const path = relative(repositoryRoot, file).split(sep).join('/');
        const lines = bodies.reduce((total, body) => total + body.split('\n').filter(line => line.trim() !== '').length, 0);
        const hits = bodies.reduce((total, body) => total + (body.match(quotedLocalizedString)?.length ?? 0), 0);

        if (lines > 0) {
            scriptLines[path] = lines;
        }

        if (hits > 0) {
            localizedStrings[path] = hits;
        }
    }

    return { scriptLines, localizedStrings };
}

function sorted(record) {
    return Object.fromEntries(Object.entries(record).sort(([left], [right]) => left.localeCompare(right)));
}

function lowered(recorded, actual) {
    const result = {};

    for (const [path, ceiling] of Object.entries(recorded)) {
        const value = Math.min(ceiling, actual[path] ?? 0);

        if (value > 0) {
            result[path] = value;
        }
    }

    return sorted(result);
}

function grown(recorded, actual, describeExcess) {
    return Object.entries(actual)
        .filter(([path, value]) => value > (recorded[path] ?? 0))
        .map(([path, value]) => describeExcess(path, recorded[path] ?? 0, value));
}

function stale(recorded, actual, unit) {
    return Object.entries(recorded)
        .filter(([path, ceiling]) => (actual[path] ?? 0) < ceiling)
        .map(([path, ceiling]) => `${path}: ${actual[path] ?? 0} ${unit}, recorded ${ceiling}. Lower it in the baseline.`);
}

describe('inline scripts in views', () => {
    // It reads every view in the repository: a few seconds, and more while a build runs beside it.
    const actual = measure();
    const baseline = JSON.parse(readFileSync(baselinePath, 'utf8'));

    if (process.env.UPDATE_INLINE_SCRIPT_BASELINE) {
        const updated = {
            scriptLines: lowered(baseline.scriptLines, actual.scriptLines),
            localizedStrings: lowered(baseline.localizedStrings, actual.localizedStrings),
        };

        writeFileSync(baselinePath, `${JSON.stringify(updated, null, 4)}\n`);
        Object.assign(baseline, updated);
    }

    it('are found, so the checks below are not passing on nothing', () => {
        expect(Object.keys(actual.scriptLines).length).toBeGreaterThan(50);
    });

    it('never grow past the lines a view had when the ratchet was set', { timeout: 30000 }, () => {
        const offenders = grown(baseline.scriptLines, actual.scriptLines, (path, ceiling, lines) => ceiling === 0
            ? `${path}: ${lines} inline script lines. Put the script in the module's Assets/js/ as a resource.`
            : `${path}: ${lines} inline script lines, up from ${ceiling}. Move script out of the view rather than adding to it.`);

        expect(offenders).toEqual([]);
    });

    it('never quote more localized strings inside script than a view did when the ratchet was set', () => {
        const offenders = grown(baseline.localizedStrings, actual.localizedStrings, (path, ceiling, hits) =>
            `${path}: ${hits} '@T[...]' or "@T[...]" inside <script>, allowed ${ceiling}. Pass the text through a data-* attribute or a JSON block (@Json.Serialize(T["..."].Value)).`);

        expect(offenders).toEqual([]);
    });

    it('record no more than the views still have, so every gain is kept', () => {
        const offenders = [
            ...stale(baseline.scriptLines, actual.scriptLines, 'inline script lines'),
            ...stale(baseline.localizedStrings, actual.localizedStrings, 'quoted localized strings'),
        ];

        expect(offenders, 'Run UPDATE_INLINE_SCRIPT_BASELINE=1 npx vitest run tests/js/lint/inline-scripts-ratchet.test.js').toEqual([]);
    });
});
