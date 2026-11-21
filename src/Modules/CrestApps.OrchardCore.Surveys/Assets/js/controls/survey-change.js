// Tells the survey runner that a control changed its inputs, so it can re-evaluate logic and autosave.
export function notifyChange(element, questionId) {
    element.dispatchEvent(new CustomEvent('survey:change', { bubbles: true, detail: { questionId } }));
}
