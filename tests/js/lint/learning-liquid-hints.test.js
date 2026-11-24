import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { describe, expect, it } from 'vitest';

// A view's localized text is formatted when the page renders, even without arguments, so "{{" in @T["..."] prints as
// "{". A Learning hint that shows a Liquid example such as {{ Workflow.Input.UserId }} passes it as an argument
// (@T["The person, such as {0}.", "{{ Workflow.Input.UserId }}"]) so the page shows both braces.
const root = join(__dirname, '..', '..', '..', 'src', 'Modules');

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

const liquidInLocalizedText = /@(?:T|H)\["[^"]*\{\{[^"]*"/g;

describe('Learning views', () => {
    it('pass a Liquid example to a localized hint as an argument', () => {
        const offenders = [];

        for (const module of readdirSync(root).filter((name) => name.startsWith('CrestApps.OrchardCore.Learning'))) {
            for (const file of views(join(root, module))) {
                const text = readFileSync(file, 'utf8');
                let match;

                while ((match = liquidInLocalizedText.exec(text)) !== null) {
                    const line = text.slice(0, match.index).split('\n').length;
                    offenders.push(`${relative(root, file)}:${line}`);
                }
            }
        }

        expect(offenders).toEqual([]);
    });
});
