// The editor of a color scheme (ColorSchemes/Edit.cshtml). Each color has a native picker and a hex field kept in
// step; the optional ones may be left empty. As the fields change, the editor asks the server for the CSS of the
// scheme being edited (the same derivation, contrast fixes included, as a saved scheme) and applies it to the
// previews.

const form = document.querySelector("[data-color-scheme-editor]");

if (form) {
    const previewUrl = form.dataset.previewUrl;
    const style = document.createElement("style");
    document.head.appendChild(style);

    const hexPattern = /^#[0-9a-f]{6}$/i;
    let timer = 0;
    let request = 0;

    const refresh = async () => {
        // Every field of the form but the anti-forgery token; an invalid one makes the server refuse the preview,
        // which then keeps showing the last valid scheme.
        const query = new URLSearchParams();

        for (const [name, value] of new FormData(form)) {
            if (name !== "__RequestVerificationToken") {
                query.append(name, value);
            }
        }

        const current = ++request;
        const response = await fetch(`${previewUrl}?${query}`, { credentials: "same-origin" });

        if (response.ok && current === request) {
            style.textContent = await response.text();
        }
    };

    const schedule = () => {
        window.clearTimeout(timer);
        timer = window.setTimeout(refresh, 200);
    };

    form.querySelectorAll("[data-color-picker]").forEach((picker) => {
        const hex = form.querySelector(`[data-color-hex="${picker.dataset.colorPicker}"]`);

        picker.addEventListener("input", () => {
            hex.value = picker.value;
            schedule();
        });

        hex.addEventListener("input", () => {
            if (hexPattern.test(hex.value.trim())) {
                picker.value = hex.value.trim().toLowerCase();
            }
        });
    });

    form.addEventListener("input", schedule);
    form.addEventListener("change", schedule);

    refresh();
}
