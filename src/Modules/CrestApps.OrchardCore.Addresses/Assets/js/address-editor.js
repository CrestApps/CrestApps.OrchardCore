// @ts-check
/*
 * The address editor.
 *
 * The page lays the editor out for the country it starts with. When the country changes, the script asks the site for
 * that country's layout and re-lays the fields: the lines and their order, the labels (State, Prefecture, ZIP code,
 * Postcode…), which are required, and whether the region is chosen from a list. Fields the country does not use are
 * set aside and disabled, so they post nothing. Without the script the server lays the form out again when it comes
 * back.
 *
 * When the site suggests addresses, typing a street lists addresses for it under the field; picking one fills in the
 * rest of the address, switching the country first when it's in another.
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

  // The layout each editor is waiting for, after its country changed.
  var layouts = new WeakMap();

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

      layouts.set(editor, fetch(url + encodeURIComponent(code), { headers: { Accept: 'application/json' }, credentials: 'same-origin' })
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
        }));
    });

    var suggestUrl = editor.getAttribute('data-suggest-url');
    var resolveUrl = editor.getAttribute('data-resolve-url');

    if (suggestUrl && resolveUrl) {
      suggest(editor, country, suggestUrl, resolveUrl);
    }
  }

  // Sets a field's value as if it had been typed, so other scripts on the page see it.
  function setValue(field, value) {
    if (!field || field.disabled) {
      return;
    }

    field.value = value || '';
    field.dispatchEvent(new Event('input', { bubbles: true }));
    field.dispatchEvent(new Event('change', { bubbles: true }));
  }

  function fill(editor, address) {
    /** @param {string} selector */
    function field(selector) {
      return /** @type {HTMLInputElement | null} */ (editor.querySelector(selector));
    }

    setValue(field('input[autocomplete="address-line1"]'), address.addressLine1);
    setValue(field('input[autocomplete="address-line2"]'), address.addressLine2);
    setValue(field('input[autocomplete="address-level3"]'), address.dependentLocality);
    setValue(field('input[autocomplete="address-level2"]'), address.city);
    setValue(field('input[autocomplete="postal-code"]'), address.postalCode);

    var list = /** @type {HTMLSelectElement | null} */ (editor.querySelector('[data-address-region-list]'));

    if (list && !list.disabled) {
      var region = (address.region || '').toUpperCase();
      var match = Array.prototype.find.call(list.options, function (option) {
        return option.value.toUpperCase() === region || option.text.toUpperCase() === region;
      });

      setValue(list, match ? match.value : '');
    } else {
      setValue(field('[data-address-region-text]'), address.region);
    }
  }

  function newSession() {
    return globalThis.crypto && globalThis.crypto.randomUUID ? globalThis.crypto.randomUUID() : String(Date.now()) + '-' + String(Math.random()).slice(2);
  }

  // Addresses suggested for the street as it's typed, in a list under the field.
  function suggest(editor, country, suggestUrl, resolveUrl) {
    var street = /** @type {HTMLInputElement | null} */ (editor.querySelector('input[autocomplete="address-line1"]'));

    if (!street || !street.parentElement) {
      return;
    }

    var listId = street.id + '-suggestions';
    var list = document.createElement('div');

    list.id = listId;
    list.className = 'list-group position-absolute shadow-sm address-suggestions';
    list.style.zIndex = '1050';
    list.setAttribute('role', 'listbox');
    list.setAttribute('aria-label', editor.getAttribute('data-suggest-label') || '');
    list.hidden = true;

    street.parentElement.classList.add('position-relative');
    street.insertAdjacentElement('afterend', list);
    street.setAttribute('role', 'combobox');
    street.setAttribute('aria-autocomplete', 'list');
    street.setAttribute('aria-controls', listId);
    street.setAttribute('aria-expanded', 'false');

    var session = newSession();
    var timer = 0;
    var latest = 0;
    var active = -1;
    /** @type {{ id: string, text: string }[]} */
    var items = [];

    function close() {
      list.hidden = true;
      list.replaceChildren();
      items = [];
      active = -1;
      street.setAttribute('aria-expanded', 'false');
      street.removeAttribute('aria-activedescendant');
    }

    function highlight(index) {
      var options = list.querySelectorAll('[role="option"]');

      active = index;
      options.forEach(function (option, position) {
        option.classList.toggle('active', position === index);
        option.setAttribute('aria-selected', position === index ? 'true' : 'false');
      });

      if (index >= 0 && options[index]) {
        street.setAttribute('aria-activedescendant', options[index].id);
      } else {
        street.removeAttribute('aria-activedescendant');
      }
    }

    function pick(index) {
      var item = items[index];

      close();

      if (!item) {
        return;
      }

      var query = '?id=' + encodeURIComponent(item.id) + '&session=' + encodeURIComponent(session);

      // A pick ends the session; the next address typed is another.
      session = newSession();

      fetch(resolveUrl + query, { headers: { Accept: 'application/json' }, credentials: 'same-origin' })
        .then(function (response) {
          return response.ok ? response.json() : null;
        })
        .then(function (address) {
          if (!address) {
            return;
          }

          var hasCountry = address.country && Array.prototype.some.call(country.options, function (option) {
            return option.value === address.country;
          });

          // Another country is laid out first, so its fields are there to fill.
          if (hasCountry && country.value !== address.country) {
            country.value = address.country;
            country.dispatchEvent(new Event('change', { bubbles: true }));

            return Promise.resolve(layouts.get(editor)).then(function () {
              fill(editor, address);
            });
          }

          fill(editor, address);
        })
        .catch(function () {
          // The shopper types the rest themselves.
        });
    }

    function show(suggestions) {
      list.replaceChildren();
      items = suggestions || [];
      active = -1;

      items.forEach(function (item, index) {
        var option = document.createElement('button');

        option.type = 'button';
        option.id = listId + '-' + index;
        option.className = 'list-group-item list-group-item-action small';
        option.setAttribute('role', 'option');
        option.setAttribute('aria-selected', 'false');
        option.tabIndex = -1;
        option.textContent = item.text;

        // Picked before the field loses focus and closes the list.
        option.addEventListener('mousedown', function (event) {
          event.preventDefault();
          pick(index);
        });

        list.appendChild(option);
      });

      // Right under the street field, as wide as it.
      list.style.left = street.offsetLeft + 'px';
      list.style.top = street.offsetTop + street.offsetHeight + 'px';
      list.style.width = street.offsetWidth + 'px';
      list.hidden = items.length === 0;
      street.setAttribute('aria-expanded', items.length > 0 ? 'true' : 'false');
    }

    street.addEventListener('input', function (event) {
      // Only what the shopper types is looked up, not the street a pick filled in.
      if (!event.isTrusted) {
        return;
      }

      var text = street.value.trim();
      var request = ++latest;

      window.clearTimeout(timer);

      if (text.length < 3) {
        close();

        return;
      }

      timer = window.setTimeout(function () {
        var query = '?q=' + encodeURIComponent(text) + '&country=' + encodeURIComponent(country.value || '') + '&session=' + encodeURIComponent(session);

        fetch(suggestUrl + query, { headers: { Accept: 'application/json' }, credentials: 'same-origin' })
          .then(function (response) {
            return response.ok ? response.json() : [];
          })
          .then(function (suggestions) {
            // Only the latest typing is shown, however the answers arrive.
            if (request === latest && document.activeElement === street) {
              show(suggestions);
            }
          })
          .catch(function () {
            close();
          });
      }, 250);
    });

    street.addEventListener('keydown', function (event) {
      if (list.hidden || items.length === 0) {
        return;
      }

      if (event.key === 'ArrowDown') {
        event.preventDefault();
        highlight((active + 1) % items.length);
      } else if (event.key === 'ArrowUp') {
        event.preventDefault();
        highlight(active <= 0 ? items.length - 1 : active - 1);
      } else if (event.key === 'Enter' && active >= 0) {
        event.preventDefault();
        pick(active);
      } else if (event.key === 'Escape') {
        event.preventDefault();
        close();
      }
    });

    street.addEventListener('blur', close);
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
