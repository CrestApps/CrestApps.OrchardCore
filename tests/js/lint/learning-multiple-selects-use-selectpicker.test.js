import { readdirSync, readFileSync } from 'node:fs';
import { join, sep } from 'node:path';
import { describe, expect, it } from 'vitest';

// A multiple select in the Learning modules, and in Skills which the Learning stack added, is a bootstrap-select
// picker (selectpicker), as in the rest of the admin: searchable, with the count of what is selected. A plain
// <select multiple> needs Ctrl to choose several and shows nothing of what is chosen once it scrolls. Long lists use
// the ItemSelector or UserPicker component, which renders a selectpicker of its own and searches on the server.
const modulesDirectory = join('src', 'Modules');
const modules = /^CrestApps\.OrchardCore\.(Learning|Skills)/;

function views() {
    return readdirSync(modulesDirectory, { recursive: true })
        .map(file => String(file))
        .filter(file => modules.test(file) && file.endsWith('.cshtml'))
        .filter(file => !file.split(sep).some(part => part === 'bin' || part === 'obj'))
        .map(file => join(modulesDirectory, file))
        .map(path => ({ path, text: readFileSync(path, 'utf8') }));
}

describe('Learning multiple selects', () => {
    const all = views();

    it('are found, so the check below is not passing on nothing', () => {
        expect(all.filter(({ text }) => /<select\b[^>]*\bmultiple\b/.test(text)).length).toBeGreaterThan(10);
    });

    it('are selectpickers that load the bootstrap-select resources', () => {
        const offenders = [];

        for (const { path, text } of all) {
            for (const match of text.matchAll(/<select\b[^>]*\bmultiple\b[^>]*>/g)) {
                const line = text.slice(0, match.index).split('\n').length;

                if (!/class="[^"]*\bselectpicker\b/.test(match[0])) {
                    offenders.push(`${path}:${line} is not a selectpicker`);
                } else if (!text.includes('asp-name="bootstrap-select"')) {
                    offenders.push(`${path}:${line} does not load bootstrap-select`);
                }
            }
        }

        expect(offenders).toEqual([]);
    });
});
