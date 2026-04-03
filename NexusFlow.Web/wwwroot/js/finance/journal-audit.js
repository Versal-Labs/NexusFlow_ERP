window.journalApp = {
    _table: null,
    _modal: null,
    _currentRefNo: null,
    _currentModule: null,

    init: function () {
        var modalEl = document.getElementById('journalModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl);

        // Default to current month for performance
        let today = new Date();
        let firstDay = new Date(today.getFullYear(), today.getMonth(), 1);
        $('#filterStartDate').val(firstDay.toISOString().split('T')[0]);
        $('#filterEndDate').val(today.toISOString().split('T')[0]);

        this._initGrid();
    },

    resetFilters: function() {
        $('#filterModule').val('');
        $('#filterStartDate').val('');
        $('#filterEndDate').val('');
        this.reloadGrid();
    },

    reloadGrid: function() {
        this._table.ajax.reload();
    },

    _initGrid: function () {
        this._table = $('#journalGrid').DataTable({
            ajax: {
                url: '/api/finance/journals',
                data: function (d) {
                    d.module = $('#filterModule').val();
                    d.startDate = $('#filterStartDate').val();
                    d.endDate = $('#filterEndDate').val();
                },
                dataSrc: function (json) { return json.data || json || []; }
            },
            columns: [
                { data: 'date', render: d => new Date(d).toLocaleDateString() },
                { data: 'referenceNo', className: 'fw-bold text-primary font-monospace' },
                { 
                    data: 'module', 
                    render: function(d) {
                        let color = 'bg-secondary';
                        if(d === 'Sales') color = 'bg-success';
                        if(d === 'Purchasing') color = 'bg-danger';
                        if(d === 'Treasury') color = 'bg-info text-dark';
                        if(d === 'Inventory') color = 'bg-warning text-dark';
                        return `<span class="badge ${color}">${d}</span>`;
                    }
                },
                { data: 'description', className: 'text-muted text-truncate', width: '30%' },
                { data: 'totalAmount', className: 'text-end fw-bold', render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                {
                    data: null,
                    className: 'text-end pe-3',
                    orderable: false,
                    render: function (data, type, row) {
                        return `<button class="btn btn-sm btn-dark shadow-sm" onclick="journalApp.viewAudit(${row.id})">
                                    <i class="fa-solid fa-magnifying-glass"></i> Inspect
                                </button>`;
                    }
                }
            ],
            order: [[0, 'desc']], // Sort by Date Descending
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 25
        });
    },

    viewAudit: async function(id) {
        try {
            const doc = await api.get(`/api/finance/journals/${id}`); 
            
            this._currentRefNo = doc.referenceNo;
            this._currentModule = doc.module;

            $('#lblRefNo').text(doc.referenceNo);
            $('#lblDate').text(new Date(doc.date).toLocaleDateString());
            $('#lblModule').text(doc.module);
            $('#lblDesc').text(doc.description);

            // Show/Hide Source Button based on module
            if(doc.module === 'Manual') $('#btnViewSource').hide();
            else $('#btnViewSource').show();

            let tbody = '';
            let totalDr = 0;
            let totalCr = 0;

            if (doc.lines) {
                doc.lines.forEach(l => {
                    let dr = parseFloat(l.debit || 0);
                    let cr = parseFloat(l.credit || 0);
                    totalDr += dr;
                    totalCr += cr;

                    tbody += `
                        <tr>
                            <td class="font-monospace text-primary">${l.accountCode}</td>
                            <td class="fw-bold">${l.accountName}</td>
                            <td class="text-muted fs-12">${l.description || '-'}</td>
                            <td class="text-end fw-bold ${dr > 0 ? 'text-success' : 'text-muted'}">${dr > 0 ? dr.toLocaleString(undefined, { minimumFractionDigits: 2 }) : '-'}</td>
                            <td class="text-end fw-bold ${cr > 0 ? 'text-danger' : 'text-muted'}">${cr > 0 ? cr.toLocaleString(undefined, { minimumFractionDigits: 2 }) : '-'}</td>
                        </tr>`;
                });
            }
            $('#journalLinesBody').html(tbody);
            
            $('#lblTotalDebit').text(totalDr.toLocaleString(undefined, { minimumFractionDigits: 2 }));
            $('#lblTotalCredit').text(totalCr.toLocaleString(undefined, { minimumFractionDigits: 2 }));

            this._modal.show();
        } catch (e) {
            toastr.error("Failed to load journal details.");
            console.error(e);
        }
    },

    // SMART ROUTING TO SOURCE DOCUMENTS
    viewSourceDocument: function() {
        if(!this._currentRefNo) return;

        // Example: INV-0001, GRN-0050, RCPT-0099
        if (this._currentModule === 'Sales' && this._currentRefNo.startsWith('INV')) {
            // Note: In a true SPA, this would pop the Viewer Modal. 
            // In MVC, we might navigate to the Sales Invoices page with a query parameter to auto-open it.
            window.location.href = `/Sales/Invoices?viewRef=${this._currentRefNo}`;
        } 
        else if (this._currentModule === 'Purchasing' && this._currentRefNo.startsWith('GRN')) {
            window.location.href = `/Purchasing/GoodsReceipts?viewRef=${this._currentRefNo}`;
        }
        else {
            toastr.info(`Source document routing for ${this._currentModule} is under construction.`);
        }
    }
};

$(document).ready(() => journalApp.init());