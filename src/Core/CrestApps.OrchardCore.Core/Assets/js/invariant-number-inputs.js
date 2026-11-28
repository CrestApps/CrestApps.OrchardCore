// A number, date or time input posts its value in an invariant format ("12.50", "2026-11-27") in every language, while
// ASP.NET Core reads a posted value in the request culture, so a decimal-comma culture such as de-DE reads "12.50" as
// 1250, fr-FR doesn't read it at all, and a culture with another calendar, such as th-TH, reads the wrong year. The
// framework reads a field invariantly when an `__Invariant` field names it. This names every such input of a form as the
// form is sent, so the rows a script adds, copies or renumbers are named too. AddInvariantNumberInputs() writes it on
// every page; the server leaves the comment lines out.
window.invariantNumberInputs = (function () {
    'use strict';

    const fieldName = '__Invariant';

    // The input types the browser posts in an invariant format, the same ones the framework marks for an asp-for input.
    const invariantTypes = new Set(['number', 'range', 'date', 'datetime-local', 'time', 'month', 'week']);

    const isDisabled = (field) => field.disabled || (typeof field.matches === 'function' && field.matches(':disabled'));

    // The names of the inputs among `fields` that post an invariant value and that `named` doesn't hold yet.
    const namesToAdd = (fields, named) => {
        const seen = new Set(named);
        const names = [];

        for (const field of fields) {
            if (invariantTypes.has(field.type) && field.name && !isDisabled(field) && !seen.has(field.name)) {
                seen.add(field.name);
                names.push(field.name);
            }
        }

        return names;
    };

    // The last submit event, so the formdata event that follows knows the method the form is sent with, a submit button's
    // formmethod included.
    let lastSubmit = null;

    const onSubmit = (event) => {
        lastSubmit = event;
    };

    // Whether the values are going to the query string, which the framework always reads invariantly, so the fields would
    // only add noise to the address. When the browser itself sends the form (the submit event has finished and wasn't
    // cancelled), the method is the button's formmethod, else the form's, GET by default. A script that reads
    // new FormData(form), in a submit handler or anywhere else, may post it, so then only an explicit method="get" counts.
    const isSentWithGet = (form) => {
        const submit = lastSubmit;

        lastSubmit = null;

        const explicit = (form.getAttribute('method') || '').toLowerCase();

        if (submit && submit.target === form && submit.eventPhase === 0 && !submit.defaultPrevented) {
            const button = submit.submitter;
            const method = (button && button.getAttribute('formmethod')) || explicit || 'get';

            return method.toLowerCase() === 'get';
        }

        return explicit === 'get';
    };

    // `form.elements` holds the inputs outside the form that join it with a form attribute too.
    const onFormData = (event) => {
        const form = event.target;

        if (!form || !form.elements || isSentWithGet(form)) {
            return;
        }

        for (const name of namesToAdd(form.elements, event.formData.getAll(fieldName))) {
            event.formData.append(fieldName, name);
        }
    };

    // An asp-for input is followed by its own hidden __Invariant field. In a Bootstrap input group that field becomes the
    // group's last child, so the input loses its rounded end. Moving the field just after the group keeps it in the form,
    // unless the group is the form itself.
    const moveFieldsOutOfInputGroups = (root) => {
        for (const field of root.querySelectorAll('.input-group > input[type="hidden"][name="' + fieldName + '"]')) {
            const group = field.parentElement;

            if (group.tagName !== 'FORM') {
                group.after(field);
            }
        }
    };

    // The script is written at the foot of the page, after the content it reads.
    moveFieldsOutOfInputGroups(document);

    // Both events are caught on their way down to the form: formdata doesn't bubble, and a submit handler may stop it. The
    // formdata event fires on every submit, form.submit() included, and on new FormData(form).
    document.addEventListener('submit', onSubmit, true);
    document.addEventListener('formdata', onFormData, true);

    return { fieldName, namesToAdd, onSubmit, onFormData, moveFieldsOutOfInputGroups };
})();
