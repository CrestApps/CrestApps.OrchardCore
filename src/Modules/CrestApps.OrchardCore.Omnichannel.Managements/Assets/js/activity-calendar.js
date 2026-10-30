// @ts-check
// Adds "Add to my calendar" to each follow-up activity on the completion page. Ticking it shows a button that opens the
// agent's calendar, so they can see when they're free and choose the follow-up's time there; the time goes into the
// follow-up's "Schedule at" field, and completing the activity places the follow-up on their calendar.

(function () {
    document.addEventListener('omnichannel:followup-card', (event) => {
        const settings = /** @type {HTMLElement | null} */ (document.querySelector('[data-activity-calendar]'));
        const detail = /** @type {CustomEvent} */ (event).detail || {};
        const dateInput = /** @type {HTMLInputElement | null} */ (detail.dateInput);
        const action = detail.action || {};

        if (!settings || !dateInput || !action.actionId || !detail.cardBody) {
            return;
        }

        const data = settings.dataset;
        const id = `calendar-${action.actionId}`;

        const check = document.createElement('div');
        const box = document.createElement('input');
        const label = document.createElement('label');
        const hint = document.createElement('div');

        check.className = 'form-check mt-2';
        box.type = 'checkbox';
        box.className = 'form-check-input';
        box.id = id;
        box.name = `ActionAddToCalendar[${action.actionId}]`;
        box.value = 'true';
        label.className = 'form-check-label';
        label.htmlFor = id;
        label.textContent = data.addText || '';
        hint.className = 'form-text';
        hint.textContent = data.hintText || '';
        check.append(box, label, hint);

        const choose = document.createElement('div');
        const button = document.createElement('button');
        const summary = document.createElement('span');

        choose.className = 'mt-2 d-none d-flex flex-wrap align-items-center gap-2';
        button.type = 'button';
        button.className = 'btn btn-outline-primary btn-sm';
        button.textContent = data.chooseText || '';
        summary.className = 'small text-body-secondary';
        choose.append(button, summary);

        box.addEventListener('change', () => choose.classList.toggle('d-none', !box.checked));

        button.addEventListener('click', async () => {
            // @ts-ignore The calendar picker and flatpickr are loaded by the page.
            const picker = window.CrestAppsCalendarPicker;
            // @ts-ignore
            const flatpickrInstance = dateInput._flatpickr;

            if (!picker) {
                return;
            }

            const current = flatpickrInstance && flatpickrInstance.selectedDates.length > 0 ? flatpickrInstance.selectedDates[0] : null;
            const chosen = await picker.open({ date: current, title: data.pickerTitleText });

            if (!chosen) {
                return;
            }

            if (flatpickrInstance) {
                flatpickrInstance.setDate(chosen.local, true);
            }

            const shown = flatpickrInstance && flatpickrInstance.altInput ? flatpickrInstance.altInput.value : dateInput.value;

            summary.textContent = (data.chosenText || '').replace('{0}', shown);
        });

        detail.cardBody.append(check, choose);
    });
})();
