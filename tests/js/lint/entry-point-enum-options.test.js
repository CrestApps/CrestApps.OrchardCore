import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

// The entry point editor's dropdowns list their choices by hand, one <option> per enum member. A member added to the
// enum without an option can never be picked in the editor, and a line that already has it loses it on the next save,
// because the browser posts the first option instead. Each enum is read from its C# file and each view as text.
const src = join(__dirname, '..', '..', '..', 'src');
const models = join(src, 'Abstractions', 'CrestApps.OrchardCore.ContactCenter.Abstractions', 'Models');
const views = join(src, 'Modules', 'CrestApps.OrchardCore.ContactCenter', 'Views');

function enumMembers(name) {
    const text = readFileSync(join(models, `${name}.cs`), 'utf8')
        .replace(/\/\*[\s\S]*?\*\//g, '')
        .replace(/\/\/.*$/gm, '');
    const body = text.match(new RegExp(`enum\\s+${name}\\b[^{]*\\{([^}]*)\\}`));

    expect(body, `enum ${name}`).not.toBeNull();

    return body[1]
        .split(',')
        .map(member => member.replace(/\[[^\]]*\]/g, '').split('=')[0].trim())
        .filter(member => member.length > 0);
}

function optionValues(view, name) {
    const text = readFileSync(join(views, view), 'utf8');
    const values = new Set();
    const option = new RegExp(`<option\\s+value="@${name}\\.(\\w+)"`, 'g');
    let match;

    while ((match = option.exec(text)) !== null) {
        values.add(match[1]);
    }

    return values;
}

describe.each([
    ['EntryPointClosedAction', 'ContactCenterEntryPointHours.Edit.cshtml'],
    ['EntryPointTargetType', 'ContactCenterEntryPointRouting.Edit.cshtml'],
    ['InteractionPriority', 'ContactCenterEntryPointRouting.Edit.cshtml'],
    ['EntryPointVoicemailDestination', 'ContactCenterEntryPointVoicemail.Edit.cshtml'],
])('the %s dropdown in %s', (name, view) => {
    it('offers every member of the enum', () => {
        const members = enumMembers(name);
        const offered = optionValues(view, name);

        expect(members.length).toBeGreaterThan(1);
        expect(members.filter(member => !offered.has(member))).toEqual([]);
    });

    it('offers nothing the enum does not have', () => {
        const members = new Set(enumMembers(name));

        expect([...optionValues(view, name)].filter(value => !members.has(value))).toEqual([]);
    });
});
