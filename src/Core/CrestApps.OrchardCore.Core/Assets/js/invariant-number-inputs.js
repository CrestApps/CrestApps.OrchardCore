// A number input posts its value with a dot ("12.50") in every language, while ASP.NET Core reads a posted value in the
// request culture, so a decimal-comma culture such as de-DE reads "12.50" as 1250 and fr-FR doesn't read it at all. The
// framework reads a field invariantly when an `__Invariant` field names it. This names every number input of a form as the
// form is sent, so the rows a script adds, copies or renumbers are named too. AddInvariantNumberInputs() writes it on
// every page.
window.invariantNumberInputs = (function () {
    'use strict';

    const fieldName = '__Invariant';

    const isNumber = (field) => field.type === 'number' || field.type === 'range';

    const isDisabled = (field) => field.disabled || (typeof field.matches === 'function' && field.matches(':disabled'));

    // The names of the number inputs among `fields` that post a value and that `named` doesn't hold yet.
    const namesToAdd = (fields, named) => {
        const seen = new Set(named);
        const names = [];

        for (const field of fields) {
            if (isNumber(field) && field.name && !isDisabled(field) && !seen.has(field.name)) {
                seen.add(field.name);
                names.push(field.name);
            }
        }

        return names;
    };

    // A form sent with method="get" puts its values in the query string, which the framework always reads invariantly,
    // so it is left as it is. `form.elements` holds the inputs outside the form that join it with a form attribute too.
    const onFormData = (event) => {
        const form = event.target;

        if (!form || !form.elements || (form.getAttribute('method') || '').toLowerCase() === 'get') {
            return;
        }

        for (const name of namesToAdd(form.elements, event.formData.getAll(fieldName))) {
            event.formData.append(fieldName, name);
        }
    };

    // An asp-for input is followed by its own hidden __Invariant field. In a Bootstrap input group that field becomes the
    // group's last child, so the input loses its rounded end. Moving the field just after the group keeps it in the form.
    const moveFieldsOutOfInputGroups = (root) => {
        for (const field of root.querySelectorAll('.input-group > input[type="hidden"][name="' + fieldName + '"]')) {
            field.parentElement.after(field);
        }
    };

    // The script is written at the foot of the page, after the content it reads.
    moveFieldsOutOfInputGroups(document);

    // The formdata event fires on every submit, form.submit() included, and on new FormData(form). It doesn't bubble, so
    // it is caught on its way down to the form.
    document.addEventListener('formdata', onFormData, true);

    return { fieldName, namesToAdd, onFormData, moveFieldsOutOfInputGroups };
})();
