// Pure helpers of the course player, kept apart so they can be tested without a page.

// "3 of 7 complete · 43%" from the localized template "{0} of {1} complete · {2}%".
export function progressText(template, completed, required, percent) {
    return template
        .replace('{0}', String(completed))
        .replace('{1}', String(required))
        .replace('{2}', String(percent));
}
