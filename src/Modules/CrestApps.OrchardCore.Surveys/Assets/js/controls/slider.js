// Shows the value of a slider as it moves, and marks the question answered the first time the slider moves.
import { notifyChange } from './survey-change.js';

export function initSliders(root) {
    for (const control of root.querySelectorAll('[data-survey-slider]')) {
        const range = control.querySelector('input[type="range"]');
        const output = control.querySelector('output');
        const set = control.querySelector('[data-slider-set]');

        if (!range) {
            continue;
        }

        const update = () => {
            if (output) {
                output.value = range.value;
            }
        };

        range.addEventListener('input', () => {
            update();

            if (set && !set.checked) {
                set.checked = true;
            }
        });

        range.addEventListener('change', () => notifyChange(control, control.dataset.questionId));
        set?.addEventListener('change', () => notifyChange(control, control.dataset.questionId));
        update();
    }
}
