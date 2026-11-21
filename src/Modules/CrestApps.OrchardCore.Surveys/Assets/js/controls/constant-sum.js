// Shows the running total of a constant sum question and whether it matches the required total.
export function initConstantSums(root) {
    for (const control of root.querySelectorAll('[data-survey-constant-sum]')) {
        const total = Number(control.dataset.total);
        const output = control.querySelector('[data-constant-sum-total]');
        const inputs = control.querySelectorAll('input[type="number"]');

        const update = () => {
            let sum = 0;

            inputs.forEach((input) => {
                const value = Number(input.value);
                sum += Number.isFinite(value) ? value : 0;
            });

            if (output) {
                output.value = String(Math.round(sum * 100) / 100);
                output.classList.toggle('text-danger', sum !== total);
                output.classList.toggle('text-success', sum === total);
            }
        };

        inputs.forEach((input) => input.addEventListener('input', update));
        update();
    }
}
