/*
** NOTE: This file is generated by Gulp and should not be edited directly!
** Any changes made directly to this file will be overwritten next time its asset group is processed by Gulp.
*/

function _typeof(o) { "@babel/helpers - typeof"; return _typeof = "function" == typeof Symbol && "symbol" == typeof Symbol.iterator ? function (o) { return typeof o; } : function (o) { return o && "function" == typeof Symbol && o.constructor === Symbol && o !== Symbol.prototype ? "symbol" : typeof o; }, _typeof(o); }
function ownKeys(e, r) { var t = Object.keys(e); if (Object.getOwnPropertySymbols) { var o = Object.getOwnPropertySymbols(e); r && (o = o.filter(function (r) { return Object.getOwnPropertyDescriptor(e, r).enumerable; })), t.push.apply(t, o); } return t; }
function _objectSpread(e) { for (var r = 1; r < arguments.length; r++) { var t = null != arguments[r] ? arguments[r] : {}; r % 2 ? ownKeys(Object(t), !0).forEach(function (r) { _defineProperty(e, r, t[r]); }) : Object.getOwnPropertyDescriptors ? Object.defineProperties(e, Object.getOwnPropertyDescriptors(t)) : ownKeys(Object(t)).forEach(function (r) { Object.defineProperty(e, r, Object.getOwnPropertyDescriptor(t, r)); }); } return e; }
function _defineProperty(obj, key, value) { key = _toPropertyKey(key); if (key in obj) { Object.defineProperty(obj, key, { value: value, enumerable: true, configurable: true, writable: true }); } else { obj[key] = value; } return obj; }
function _toPropertyKey(t) { var i = _toPrimitive(t, "string"); return "symbol" == _typeof(i) ? i : i + ""; }
function _toPrimitive(t, r) { if ("object" != _typeof(t) || !t) return t; var e = t[Symbol.toPrimitive]; if (void 0 !== e) { var i = e.call(t, r || "default"); if ("object" != _typeof(i)) return i; throw new TypeError("@@toPrimitive must return a primitive value."); } return ("string" === r ? String : Number)(t); }
aiChatProfileAdmin = function () {
  var initialize = function initialize(selectedLabel) {
    var searchBox = document.getElementById('search-box');
    var searchAlert = document.getElementById('list-alert');
    var filterElements = document.querySelectorAll('[data-filter-value]');

    // If the user press Enter, don't submit.
    searchBox.addEventListener('keydown', function (e) {
      if (e.key === 'Enter') {
        e.preventDefault();
      }
    });
    searchBox.addEventListener('keyup', function (e) {
      var search = e.target.value.toLowerCase();
      // On ESC, clear the search box and display all rules.
      if (e.key == 'Escape' || search == '') {
        searchAlert.classList.add('d-none');
        searchBox.value = '';
        for (var i = 0; i < filterElements.length; i++) {
          filterElements[i].classList.remove("d-none");
          filterElements[i].classList.remove("first-child-visible");
          filterElements[i].classList.remove("last-child-visible");
        }
        if (filterElements.length > 0) {
          filterElements[0].classList.add('first-child-visible');
          filterElements[filterElements.length - 1].classList.add('last-child-visible');
        }
      } else {
        var visibleElements = [];
        for (var _i = 0; _i < filterElements.length; _i++) {
          var filter = filterElements[_i];
          var text = filter.getAttribute('data-filter-value');
          if (!text) {
            filter.classList.add("d-none");
            continue;
          }
          var found = text.indexOf(search) > -1;
          if (found) {
            filter.classList.remove("d-none");
            filter.classList.remove("first-child-visible");
            filter.classList.remove("last-child-visible");
            visibleElements.push(filter);
          } else {
            filter.classList.add("d-none");
          }
        }
        if (visibleElements.length > 0) {
          visibleElements[0].classList.add('first-child-visible');
          visibleElements[visibleElements.length - 1].classList.add('last-child-visible');
          searchAlert.classList.add('d-none');
        } else {
          searchAlert.classList.remove('d-none');
        }
      }
    });
    var actions = document.getElementById('actions');
    var items = document.getElementById('items');
    var filters = document.querySelectorAll('.filter');
    var selectAllCtrl = document.getElementById('select-all');
    var selectedItems = document.getElementById('selected-items');
    var itemsCheckboxes = document.querySelectorAll("input[type='checkbox'][name='itemIds']");
    function displayActionsOrFilters() {
      // Select all checked checkboxes with name 'itemIds'
      var checkedCheckboxes = document.querySelectorAll("input[type='checkbox'][name='itemIds']:checked");
      if (checkedCheckboxes.length > 1) {
        actions.classList.remove('d-none');
        for (var i = 0; i < filters.length; i++) {
          filters[i].classList.add('d-none');
        }
        selectedItems.classList.remove('d-none');
        items.classList.add('d-none');
      } else {
        actions.classList.add('d-none');
        for (var _i2 = 0; _i2 < filters.length; _i2++) {
          filters[_i2].classList.remove('d-none');
        }
        selectedItems.classList.add('d-none');
        items.classList.remove('d-none');
      }
    }
    var dropdownItems = document.querySelectorAll(".dropdown-menu .dropdown-item");

    // Add click event listeners to each dropdown item
    dropdownItems.forEach(function (item) {
      // Check if the item has a data-action attribute
      if (item.dataset.action) {
        item.addEventListener("click", function () {
          // Get all checked checkboxes
          var checkedCheckboxes = document.querySelectorAll("input[type='checkbox'][name='itemIds']:checked");

          // Check if more than one checkbox is checked
          if (checkedCheckboxes.length > 1) {
            // Get data attributes from the clicked item
            var actionData = Object.assign({}, item.dataset);
            confirmDialog(_objectSpread(_objectSpread({}, actionData), {}, {
              callback: function callback(r) {
                if (r) {
                  // Set the value of the BulkAction option
                  document.querySelector("[name='Options.BulkAction']").value = actionData.action;
                  // Trigger the submit action
                  document.querySelector("[name='submit.BulkAction']").click();
                }
              }
            }));
          }
        });
      }
    });
    selectAllCtrl.addEventListener("click", function () {
      itemsCheckboxes.forEach(function (checkbox) {
        if (checkbox !== selectAllCtrl) {
          checkbox.checked = selectAllCtrl.checked; // Set the checked state of all checkboxes
        }
      });

      // Update the selected items text
      updateSelectedItemsText();
      displayActionsOrFilters();
    });

    // Event listener for individual checkboxes
    itemsCheckboxes.forEach(function (checkbox) {
      checkbox.addEventListener("click", function () {
        var itemsCount = itemsCheckboxes.length;
        var selectedItemsCount = document.querySelectorAll("input[type='checkbox'][name='itemIds']:checked").length;

        // Update selectAllCtrl state
        selectAllCtrl.checked = selectedItemsCount === itemsCount;
        selectAllCtrl.indeterminate = selectedItemsCount > 0 && selectedItemsCount < itemsCount;

        // Update the selected items text
        updateSelectedItemsText();
        displayActionsOrFilters();
      });
    });

    // Function to update selected items text
    function updateSelectedItemsText() {
      var selectedCount = document.querySelectorAll("input[type='checkbox'][name='itemIds']:checked").length;
      selectedItems.textContent = selectedCount + ' ' + selectedLabel;
    }
  };
  return {
    initialize: initialize
  };
}();