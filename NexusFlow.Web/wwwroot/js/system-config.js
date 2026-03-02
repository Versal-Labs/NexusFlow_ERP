/**
 * Nexus ERP - System Configuration Module
 * Architecture: Clean Architecture Client (Tabbed + API + Offcanvas)
 */

var configApp = (function () {
    "use strict";

    // --- State ---
    var _settingsTable, _seqTable;
    var _offcanvasSettingEl = document.getElementById('offcanvasSetting');
    var _offcanvasSequenceEl = document.getElementById('offcanvasSequence');
    var _settingOffcanvas, _sequenceOffcanvas;

    // --- Endpoints ---
    const URL_SETTINGS = "/api/config/settings"; // Ensure this matches your ConfigController Routes
    const URL_SEQUENCES = "/api/config/sequences";

    // --- Initialization ---
    function init() {
        // Init Offcanvas Instances
        if (_offcanvasSettingEl) _settingOffcanvas = new bootstrap.Offcanvas(_offcanvasSettingEl);
        if (_offcanvasSequenceEl) _sequenceOffcanvas = new bootstrap.Offcanvas(_offcanvasSequenceEl);

        _initSettingsGrid();
        _registerEvents();

        // Lazy load Sequences tab
        document.getElementById('sequences-tab').addEventListener('shown.bs.tab', function () {
            if (!_seqTable) _initSequenceGrid();
            else _seqTable.columns.adjust();
        });
    }

    function _registerEvents() {
        // Form Submissions
        $('#frmSetting').on('submit', function (e) {
            e.preventDefault();
            _saveSetting();
        });

        $('#frmSequence').on('submit', function (e) {
            e.preventDefault();
            _saveSequence();
        });
    }

    // ============================================================
    // TAB 1: GENERAL SETTINGS
    // ============================================================

    function _initSettingsGrid() {
        // We use $.ajax for DataTables to keep it simple, or we can use the pipeline. 
        // For consistency with previous modules, we use standard ajax source.
        _settingsTable = $('#settingsGrid').DataTable({
            ajax: {
                url: URL_SETTINGS,
                type: "GET",
                dataSrc: "data",
                error: function (xhr, error, thrown) {
                    console.error("DataTables Error:", error);
                }
            },
            columns: [
                { data: 'key', className: "ps-4 fw-semibold font-monospace text-dark", width: "30%" },
                { data: 'description', className: "text-muted small", width: "35%" },
                {
                    data: 'value', width: "25%",
                    render: function (data, type, row) {
                        if (row.dataType === 'Boolean') {
                            return (data.toString().toLowerCase() === 'true')
                                ? '<span class="badge bg-success-subtle text-success border border-success-subtle">Enabled</span>'
                                : '<span class="badge bg-secondary-subtle text-secondary border border-secondary-subtle">Disabled</span>';
                        }
                        return `<span class="fw-medium text-dark font-monospace">${data}</span>`;
                    }
                },
                {
                    data: null, className: "text-end pe-4", width: "10%",
                    render: function (data, type, row) {
                        // Securely escape data for the onclick handler
                        const safeRow = JSON.stringify(row).replace(/"/g, '&quot;');
                        return `<button class="btn btn-sm btn-outline-secondary" onclick="configApp.editSetting(${safeRow})"><i class="bi bi-pencil"></i> Edit</button>`;
                    }
                }
            ],
            responsive: true,
            language: { emptyTable: "No configurations defined." }
        });
    }

    function openSettingPanel() {
        $('#offcanvasSettingLabel').text("New Setting");
        $('#frmSetting')[0].reset();
        $('#IsEditMode').val("false");

        // Enable Key Editing
        $('#ConfigKey').prop('readonly', false).removeClass('form-control-plaintext').addClass('form-control');
        $('#ConfigDataType').prop('disabled', false);

        renderInput(""); // Render default text input
        _settingOffcanvas.show();
    }

    function editSetting(row) {
        $('#offcanvasSettingLabel').text("Edit Setting");
        $('#IsEditMode').val("true");
        $('#ConfigKey').val(row.key);
        $('#ConfigDataType').val(row.dataType);
        $('#ConfigDescription').val(row.description);

        // Lock Key & Type
        $('#ConfigKey').prop('readonly', true).removeClass('form-control').addClass('form-control-plaintext');
        $('#ConfigDataType').prop('disabled', true);

        renderInput(row.value);
        _settingOffcanvas.show();
    }

    function renderInput(value = "") {
        var type = $('#ConfigDataType').val();
        var container = $('#dynamicInputContainer');
        container.empty();

        if (type === 'Boolean') {
            var isChecked = (value.toString().toLowerCase() === 'true') ? 'checked' : '';
            container.html(`
                <div class="form-check form-switch">
                    <input class="form-check-input" type="checkbox" id="ConfigValueInput" ${isChecked}>
                    <label class="form-check-label ms-2">Enable this feature</label>
                </div>
            `);
        } else if (type === 'Decimal' || type === 'Integer') {
            var step = (type === 'Decimal') ? '0.01' : '1';
            container.html(`<input type="number" class="form-control font-monospace" id="ConfigValueInput" value="${value}" step="${step}" required>`);
        } else {
            container.html(`<input type="text" class="form-control" id="ConfigValueInput" value="${value}" required>`);
        }
    }

    function _validateInput(type, value) {
        if (type === 'Integer') {
            if (!/^-?\d+$/.test(value)) return "Value must be a valid integer.";
        }
        if (type === 'Decimal') {
            if (isNaN(value) || value === '') return "Value must be a valid number.";
        }
        if (type === 'Boolean') return null; // Checkbox always valid
        if (type === 'String' && (!value || value.trim() === '')) return "Text value cannot be empty.";

        return null; // Valid
    }

    async function _saveSetting() {
        var isEdit = $('#IsEditMode').val() === "true";
        var key = $('#ConfigKey').val();
        var dataType = $('#ConfigDataType').val();

        // Get Value
        var valInput = $('#ConfigValueInput');
        var finalValue;
        if (dataType === 'Boolean') {
            finalValue = valInput.is(':checked').toString().toLowerCase();
        } else {
            finalValue = valInput.val();
        }

        // Client-Side Validation
        var error = _validateInput(dataType, finalValue);
        if (error) {
            // If we had toastr, we'd use it. For now, alert or fallback.
            if (typeof toastr !== 'undefined') toastr.warning(error);
            else alert(error);
            return;
        }

        var payload = {
            Key: key,
            Value: finalValue,
            DataType: dataType,
            Description: $('#ConfigDescription').val()
        };

        // Use Site.js API
        var result;
        if (isEdit) {
            result = await api.put(URL_SETTINGS, payload);
        } else {
            result = await api.post(URL_SETTINGS, payload);
        }

        if (result && result.succeeded) {
            _settingOffcanvas.hide();
            _settingsTable.ajax.reload();
        }
    }

    // ============================================================
    // TAB 2: NUMBER SEQUENCES
    // ============================================================

    function _initSequenceGrid() {
        if ($.fn.DataTable.isDataTable('#sequenceGrid')) return;

        _seqTable = $('#sequenceGrid').DataTable({
            ajax: {
                url: URL_SEQUENCES,
                type: "GET",
                dataSrc: "data"
            },
            columns: [
                { data: 'module', className: "ps-4 fw-semibold" },
                { data: 'prefix', className: "text-center", render: d => `<span class="badge bg-light text-dark border font-monospace">${d}</span>` },
                { data: 'nextNumber', className: "font-monospace" },
                {
                    data: null, className: "font-monospace text-muted",
                    render: function (data, type, row) {
                        return `${row.prefix}${row.delimiter}${row.nextNumber}`;
                    }
                },
                {
                    data: null, className: "text-end pe-4",
                    render: function (data, type, row) {
                        const safeRow = JSON.stringify(row).replace(/"/g, '&quot;');
                        return `<button class="btn btn-sm btn-outline-secondary" onclick="configApp.editSequence(${safeRow})"><i class="bi bi-pencil"></i> Edit</button>`;
                    }
                }
            ],
            responsive: true
        });
    }

    function openSequencePanel() {
        $('#offcanvasSequenceLabel').text("New Sequence");
        $('#frmSequence')[0].reset();
        $('#SeqId').val(0);

        // Enable Module ID editing
        $('#SeqModule').prop('readonly', false).removeClass('form-control-plaintext').addClass('form-control');

        $('#SeqNext').val(1);
        $('#SeqDelimiter').val('-');
        updatePreview();
        _sequenceOffcanvas.show();
    }

    function editSequence(row) {
        $('#offcanvasSequenceLabel').text("Edit Sequence");
        $('#SeqId').val(row.id);
        $('#SeqModule').val(row.module);
        $('#SeqPrefix').val(row.prefix);
        $('#SeqDelimiter').val(row.delimiter);
        $('#SeqNext').val(row.nextNumber);

        // Lock Module ID
        $('#SeqModule').prop('readonly', true).removeClass('form-control').addClass('form-control-plaintext');

        updatePreview();
        _sequenceOffcanvas.show();
    }

    function updatePreview() {
        var pre = $('#SeqPrefix').val() || '';
        var del = $('#SeqDelimiter').val() || '';
        var num = $('#SeqNext').val() || '';
        $('#SeqPreview').text(`${pre}${del}${num}`);
    }

    async function _saveSequence() {
        var id = parseInt($('#SeqId').val()) || 0;
        var payload = {
            Id: id,
            Module: $('#SeqModule').val(),
            Prefix: $('#SeqPrefix').val(),
            Delimiter: $('#SeqDelimiter').val(),
            NextNumber: parseInt($('#SeqNext').val())
        };

        if (!payload.Module || !payload.Prefix) {
            if (typeof toastr !== 'undefined') toastr.warning("Module and Prefix are required.");
            return;
        }

        var result;
        if (id === 0) {
            result = await api.post(URL_SEQUENCES, payload);
        } else {
            result = await api.put(URL_SEQUENCES, payload);
        }

        if (result && result.succeeded) {
            _sequenceOffcanvas.hide();
            _seqTable.ajax.reload();
        }
    }

    // --- Public API ---
    return {
        init: init,
        openSettingPanel: openSettingPanel,
        editSetting: editSetting,
        renderInput: renderInput,

        openSequencePanel: openSequencePanel,
        editSequence: editSequence,
        updatePreview: updatePreview
    };
})();

$(document).ready(function () {
    configApp.init();
});