// @ts-check

import { getHidden, isConditionMet, isRuleMet, same } from '../../../CrestApps.OrchardCore.Resources/Assets/js/conditions.js';

/**
 * Shows and hides the fields of a content editor whose settings say "Show this field when". The server makes the
 * same decision when the form is posted, drops what a hidden field holds and ignores its validation; this script
 * only spares people the questions that don't apply. The rules themselves are the shared Conditions evaluator
 * (Resources module, conditions.js), the browser mirror of the server's ConditionEvaluator.
 *
 * Each editor with rules carries a <script type="application/json" data-conditional-fields> block:
 * {
 *   "prefix": "",                       the editor's HTML field prefix, if any
 *   "fields": [{ "key": "Ticket.Model", "rule": { "Match": "All", "Conditions": [...] } }],
 *   "sources": { "Ticket.Outage": { "kind": "BooleanField", "input": "Ticket.Outage", "values": ["false"] } },
 *   "ancestors": { "<value>": ["<parent>", "<grandparent>"] }
 * }
 * A source's "input" is the name its editor posts under (a field's prefix, or a module's own input); "values" is
 * what the server read, used when the form has no input for it.
 */

/**
 * @typedef {import('../../../CrestApps.OrchardCore.Resources/Assets/js/conditions.js').Rule} Rule
 * @typedef {import('../../../CrestApps.OrchardCore.Resources/Assets/js/conditions.js').Value} Value
 * @typedef {{ kind?: string, input?: string, values?: string[] }} Source
 * @typedef {{ prefix?: string, fields: { key: string, rule: Rule }[], sources: Record<string, Source>, ancestors?: Record<string, string[]> }} Model
 */

/**
 * The form controls posted under a name: the name itself or anything below it.
 * @param {ParentNode} scope
 * @param {string} name
 * @returns {(HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement)[]}
 */
function controlsUnder(scope, name) {
    /** @type {(HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement)[]} */
    const found = [];

    scope.querySelectorAll('input[name], select[name], textarea[name]').forEach((element) => {
        const control = /** @type {HTMLInputElement} */ (element);

        if (control.name === name || control.name.startsWith(name + '.') || control.name.startsWith(name + '[')) {
            found.push(control);
        }
    });

    return found;
}

/**
 * Reads what a source holds in the form, or null when the form has no control for it.
 * @param {ParentNode} scope
 * @param {Source} source
 * @returns {string[] | null}
 */
function readControls(scope, source) {
    if (!source.input) {
        return null;
    }

    // A control hidden by a rule still holds its value but must not count; disabled controls are left out.
    const controls = controlsUnder(scope, source.input).filter((control) => !control.disabled || control.dataset.conditionalDisabled === '1');

    if (controls.length === 0) {
        return null;
    }

    const kind = source.kind ?? '';

    if (kind === 'BooleanField') {
        return [controls.some((control) => control instanceof HTMLInputElement && control.type === 'checkbox' && control.checked && same(control.value, 'true')) ? 'true' : 'false'];
    }

    if (kind === 'TaxonomyField') {
        const unique = controls.find((control) => control instanceof HTMLInputElement && control.type === 'radio' && control.checked);

        if (unique) {
            return [unique.value];
        }

        /** @type {string[]} */
        const values = [];

        for (const control of controls) {
            if (control instanceof HTMLInputElement && control.type === 'checkbox' && control.checked && control.name.endsWith('.Selected')) {
                const idName = control.name.slice(0, -'.Selected'.length) + '.ContentItemId';
                const id = controls.find((other) => other.name === idName);

                if (id && id.value) {
                    values.push(id.value);
                }
            } else if (control.name.endsWith('.TermContentItemIds') || control instanceof HTMLSelectElement) {
                values.push(...readOne(control).flatMap((value) => value.split(',')));
            }
        }

        return values.filter((value) => value.trim() !== '');
    }

    if (kind === 'ContentPickerField' || kind === 'UserPickerField' || kind === 'LocalizationSetContentPickerField') {
        return controls.flatMap(readOne).flatMap((value) => value.split(',')).filter((value) => value.trim() !== '');
    }

    return controls.flatMap(readOne);
}

/** @param {HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement} control @returns {string[]} */
function readOne(control) {
    if (control instanceof HTMLSelectElement) {
        return Array.from(control.selectedOptions).map((option) => option.value);
    }

    if (control instanceof HTMLInputElement && (control.type === 'checkbox' || control.type === 'radio')) {
        return control.checked ? [control.value] : [];
    }

    return [control.value];
}

/**
 * The wrapper of a field's editor: Orchard Core wraps every field editor in a .field-wrapper.
 * @param {ParentNode} scope
 * @param {string} name
 * @returns {HTMLElement | null}
 */
function wrapperOf(scope, name) {
    const control = controlsUnder(scope, name)[0];

    return control ? /** @type {HTMLElement | null} */ (control.closest('.field-wrapper')) : null;
}

/** @param {HTMLElement} wrapper @param {boolean} hide */
function toggle(wrapper, hide) {
    if (wrapper.hidden === hide) {
        return;
    }

    wrapper.hidden = hide;

    wrapper.querySelectorAll('input, select, textarea, button').forEach((element) => {
        const control = /** @type {HTMLInputElement} */ (element);

        if (hide && !control.disabled) {
            // A hidden control doesn't post, and its browser validation can't block the form.
            control.disabled = true;
            control.dataset.conditionalDisabled = '1';
        } else if (!hide && control.dataset.conditionalDisabled === '1') {
            control.disabled = false;
            delete control.dataset.conditionalDisabled;
        }
    });
}

/** @param {HTMLScriptElement} element */
function start(element) {
    /** @type {Model} */
    let model;

    try {
        model = JSON.parse(element.textContent ?? '{}');
    } catch {
        return;
    }

    const scope = element.closest('form') ?? document;
    const prefix = model.prefix ? model.prefix + '.' : '';
    const ancestors = model.ancestors ?? {};

    /** @type {Record<string, Rule>} */
    const rules = {};

    for (const field of model.fields ?? []) {
        rules[field.key] = field.rule;
    }

    /** @param {string} key @returns {Value | null} */
    const lookup = (key) => {
        const source = model.sources?.[key];

        if (!source) {
            return null;
        }

        const values = readControls(scope, source) ?? source.values ?? [];

        return { values, ancestors: values.flatMap((value) => ancestors[value] ?? []) };
    };

    const update = () => {
        const hidden = getHidden(rules, lookup);

        for (const field of model.fields ?? []) {
            const wrapper = wrapperOf(scope, prefix + field.key);

            if (wrapper) {
                toggle(wrapper, hidden.has(field.key));
            }
        }
    };

    scope.addEventListener('input', update);
    scope.addEventListener('change', update);

    // Pickers that set a hidden input from script raise no event; look again after a click.
    scope.addEventListener('click', () => window.setTimeout(update, 0));

    update();
}

function startAll() {
    document.querySelectorAll('script[type="application/json"][data-conditional-fields]').forEach((element) => {
        const script = /** @type {HTMLScriptElement} */ (element);

        if (script.dataset.conditionalFieldsStarted !== '1') {
            script.dataset.conditionalFieldsStarted = '1';
            start(script);
        }
    });
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', startAll);
} else {
    startAll();
}

// The rules, for tests and for scripts that build editors of their own.
globalThis.CrestAppsConditionalFields = { conditionMet: isConditionMet, ruleMet: isRuleMet, hiddenKeys: getHidden, startAll };
