// @ts-check
/*
 * The product price editor.
 *
 * "Add a price" clones the last price row, numbers its fields for the new index and empties what a person types into.
 * Hidden inputs keep their value: MVC writes a hidden "false" after every checkbox so an unticked box still posts,
 * and blanking it makes the next save fail with "Invalid Boolean value". Only the row's PriceId is cleared, since a
 * blank id is what tells the server the row is a new price rather than an edit of the one it was cloned from.
 * ASP.NET Core writes a hidden "__Invariant" input naming each number and date input, so those values are culture
 * invariant; its value is renumbered with the row so the new row's numbers stay invariant too.
 *
 * The recurring fields only mean anything for a recurring price, so they follow the Kind selector.
 */
(function () {
  'use strict';

  // Renumbers the first index in a field name or id ("Prices[0].Amount", "Prices_0__Amount") to "next".
  function renumber(value, next) {
    if (!value) {
      return value;
    }

    return value.replace(/[[_](\d+)[\]_]/, function (match) {
      return match.replace(/\d+/, String(next));
    });
  }

  // What the clone does with a field: 'uncheck' a checkbox, 'clear' a typed-in value, 'renumber' the value of an
  // invariant marker, or 'keep' it.
  function resetAction(field) {
    var tagName = (field.tagName || '').toUpperCase();
    var type = (field.type || '').toLowerCase();
    var name = field.name || '';

    if (tagName === 'INPUT' && (type === 'checkbox' || type === 'radio')) {
      return 'uncheck';
    }

    if (/\.PriceId$/.test(name)) {
      return 'clear';
    }

    if (tagName === 'INPUT' && type === 'hidden') {
      return name === '__Invariant' ? 'renumber' : 'keep';
    }

    if (tagName === 'INPUT' || tagName === 'TEXTAREA') {
      return 'clear';
    }

    // A select keeps the choice of the row it was cloned from: every option is a valid value.
    return 'keep';
  }

  globalThis.CrestAppsProductPriceEditor = {
    renumber: renumber,
    resetAction: resetAction,
  };

  if (typeof document === 'undefined' || !document.querySelectorAll) {
    return;
  }

  function bindKind(row) {
    var kind = row.querySelector('select[name$=".Kind"]');
    var fields = row.querySelector('[data-recurring-fields]');

    if (!kind || !fields) {
      return;
    }

    kind.addEventListener('change', function () {
      fields.classList.toggle('d-none', kind.value !== 'Recurring');
    });
  }

  function setUp(container) {
    container.querySelectorAll('.product-price-row').forEach(bindKind);

    var add = document.querySelector('[data-product-price-add]');

    if (!add) {
      return;
    }

    add.addEventListener('click', function () {
      var rows = container.querySelectorAll('.product-price-row');

      if (rows.length === 0) {
        return;
      }

      var clone = /** @type {HTMLElement} */ (rows[rows.length - 1].cloneNode(true));
      var next = rows.length;

      clone.querySelectorAll('input, select, textarea, label').forEach(function (element) {
        ['name', 'id', 'for'].forEach(function (attribute) {
          var value = element.getAttribute(attribute);

          if (value) {
            element.setAttribute(attribute, renumber(value, next));
          }
        });

        if (element.tagName === 'LABEL') {
          return;
        }

        var field = /** @type {HTMLInputElement} */ (element);

        switch (resetAction(field)) {
          case 'uncheck':
            field.checked = false;
            break;
          case 'clear':
            field.value = '';
            break;
          case 'renumber':
            field.value = renumber(field.value, next);
            break;
        }
      });

      container.appendChild(clone);
      bindKind(clone);
    });
  }

  function start() {
    document.querySelectorAll('[data-product-prices]').forEach(setUp);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', start);
  } else {
    start();
  }
})();
