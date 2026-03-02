/**
 * Nexus ERP - System Lookups Module
 * Manages dynamic dropdown values via ConfigController
 */
var lookupApp = (function () {
    "use strict";

    var table;
    var _offcanvasEl = document.getElementById('offcanvasLookup');
    var _offcanvas;
    var _currentType = "PaymentMethod"; // Default

    // --- API Configuration ---
    // User moved endpoints to ConfigController
    const API_BASE = '/api/Config/lookups';

    var init = function () {
        if (_offcanvasEl) _offcanvas = new bootstrap.Offcanvas(_offcanvasEl);

        _initGrid();

        // Form Submit
        $('#frmLookup').on('submit', function (e) {
            e.preventDefault();
            _save();
        });

        // Load default type on init
        $('#selectedLookupTitle').text("Payment Methods");
        // Initial Load
        //table.ajax.url(`${API_BASE}?type=${_currentType}`).load();
    };

    var _initGrid = function () {
        table = $('#lookupsGrid').DataTable({
            ajax: {
                url: API_BASE,
                type: 'GET',
                data: function (d) { d.type = _currentType; },
                dataSrc: "data"
            },
            columns: [
                { data: "sortOrder" },

                // NEW: Visual Type Column (The "Fail-Safe")
                {
                    data: "type",
                    className: "small text-muted",
                    render: function (data) {
                        // Make it subtle so it doesn't distract, but is visible
                        return `<span class="badge bg-light text-secondary border">${data}</span>`;
                    }
                },

                { data: "code", className: "font-monospace fw-semibold" },
                { data: "value", className: "fw-semibold text-primary" },
                {
                    data: "isActive",
                    render: function (data) {
                        return data
                            ? '<i class="bi bi-check-circle-fill text-success"></i>'
                            : '<i class="bi bi-dash-circle text-muted"></i>';
                    }
                },
                {
                    data: null, className: "text-end",
                    render: function (data, type, row) {
                        const safeRow = JSON.stringify(row).replace(/"/g, '&quot;');
                        return `<button class="btn btn-sm btn-outline-secondary" onclick="lookupApp.edit(${safeRow})">Edit</button>`;
                    }
                }
            ],
            order: [[0, 'asc']]
        });
    };

    // --- Public Actions ---

    var loadType = function (type, el) {
        // 1. UI Highlight
        $('#lookupTypeList a').removeClass('active');
        $(el).addClass('active');

        // 2. State Update
        _currentType = type;
        $('#selectedLookupTitle').text($(el).text().trim());

        // 3. UX Safety: Clear table immediately so user knows data is changing
        table.clear().draw();

        // 4. Reload
        table.ajax.reload();
    };

    var openCreatePanel = function () {
        $('#offcanvasLookupLabel').text("Add Value to " + _currentType);
        $('#frmLookup')[0].reset();
        $('#hdnLookupId').val(0);
        $('#hdnLookupType').val(_currentType);

        // Unlock Code field for new entries
        $('#txtLookupCode').prop('readonly', false).removeClass('form-control-plaintext').addClass('form-control');

        _offcanvas.show();
    };

    var edit = function (row) {
        $('#offcanvasLookupLabel').text("Edit " + row.value);
        $('#hdnLookupId').val(row.id);
        $('#hdnLookupType').val(row.type);

        // Lock Code field for edits (System Integrity)
        $('#txtLookupCode').val(row.code).prop('readonly', true).removeClass('form-control').addClass('form-control-plaintext');
        $('#txtLookupValue').val(row.value);
        $('#numSortOrder').val(row.sortOrder);
        $('#chkIsActive').prop('checked', row.isActive);

        _offcanvas.show();
    };

    var _save = async function () {
        var id = parseInt($('#hdnLookupId').val());

        var payload = {
            Id: id,
            Type: $('#hdnLookupType').val(),
            Code: $('#txtLookupCode').val(),
            Value: $('#txtLookupValue').val(),
            SortOrder: parseInt($('#numSortOrder').val()),
            IsActive: $('#chkIsActive').is(':checked')
        };

        // Basic Validation
        if (!payload.Code || !payload.Value) {
            toastr.warning("Code and Value are required.");
            return;
        }

        let res;
        if (id === 0) {
            // POST /api/Config/lookups
            res = await api.post(API_BASE, payload);
        } else {
            // PUT /api/Config/lookups?id=5 (Matches your Controller signature)
            res = await api.put(`${API_BASE}?id=${id}`, payload);
        }

        if (res && res.succeeded) {
            _offcanvas.hide();
            table.ajax.reload();
        }
    };

    return {
        init: init,
        loadType: loadType,
        openCreatePanel: openCreatePanel,
        edit: edit
    };
})();

$(document).ready(function () {
    lookupApp.init();
});