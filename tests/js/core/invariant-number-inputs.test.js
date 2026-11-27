import { beforeAll, beforeEach, describe, expect, it } from 'vitest';

// The script listens on the document for the submit and formdata events. Without a DOM, a stand-in records the listeners.
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

const button = (formmethod) => ({ getAttribute: (name) => (name === 'formmethod' ? formmethod : null) });

// A submit event as the formdata listener sees it: finished (eventPhase 0) when the browser sends the form, still being
// dispatched (eventPhase 2) when a submit handler reads new FormData(form).
const submit = (target, { submitter = null, finished = true, defaultPrevented = false } = {}) => ({ target, submitter, eventPhase: finished ? 0 : 2, defaultPrevented });

const send = (target, data = formData(), submitEvent = null) => {
    if (submitEvent) {
        numbers.onSubmit(submitEvent);
    }

    numbers.onFormData({ target, formData: data });

    return data.getAll('__Invariant');
};

describe('namesToAdd', () => {
    it('names the inputs the browser posts in an invariant format', () => {
        const fields = [
            input('number', 'Lines[0].UnitPrice'),
            input('text', 'Lines[0].Name'),
            input('range', 'Rate'),
            input('date', 'DueDate'),
            input('datetime-local', 'Prices[1].EffectiveFrom'),
            input('time', 'Start'),
            input('month', 'Period'),
            input('week', 'Week'),
            input('hidden', 'Id'),
            input('checkbox', 'IsActive'),
        ];

        expect(numbers.namesToAdd(fields, [])).toEqual(['Lines[0].UnitPrice', 'Rate', 'DueDate', 'Prices[1].EffectiveFrom', 'Start', 'Period', 'Week']);
    });

    it('leaves out an input with no name or that is disabled', () => {
        const fields = [input('number', ''), input('number', 'Amount', { disabled: true }), input('date', 'Inside', { matches: () => true })];

        expect(numbers.namesToAdd(fields, [])).toEqual([]);
    });

    // An asp-for input already carries its own field, and two inputs can share a name.
    it('names each input once', () => {
        const fields = [input('number', 'Amount'), input('number', 'Amount'), input('number', 'Rows[0].Rate')];

        expect(numbers.namesToAdd(fields, ['Rows[0].Rate'])).toEqual(['Amount']);
    });
});

describe('onFormData', () => {
    beforeEach(() => {
        // Clears a submit event a previous test left behind.
        numbers.onFormData({ target: form([]), formData: formData() });
    });

    it('listens for every form being sent, on the way down to the form', () => {
        expect(listeners).toContainEqual({ type: 'formdata', listener: numbers.onFormData, capture: true });
        expect(listeners).toContainEqual({ type: 'submit', listener: numbers.onSubmit, capture: true });
    });

    // A row a script added or renumbered is named by what it is called when the form is sent.
    it('adds an __Invariant field for each input of the form', () => {
        const target = form([input('number', 'Lines[0].UnitPrice'), input('number', 'Lines[1].UnitPrice')]);

        expect(send(target, formData([['Lines[1].UnitPrice', '12.50']]), submit(target))).toEqual(['Lines[0].UnitPrice', 'Lines[1].UnitPrice']);
    });

    it('keeps the fields the form already has', () => {
        const target = form([input('number', 'Latitude'), input('number', 'Points[Late]')]);

        expect(send(target, formData([['__Invariant', 'Latitude']]))).toEqual(['Latitude', 'Points[Late]']);
    });

    // Values in the query string are read invariantly already, and the field would only add noise to the address.
    it('leaves a form the browser sends with GET alone', () => {
        const target = form([input('number', 'people')], 'GET');

        expect(send(target, formData(), submit(target))).toEqual([]);
    });

    it('treats a form with no method that the browser sends as GET', () => {
        const target = form([input('number', 'people')], null);

        expect(send(target, formData(), submit(target))).toEqual([]);
    });

    it('follows the formmethod of the button that sends the form', () => {
        const getForm = form([input('number', 'Amount')], 'get');
        const postForm = form([input('number', 'Amount')], 'post');

        expect(send(getForm, formData(), submit(getForm, { submitter: button('post') }))).toEqual(['Amount']);
        expect(send(postForm, formData(), submit(postForm, { submitter: button('GET') }))).toEqual([]);
    });

    // A submit handler that reads new FormData(form) usually posts it with fetch, whatever the form says.
    it('names the inputs for a script that reads the form in its submit handler', () => {
        const target = form([input('number', 'Amount')], null);

        expect(send(target, formData(), submit(target, { finished: false }))).toEqual(['Amount']);
        expect(send(target, formData(), submit(target, { defaultPrevented: true }))).toEqual(['Amount']);
    });

    it('names the inputs for new FormData(form) outside a submit unless the form says GET', () => {
        expect(send(form([input('number', 'Amount')], null))).toEqual(['Amount']);
        expect(send(form([input('number', 'Amount')], 'get'))).toEqual([]);
    });

    it('uses a submit event only for the form it was sent with', () => {
        const other = form([input('number', 'Amount')], null);
        const target = form([input('number', 'Amount')], null);

        numbers.onSubmit(submit(other));

        expect(send(target)).toEqual(['Amount']);
    });
});

// Bootstrap rounds an input group's last child: the asp-for field after a number box would take that place.
describe('moveFieldsOutOfInputGroups', () => {
    const run = (fields) => {
        let selector;

        numbers.moveFieldsOutOfInputGroups({ querySelectorAll: (query) => { selector = query; return fields; } });

        return selector;
    };

    it('moves each __Invariant field in an input group to just after the group', () => {
        const moved = [];
        const group = { tagName: 'DIV', after: (node) => moved.push(node) };

        const selector = run([{ parentElement: group, id: 'amount' }, { parentElement: group, id: 'fee' }]);

        expect(selector).toBe('.input-group > input[type="hidden"][name="__Invariant"]');
        expect(moved.map((node) => node.id)).toEqual(['amount', 'fee']);
    });

    // Moving it after the form would take it out of the form, and it would no longer be sent.
    it('leaves a field in a form that is itself the input group', () => {
        const moved = [];

        run([{ parentElement: { tagName: 'FORM', after: (node) => moved.push(node) } }]);

        expect(moved).toEqual([]);
    });
});
