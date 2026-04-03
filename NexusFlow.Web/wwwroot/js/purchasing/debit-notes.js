window.dnApp = {
    _table: null,
    _viewModal: null,
    _currentDocId: null,

    init: function() {
        var viewEl = document.getElementById('viewDnModal');
        if (viewEl) this._viewModal = new bootstrap.Modal(viewEl);

        this._initFilters();
        this._initGrid();

        $('#btnModalPrint').click(() => window.open(`/api/purchasing/debit-notes/${this._currentDocId}/pdf`, '_blank'));
    },

    _initFilters: async function() {
        try {
            const suppRes = await api.get('/api/supplier');
            let $supp = $('#filterSupplier');
            (suppRes.data || suppRes || []).forEach(c => $supp.append(`<option value="${c.id}">${c.name}</option>`));
            $supp.select2();
        } catch (e) { console.error("Filter load failed", e); }
    },

    resetFilters: function() {
        $('#filterSupplier').val('').trigger('change');
        $('#filterStartDate').val('');
        $('#filterEndDate').val('');
        this.reloadGrid();
    },

    reloadGrid: function() {
        this._table.ajax.reload();
    },

    _initGrid: function() {
        this._table = $('#dnGrid').DataTable({
            ajax: async function(data, callback) {
                try {
                    let sId = $('#filterSupplier').val() || '';
                    let sDate = $('#filterStartDate').val() || '';
                    let eDate = $('#filterEndDate').val() || '';
                    const res = await api.get(`/api/purchasing/debit-notes?supplierId=${sId}&startDate=${sDate}&endDate=${eDate}`);
                    callback({ data: res.data || res || [] });
                } catch (e) { callback({ data: [] }); }
            },
            columns: [
                { data: 'debitNoteNumber', className: 'fw-bold text-primary font-monospace ps-3' },
                { data: 'date', render: d => new Date(d).toLocaleDateString() },
                { data: 'supplierName', className: 'fw-bold text-dark' },
                { data: 'grandTotal', className: 'text-end fw-bold text-primary', render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                {
                    data: null, className: 'text-center pe-3', orderable: false,
                    render: function(data, type, row) {
                        return `<button class="btn btn-sm btn-outline-dark shadow-sm me-1" onclick="dnApp.viewDocument(${row.id})" title="View Details"><i class="fa-solid fa-eye"></i></button>
                                <button class="btn btn-sm btn-outline-secondary shadow-sm" onclick="window.open('/api/purchasing/debit-notes/${row.id}/pdf', '_blank')" title="Print Document"><i class="fa-solid fa-print"></i></button>`;
                    }
                }
            ],
            order: [[0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    viewDocument: async function(id) {
        try {
            this._currentDocId = id;
            const doc = await api.get(`/api/purchasing/debit-notes/${id}`);
            
            $('#docDnNo').text(doc.debitNoteNumber);
            $('#docTotalLarge').text('$' + doc.grandTotal.toLocaleString(undefined, { minimumFractionDigits: 2 }));
            $('#docSupplier').text(doc.supplierName);
            $('#docDate').text(new Date(doc.date).toLocaleDateString());
            $('#docReason').text(doc.reason || 'N/A');

            let tbody = '';
            doc.items.forEach(item => {
            tbody += `
                    <tr>
                        <td class="fw-bold">${item.description}</td>
                        <td class="font-monospace text-muted fs-12">${item.sku}</td>
                        <td class="text-center">${item.qty}</td>
                        <td class="text-end">${item.price.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                        <td class="text-end fw-bold">${item.total.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                    </tr>`;
        });
            $('#docItemsBody').html(tbody);

            $('#docSubtotal').text(doc.subTotal.toLocaleString(undefined, { minimumFractionDigits: 2 }));
            $('#docTax').text(doc.totalTax.toLocaleString(undefined, { minimumFractionDigits: 2 }));
            $('#docTotal').text(doc.grandTotal.toLocaleString(undefined, { minimumFractionDigits: 2 }));

            this._viewModal.show();
        } catch (e) { toastr.error("Failed to load document details."); }
    }
};

$(document).ready(() => dnApp.init());