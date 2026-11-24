// @ts-check
// Drag into text questions in the quiz player. Each blank is a <select data-blank-choices> that posts the id of the
// word put in it, so the question works with the keyboard and without script. With script, the words of the question
// ([data-blank-word] chips with data-choice) can be dragged onto a blank, which only sets that list's value; the usual
// input event then saves the answer.

/**
 * The blank a word dropped on an element goes into: the list itself or the list the element is inside, when it offers
 * that word.
 * @param {any} element The element under the drop.
 * @param {string} choiceId The word.
 * @returns {any} The list, or null.
 */
export function dropTarget(element, choiceId) {
    if (!element) {
        return null;
    }

    const select = element.tagName === 'SELECT' ? element : element.closest?.('select[data-blank-choices]');

    if (!select || select.tagName !== 'SELECT') {
        return null;
    }

    return [...select.options].some(option => option.value === choiceId && choiceId !== '') ? select : null;
}

/**
 * The text of a word a list offers.
 * @param {any} select The list.
 * @param {string} choiceId The word.
 * @returns {string}
 */
export function choiceLabel(select, choiceId) {
    const option = [...select.options].find(candidate => candidate.value === choiceId);

    return option ? (option.textContent ?? '').trim() : '';
}

/**
 * Lets the words of the drag into text questions under a root be dragged onto their blanks.
 * @param {ParentNode} root The page or form.
 */
export function initBlanks(root) {
    root.querySelectorAll('[data-blank-word]').forEach(word => {
        const chip = /** @type {HTMLElement} */ (word);

        chip.setAttribute('draggable', 'true');
        chip.addEventListener('dragstart', event => {
            event.dataTransfer?.setData('text/plain', chip.dataset.choice ?? '');
            chip.classList.add('opacity-50');
        });
        chip.addEventListener('dragend', () => chip.classList.remove('opacity-50'));
    });

    root.querySelectorAll('select[data-blank-choices]').forEach(element => {
        const select = /** @type {HTMLSelectElement} */ (element);

        select.addEventListener('dragover', event => {
            event.preventDefault();
            select.classList.add('border-primary');
        });
        select.addEventListener('dragleave', () => select.classList.remove('border-primary'));
        select.addEventListener('drop', event => {
            event.preventDefault();
            select.classList.remove('border-primary');

            const choiceId = event.dataTransfer?.getData('text/plain') ?? '';
            const target = dropTarget(select, choiceId);

            if (!target) {
                return;
            }

            target.value = choiceId;
            target.title = choiceLabel(target, choiceId);
            target.dispatchEvent(new Event('input', { bubbles: true }));
            target.dispatchEvent(new Event('change', { bubbles: true }));
        });
    });
}
