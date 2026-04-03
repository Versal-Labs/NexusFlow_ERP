var coaApp = (function () {
    "use strict";

    var _drawer;

    function init() {
        _drawer = new bootstrap.Offcanvas(document.getElementById('createAccountDrawer'));
        loadTree();
    }

    async function loadTree() {
        $('#coaTbody').html('<tr><td colspan="5" class="text-center text-muted py-4"><div class="spinner-border spinner-border-sm me-2"></div>Loading Ledger...</td></tr>');

        // 1. HIT THE CORRECT TREE ENDPOINT
        const response = await api.get('/api/finance/chart-of-accounts');

        // 2. HANDLE RAW ARRAY RETURN (Because your controller does return Ok(result.Data))
        var dataArray = null;
        if (Array.isArray(response)) {
            dataArray = response; // Direct array
        } else if (response && response.succeeded && response.data) {
            dataArray = response.data; // Wrapped Result<T> fallback
        }

        if (dataArray) {
            var html = '';
            dataArray.forEach(root => {
                html += buildRow(root, 0);
            });

            if (html === '') {
                html = '<tr><td colspan="5" class="text-center text-muted py-4">No Accounts Found. Create a Root Account to begin.</td></tr>';
            }
            $('#coaTbody').html(html);
        } else {
            $('#coaTbody').html('<tr><td colspan="5" class="text-center text-danger py-4">Error loading data.</td></tr>');
        }
    }

    function buildRow(account, depth) {
        var indent = depth * 25;

        var isFolder = (account.isTransactionAccount === false || account.isTransactionAccount === 'false');
        var icon = isFolder ? '<i class="fa-solid fa-folder text-warning me-2"></i>' : '<i class="fa-solid fa-file-invoice text-secondary me-2"></i>';

        var fwClass = isFolder ? 'fw-semibold text-dark' : 'text-dark';
        var balanceFormatted = parseFloat(account.balance || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        var typeInt = mapTypeStringToInt(account.type);

        // --- 2. RENDER ACTION BUTTONS (FontAwesome) ---
        var actionBtns = `<div class="btn-group shadow-sm">`;

        if (isFolder) {
            // fa-plus for Add
            actionBtns += `<button type="button" class="btn btn-sm btn-light border px-2" title="Add Sub-Account" onclick="coaApp.openCreateDrawer(${account.id}, '${account.name}', ${typeInt})"><i class="fa-solid fa-plus text-primary"></i></button>`;
        }

        // fa-pen-to-square for Edit, fa-trash for Delete
        actionBtns += `<button type="button" class="btn btn-sm btn-light border px-2" title="Edit" onclick="coaApp.editAccount(${account.id})"><i class="fa-solid fa-pen-to-square text-secondary"></i></button>`;
        actionBtns += `<button type="button" class="btn btn-sm btn-light border px-2" title="Delete" onclick="coaApp.deleteAccount(${account.id})"><i class="fa-solid fa-trash-can text-danger"></i></button>`;

        actionBtns += `</div>`;

        var html = `
            <tr class="border-bottom align-middle">
                <td class="${fwClass}" style="padding-left: ${indent + 10}px;">
                    ${icon} ${account.name}
                </td>
                <td class="font-monospace text-muted">${account.code}</td>
                <td><span class="badge bg-light text-dark border">${account.type}</span></td>
                <td class="text-end ${fwClass}">${balanceFormatted}</td>
                <td class="text-end pe-3">
                    ${actionBtns}
                </td>
            </tr>
        `;

        if (account.children && account.children.length > 0) {
            account.children.forEach(child => {
                html += buildRow(child, depth + 1);
            });
        }

        return html;
    }

    function mapTypeStringToInt(typeStr) {
        switch (typeStr) {
            case "Asset": return 1;
            case "Liability": return 2;
            case "Equity": return 3;
            case "Revenue": return 4;
            case "Expense": return 5;
            default: return 1;
        }
    }

    function openCreateDrawer(parentId, parentName, parentType) {
        document.getElementById('createAccountForm').reset();

        if (parentId) {
            $('#ParentAccountId').val(parentId);
            $('#ParentAccountName').val(parentName);
            $('#Type').val(parentType).prop('disabled', true);
        } else {
            $('#ParentAccountId').val('');
            $('#ParentAccountName').val('Root (None)');
            $('#Type').prop('disabled', false).val('1');
        }

        _drawer.show();
    }

    async function saveAccount() {
        var form = $('#createAccountForm')[0];
        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        var payload = {
            code: $('#Code').val(),
            name: $('#Name').val(),
            type: parseInt($('#Type').val()),
            parentAccountId: $('#ParentAccountId').val() ? parseInt($('#ParentAccountId').val()) : null,
            isTransactionAccount: $('#IsTransactionAccount').is(':checked')
        };

        // HIT THE POST ENDPOINT (Assuming it remains /api/finance/accounts)
        const response = await api.post('/api/finance/account', payload);

        if (response && response.succeeded) {
            _drawer.hide();
            loadTree(); // Refresh Grid silently
        }
    }

    function editAccount(id) {
        toastr.info("Edit functionality requires strict Ledger integrity checks. Pending Phase 2.");
    }

    function deleteAccount(id) {
        toastr.warning("Cannot delete an account. Only soft-deactivation is allowed in accounting systems.");
    }

    return {
        init: init,
        openCreateDrawer: openCreateDrawer,
        saveAccount: saveAccount,
        editAccount: editAccount,
        deleteAccount: deleteAccount
    };
})();

$(document).ready(function () {
    coaApp.init();
});