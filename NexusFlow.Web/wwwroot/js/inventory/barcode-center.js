window.barcodeApp = {
    templates: [],
    items: new Map(),
    templateModal: null,
    canManageTemplates: false,

    init: function () {
        this.canManageTemplates = document.getElementById('barcodeCenter')?.dataset.canManageTemplates === 'true';
        const modal = document.getElementById('barcodeTemplateModal');
        if (modal) this.templateModal = new bootstrap.Modal(modal);

        $('#barcodeTemplateId').on('change', () => {
            this.renderTemplateSummary();
            this.updateGenerateState();
        });

        $('#variantSearch').select2({
            theme: 'bootstrap-5',
            placeholder: 'Search product variants...',
            minimumInputLength: 1,
            ajax: {
                url: '/api/barcode/variants',
                dataType: 'json',
                delay: 250,
                data: params => ({ query: params.term }),
                processResults: data => ({
                    results: (data || []).map(item => ({
                        id: item.id,
                        text: `${item.name} [${item.sku}]`,
                        sku: item.sku,
                        name: item.name
                    }))
                })
            }
        }).on('select2:select', event => {
            this.addItem(event.params.data);
            $('#variantSearch').val(null).trigger('change');
        });

        this.loadTemplates();
    },

    loadTemplates: async function () {
        const selectedId = parseInt($('#barcodeTemplateId').val()) || 0;
        const data = await api.get('/api/barcode/templates');
        this.templates = Array.isArray(data) ? data : [];

        const $select = $('#barcodeTemplateId').empty();
        if (this.templates.length === 0) {
            $select.append('<option value="">No barcode templates configured</option>');
        } else {
            this.templates.forEach(template => {
                $select.append(`<option value="${template.id}">${this.escape(template.name)}${template.isDefault ? ' (Default)' : ''}</option>`);
            });
            const preferred = this.templates.find(x => x.id === selectedId)
                || this.templates.find(x => x.isDefault)
                || this.templates[0];
            $select.val(preferred.id);
        }

        this.renderTemplateSummary();
        this.renderTemplateRows();
        this.updateGenerateState();
    },

    renderTemplateSummary: function () {
        const template = this.selectedTemplate();
        $('#templateSummary').text(template
            ? `${template.pageWidthMM} x ${template.pageHeightMM} mm page, ${template.stickerWidthMM} x ${template.stickerHeightMM} mm sticker, ${template.stickersPerRow} x ${template.rowsPerPage} grid, ${this.symbologyName(template.symbology)}.`
            : 'An administrator must configure exact page and sticker measurements before printing.');
    },

    renderTemplateRows: function () {
        const $body = $('#templateRows');
        if (!$body.length) return;
        $body.empty();

        if (this.templates.length === 0) {
            $body.append('<tr><td colspan="7" class="text-center text-muted py-3">No templates configured.</td></tr>');
            return;
        }

        this.templates.forEach(template => {
            $body.append(`
                <tr>
                    <td class="fw-semibold">${this.escape(template.name)}</td>
                    <td>${template.pageWidthMM} x ${template.pageHeightMM} mm</td>
                    <td>${template.stickerWidthMM} x ${template.stickerHeightMM} mm</td>
                    <td>${template.stickersPerRow} x ${template.rowsPerPage}</td>
                    <td>${this.symbologyName(template.symbology)}</td>
                    <td class="text-center">${template.isDefault ? '<span class="badge bg-success">Default</span>' : ''}</td>
                    <td class="text-end">
                        <button class="btn btn-sm btn-outline-primary me-1" onclick="barcodeApp.openTemplateModal(${template.id})"><i class="fa-solid fa-pen"></i></button>
                        <button class="btn btn-sm btn-outline-danger" onclick="barcodeApp.deleteTemplate(${template.id})"><i class="fa-solid fa-trash"></i></button>
                    </td>
                </tr>`);
        });
    },

    addItem: function (item) {
        const id = parseInt(item.id);
        if (this.items.has(id)) {
            const current = this.items.get(id);
            current.quantity += 1;
        } else {
            this.items.set(id, { id, sku: item.sku, name: item.name, quantity: 1 });
        }
        this.renderItems();
    },

    renderItems: function () {
        const $body = $('#printRows').empty();
        if (this.items.size === 0) {
            $body.append('<tr id="emptyPrintRow"><td colspan="4" class="text-center text-muted py-4">Search for a product variant to add it to the print queue.</td></tr>');
        } else {
            this.items.forEach(item => {
                $body.append(`
                    <tr>
                        <td class="font-monospace fw-semibold">${this.escape(item.sku)}</td>
                        <td>${this.escape(item.name)}</td>
                        <td><input type="number" class="form-control form-control-sm text-center fw-bold" min="1" max="5000" step="1" value="${item.quantity}" onchange="barcodeApp.changeQuantity(${item.id}, this.value)"></td>
                        <td class="text-end"><button class="btn btn-sm text-danger" onclick="barcodeApp.removeItem(${item.id})"><i class="fa-solid fa-trash"></i></button></td>
                    </tr>`);
            });
        }
        this.updateGenerateState();
    },

    changeQuantity: function (id, value) {
        const item = this.items.get(id);
        if (!item) return;
        item.quantity = Math.max(1, Math.floor(Number(value) || 1));
        this.renderItems();
    },

    removeItem: function (id) {
        this.items.delete(id);
        this.renderItems();
    },

    updateGenerateState: function () {
        const total = Array.from(this.items.values()).reduce((sum, item) => sum + item.quantity, 0);
        $('#stickerCount').text(`${total.toLocaleString()} sticker${total === 1 ? '' : 's'}`);
        $('#generateBarcodePdf').prop('disabled', !this.selectedTemplate() || total === 0 || total > 5000);
    },

    generatePdf: async function (event) {
        const template = this.selectedTemplate();
        const total = Array.from(this.items.values()).reduce((sum, item) => sum + item.quantity, 0);
        if (!template || total < 1 || total > 5000) {
            toastr.error('Select a template and keep the print quantity between 1 and 5,000 stickers.');
            return;
        }

        const printWindow = window.open('', '_blank');
        if (!printWindow) {
            toastr.error('Allow pop-ups for this site to open the generated PDF.');
            return;
        }
        printWindow.document.write('<p style="font-family:sans-serif;padding:24px">Generating barcode PDF...</p>');

        const button = event.currentTarget;
        const original = button.innerHTML;
        button.disabled = true;
        button.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Generating...';

        try {
            const response = await fetch('/api/barcode/generate', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': api.getToken()
                },
                body: JSON.stringify({
                    templateId: template.id,
                    items: Array.from(this.items.values()).map(item => ({ productVariantId: item.id, quantity: item.quantity }))
                })
            });

            if (!response.ok) {
                const error = await response.json().catch(() => null);
                throw new Error(error?.errors?.[0] || error?.message || 'Barcode PDF generation failed.');
            }

            const blob = await response.blob();
            const url = URL.createObjectURL(blob);
            printWindow.location.href = url;
            window.setTimeout(() => URL.revokeObjectURL(url), 60000);
        } catch (error) {
            printWindow.close();
            toastr.error(error.message);
        } finally {
            button.innerHTML = original;
            this.updateGenerateState();
        }
    },

    openTemplateModal: function (id) {
        if (!this.templateModal) return;
        document.getElementById('barcodeTemplateForm').reset();
        document.getElementById('barcodeTemplateForm').classList.remove('was-validated');
        $('#templateId').val(0);
        $('#printProductName').prop('checked', true);
        $('#marginTopMM, #marginLeftMM, #horizontalGapMM, #verticalGapMM').val(0);

        const template = this.templates.find(x => x.id === id);
        if (template) {
            Object.entries({
                templateId: template.id,
                templateName: template.name,
                templateSymbology: template.symbology,
                pageWidthMM: template.pageWidthMM,
                pageHeightMM: template.pageHeightMM,
                stickerWidthMM: template.stickerWidthMM,
                stickerHeightMM: template.stickerHeightMM,
                stickersPerRow: template.stickersPerRow,
                rowsPerPage: template.rowsPerPage,
                marginTopMM: template.marginTopMM,
                marginLeftMM: template.marginLeftMM,
                horizontalGapMM: template.horizontalGapMM,
                verticalGapMM: template.verticalGapMM
            }).forEach(([key, value]) => $(`#${key}`).val(value));
            $('#templateIsDefault').prop('checked', template.isDefault);
            $('#printCompanyName').prop('checked', template.printCompanyName);
            $('#printProductName').prop('checked', template.printProductName);
            $('#printPrice').prop('checked', template.printPrice);
            $('#printSizeColor').prop('checked', template.printSizeColor);
        }

        this.templateModal.show();
    },

    saveTemplate: async function (event) {
        const form = document.getElementById('barcodeTemplateForm');
        if (!form.checkValidity()) {
            form.classList.add('was-validated');
            return;
        }

        const id = parseInt($('#templateId').val()) || 0;
        const payload = {
            id,
            name: $('#templateName').val(),
            symbology: parseInt($('#templateSymbology').val()),
            pageWidthMM: this.numberValue('pageWidthMM'),
            pageHeightMM: this.numberValue('pageHeightMM'),
            stickerWidthMM: this.numberValue('stickerWidthMM'),
            stickerHeightMM: this.numberValue('stickerHeightMM'),
            stickersPerRow: this.numberValue('stickersPerRow'),
            rowsPerPage: this.numberValue('rowsPerPage'),
            marginTopMM: this.numberValue('marginTopMM'),
            marginLeftMM: this.numberValue('marginLeftMM'),
            horizontalGapMM: this.numberValue('horizontalGapMM'),
            verticalGapMM: this.numberValue('verticalGapMM'),
            isDefault: $('#templateIsDefault').is(':checked'),
            printCompanyName: $('#printCompanyName').is(':checked'),
            printProductName: $('#printProductName').is(':checked'),
            printPrice: $('#printPrice').is(':checked'),
            printSizeColor: $('#printSizeColor').is(':checked')
        };

        const button = event.currentTarget;
        button.disabled = true;
        const result = id > 0
            ? await api.put(`/api/barcode/templates/${id}`, payload)
            : await api.post('/api/barcode/templates', payload);
        button.disabled = false;

        if (result?.succeeded) {
            toastr.success(result.message);
            this.templateModal.hide();
            await this.loadTemplates();
        }
    },

    deleteTemplate: async function (id) {
        const result = await Swal.fire({
            title: 'Delete barcode template?',
            text: 'Existing PDFs are unaffected, but this template will no longer be available for printing.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Delete'
        });
        if (!result.isConfirmed) return;

        const response = await api.delete(`/api/barcode/templates/${id}`);
        if (response?.succeeded) {
            toastr.success(response.message);
            await this.loadTemplates();
        }
    },

    selectedTemplate: function () {
        const id = parseInt($('#barcodeTemplateId').val());
        return this.templates.find(x => x.id === id) || null;
    },

    symbologyName: function (value) {
        return ({ 1: 'Code128', 2: 'EAN13', 3: 'UPC' })[value] || 'Unknown';
    },

    numberValue: function (id) {
        return Number($(`#${id}`).val()) || 0;
    },

    escape: function (value) {
        return $('<div>').text(value || '').html();
    }
};

$(document).ready(() => barcodeApp.init());
