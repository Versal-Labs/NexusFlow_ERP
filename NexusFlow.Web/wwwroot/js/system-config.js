/**
 * Nexus ERP - System Configuration
 * Architecture: Hybrid (Tabbed View + API CRUD)
 */

var configApp = (function () {
    "use strict";

    var _settingsTable, _seqTable;
    var _settingModal, _seqModal;

    // API Endpoints
    const URL_SETTINGS = "/api/config/settings";
    const URL_SEQUENCES = "/api/config/sequences";

    function init() {
        // Init Modals
        _settingModal = new bootstrap.Modal(document.getElementById('settingModal'));
        _seqModal = new bootstrap.Modal(document.getElementById('sequenceModal'));

        // Init Grids
        _initSettingsGrid();

        // Lazy load Sequence grid only when tab is clicked (Performance)
        $('button[data-bs-target="#tab-sequences"]').on('shown.bs.tab', function (e) {
            if (!_seqTable) _initSequenceGrid();
        });
    }

    // --- TAB 1: SYSTEM SETTINGS ---

    function _initSettingsGrid() {
        _settingsTable = $('#settingsGrid').DataTable({
            ajax: { url: URL_SETTINGS, dataSrc: "data" },
            columns: [
                { data: 'key', className: "ps-4 fw-bold font-monospace text-dark" },
                { data: 'description', className: "text-muted small" },
                {
                    data: 'value',
                    render: function (data, type, row) {
                        // Visual cues based on data type
                        if (row.dataType === 'Boolean') {
                            return data === 'true'
                                ? '<span class="badge bg-success bg-opacity-10 text-success">Enabled</span>'
                                : '<span class="badge bg-secondary bg-opacity-10 text-secondary">Disabled</span>';
                        }
                        return `<span class="fw-medium text-dark">${data}</span>`;
                    }
                },
                {
                    data: null, className: "text-end pe-4",
                    render: function (data, type, row) {
                        return `<button class="btn btn-sm btn-white border shadow-sm" onclick='configApp.editSetting(${JSON.stringify(row)})'>Edit</button>`;
                    }
                }
            ],
            dom: 't', // Simple table, no search/pagination needed for limited configs
            paging: false
        });
    }

    function editSetting(row) {
        $('#ConfigKey').val(row.key);
        $('#ConfigDataType').val(row.dataType);
        $('#ConfigKeyDisplay').val(row.key);
        $('#ConfigDescDisplay').text(row.description);

        // Dynamic Input Renderer
        var container = $('#dynamicInputContainer');
        container.empty();

        if (row.dataType === 'Boolean') {
            // Render Switch
            var isChecked = row.value.toLowerCase() === 'true' ? 'checked' : '';
            container.html(`
                <div class="form-check form-switch">
                    <input class="form-check-input" type="checkbox" id="ConfigValueInput" ${isChecked}>
                    <label class="form-check-label">Enable Feature</label>
                </div>
            `);
        } else if (row.dataType === 'Decimal' || row.dataType === 'Integer') {
            // Render Number Input
            container.html(`<input type="number" class="form-control" id="ConfigValueInput" value="${row.value}" step="0.01">`);
        } else {
            // Default String Input
            container.html(`<input type="text" class="form-control" id="ConfigValueInput" value="${row.value}">`);
        }

        _settingModal.show();
    }

    function saveSetting() {
        var key = $('#ConfigKey').val();
        var dataType = $('#ConfigDataType').val();
        var valInput = $('#ConfigValueInput');

        var newValue;
        if (dataType === 'Boolean') {
            newValue = valInput.is(':checked').toString().toLowerCase(); // "true" or "false"
        } else {
            newValue = valInput.val();
        }

        $.ajax({
            url: URL_SETTINGS,
            type: "PUT",
            contentType: "application/json",
            data: JSON.stringify({ key: key, value: newValue }),
            success: function () {
                _settingModal.hide();
                _settingsTable.ajax.reload();
                _showToast("Setting updated successfully");
            },
            error: function () {
                _showToast("Failed to update setting", "error");
            }
        });
    }

    // --- TAB 2: NUMBER SEQUENCES ---

    function _initSequenceGrid() {
        _seqTable = $('#sequenceGrid').DataTable({
            ajax: { url: URL_SEQUENCES, dataSrc: "data" },
            columns: [
                { data: 'module', className: "ps-4 fw-bold" },
                { data: 'prefix', render: function (d) { return `<span class="badge bg-light text-dark border">${d}</span>`; } },
                { data: 'nextNumber' },
                {
                    data: null,
                    render: function (d, t, row) {
                        // Logic matching Entity PreviewNext()
                        var suffix = row.suffix || '';
                        return `<span class="font-monospace text-primary bg-primary bg-opacity-10 px-2 py-1 rounded">${row.prefix}${row.delimiter}${row.nextNumber}${suffix}</span>`;
                    }
                },
                {
                    data: null, className: "text-end pe-4",
                    render: function (data, type, row) {
                        return `<button class="btn btn-sm btn-white border shadow-sm" onclick='configApp.editSequence(${JSON.stringify(row)})'>Edit</button>`;
                    }
                }
            ],
            dom: 't',
            paging: false
        });
    }

    function editSequence(row) {
        $('#SeqId').val(row.id);
        $('#SeqModule').val(row.module);
        $('#SeqPrefix').val(row.prefix);
        $('#SeqDelimiter').val(row.delimiter);
        $('#SeqNext').val(row.nextNumber);

        updatePreview(); // Show initial preview
        _seqModal.show();
    }

    function updatePreview() {
        var pre = $('#SeqPrefix').val();
        var del = $('#SeqDelimiter').val();
        var num = $('#SeqNext').val();
        // Assuming suffix is not editable in this UI for simplicity, or add hidden input
        // Using a safe default or passing suffix via row data if needed

        $('#SeqPreview').text(`${pre}${del}${num}`);
    }

    function saveSequence() {
        var payload = {
            id: $('#SeqId').val(),
            prefix: $('#SeqPrefix').val(),
            delimiter: $('#SeqDelimiter').val(),
            nextNumber: parseInt($('#SeqNext').val())
        };

        if (!payload.prefix || !payload.nextNumber) return; // Simple validation

        $.ajax({
            url: URL_SEQUENCES,
            type: "PUT",
            contentType: "application/json",
            data: JSON.stringify(payload),
            success: function () {
                _seqModal.hide();
                _seqTable.ajax.reload();
                _showToast("Sequence updated");
            }
        });
    }

    // Helper
    function _showToast(msg, type = 'success') {
        const Toast = Swal.mixin({
            toast: true, position: 'top-end', showConfirmButton: false, timer: 3000
        });
        Toast.fire({ icon: type, title: msg });
    }

    return {
        init: init,
        editSetting: editSetting,
        saveSetting: saveSetting,
        editSequence: editSequence,
        updatePreview: updatePreview,
        saveSequence: saveSequence
    };
})();

$(document).ready(function () {
    configApp.init();
});