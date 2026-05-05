flatpickrCulture = function () {
    const machineDateFormat = "Y-m-d";
    const machineDateTimeFormat = "Y-m-d\\TH:i";
    const directionMarksPattern = /[\u200e\u200f\u202a-\u202e\u2066-\u2069]/g;

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

    const stripDirectionMarks = (value) => {
        if (typeof value !== "string") {
            return value;
        }

        return value.replace(directionMarksPattern, "").trim();
    };

    const pad = (value) => value.toString().padStart(2, "0");

    const formatMachineDate = (date) => {
        if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
            return "";
        }

        return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
    };

    const formatMachineDateTime = (date) => {
        if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
            return "";
        }

        return `${formatMachineDate(date)}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
    };

    const parseMachineDate = (value) => {
        const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(stripDirectionMarks(value));

        if (!match) {
            return null;
        }

        return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
    };

    const parseMachineDateTime = (value) => {
        const match = /^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})(?::(\d{2}))?$/.exec(stripDirectionMarks(value));

        if (!match) {
            return null;
        }

        return new Date(
            Number(match[1]),
            Number(match[2]) - 1,
            Number(match[3]),
            Number(match[4]),
            Number(match[5]),
            match[6] ? Number(match[6]) : 0
        );
    };

    const createParseDate = (displayFormat, enableTime) => (value, format) => {
        const sanitizedValue = stripDirectionMarks(value);

        if (!sanitizedValue) {
            return undefined;
        }

        const machineDate = parseMachineDate(sanitizedValue);
        if (machineDate) {
            return machineDate;
        }

        const machineDateTime = parseMachineDateTime(sanitizedValue);
        if (machineDateTime) {
            return machineDateTime;
        }

        if (displayFormat && typeof flatpickr !== "undefined") {
            const localizedDate = flatpickr.parseDate(sanitizedValue, displayFormat);
            if (localizedDate) {
                return localizedDate;
            }
        }

        if (typeof flatpickr !== "undefined") {
            return flatpickr.parseDate(sanitizedValue, format);
        }

        return enableTime ? parseMachineDateTime(sanitizedValue) : parseMachineDate(sanitizedValue);
    };

    const createFormatDate = () => (date, format) => {
        if (format === machineDateFormat) {
            return formatMachineDate(date);
        }

        if (format === machineDateTimeFormat) {
            return formatMachineDateTime(date);
        }

        if (typeof flatpickr !== "undefined") {
            return flatpickr.formatDate(date, format);
        }

        return formatMachineDateTime(date);
    };

    const createLocalizedDateConfig = (csharpShortDatePattern, options) => {
        const datePattern = csharpDatePatternToFlatpickr(csharpShortDatePattern) || machineDateFormat;
        const useAltInput = datePattern !== machineDateFormat;

        return Object.assign({
            allowInput: true,
            dateFormat: machineDateFormat,
            altInput: useAltInput,
            altFormat: datePattern,
            parseDate: createParseDate(datePattern, false),
            formatDate: createFormatDate(),
        }, options || {});
    };

    const createLocalizedDateTimeConfig = (csharpShortDatePattern, csharpShortTimePattern, options) => {
        const patterns = createFlatpickrDateTimePattern(csharpShortDatePattern, csharpShortTimePattern, machineDateFormat, "H:i");
        const useAltInput = patterns.dateTimePattern !== machineDateTimeFormat;

        return Object.assign({
            enableTime: true,
            time_24hr: patterns.time24hr,
            allowInput: true,
            dateFormat: machineDateTimeFormat,
            altInput: useAltInput,
            altFormat: patterns.dateTimePattern,
            parseDate: createParseDate(patterns.dateTimePattern, true),
            formatDate: createFormatDate(),
        }, options || {});
    };

    return {
        machineDateFormat,
        machineDateTimeFormat,
        csharpDatePatternToFlatpickr,
        csharpTimePatternToFlatpickr,
        createFlatpickrDateTimePattern,
        createLocalizedDateConfig,
        createLocalizedDateTimeConfig,
        formatMachineDate,
        formatMachineDateTime,
        stripDirectionMarks
    };
}();
