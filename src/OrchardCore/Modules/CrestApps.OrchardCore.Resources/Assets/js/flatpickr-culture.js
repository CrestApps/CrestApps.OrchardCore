flatpickrCulture = function () {

    const csharpDatePatternToFlatpickr = (pattern) => {
        if (!pattern || typeof pattern !== "string") {
            return pattern;
        }

        // Order matters: longer tokens first
        return pattern
            // Year
            .replace(/yyyy/g, "Y")
            .replace(/yyy/g, "Y")
            .replace(/yy/g, "y")

            // Month
            .replace(/MMMM/g, "F")
            .replace(/MMM/g, "M")
            .replace(/MM/g, "m")
            .replace(/M/g, "n")

            // Day
            .replace(/dddd/g, "l")
            .replace(/ddd/g, "D")
            .replace(/dd/g, "d")
            .replace(/d/g, "j");
    };

    const csharpTimePatternToFlatpickr = (pattern) => {
        if (!pattern || typeof pattern !== "string") {
            return pattern;
        }

        // flatpickr uses 24hr tokens (H) and 12hr tokens (h + K)
        // We'll detect 12-hour patterns by the presence of 't' (AM/PM designator).
        const is12Hour = /t+/i.test(pattern);

        let p = pattern;

        // Seconds (ignored by our UIs; keep mapping for completeness)
        p = p.replace(/ss/g, "S").replace(/s/g, "S");

        // Minutes
        p = p.replace(/mm/g, "i").replace(/m/g, "i");

        // Hours
        if (is12Hour) {
            p = p.replace(/hh/g, "h");
            p = p.replace(/HH/g, "h").replace(/H/g, "h");
            p = p.replace(/tt/g, "K").replace(/t/g, "K");
        } else {
            p = p.replace(/HH/g, "H");
            p = p.replace(/hh/g, "H").replace(/h/g, "H");
            p = p.replace(/tt/g, "").replace(/t/g, "");
        }

        return p.trim();
    };

    const createFlatpickrDateTimePattern = (csharpShortDatePattern, csharpShortTimePattern, defaultDatePattern, defaultTimePattern) => {
        const datePattern = csharpDatePatternToFlatpickr(csharpShortDatePattern) || defaultDatePattern || "Y-m-d";
        const timePattern = csharpTimePatternToFlatpickr(csharpShortTimePattern) || defaultTimePattern || "H:i";
        return {
            datePattern,
            timePattern,
            dateTimePattern: (datePattern + " " + timePattern).trim(),
            time24hr: !/K/.test(timePattern)
        };
    };

    return {
        csharpDatePatternToFlatpickr,
        csharpTimePatternToFlatpickr,
        createFlatpickrDateTimePattern
    };
}();
