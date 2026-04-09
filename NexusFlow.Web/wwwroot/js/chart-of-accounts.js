var coaApp = (function () {
    "use strict";

    var _drawer;
    var _allAccountsData = [];

    // LKR Currency Formatter
    const lkrFormatter = new Intl.NumberFormat('en-LK', {
        style: 'currency',
        currency: 'LKR',
        minimumFractionDigits: 2
    });

    function init() {
        _drawer = new bootstrap.Offcanvas(document.getElementById('accountDrawer'));
        loadTree();
    }

    async function loadTree() {
        $('#coaTbody').html('<tr><td colspan="6" class="text-center text-muted py-4"><div class="spinner-border spinner-border-sm me-2"></div>Loading Ledger...</td></tr>');

        const dataArray = await api.get('/api/finance/chart-of-accounts');
        _allAccountsData = dataArray; // Store for local access during edit

        if (dataArray && dataArray.length > 0) {
            var html = '';
            dataArray.forEach(root => {
                html += buildRow(root, 0);
            });
            $('#coaTbody').html(html);
        } else {
            $('#coaTbody').html('<tr><td colspan="6" class="text-center text-muted py-4">No Accounts Found. Create a Root Account.</td></tr>');
        }
    }

    function buildRow(account, depth) {
        var indent = depth * 25;
        var isFolder = !account.isTransactionAccount;
        
        var icon = isFolder ? '<i class="fa-solid fa-folder text-warning me-2"></i>' : '<i class="fa-solid fa-file-invoice text-secondary me-2"></i>';
        var fwClass = isFolder ? 'fw-bold text-dark' : 'text-dark';
        
        // Formatted LKR Balance
        var balanceFormatted = lkrFormatter.format(account.balance || 0);
        
        // Status Badges
        // Status Badges
        var statusHtml = '';
        if (account.isSystemAccount) {
            statusHtml += '<span class="badge bg-primary ms-1" title="System Control Account"><i class="fa-solid fa-lock"></i> SYS</span>';
        }

        if (!account.isActive) {
            statusHtml += '<span class="badge bg-danger ms-1">INACTIVE</span>';
        } else {
            // Adding the explicit ACTIVE badge
            statusHtml += '<span class="badge bg-success-subtle text-success border border-success-subtle ms-1">ACTIVE</span>';
        }

        var typeInt = mapTypeStringToInt(account.type);

        var actionBtns = `<div class="btn-group shadow-sm">`;
        if (isFolder && account.isActive) {
            actionBtns += `<button type="button" class="btn btn-sm btn-light border px-2" title="Add Sub-Account" onclick="coaApp.openDrawer(0, ${account.id}, '${account.name.replace("'", "\\'")}', ${typeInt})"><i class="fa-solid fa-plus text-primary"></i></button>`;
        }
        
        // Allow Edit always. Delete (Deactivate) only if active and not system.
        actionBtns += `<button type="button" class="btn btn-sm btn-light border px-2" title="Edit" onclick="coaApp.openDrawer(${account.id})"><i class="fa-solid fa-pen-to-square text-secondary"></i></button>`;
        
        if (account.isActive && !account.isSystemAccount) {
            actionBtns += `<button type="button" class="btn btn-sm btn-light border px-2" title="Deactivate" onclick="coaApp.deactivateAccount(${account.id})"><i class="fa-solid fa-ban text-danger"></i></button>`;
        }
        actionBtns += `</div>`;

        var html = `
            <tr class="border-bottom align-middle ${!account.isActive ? 'table-light text-muted' : ''}">
                <td class="${fwClass}" style="padding-left: ${indent + 10}px;">
                    ${icon} ${account.name}
                </td>
                <td class="font-monospace">${account.code}</td>
                <td><span class="badge bg-light text-dark border">${account.type}</span></td>
                <td class="text-center">${statusHtml}</td>
                <td class="text-end ${fwClass} font-monospace">${balanceFormatted}</td>
                <td class="text-end pe-3">${actionBtns}</td>
            </tr>
        `;

        if (account.children && account.children.length > 0) {
            account.children.forEach(child => {
                html += buildRow(child, depth + 1);
            });
        }
        return html;
    }

    function findAccountInTree(tree, id) {
        for (let node of tree) {
            if (node.id === id) return node;
            if (node.children) {
                let found = findAccountInTree(node.children, id);
                if (found) return found;
            }
        }
        return null;
    }

    function openDrawer(id = 0, parentId = null, parentName = 'Root (None)', parentType = 1) {
        document.getElementById('accountForm').reset();
        
        if (id === 0) {
            // CREATE MODE
            $('#drawerTitle').text('New Account');
            $('#AccountId').val(0);
            $('#ParentAccountId').val(parentId || '');
            $('#ParentAccountName').val(parentName);
            $('#Type').val(parentType).prop('disabled', parentId !== null);
            $('#IsTransactionAccount').prop('disabled', false).prop('checked', true);
            $('#TypeHelpText').text(parentId ? 'Inherited from parent account.' : 'Select root classification.');
        } else {
            // EDIT MODE
            var acc = findAccountInTree(_allAccountsData, id);
            if(!acc) return;

            $('#drawerTitle').text('Edit Account');
            $('#AccountId').val(acc.id);
            $('#Code').val(acc.code).prop('disabled', acc.isSystemAccount);
            $('#Name').val(acc.name);
            $('#Type').val(mapTypeStringToInt(acc.type)).prop('disabled', true); // Cannot change type on edit
            $('#IsTransactionAccount').prop('checked', acc.isTransactionAccount).prop('disabled', true); // Cannot toggle transaction status easily
            $('#RequiresReconciliation').prop('checked', acc.requiresReconciliation);
            $('#ParentAccountId').val(acc.parentAccountId || '');
            $('#TypeHelpText').text('Account Type and Transaction Flag cannot be changed after creation.');
        }

        _drawer.show();
    }

    async function saveAccount() {
        var form = $('#accountForm')[0];
        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        var id = parseInt($('#AccountId').val());
        var isEdit = id > 0;

        var payload = {
            id: id,
            code: $('#Code').val(),
            name: $('#Name').val(),
            type: parseInt($('#Type').val()),
            parentAccountId: $('#ParentAccountId').val() ? parseInt($('#ParentAccountId').val()) : null,
            isTransactionAccount: $('#IsTransactionAccount').is(':checked'),
            requiresReconciliation: $('#RequiresReconciliation').is(':checked')
        };

        let response;
        if (isEdit) {
            response = await api.put(`/api/finance/account/${id}`, payload);
        } else {
            response = await api.post('/api/finance/account', payload);
        }

        if (response && response.succeeded) {
            toastr.success(response.message[0]);
            _drawer.hide();
            loadTree(); 
        } else {
            toastr.error(response?.message[0] || "Operation failed.");
        }
    }

    async function deactivateAccount(id) {
        if(confirm("Are you sure you want to deactivate this account? Ensure the LKR balance is 0.00 first.")) {
            const response = await api.delete(`/api/finance/account/${id}`);
            if (response && response.succeeded) {
                toastr.success("Account deactivated.");
                loadTree();
            } else {
                toastr.error(response?.message[0] || "Deactivation failed.");
            }
        }
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

    return {
        init: init,
        openDrawer: openDrawer,
        saveAccount: saveAccount,
        deactivateAccount: deactivateAccount
    };
})();

$(document).ready(function () {
    coaApp.init();
});