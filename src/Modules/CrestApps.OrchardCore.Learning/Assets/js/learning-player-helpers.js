// Pure helpers of the course player, kept apart so they can be tested without a page.

// "3 of 7 complete · 43%" from the localized template "{0} of {1} complete · {2}%".
export function progressText(template, completed, required, percent) {
    return template
        .replace('{0}', String(completed))
        .replace('{1}', String(required))
        .replace('{2}', String(percent));
}

// Counts the seconds a page is visible, so the time on an item is reported however the learner leaves it: each take()
// returns the visible seconds since the previous one; hiding the page takes what is left and stops counting until it
// shows again.
export function createActiveTimer(startedAt) {
    let last = startedAt;
    let visible = true;

    const take = at => {
        const seconds = visible ? Math.max(0, Math.round((at - last) / 1000)) : 0;

        last = at;

        return seconds;
    };

    return {
        take,
        hide(at) {
            const seconds = take(at);

            visible = false;

            return seconds;
        },
        show(at) {
            visible = true;
            last = at;
        },
    };
}
