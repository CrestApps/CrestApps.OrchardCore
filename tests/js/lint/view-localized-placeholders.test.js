import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { describe, expect, it } from 'vitest';

// A localized string with a {0} placeholder written straight into a view (@T["… {0} …"]) is formatted when the page
// renders, and with no argument that throws and takes the whole page down (the soft phone went blank this way). Such a
// string must either be given its arguments or be taken as plain text with .Value for script to fill in.
const root = join(__dirname, '..', '..', '..', 'src');

function views(dir, found = []) {
    for (const name of readdirSync(dir)) {
        if (name === 'node_modules' || name === 'bin' || name === 'obj' || name === 'wwwroot') {
            continue;
        }

        const path = join(dir, name);

        if (statSync(path).isDirectory()) {
            views(path, found);
        } else if (name.endsWith('.cshtml')) {
            found.push(path);
        }
    }

    return found;
}

// @T["...{0}..."] or @H["...{0}..."] rendered as-is: nothing (no argument list, no .Value) right after the key.
const unformatted = new RegExp('@(?:T|H)\\["([^"]*\\{\\d+\\}[^"]*)"\\](?!\\s*[.,(\\[])', 'g');

describe('views', () => {
    it('never render a localized placeholder string without its arguments', () => {
        const offenders = [];

        for (const file of views(root)) {
            const text = readFileSync(file, 'utf8');
            let match;

            while ((match = unformatted.exec(text)) !== null) {
                const line = text.slice(0, match.index).split('\n').length;
                offenders.push(`${relative(root, file)}:${line} ${match[0]}`);
            }
        }

        expect(offenders).toEqual([]);
    });
});
