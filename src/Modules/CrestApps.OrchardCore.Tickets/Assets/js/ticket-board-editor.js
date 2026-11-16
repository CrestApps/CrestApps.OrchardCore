// @ts-check
/*
 * The columns of the ticket board editor.
 *
 * Columns are added, removed, and reordered by dragging their handle or with the up and down buttons. Each row posts
 * its fields as Columns[n], so the rows are numbered again, in the order they stand, after every change and before
 * the form is sent. A status is in one column only: once it is ticked in a column, the other columns show it as taken.
 * A column's default status can only be one of its ticked statuses.
 */
(function () {
    'use strict';

    // Gives a field name, id or label target the row number n: Columns[3].Name, Columns_3__Name...
    function renumber(value, prefixName, prefixId, index) {
        if (!value) {
            return value;
        }

        const escape = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
        const byName = new RegExp(`^${escape(prefixName)}\\[(?:\\d+|__index__)\\]`);
        const byId = new RegExp(`^${escape(prefixId)}_(?:\\d+|__index__)(?=_|$)`);

        return value.replace(byName, `${prefixName}[${index}]`).replace(byId, `${prefixId}_${index}`);
    }

    // For each status, the first column (by position) that has it ticked.
    function takenStatuses(columns) {
        const taken = {};

        columns.forEach((column, index) => {
            for (const statusId of column.statusIds) {
                if (!(statusId in taken)) {
                    taken[statusId] = index;
                }
            }
        });

        return taken;
    }

    // The default a column can keep: its current one while still ticked, otherwise "ask" (empty).
    function keptDefault(current, statusIds) {
        return current && statusIds.includes(current) ? current : '';
    }

    // Moves the item at "from" to "to" and returns the new array.
    function move(items, from, to) {
        const copy = items.slice();

        if (from < 0 || from >= copy.length || to < 0 || to >= copy.length || from === to) {
            return copy;
        }

        const [item] = copy.splice(from, 1);
        copy.splice(to, 0, item);

        return copy;
    }

    globalThis.CrestAppsTicketBoardEditor = { renumber, takenStatuses, keptDefault, move };

    if (typeof document === 'undefined' || !document.querySelectorAll) {
        return;
    }

    function setUp(list) {
        const prefixName = list.dataset.columnsName;
        const prefixId = list.dataset.columnsId;
        const fieldset = list.closest('fieldset');
        const template = /** @type {HTMLTemplateElement} */ (fieldset.querySelector('template[data-board-column-template]'));
        const addButton = fieldset.querySelector('[data-board-add-column]');
        const form = list.closest('form');

        const rows = () => Array.from(list.querySelectorAll(':scope > [data-board-column]'));

        const refresh = () => {
            const all = rows();

            all.forEach((row, index) => {
                for (const field of row.querySelectorAll('[name], [id], label[for]')) {
                    for (const attribute of ['name', 'id', 'for']) {
                        if (field.hasAttribute(attribute)) {
                            field.setAttribute(attribute, renumber(field.getAttribute(attribute), prefixName, prefixId, index));
                        }
                    }
                }

                const up = /** @type {HTMLButtonElement} */ (row.querySelector('[data-board-move="-1"]'));
                const down = /** @type {HTMLButtonElement} */ (row.querySelector('[data-board-move="1"]'));

                up.disabled = index === 0;
                down.disabled = index === all.length - 1;
            });

            const columns = all.map((row) => ({
                name: /** @type {HTMLInputElement} */ (row.querySelector('[data-board-column-name]')).value.trim(),
                statusIds: Array.from(row.querySelectorAll('[data-board-status]:checked')).map((box) => /** @type {HTMLInputElement} */ (box).value),
            }));
            const taken = takenStatuses(columns);

            all.forEach((row, index) => {
                const ticked = columns[index].statusIds;

                for (const box of /** @type {NodeListOf<HTMLInputElement>} */ (row.querySelectorAll('[data-board-status]'))) {
                    const owner = taken[box.value];
                    const elsewhere = owner !== undefined && owner !== index && !box.checked;
                    const option = box.closest('[data-board-status-option]');

                    box.disabled = elsewhere;
                    option.classList.toggle('text-muted', elsewhere);
                    option.title = elsewhere ? (columns[owner].name || '') : '';
                }

                const select = /** @type {HTMLSelectElement} */ (row.querySelector('[data-board-default]'));

                for (const option of Array.from(select.options)) {
                    option.hidden = option.value !== '' && !ticked.includes(option.value);
                }

                select.value = keptDefault(select.value, ticked);
                row.querySelector('[data-board-default-row]').classList.toggle('d-none', ticked.length < 2);
            });
        };

        const moveRow = (row, step) => {
            const all = rows();
            const from = all.indexOf(row);
            const to = from + step;

            if (to < 0 || to >= all.length) {
                return;
            }

            if (step < 0) {
                list.insertBefore(row, all[to]);
            } else {
                list.insertBefore(row, all[to].nextSibling);
            }

            refresh();
            /** @type {HTMLElement} */ (row.querySelector(`[data-board-move="${step}"]`)).focus();
        };

        list.addEventListener('click', (event) => {
            const target = /** @type {HTMLElement} */ (event.target);
            const row = target.closest('[data-board-column]');

            if (!row) {
                return;
            }

            const moveButton = target.closest('[data-board-move]');

            if (moveButton) {
                moveRow(row, Number(moveButton.getAttribute('data-board-move')));

                return;
            }

            if (target.closest('[data-board-remove]')) {
                row.remove();
                refresh();
            }
        });

        list.addEventListener('change', refresh);
        list.addEventListener('input', (event) => {
            if (/** @type {HTMLElement} */ (event.target).matches('[data-board-column-name]')) {
                refresh();
            }
        });

        addButton.addEventListener('click', () => {
            const fragment = /** @type {DocumentFragment} */ (template.content.cloneNode(true));
            const row = fragment.querySelector('[data-board-column]');

            list.append(fragment);
            refresh();
            /** @type {HTMLElement} */ (row.querySelector('[data-board-column-name]')).focus();
        });

        if (typeof globalThis.Sortable !== 'undefined') {
            globalThis.Sortable.create(list, {
                handle: '[data-board-handle]',
                draggable: '[data-board-column]',
                animation: 150,
                onEnd: refresh,
            });
        }

        // Disabled boxes aren't posted, and a taken status is never ticked, so nothing is lost by sending as is.
        form.addEventListener('submit', refresh);

        // The team picker shows only when the board is shared with a team.
        const sharing = form.querySelector('[data-board-sharing]');
        const team = form.querySelector('[data-board-team]');

        if (sharing && team) {
            // A team share needs its team.
            const showTeam = () => {
                const chosen = /** @type {HTMLInputElement} */ (sharing.querySelector('input[type="radio"]:checked'));
                const byTeam = Boolean(chosen) && chosen.value === 'Team';
                const picker = /** @type {HTMLSelectElement} */ (team.querySelector('select'));

                team.classList.toggle('d-none', !byTeam);

                if (picker) {
                    picker.required = byTeam;
                }
            };

            sharing.addEventListener('change', showTeam);
            showTeam();
        }

        refresh();
    }

    function start() {
        document.querySelectorAll('[data-board-editor-columns]').forEach((list) => setUp(list));
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})();
