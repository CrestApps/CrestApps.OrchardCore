import { beforeAll, describe, expect, it } from 'vitest';

// The script listens on the document for the formdata event. Without a DOM, a stand-in records the listener.
const listeners = [];

globalThis.window = globalThis.window ?? globalThis;
globalThis.document = globalThis.document ?? {
    addEventListener: (type, listener, capture) => listeners.push({ type, listener, capture }),
    querySelectorAll: () => [],
};

let numbers;

beforeAll(async () => {
    await import('../../../src/Core/CrestApps.OrchardCore.Core/Assets/js/invariant-number-inputs.js');
    numbers = globalThis.invariantNumberInputs;
});

const input = (type, name, options = {}) => ({ type, name, disabled: false, ...options });

// The FormData of a form being sent: only the parts the script uses.
const formData = (entries = []) => ({
    entries: [...entries],
    getAll(name) {
        return this.entries.filter(([key]) => key === name).map(([, value]) => value);
    },
    append(name, value) {
        this.entries.push([name, value]);
    },
});

const form = (elements, method = 'post') => ({ elements, getAttribute: (name) => (name === 'method' ? method : null) });

const invariantNames = (data) => data.getAll('__Invariant');

describe('namesToAdd', () => {
    it('names the number and range inputs that post a value', () => {
        const fields = [input('number', 'Lines[0].UnitPrice'), input('text', 'Lines[0].Name'), input('range', 'Rate'), input('hidden', 'Id')];

        expect(numbers.namesToAdd(fields, [])).toEqual(['Lines[0].UnitPrice', 'Rate']);
    });

    it('leaves out an input with no name or that is disabled', () => {
        const fields = [input('number', ''), input('number', 'Amount', { disabled: true }), input('number', 'Inside', { matches: () => true })];

        expect(numbers.namesToAdd(fields, [])).toEqual([]);
    });

    // An asp-for input already carries its own field, and two inputs can share a name.
    it('names each input once', () => {
        const fields = [input('number', 'Amount'), input('number', 'Amount'), input('number', 'Rows[0].Rate')];

        expect(numbers.namesToAdd(fields, ['Rows[0].Rate'])).toEqual(['Amount']);
    });
});

// Bootstrap rounds an input group's last child: the asp-for field after a number box would take that place.
describe('moveFieldsOutOfInputGroups', () => {
    it('moves each __Invariant field in an input group to just after the group', () => {
        const moved = [];
        const group = { after: (node) => moved.push(node) };
        const fields = [{ parentElement: group, id: 'amount' }, { parentElement: group, id: 'fee' }];
        let selector;

        numbers.moveFieldsOutOfInputGroups({ querySelectorAll: (query) => { selector = query; return fields; } });

        expect(selector).toBe('.input-group > input[type="hidden"][name="__Invariant"]');
        expect(moved.map((node) => node.id)).toEqual(['amount', 'fee']);
    });
});

describe('onFormData', () => {
    it('listens for every form being sent, on the way down to the form', () => {
        expect(listeners).toContainEqual({ type: 'formdata', listener: numbers.onFormData, capture: true });
    });

    // A row a script added or renumbered is named by what it is called when the form is sent.
    it('adds an __Invariant field for each number input of the form', () => {
        const data = formData([['Lines[1].UnitPrice', '12.50']]);

        numbers.onFormData({ target: form([input('number', 'Lines[0].UnitPrice'), input('number', 'Lines[1].UnitPrice')]), formData: data });

        expect(invariantNames(data)).toEqual(['Lines[0].UnitPrice', 'Lines[1].UnitPrice']);
    });

    it('keeps the fields the form already has', () => {
        const data = formData([['__Invariant', 'Latitude']]);

        numbers.onFormData({ target: form([input('number', 'Latitude'), input('number', 'Points[Late]')]), formData: data });

        expect(invariantNames(data)).toEqual(['Latitude', 'Points[Late]']);
    });

    // Values in the query string are read invariantly already, and the field would only add noise to the address.
    it('leaves a form sent with method get alone', () => {
        const data = formData();

        numbers.onFormData({ target: form([input('number', 'people')], 'GET'), formData: data });

        expect(invariantNames(data)).toEqual([]);
    });
});
