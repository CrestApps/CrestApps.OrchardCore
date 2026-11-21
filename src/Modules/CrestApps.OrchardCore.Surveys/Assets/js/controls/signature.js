// Lets a respondent sign on a canvas with a finger, a pen or the mouse, and keeps the drawing as a PNG data URL in the
// question's hidden input.
import { notifyChange } from './survey-change.js';

export function initSignatures(root) {
    for (const control of root.querySelectorAll('[data-survey-signature]')) {
        const canvas = control.querySelector('canvas');
        const input = control.querySelector('input[type="hidden"]');
        const clear = control.querySelector('[data-signature-clear]');

        if (!canvas || !input || control.dataset.readonly === 'true') {
            continue;
        }

        control.querySelector('[data-signature-pad]').hidden = false;

        const context = canvas.getContext('2d');
        const ratio = window.devicePixelRatio || 1;
        let drawing = false;
        let drawn = false;

        const resize = () => {
            const width = canvas.clientWidth;
            const height = canvas.clientHeight;
            canvas.width = width * ratio;
            canvas.height = height * ratio;
            context.scale(ratio, ratio);
            context.lineWidth = 2;
            context.lineCap = 'round';
            context.strokeStyle = getComputedStyle(control).color;
        };

        const point = (event) => {
            const rect = canvas.getBoundingClientRect();

            return { x: event.clientX - rect.left, y: event.clientY - rect.top };
        };

        canvas.addEventListener('pointerdown', (event) => {
            drawing = true;
            canvas.setPointerCapture(event.pointerId);
            const { x, y } = point(event);
            context.beginPath();
            context.moveTo(x, y);
        });

        canvas.addEventListener('pointermove', (event) => {
            if (!drawing) {
                return;
            }

            const { x, y } = point(event);
            context.lineTo(x, y);
            context.stroke();
            drawn = true;
        });

        const finish = () => {
            if (!drawing) {
                return;
            }

            drawing = false;

            if (drawn) {
                input.value = canvas.toDataURL('image/png');
                notifyChange(control, control.dataset.questionId);
            }
        };

        canvas.addEventListener('pointerup', finish);
        canvas.addEventListener('pointercancel', finish);

        clear?.addEventListener('click', () => {
            context.clearRect(0, 0, canvas.width, canvas.height);
            input.value = '';
            drawn = false;
            notifyChange(control, control.dataset.questionId);
        });

        resize();
    }
}
