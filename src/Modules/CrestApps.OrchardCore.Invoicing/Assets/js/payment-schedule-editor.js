// @ts-check
/*
 * The payment schedule editor of a draft invoice.
 *
 * The parts are all given the same way, as amounts or as percents, chosen once above them; each part shows one value
 * field, posted as its amount or its percent. Parts are added and removed in the browser, and the editor shows what is
 * allocated and what is left: of the invoice's total (following the line items editor as it changes), or of 100%.
 * The server checks the parts when the invoice is saved and issued.
 */
(function () {
  'use strict';

  function round(amount, decimals) {
    var factor = Math.pow(10, decimals);

    return Math.round((amount + Number.EPSILON * Math.sign(amount)) * factor) / factor;
  }

  // The way the saved parts were given: percents when they all were, amounts otherwise.
  function detectMode(rows) {
    var withPercent = rows.filter(function (row) { return row.percent !== null && row.percent !== undefined; });
    var withAmount = rows.filter(function (row) { return row.amount !== null && row.amount !== undefined; });

    return withPercent.length > 0 && withAmount.length === 0 ? 'percent' : 'amount';
  }

  function allocation(values, mode, total, decimals) {
    var allocated = values.reduce(function (sum, value) { return value === null || value === undefined ? sum : sum + value; }, 0);
    var whole = mode === 'percent' ? 100 : total;
    var places = mode === 'percent' ? 3 : decimals;

    allocated = round(allocated, places);

    return { allocated: allocated, left: round(whole - allocated, places) };
  }

  function valueName(index, mode) {
    return 'Schedule[' + index + '].' + (mode === 'percent' ? 'Percent' : 'Amount');
  }

  globalThis.CrestAppsPaymentSchedule = {
    detectMode: detectMode,
    allocation: allocation,
    valueName: valueName,
  };

  if (typeof document === 'undefined' || !document.querySelectorAll) {
    return;
  }

  function parse(text) {
    var value = String(text === null || text === undefined ? '' : text).trim().replace(',', '.');

    return value === '' || isNaN(Number(value)) ? null : Number(value);
  }

  function setUp(editor) {
    var body = editor.querySelector('[data-schedule-body]');
    var template = editor.querySelector('template[data-schedule-template]');
    var decimals = Number(editor.getAttribute('data-decimals') || '2');
    var currency = editor.getAttribute('data-currency') || '';
    var culture = editor.getAttribute('data-culture') || undefined;
    var total = parse(editor.getAttribute('data-total')) || 0;
    var format;

    try {
      format = currency
        ? new Intl.NumberFormat(culture, { style: 'currency', currency: currency })
        : new Intl.NumberFormat(culture, { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
    } catch {
      format = new Intl.NumberFormat(undefined, { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
    }

    function rows() {
      return Array.prototype.slice.call(body.querySelectorAll('[data-schedule-row]'));
    }

    function mode() {
      var checked = editor.querySelector('[data-schedule-mode]:checked');

      return checked ? checked.value : 'amount';
    }

    function renumber() {
      var current = mode();

      rows().forEach(function (row, index) {
        var date = row.querySelector('[data-schedule-date]');
        var value = row.querySelector('[data-schedule-value]');
        var suffix = row.querySelector('[data-schedule-suffix]');

        if (date) {
          date.setAttribute('name', 'Schedule[' + index + '].DueDate');
        }

        if (value) {
          value.setAttribute('name', valueName(index, current));

          if (current === 'percent') {
            value.setAttribute('max', '100');
          } else {
            value.removeAttribute('max');
          }
        }

        if (suffix) {
          suffix.textContent = current === 'percent' ? '%' : currency;
        }

        // Each number input is followed by the __Invariant field that names it, so its dot is read the same in every culture.
        row.querySelectorAll('input[name="__Invariant"]').forEach(function (marker) {
          marker.value = marker.previousElementSibling ? marker.previousElementSibling.getAttribute('name') : '';
        });
      });
    }

    function update() {
      var current = mode();
      var values = rows().map(function (row) {
        var value = row.querySelector('[data-schedule-value]');

        return value ? parse(value.value) : null;
      });
      var result = allocation(values, current, total, decimals);
      var allocated = editor.querySelector('[data-schedule-allocated]');
      var left = editor.querySelector('[data-schedule-left]');
      var show = current === 'percent'
        ? function (value) { return value.toLocaleString(culture, { maximumFractionDigits: 3 }) + '%'; }
        : function (value) { return format.format(value); };

      if (allocated) {
        allocated.textContent = show(result.allocated);
      }

      if (left) {
        left.textContent = show(result.left);
        left.classList.toggle('text-danger', result.left < 0);
        left.classList.toggle('text-success', result.left === 0 && values.some(function (value) { return value !== null; }));
      }
    }

    editor.addEventListener('click', function (event) {
      var button = /** @type {HTMLElement} */ (event.target).closest('button');

      if (!button || !editor.contains(button)) {
        return;
      }

      if (button.matches('[data-schedule-add]')) {
        var fragment = template.content.cloneNode(true);
        var row = fragment.querySelector('[data-schedule-row]');

        body.appendChild(fragment);
        renumber();
        update();

        var date = row.querySelector('[data-schedule-date]');

        if (date) {
          date.focus();
        }
      } else if (button.matches('[data-schedule-remove]')) {
        var removed = button.closest('[data-schedule-row]');

        if (removed) {
          removed.remove();
          renumber();
          update();
        }
      }
    });

    editor.addEventListener('change', function (event) {
      if (/** @type {HTMLElement} */ (event.target).matches('[data-schedule-mode]')) {
        renumber();
      }

      update();
    });

    editor.addEventListener('input', update);

    // The line items editor tells the page the invoice's total as it changes.
    document.addEventListener('documentlines:change', function (event) {
      var detail = /** @type {CustomEvent} */ (event).detail;

      if (detail && typeof detail.total === 'number') {
        total = detail.total;
        update();
      }
    });

    renumber();
    update();
  }

  function start() {
    document.querySelectorAll('[data-payment-schedule]').forEach(setUp);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', start);
  } else {
    start();
  }
})();
