// @ts-check
/*
 * The rule builder: edits a condition rule (all or any of a list of conditions, with nested groups) against the
 * sources a rule may read (earlier questions with their choices and rows, variables, scores), and writes the rule as
 * JSON into the hidden input of its field. The server checks the rule again when it is saved.
 */

/**
 * @typedef {{ id: string, text: string }} Option
 * @typedef {{ id: string, label: string, choices: Option[], rows: Option[] }} Source
 * @typedef {{ value: string, text: string, noValue?: boolean, twoValues?: boolean }} Operator
 * @typedef {{ source?: string, operator?: string, values?: string[] }} Condition
 * @typedef {{ match?: string, conditions?: Condition[], groups?: Rule[] }} Rule
 * @typedef {{ operators: Operator[], texts: Record<string, string> }} Context
 */

const choiceOperators = new Set(['Is', 'IsNot', 'IsOneOf', 'IsNoneOf']);
const multipleOperators = new Set(['IsOneOf', 'IsNoneOf']);
const maxDepth = 3;

/**
 * @param {string} tag The tag.
 * @param {string} [className] The classes.
 * @returns {HTMLElement} The element.
 */
function element(tag, className) {
    const result = document.createElement(tag);

    if (className) {
        result.className = className;
    }

    return result;
}

/**
 * @param {string} value The value.
 * @param {string} text The text.
 * @param {boolean} [selected] Whether it is selected.
 * @returns {HTMLOptionElement} The option.
 */
function option(value, text, selected) {
    const result = /** @type {HTMLOptionElement} */ (element('option'));

    result.value = value;
    result.textContent = text;
    result.selected = Boolean(selected);

    return result;
}

/**
 * Reads a rule from JSON, or an empty one.
 * @param {string} json The JSON.
 * @returns {Rule} The rule.
 */
export function parseRule(json) {
    if (!json) {
        return { match: 'All', conditions: [], groups: [] };
    }

    try {
        const rule = JSON.parse(json);

        return { match: rule.match ?? rule.Match ?? 'All', conditions: rule.conditions ?? rule.Conditions ?? [], groups: (rule.groups ?? rule.Groups ?? []).map(group => parseRule(JSON.stringify(group))) };
    } catch {
        return { match: 'All', conditions: [], groups: [] };
    }
}

/**
 * Tells whether a rule has no condition with a source.
 * @param {Rule} rule The rule.
 * @returns {boolean} Whether it is empty.
 */
export function isEmptyRule(rule) {
    return !(rule.conditions ?? []).some(condition => condition.source) && !(rule.groups ?? []).some(group => !isEmptyRule(group));
}

/**
 * Writes a rule as JSON, or an empty string when it has no condition.
 * @param {Rule} rule The rule.
 * @returns {string} The JSON.
 */
export function writeRule(rule) {
    /**
     * @param {Rule} item The rule.
     * @returns {Rule} The rule without empty parts.
     */
    const clean = item => ({
        match: item.match ?? 'All',
        conditions: (item.conditions ?? []).filter(condition => condition.source).map(condition => ({ source: condition.source, operator: condition.operator ?? 'Is', values: (condition.values ?? []).filter(value => value !== '') })),
        groups: (item.groups ?? []).filter(group => !isEmptyRule(group)).map(clean),
    });

    return isEmptyRule(rule) ? '' : JSON.stringify(clean(rule));
}

/**
 * Finds the source a condition reads, and the row when it reads one.
 * @param {Source[]} sources The sources.
 * @param {string | undefined} key The condition's source.
 * @returns {{ source?: Source, rowId?: string }} The source and row.
 */
function findSource(sources, key) {
    if (!key) {
        return {};
    }

    const exact = sources.find(source => source.id === key);

    if (exact) {
        return { source: exact };
    }

    const dot = key.indexOf('.');

    if (dot > 0) {
        return { source: sources.find(source => source.id === key.substring(0, dot)), rowId: key.substring(dot + 1) };
    }

    return {};
}

