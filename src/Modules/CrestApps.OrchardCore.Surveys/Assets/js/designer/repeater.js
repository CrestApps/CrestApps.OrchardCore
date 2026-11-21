// @ts-check
/*
 * Lists of rows a form edits (choices, rows, endings, branches...). A row is added from the list's template, where
 * __index__ stands for its position, and removed with its button. The inputs of the rows are renumbered after every
 * change, so the posted indexes run from 0 without gaps, as model binding expects. Rows of a sortable list can be
 * dragged to a new place.
 */

/**
 * @param {string} value The text to escape.
 * @returns {string} The text, safe inside a regular expression.
 */
function escapeRegExp(value) {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/**
 * Renumbers the names, ids and labels of the rows of a list.
 * @param {HTMLElement} repeater The list.
 */
export function renumber(repeater) {
    const prefixes = [repeater.dataset.repeaterPrefix, repeater.dataset.repeaterAlso].filter(Boolean);
    const items = repeater.querySelectorAll(':scope > [data-repeater-items] > [data-repeater-item]');

    items.forEach((item, index) => {
        for (const prefix of prefixes) {
            const pattern = new RegExp(escapeRegExp(String(prefix)) + '\\[(\\d+|__index__)\\]', 'g');
            const idPattern = new RegExp(escapeRegExp(String(prefix).replace(/[.[\]]/g, '_')) + '_(\\d+|__index__)__', 'g');

            item.querySelectorAll('[name], [id], [for]').forEach(element => {
                for (const attribute of ['name', 'id', 'for']) {
                    const value = element.getAttribute(attribute);

                    if (value) {
                        element.setAttribute(attribute, value.replace(pattern, `${prefix}[${index}]`).replace(idPattern, `${String(prefix).replace(/[.[\]]/g, '_')}_${index}__`));
                    }
                }
            });
        }
    });
}

/**
 * Makes the rows of a sortable list draggable.
 * @param {HTMLElement} list The list of rows.
 * @param {HTMLElement} repeater The repeater it belongs to.
 * @param {(root: HTMLElement) => void} onChange Called after a row moved.
 */
function makeSortable(list, repeater, onChange) {
    /** @type {HTMLElement | null} */
    let dragged = null;

    list.addEventListener('dragstart', event => {
        const item = /** @type {HTMLElement | null} */ (/** @type {HTMLElement} */ (event.target).closest('[data-repeater-item]'));

        if (!item || item.parentElement !== list) {
            return;
        }

        dragged = item;
        item.classList.add('opacity-50');
        event.dataTransfer?.setData('text/plain', '');
    });

    list.addEventListener('dragover', event => {
        if (!dragged) {
            return;
        }

        event.preventDefault();

        const over = /** @type {HTMLElement | null} */ (/** @type {HTMLElement} */ (event.target).closest('[data-repeater-item]'));

        if (over && over !== dragged && over.parentElement === list) {
            const box = over.getBoundingClientRect();
            const after = event.clientY > box.top + box.height / 2;

            list.insertBefore(dragged, after ? over.nextSibling : over);
        }
    });

    list.addEventListener('dragend', () => {
        if (dragged) {
            dragged.classList.remove('opacity-50');
            dragged = null;
            renumber(repeater);
            onChange(repeater);
        }
    });

    list.querySelectorAll(':scope > [data-repeater-item]').forEach(item => item.setAttribute('draggable', 'true'));
}

/**
 * Wires the lists under a root.
 * @param {ParentNode} root Where to look.
 * @param {(root: HTMLElement) => void} onAdd Called with each new row, to wire what it holds.
 */
export function initRepeaters(root, onAdd) {
    root.querySelectorAll('[data-repeater]').forEach(element => {
        const repeater = /** @type {HTMLElement} */ (element);

        if (repeater.dataset.repeaterReady) {
            return;
        }

        repeater.dataset.repeaterReady = 'true';

        const list = /** @type {HTMLElement | null} */ (repeater.querySelector(':scope > [data-repeater-items]'));
        const template = /** @type {HTMLTemplateElement | null} */ (repeater.querySelector(':scope > template[data-repeater-template]'));

        if (!list) {
            return;
        }

        if (list.hasAttribute('data-repeater-sortable')) {
            makeSortable(list, repeater, () => {});
        }

        repeater.addEventListener('click', event => {
            const target = /** @type {HTMLElement} */ (event.target);
            const add = target.closest('[data-repeater-add]');
            const remove = target.closest('[data-repeater-remove]');

            if (add && add.closest('[data-repeater]') === repeater && template) {
                event.preventDefault();

                const index = list.querySelectorAll(':scope > [data-repeater-item]').length;
                const html = template.innerHTML.replace(/__index__/g, String(index));
                const holder = document.createElement('div');

                holder.innerHTML = html.trim();

                const item = /** @type {HTMLElement | null} */ (holder.firstElementChild);

                if (item) {
                    if (list.hasAttribute('data-repeater-sortable')) {
                        item.setAttribute('draggable', 'true');
                    }

                    list.appendChild(item);
                    renumber(repeater);
                    onAdd(item);

                    const first = /** @type {HTMLElement | null} */ (item.querySelector('input:not([type=hidden]), select, textarea'));

                    first?.focus();
                }
            } else if (remove && remove.closest('[data-repeater]') === repeater) {
                event.preventDefault();
                remove.closest('[data-repeater-item]')?.remove();
                renumber(repeater);
            }
        });
    });
}
