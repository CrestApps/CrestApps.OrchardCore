import { readdirSync, readFileSync } from 'node:fs';
import { join, sep } from 'node:path';
import { describe, expect, it } from 'vitest';

// Small consistency rules from the November 2026 review (UX-6): the admin runs Bootstrap 5, every word a person reads
// or hears is localized, and the admin does not load jQuery for a script that never calls it.
const modulesDirectory = join('src', 'Modules');

function readViews() {
    return readdirSync(modulesDirectory, { recursive: true })
        .map(file => String(file))
        .filter(file => file.endsWith('.cshtml') && !file.split(sep).some(part => part === 'bin' || part === 'obj'))
        .map(file => ({ name: file.split(sep).join('/'), text: readFileSync(join(modulesDirectory, file), 'utf8') }));
}

// Read once while the file is collected: reading every view takes a few seconds, more while a build runs beside it.
const all = readViews();

function offending(pattern) {
    return all.flatMap(({ name, text }) => text.split(/\r?\n/)
        .map((line, index) => ({ line, number: index + 1 }))
        .filter(({ line }) => pattern.test(line))
        .map(({ line, number }) => `${name}:${number}: ${line.trim()}`));
}

describe('views', () => {
    it('use the Bootstrap 5 names of the classes Bootstrap 4 called text-right, text-left and font-weight-*', () => {
        expect(offending(/class="[^"]*\b(?:text-right|text-left|font-weight-(?:normal|bold|light))\b/)).toEqual([]);
    });

    // A label a screen reader speaks, a tooltip, or a dialog title written in English straight into the markup.
    it('localize aria-label, title and data-title', () => {
        expect(offending(/\s(?:aria-label|title|data-title)="[A-Za-z]/)).toEqual([]);
    });

    it('localize an English sentence used as a placeholder', () => {
        expect(offending(/\splaceholder="[A-Z][a-z]+(?: [a-z]+)+\.?"/)).toEqual([]);
    });
});

describe('scripts', () => {
    // The tooltip of a button a script builds comes from the page (a data-* attribute rendered with T[...]).
    it.each([
        'CrestApps.OrchardCore.Omnichannel.Managements/Assets/js/activity-query-builder.js',
        'CrestApps.OrchardCore.Telephony/Assets/js/phone-field-dialer.js',
    ])('%s writes no English tooltip of its own', script => {
        expect(readFileSync(join(modulesDirectory, script), 'utf8')).not.toMatch(/\.title\s*=\s*'[A-Z]/);
    });
});

describe('jQuery', () => {
    it.each([
        'CrestApps.OrchardCore.ContactCenter/Views/MyVoicemailGreeting/Index.cshtml',
        'CrestApps.OrchardCore.Taxation/Views/TaxTableFields.Edit.cshtml',
        'CrestApps.OrchardCore.AI/Views/AIProfileTemplateFields.Edit.cshtml',
    ])('is not loaded by %s, whose script does not use it', view => {
        const text = readFileSync(join(modulesDirectory, view), 'utf8');

        expect(text).not.toMatch(/depends-on="jQuery"|asp-name="jQuery"/);
        expect(text).not.toMatch(/\$\(/);
    });
});
