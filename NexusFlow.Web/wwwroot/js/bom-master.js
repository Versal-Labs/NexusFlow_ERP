window.bomApp = {
    _table: null,
    _modal: null,
    _headers: { 'Authorization': `Bearer ${localStorage.getItem('nexus_token')}` },

    init: function () {
        var modalEl = document.getElementById('bomModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl);

        this._initGrid();

        // Event Bindings
        $('#addRmBtn').on('click', () => this.appendComponentRow());
        
        $(document).on('click', '.remove-rm', function() {
            $(this).closest('tr').remove();
        });

        $('#btnSaveBom').on('click', (e) => this.save(e));
    },

    _initGrid: function () {
        this._table = $('#bomTable').DataTable({
            ajax: { 
                url: '/api/bom', 
                headers: this._headers,
                // Handles standard array OR wrapped { data: [...] } responses
                dataSrc: function (json) { return json.data || json || []; } 
            },
            columns: [
                { data: 'id' },
                { data: 'name', className: 'fw-bold' },
                { data: 'productVariantName' },
                { data: 'componentCount' },
                { 
                    data: 'isActive',
                    render: d => d 
                        ? '<span class="badge bg-success">Active</span>' 
                        : '<span class="badge bg-secondary">Inactive</span>'
                },
                { 
                    data: 'id',
                    className: 'text-end',
                    render: id => `<button class="btn btn-sm btn-outline-primary shadow-sm" onclick="window.bomApp.editBom(${id})"><i class="bi bi-pencil"></i> Edit</button>`
                }
            ],
            dom: '<"row"<"col-sm-12 col-md-6"f><"col-sm-12 col-md-6 text-end"B>>rt<"row"<"col-sm-12 col-md-5"i><"col-sm-12 col-md-7"p>>',
            pageLength: 20,
            language: { search: "", searchPlaceholder: "Search BOMs..." }
        });
    },

    initSelect2ForRow: function (element, productType) {
        element.select2({
            theme: 'bootstrap-5',
            dropdownParent: $('#bomModal'),
            placeholder: 'Search Variant...',
            ajax: {
                url: '/api/masterdata/variants/search',
                headers: this._headers,
                dataType: 'json',
                delay: 250,
                // Strict Domain Guard: Filters by ProductType (0 = RM, 1 = FG)
                data: params => ({ query: params.term, productType: productType }),
                processResults: data => ({
                    results: $.map(data, item => ({ id: item.id, text: `${item.sku} - ${item.name}` }))
                })
            }
        });
    },

    appendComponentRow: function (materialId = '', materialName = '', qty = '') {
        const rowId = 'rm_' + Date.now() + Math.floor(Math.random() * 1000);
        const rowHtml = `
            <tr>
                <td>
                    <select class="form-select form-select-sm variant-select" data-rowid="${rowId}" required>
                        ${materialId ? `<option value="${materialId}" selected>${materialName}</option>` : ''}
                    </select>
                </td>
                <td>
                    <input type="number" step="0.001" min="0.001" class="form-control form-control-sm qty-input" value="${qty}" placeholder="0.000" required>
                </td>
                <td class="text-center">
                    <button type="button" class="btn btn-danger btn-sm remove-rm"><i class="bi bi-trash"></i></button>
                </td>
            </tr>`;
        $('#rmList').append(rowHtml);
        
        // 0 = RawMaterial enum mapping
        this.initSelect2ForRow($(`select[data-rowid="${rowId}"]`), 0); 
    },

    openCreateModal: function () {
        $('#bomForm')[0].reset();
        $('#bomId').val(0);
        $('#rmList').empty();
        $('#productVariantId').empty().trigger('change');
        
        // 1 = FinishedGood enum mapping
        this.initSelect2ForRow($('#productVariantId'), 1); 
        this._modal.show();
    },

    editBom: async function (id) {
        try {
            // Utilizing your custom API wrapper
            const res = await api.get(`/api/bom/${id}`);
            const bom = res.data || res; // Accommodate standard or wrapped responses
            
            $('#bomId').val(bom.id);
            $('#bomName').val(bom.name);
            $('#isActive').prop('checked', bom.isActive);
            
            $('#productVariantId').empty().append(new Option(bom.productVariantName, bom.productVariantId, true, true)).trigger('change');
            this.initSelect2ForRow($('#productVariantId'), 1);

            $('#rmList').empty();
            bom.components.forEach(c => this.appendComponentRow(c.materialVariantId, c.materialVariantName, c.quantity));
            
            this._modal.show();
        } catch (e) {
            toastr.error("Failed to load BOM details.");
            console.error(e);
        }
    },

    save: async function (e) {
        if (!$('#bomForm')[0].checkValidity()) {
            $('#bomForm')[0].reportValidity();
            return;
        }

        const payload = {
            id: parseInt($('#bomId').val()) || 0,
            name: $('#bomName').val(),
            productVariantId: parseInt($('#productVariantId').val()),
            isActive: $('#isActive').is(':checked'),
            components: []
        };

        $('#rmList tr').each(function() {
            payload.components.push({
                materialVariantId: parseInt($(this).find('.variant-select').val()),
                quantity: parseFloat($(this).find('.qty-input').val())
            });
        });

        if (payload.components.length === 0) {
            toastr.warning("A BOM must contain at least one raw material component.");
            return;
        }

        // Button Loading State
        var $btn = $(e.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm"></i> Saving...');

        try {
            // Utilizing your custom API wrapper. Nesting inside 'payload' to match the CQRS Command structure
            const res = await api.post('/api/bom', { payload: payload }); 
            
            if (res && res.succeeded) {
                toastr.success(res.message || "Bill of Materials saved successfully.");
                this._modal.hide();
                this._table.ajax.reload(null, false);
            } else if (res && res.messages) {
                // Handle wrapper failure messages
                toastr.error(res.messages[0]);
            }
        } catch (err) {
            // Handle HTTP errors
            const errorMessage = err.responseJSON?.messages?.[0] || err.message || "Failed to save BOM. Check constraints.";
            toastr.error(errorMessage);
            console.error(err);
        } finally {
            $btn.prop('disabled', false).html(ogText);
        }
    }
};

// Initialize Application on DOM Ready
$(document).ready(() => window.bomApp.init());