// @ts-check
/*
 * The address editor.
 *
 * The page lays the editor out for the country it starts with. When the country changes, the script asks the site for
 * that country's layout and re-lays the fields: the lines and their order, the labels (State, Prefecture, ZIP code,
 * Postcode…), which are required, and whether the region is chosen from a list. Fields the country does not use are
 * set aside and disabled, so they post nothing. Without the script the server lays the form out again when it comes
 * back.
 */
(function () {
  'use strict';

  // The order of the components a page can offer; a country's layout decides which it uses and where.
  var COMPONENTS = ['Name', 'Company', 'StreetAddress', 'DependentLocality', 'City', 'Region', 'PostalCode', 'SortingCode'];

  // The rows to show: the country's lines, keeping only the components the page offers.
  function planRows(layout, offered) {
    var rows = [];

    ((layout && layout.lines) || []).forEach(function (line) {
      var row = line.filter(function (component) {
        return offered.indexOf(component) >= 0;
      });

      if (row.length > 0) {
        rows.push(row);
      }
    });

    return rows;
  }

  // The components the page offers that the country does not use.
  function unusedComponents(rows, offered) {
    var placed = [];

    rows.forEach(function (row) {
      placed = placed.concat(row);
    });

    return offered.filter(function (component) {
      return placed.indexOf(component) < 0;
    });
  }

  // The street takes the whole width; a part alone on its line takes half; parts sharing a line share it.
  function columnClass(component, count) {
    if (component === 'StreetAddress') {
      return 'col-12';
    }

    return count === 1 ? 'col-md-6' : 'col-md';
  }

  globalThis.CrestAppsAddressEditor = {
    planRows: planRows,
    unusedComponents: unusedComponents,
    columnClass: columnClass,
  };

  if (typeof document === 'undefined' || !document.querySelectorAll) {
    return;
  }

  function fieldsOf(wrapper) {
    return wrapper.querySelectorAll('input, select, textarea');
  }

  function showRegion(wrapper, layout, used) {
    var list = /** @type {HTMLSelectElement | null} */ (wrapper.querySelector('[data-address-region-list]'));
    var text = /** @type {HTMLInputElement | null} */ (wrapper.querySelector('[data-address-region-text]'));

    if (!list || !text) {
      return;
    }

    var regions = (layout && layout.regions) || [];
    var hasList = regions.length > 0;
    var current = list.disabled ? text.value : list.value;

    if (hasList) {
      var prompt = list.options.length > 0 ? list.options[0].text : '';

      list.replaceChildren();
      list.appendChild(new Option(prompt, ''));

      regions.forEach(function (region) {
        list.appendChild(new Option(region.name, region.code));
      });

      // A region of the previous country is not one of this country's.
      list.value = regions.some(function (region) { return region.code === current; }) ? current : '';
    } else {
      text.value = list.disabled ? current : '';
    }

    list.hidden = !hasList;
    text.hidden = hasList;
    list.disabled = !(used && hasList);
    text.disabled = !(used && !hasList);
  }

  function applyLayout(editor, layout) {
    var lines = editor.querySelector('[data-address-lines]');
    var unused = editor.querySelector('[data-address-unused]');

    if (!lines || !unused) {
      return;
    }

    /** @type {Record<string, HTMLElement>} */
    var wrappers = {};

    editor.querySelectorAll('[data-address-component]').forEach(function (wrapper) {
      wrappers[wrapper.getAttribute('data-address-component') || ''] = /** @type {HTMLElement} */ (wrapper);
    });

    var offered = COMPONENTS.filter(function (component) {
      return wrappers[component];
    });

    var rows = planRows(layout, offered);
    var required = (layout && layout.required) || [];
    var labels = (layout && layout.labels) || {};

    lines.replaceChildren();

    rows.forEach(function (row) {
      var line = document.createElement('div');

      line.className = 'row g-3 mb-3';
      line.setAttribute('data-address-line', '');

      row.forEach(function (component) {
        var wrapper = wrappers[component];

        wrapper.className = columnClass(component, row.length);
        line.appendChild(wrapper);
      });

      lines.appendChild(line);
    });

    unusedComponents(rows, offered).forEach(function (component) {
      unused.appendChild(wrappers[component]);
    });

    offered.forEach(function (component) {
      var wrapper = wrappers[component];
      var used = wrapper.parentElement !== unused;
      var isRequired = required.indexOf(component) >= 0 || wrapper.getAttribute('data-address-page-required') === 'true';
      var label = wrapper.querySelector('[data-address-label]');
      var star = wrapper.querySelector('[data-address-required]');

      if (label && labels[component]) {
        label.textContent = labels[component];
      }

      if (star) {
        /** @type {HTMLElement} */ (star).hidden = !isRequired;
      }

      if (component === 'Region') {
        showRegion(wrapper, layout, used);
      } else {
        fieldsOf(wrapper).forEach(function (field) {
          /** @type {HTMLInputElement} */ (field).disabled = !used;
        });
      }

      fieldsOf(wrapper).forEach(function (field, index) {
        // The second street line is never required.
        field.setAttribute('aria-required', isRequired && !(component === 'StreetAddress' && index > 0) ? 'true' : 'false');
      });

      if (component === 'PostalCode') {
        var input = /** @type {HTMLInputElement | null} */ (wrapper.querySelector('input'));

        if (input) {
          input.placeholder = (layout && layout.postalCodeExample) || '';
        }
      }
    });
  }

  function initialize(editor) {
    if (editor.getAttribute('data-address-editor-ready') === 'true') {
      return;
    }

    editor.setAttribute('data-address-editor-ready', 'true');

    var country = /** @type {HTMLSelectElement | null} */ (editor.querySelector('[data-address-country]'));
    var url = editor.getAttribute('data-formats-url');

    if (!country || !url) {
      return;
    }

    var latest = 0;

    country.addEventListener('change', function () {
      var code = country.value;
      var request = ++latest;

      if (!code) {
        return;
      }

      fetch(url + encodeURIComponent(code), { headers: { Accept: 'application/json' }, credentials: 'same-origin' })
        .then(function (response) {
          return response.ok ? response.json() : null;
        })
        .then(function (layout) {
          // Only the country chosen last is laid out, however the answers arrive.
          if (layout && request === latest) {
            applyLayout(editor, layout);
          }
        })
        .catch(function () {
          // The fields stay as they are; the server checks the address against the country anyway.
        });
    });
  }

  function initializeAll() {
    document.querySelectorAll('[data-address-editor]').forEach(initialize);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeAll);
  } else {
    initializeAll();
  }
})();
