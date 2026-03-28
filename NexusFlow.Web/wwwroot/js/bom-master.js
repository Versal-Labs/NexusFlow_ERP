// NexusFlow.Web/wwwroot/js/bom-master.js
let bomTable;
const apiUrl = '/api/bom';

$(document).ready(function() {
    initTable();
});

function initTable() {
    bomTable = $('#bomTable').DataTable({
        // Override default DataTables AJAX to route through our global api utility
        ajax: async function(data, callback, settings) {
            const response = await api.get(apiUrl);
            
            if (response && response.succeeded) {
                callback({ data: response.data });
            } else {
                console.error("Failed to load BOMs:", response);
                callback({ data: [] }); // Prevent DataTables from crashing on failure
            }
        },
        columns: [
            { data: 'id' },
            { data: 'name' },
            { data: 'productVariantId' },
            { 
                data: 'isActive',
                render: function(data) {
                    return data ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-danger">Inactive</span>';
                }
            },
            {
                data: 'id',
                render: function(data, type, row) {
                    // Storing object representation in attribute for easy edit binding
                    return `<button class="btn btn-sm btn-outline-primary" onclick='editBom(${JSON.stringify(row)})'>Edit</button>`;
                },
                orderable: false
            }
        ],
        responsive: true
    });
}

function openBomModal() {
    $('#bomForm')[0].reset();
    $('#bomId').val(0);
    $('#bomIsActive').prop('checked', true);
    $('#bomComponentsTable tbody').empty();
    addComponentRow(); // Start with at least one empty row
    
    let modal = new bootstrap.Modal(document.getElementById('bomModal'));
    modal.show();
}

function addComponentRow(materialId = '', quantity = '') {
    const rowId = Date.now();
    const rowHtml = `
        <tr id="compRow_${rowId}">
            <td><input type="number" class="form-control form-control-sm comp-material" value="${materialId}" required /></td>
            <td><input type="number" step="0.0001" class="form-control form-control-sm comp-qty" value="${quantity}" required /></td>
            <td><button type="button" class="btn btn-danger btn-sm w-100" onclick="$('#compRow_${rowId}').remove()">X</button></td>
        </tr>
    `;
    $('#bomComponentsTable tbody').append(rowHtml);
}

function editBom(bom) {
    $('#bomId').val(bom.id);
    $('#bomName').val(bom.name);
    $('#bomFgId').val(bom.productVariantId);
    $('#bomIsActive').prop('checked', bom.isActive);
    
    $('#bomComponentsTable tbody').empty();
    
    if (bom.components && bom.components.length > 0) {
        bom.components.forEach(c => {
            addComponentRow(c.materialVariantId, c.quantity);
        });
    } else {
        addComponentRow(); // Ensure at least one row exists if components are somehow empty
    }

    let modal = new bootstrap.Modal(document.getElementById('bomModal'));
    modal.show();
}

async function saveBom() {
    // 1. Rigorous Client-Side Validation
    if (!$('#bomForm')[0].checkValidity()) {
        $('#bomForm')[0].reportValidity();
        return;
    }

    // 2. Construct Payload
    const payload = {
        id: parseInt($('#bomId').val()) || 0,
        name: $('#bomName').val(),
        productVariantId: parseInt($('#bomFgId').val()),
        isActive: $('#bomIsActive').is(':checked'),
        components: []
    };

    $('#bomComponentsTable tbody tr').each(function() {
        payload.components.push({
            id: 0, // ID resolution happens contextually on the backend
            materialVariantId: parseInt($(this).find('.comp-material').val()),
            quantity: parseFloat($(this).find('.comp-qty').val())
        });
    });

    // 3. Execute via Global API Utility (Handles UI Toasting automatically)
    const response = await api.post(apiUrl, payload);

    if (response && response.succeeded) {
        // Only manually handle the modal state and table reload. 
        // Notifications are handled by the API wrapper.
        let modal = bootstrap.Modal.getInstance(document.getElementById('bomModal'));
        if (modal) {
            modal.hide();
        }
        bomTable.ajax.reload();
    }
} 