/**
 * Renders one rule (or group) into a container.
 * @param {HTMLElement} container Where to render.
 * @param {Rule} rule The rule, changed in place.
 * @param {Source[]} sources The sources.
 * @param {Context} context The operators and texts.
 * @param {() => void} changed Called after every change.
 * @param {number} depth The depth of the group.
 */
function renderRule(container, rule, sources, context, changed, depth) {
    const texts = context.texts;

    container.replaceChildren();

    const rerender = () => {
        renderRule(container, rule, sources, context, changed, depth);
        changed();
    };

    const match = /** @type {HTMLSelectElement} */ (element('select', 'form-select form-select-sm w-auto mb-1'));

    match.setAttribute('aria-label', texts.all);
    match.append(option('All', texts.all, rule.match !== 'Any'), option('Any', texts.any, rule.match === 'Any'));
    match.addEventListener('change', () => {
        rule.match = match.value;
        changed();
    });
    container.append(match);

    rule.conditions = rule.conditions ?? [];
    rule.groups = rule.groups ?? [];

    if (rule.conditions.length === 0 && rule.groups.length === 0) {
        const empty = element('div', 'small text-body-secondary mb-1');

        empty.textContent = texts.always;
        container.append(empty);
    }

    rule.conditions.forEach((condition, index) => {
        container.append(renderCondition(condition, sources, context, changed, () => {
            rule.conditions?.splice(index, 1);
            rerender();
        }, rerender));
    });

    rule.groups.forEach((group, index) => {
        const box = element('div', 'border-start border-2 ps-2 ms-1 mb-1');
        const inner = element('div');
        const remove = /** @type {HTMLButtonElement} */ (element('button', 'btn btn-sm btn-link text-danger p-0'));

        remove.type = 'button';
        remove.textContent = texts.remove;
        remove.addEventListener('click', () => {
            rule.groups?.splice(index, 1);
            rerender();
        });

        renderRule(inner, group, sources, context, changed, depth + 1);
        box.append(inner, remove);
        container.append(box);
    });

    const actions = element('div', 'd-flex gap-2');
    const addCondition = /** @type {HTMLButtonElement} */ (element('button', 'btn btn-sm btn-outline-secondary'));

    addCondition.type = 'button';
    addCondition.textContent = texts.addCondition;
    addCondition.disabled = sources.length === 0;
    addCondition.addEventListener('click', () => {
        rule.conditions?.push({ source: '', operator: 'Is', values: [] });
        rerender();
    });
    actions.append(addCondition);

    if (depth < maxDepth) {
        const addGroup = /** @type {HTMLButtonElement} */ (element('button', 'btn btn-sm btn-outline-secondary'));

        addGroup.type = 'button';
        addGroup.textContent = texts.addGroup;
        addGroup.disabled = sources.length === 0;
        addGroup.addEventListener('click', () => {
            rule.groups?.push({ match: 'Any', conditions: [{ source: '', operator: 'Is', values: [] }], groups: [] });
            rerender();
        });
        actions.append(addGroup);
    }

    container.append(actions);
}

/**
 * Renders one condition.
 * @param {Condition} condition The condition, changed in place.
 * @param {Source[]} sources The sources.
 * @param {Context} context The operators and texts.
 * @param {() => void} changed Called after a value changes.
 * @param {() => void} removed Called when the condition is removed.
 * @param {() => void} rerender Called when the condition needs other inputs.
 * @returns {HTMLElement} The row.
 */
