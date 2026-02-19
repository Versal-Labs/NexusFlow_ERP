/**
 * Nexus ERP - GRN Wizard
 * Handles the logic for receiving items against a PO.
 */
var grnApp = (function () {
    "use strict";

    var drawer;
    var _poData = null; // Stores the full PO object

    var init = function () {
        var el = document.getElementById('grnDrawer');
        if (el) drawer = new bootstrap.Offcanvas(el);
    };

    // Called when user clicks "Receive" on the main grid
    var openWizard = async function (poId) {
        try {
            // 1. Fetch PO Details
            const res = await api.get(`/api/Purchasing/${poId}`);
            if (!res || !res.succeeded) {
                toastr.error("Could not load PO details.");
                return;
            }

            _poData = res.data;

            // 2. Setup UI
            $('#lblGrnPoNumber').text(`Receiving against: ${_poData.poNumber}`);
            $('#hdnGrnPoId').val(_poData.id);
            document.getElementById('txtGrnDate').valueAsDate = new Date(); // Default Today
            $('#txtSupInvoice').val('');

            // 3. Render Items
            _renderItems();

            drawer.show();

        } catch (e) {
            console.error("GRN Load Error", e);
            toastr.error("Network error while loading PO.");
        }
    };

    var _renderItems = function () {
        const tbody = $('#grnItemsBody');
        tbody.empty();

        let allCompleted = true;

        _poData.items.forEach((item, index) => {
            // Calculate what is left to receive
            const remaining = item.quantityOrdered - item.quantityReceived;

            // If remaining is 0, we still show it but disabled, for context
            const isCompleted = remaining <= 0;
            if (!isCompleted) allCompleted = false;

            // Default 'Receive Now' to the remaining amount
            const defaultReceive = isCompleted ? 0 : remaining;

            const html = `
                <tr class="${isCompleted ? 'bg-light text-muted' : ''}">
                    <td>
                        <div class="fw-bold">${item.productName}</div>
                        <div class="small font-monospace">${item.sku}</div>
                    </td>
                    <td class="text-center">${item.quantityOrdered}</td>
                    <td class="text-center">${item.quantityReceived}</td>
                    
                    <td class="bg-success-subtle">
                        <input type="number" class="form-control form-control-sm text-center fw-bold text-success grn-qty" 
                               data-index="${index}"
                               value="${defaultReceive}" 
                               min="0" 
                               max="${remaining}"
                               ${isCompleted ? 'disabled' : ''}>
                    </td>

                    <td class="text-end">
                         <input type="number" class="form-control form-control-sm text-end text-muted grn-cost" 
                               data-index="${index}"
                               value="${item.unitCost}" 
                               ${isCompleted ? 'disabled' : ''}>
                    </td>
                </tr>
            `;
            tbody.append(html);
        });

        if (allCompleted) {
            tbody.html('<tr><td colspan="5" class="text-center p-4 text-success"><i class="bi bi-check-circle display-4"></i><br>This PO is fully received.</td></tr>');
            // Disable submit button logic could go here
        }
    };

    var submitGrn = async function () {
        const poId = $('#hdnGrnPoId').val();
        const warehouseId = $('#ddlWarehouse').val();

        // Build the payload from the table inputs
        const items = [];
        let hasError = false;

        $('.grn-qty').each(function () {
            const qtyInput = $(this);
            const index = qtyInput.data('index');
            const qtyToReceive = parseFloat(qtyInput.val()) || 0;

            if (qtyToReceive > 0) {
                // Find corresponding cost input
                const costInput = $(`.grn-cost[data-index="${index}"]`);
                const unitCost = parseFloat(costInput.val());

                // Validation
                const originalItem = _poData.items[index];
                const remaining = originalItem.quantityOrdered - originalItem.quantityReceived;

                if (qtyToReceive > remaining) {
                    toastr.error(`Cannot receive ${qtyToReceive} for ${originalItem.sku}. Max remaining is ${remaining}.`);
                    qtyInput.addClass('is-invalid');
                    hasError = true;
                    return false; // Break loop
                }

                items.push({
                    ProductVariantId: originalItem.productVariantId,
                    QuantityReceived: qtyToReceive,
                    UnitCost: unitCost
                });
            }
        });

        if (hasError) return;
        if (items.length === 0) {
            toastr.warning("Please enter a quantity to receive for at least one item.");
            return;
        }

        // Payload matches CreateGrnCommand
        const payload = {
            PurchaseOrderId: parseInt(poId),
            WarehouseId: parseInt(warehouseId),
            DateReceived: $('#txtGrnDate').val(),
            SupplierInvoiceNo: $('#txtSupInvoice').val(),
            Items: items
        };

        const res = await api.post('/api/Purchasing/grn', payload);

        if (res && res.succeeded) {
            toastr.success("Stock Received Successfully!");
            drawer.hide();
            // Reload the PO grid to show updated status
            if (window.poApp) window.poApp.refreshGrid(); // Need to expose this in purchase-order.js
        }
    };

    return { init, openWizard, submitGrn };
})();

$(document).ready(function () { grnApp.init(); });