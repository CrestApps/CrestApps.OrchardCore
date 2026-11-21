// Turns the rank drop-downs of a ranking question into a list ordered with Move up and Move down buttons. The list
// posts the item ids in order under the question's prefix and disables the drop-downs, which stay the way to answer
// without script.
import { notifyChange } from './survey-change.js';

function renumber(list) {
    list.querySelectorAll('[data-ranking-position]').forEach((position, index) => {
        position.textContent = String(index + 1);
    });
}

function move(list, item, offset) {
    const sibling = offset < 0 ? item.previousElementSibling : item.nextElementSibling;

    if (!sibling) {
        return;
    }

    if (offset < 0) {
        list.insertBefore(item, sibling);
    } else {
        list.insertBefore(sibling, item);
    }

    renumber(list);
    item.querySelector(offset < 0 ? '[data-ranking-up]' : '[data-ranking-down]')?.focus();
}

export function initRanking(root) {
    for (const control of root.querySelectorAll('[data-survey-ranking]')) {
        const list = control.querySelector('[data-ranking-list]');
        const selects = control.querySelector('[data-ranking-selects]');

        if (!list || !selects || control.dataset.readonly === 'true') {
            continue;
        }

        selects.hidden = true;
        selects.querySelectorAll('select').forEach((select) => {
            select.disabled = true;
        });
        list.hidden = false;
        list.querySelectorAll('input[type="hidden"]').forEach((input) => {
            input.disabled = false;
        });

        list.addEventListener('click', (event) => {
            const button = event.target.closest('[data-ranking-up], [data-ranking-down]');

            if (!button) {
                return;
            }

            move(list, button.closest('li'), button.hasAttribute('data-ranking-up') ? -1 : 1);
            notifyChange(control, control.dataset.questionId);
        });

        renumber(list);
    }
}
