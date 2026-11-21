// The order of a quiz's fixed questions in the quiz editor. The question picker (an ItemSelector multiple select)
// posts its chosen options in the order they stand in the <select>, so this script lists the chosen questions with up
// and down buttons and moves their <option> elements to match: what is posted is the order shown.
//
// Markup (QuizPart.Edit.cshtml): an element with data-question-order holding a list ([data-question-order-list]), an
// empty note ([data-question-order-empty]) and a <template data-question-order-template> of one row with
// [data-question-order-title], [data-question-order-up] and [data-question-order-down]. Its data-select is the id of
// the picker's <select>.

import { mergeOrder, move } from './quiz-question-order-helpers.js';

function chosen(select) {
    return [...select.options].filter(option => option.selected && option.value).map(option => option.value);
}

function apply(select, order) {
    const byValue = new Map([...select.options].map(option => [option.value, option]));
    let anchor = null;

    // The chosen options go first, in the order set; the others keep their place after them.
    for (const id of order) {
        const option = byValue.get(id);

        if (!option) {
            continue;
        }

        if (anchor) {
            anchor.after(option);
        } else {
            select.prepend(option);
        }

        anchor = option;
    }

    if (window.Selectpicker) {
        window.Selectpicker.getOrCreateInstance(select)?.refresh();
    }
}

function init(root) {
    const select = document.getElementById(root.dataset.select);
    const list = root.querySelector('[data-question-order-list]');
    const empty = root.querySelector('[data-question-order-empty]');
    const template = root.querySelector('[data-question-order-template]');

    if (!select || !list || !template) {
        return;
    }

    let order = chosen(select);

    const render = () => {
        list.replaceChildren();

        order.forEach((id, index) => {
            const row = template.content.firstElementChild.cloneNode(true);
            const option = [...select.options].find(candidate => candidate.value === id);

            row.querySelector('[data-question-order-title]').textContent = `${index + 1}. ${option?.textContent?.trim() ?? id}`;

            const up = row.querySelector('[data-question-order-up]');
            const down = row.querySelector('[data-question-order-down]');

            up.disabled = index === 0;
            down.disabled = index === order.length - 1;
            up.addEventListener('click', () => update(move(order, index, -1), index - 1, 'up'));
            down.addEventListener('click', () => update(move(order, index, 1), index + 1, 'down'));
            list.append(row);
        });

        if (empty) {
            empty.hidden = order.length > 0;
        }
    };

    const update = (next, focusIndex, button) => {
        order = next;
        apply(select, order);
        render();

        // Keep the keyboard on the question that moved.
        const row = list.children[focusIndex];
        const target = row?.querySelector(`[data-question-order-${button}]`);

        (target && !target.disabled ? target : row?.querySelector('button:not([disabled])'))?.focus();
    };

    select.addEventListener('changed.bs.select', () => {
        order = mergeOrder(order, chosen(select));
        apply(select, order);
        render();
    });
    select.addEventListener('change', () => {
        order = mergeOrder(order, chosen(select));
        render();
    });

    render();
}

document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-question-order]').forEach(init);
});
