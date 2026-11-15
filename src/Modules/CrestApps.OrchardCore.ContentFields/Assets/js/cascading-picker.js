// @ts-check

/**
 * Turns the indented list of the TaxonomyCascadingPicker shape into one list per level: Category, then Sub-category,
 * each filled from the choice above it. The original list stays in the form, hidden, and holds the answer, so the
 * form posts the same field and other scripts (conditional fields) see its change events.
 */

/**
 * @typedef {{ id: string, text: string, parentId: string, selectable: boolean, description: string }} Item
 */

/** @param {HTMLSelectElement} source @returns {Item[]} */
function readItems(source) {
    return Array.from(source.options)
        .filter((option) => option.value !== '')
        .map((option) => ({
            id: option.value,
            text: (option.textContent ?? '').replace(/^\u00a0+/, ''),
            parentId: option.dataset.parent ?? '',
            selectable: option.dataset.selectable !== 'false',
            description: option.dataset.description ?? '',
        }));
}

/**
 * The ids from the top down to an item.
 * @param {Item[]} items
 * @param {string} id
 * @returns {string[]}
 */
function pathTo(items, id) {
    const byId = new Map(items.map((item) => [item.id, item]));
    const path = [];
    let current = byId.get(id);

    while (current) {
        path.unshift(current.id);
        current = current.parentId ? byId.get(current.parentId) : undefined;
    }

    return path;
}

/** @param {HTMLSelectElement} source */
function start(source) {
    if (source.dataset.cascadingPickerStarted === '1') {
        return;
    }

    source.dataset.cascadingPickerStarted = '1';

    const items = readItems(source);
    const container = source.closest('[data-cascading-picker-container]') ?? source.parentElement;
    const description = container?.querySelector('[data-cascading-picker-description]');
    const labels = (source.dataset.levelLabels ?? '').split('|').filter((label) => label !== '');
    const emptyText = source.options.length > 0 ? source.options[0].textContent ?? '' : '';
    const levels = document.createElement('div');

    levels.className = 'd-flex flex-wrap gap-2';
    source.hidden = true;

    // A hidden list the browser must check can't show its message; the server checks the answer.
    source.required = false;
    source.insertAdjacentElement('afterend', levels);

    /** @param {string[]} chosen ids from the top down */
    const render = (chosen) => {
        levels.replaceChildren();

        let parentId = '';

        for (let level = 0; ; level++) {
            const children = items.filter((item) => item.parentId === parentId);

            if (children.length === 0) {
                break;
            }

            const select = document.createElement('select');
            select.className = 'form-select w-auto';
            select.setAttribute('aria-label', labels[level] ?? labels[labels.length - 1] ?? emptyText);

            const empty = document.createElement('option');
            empty.value = '';
            empty.textContent = labels[level] ? `${labels[level]}…` : emptyText;
            select.append(empty);

            for (const child of children) {
                const option = document.createElement('option');
                option.value = child.id;
                option.textContent = child.text;
                option.selected = child.id === chosen[level];
                select.append(option);
            }

            select.addEventListener('change', () => {
                const next = chosen.slice(0, level);

                if (select.value) {
                    next.push(select.value);
                }

                commit(next);
            });

            levels.append(select);

            if (!chosen[level]) {
                break;
            }

            parentId = chosen[level];
        }
    };

    /** @param {string[]} chosen */
    const commit = (chosen) => {
        const deepest = chosen.length > 0 ? items.find((item) => item.id === chosen[chosen.length - 1]) : undefined;
        const value = deepest && deepest.selectable ? deepest.id : '';

        render(chosen);

        if (description) {
            description.textContent = deepest?.description ?? '';
        }

        if (source.value !== value) {
            source.value = value;
            source.dispatchEvent(new Event('change', { bubbles: true }));
        }
    };

    const initial = source.value ? pathTo(items, source.value) : [];

    render(initial);

    if (description) {
        description.textContent = items.find((item) => item.id === source.value)?.description ?? '';
    }
}

function startAll() {
    document.querySelectorAll('select[data-cascading-picker]').forEach((element) => start(/** @type {HTMLSelectElement} */ (element)));
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', startAll);
} else {
    startAll();
}

globalThis.CrestAppsCascadingPicker = { pathTo, startAll };