function renderCondition(condition, sources, context, changed, removed, rerender) {
    const texts = context.texts;
    const row = element('div', 'input-group input-group-sm mb-1 flex-nowrap');
    const { source, rowId } = findSource(sources, condition.source);

    const sourceSelect = /** @type {HTMLSelectElement} */ (element('select', 'form-select'));

    sourceSelect.setAttribute('aria-label', texts.source);
    sourceSelect.append(option('', texts.chooseSource, !source));

    for (const item of sources) {
        sourceSelect.append(option(item.id, item.label, source?.id === item.id));
    }

    sourceSelect.addEventListener('change', () => {
        condition.source = sourceSelect.value;
        condition.values = [];
        rerender();
    });
    row.append(sourceSelect);

    if (source && source.rows.length > 0) {
        const rowSelect = /** @type {HTMLSelectElement} */ (element('select', 'form-select'));

        rowSelect.setAttribute('aria-label', texts.row);
        rowSelect.append(option('', texts.anyRow, !rowId));

        for (const item of source.rows) {
            rowSelect.append(option(item.id, item.text, item.id === rowId));
        }

        rowSelect.addEventListener('change', () => {
            condition.source = rowSelect.value ? `${source.id}.${rowSelect.value}` : source.id;
            changed();
        });
        row.append(rowSelect);
    }

    const operatorSelect = /** @type {HTMLSelectElement} */ (element('select', 'form-select'));

    operatorSelect.setAttribute('aria-label', texts.comparison);

    for (const item of context.operators) {
        operatorSelect.append(option(item.value, item.text, (condition.operator ?? 'Is') === item.value));
    }

    operatorSelect.addEventListener('change', () => {
        condition.operator = operatorSelect.value;
        rerender();
    });
    row.append(operatorSelect);

    const operator = context.operators.find(item => item.value === (condition.operator ?? 'Is'));
    condition.values = condition.values ?? [];

    if (!operator?.noValue) {
        if (source && source.choices.length > 0 && choiceOperators.has(condition.operator ?? 'Is')) {
            const valueSelect = /** @type {HTMLSelectElement} */ (element('select', 'form-select'));

            valueSelect.setAttribute('aria-label', texts.value);
            valueSelect.multiple = multipleOperators.has(condition.operator ?? 'Is');

            for (const item of source.choices) {
                valueSelect.append(option(item.id, item.text, condition.values.includes(item.id)));
            }

            if (!valueSelect.multiple && condition.values.length === 0 && source.choices.length > 0) {
                condition.values = [source.choices[0].id];
            }

            valueSelect.addEventListener('change', () => {
                condition.values = Array.from(valueSelect.selectedOptions).map(item => item.value);
                changed();
            });
            row.append(valueSelect);
        } else {
            const count = operator?.twoValues ? 2 : 1;

            for (let i = 0; i < count; i++) {
                const input = /** @type {HTMLInputElement} */ (element('input', 'form-control'));

                input.setAttribute('aria-label', texts.value);
                input.value = condition.values[i] ?? '';
                input.addEventListener('input', () => {
                    const values = condition.values ?? [];

                    values[i] = input.value;
                    condition.values = values;
                    changed();
                });
                row.append(input);
            }
        }
    }

    const remove = /** @type {HTMLButtonElement} */ (element('button', 'btn btn-outline-danger'));

    remove.type = 'button';
    remove.setAttribute('aria-label', texts.remove);
    remove.innerHTML = '<i class="fa-solid fa-xmark" aria-hidden="true"></i>';
    remove.addEventListener('click', removed);
    row.append(remove);

    return row;
}

/**
 * Wires the rule builders under a root.
 * @param {ParentNode} root Where to look.
 * @param {Context} context The operators and texts.
 */
export function initRuleBuilders(root, context) {
    root.querySelectorAll('[data-rule-builder]').forEach(node => {
        const builder = /** @type {HTMLElement} */ (node);

        if (builder.dataset.ruleReady) {
            return;
        }

        const field = builder.closest('[data-rule-field]');
        const input = /** @type {HTMLInputElement | null} */ (field?.querySelector('[data-rule-value]') ?? null);
        const sourcesHolder = /** @type {HTMLElement | null} */ (builder.closest('[data-rule-sources]'));

        if (!input) {
            return;
        }

        builder.dataset.ruleReady = 'true';

        /** @type {Source[]} */
        let sources;

        try {
            sources = JSON.parse(sourcesHolder?.dataset.ruleSources || '[]');
        } catch {
            sources = [];
        }

        const rule = parseRule(input.value);

        renderRule(builder, rule, sources, context, () => {
            input.value = writeRule(rule);
            input.dispatchEvent(new Event('change', { bubbles: true }));
        }, 0);
    });
}
