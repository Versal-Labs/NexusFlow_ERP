window.barcodeTemplatePage = {
    templates: [],
    templateModal: null,

    init: function () {
        const modal = document.getElementById('barcodeTemplateModal');
        if (modal) this.templateModal = new bootstrap.Modal(modal);
        this.loadTemplates();
    },

    loadTemplates: async function () {
        const data = await api.get('/api/barcode/templates');
        this.templates = Array.isArray(data) ? data : [];
        this.renderTemplateRows();
    },

    renderTemplateRows: function () {
        const $body = $('#templateRows');
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
                        <button class="btn btn-sm btn-outline-primary me-1" onclick="barcodeTemplatePage.openTemplateModal(${template.id})"><i class="fa-solid fa-pen"></i></button>
                        <button class="btn btn-sm btn-outline-danger" onclick="barcodeTemplatePage.deleteTemplate(${template.id})"><i class="fa-solid fa-trash"></i></button>
                    </td>
                </tr>`);
        });
    },

    openTemplateModal: function (id) {
        if (!this.templateModal) return;
        const form = document.getElementById('barcodeTemplateForm');
        form.reset();
        form.classList.remove('was-validated');
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

$(document).ready(() => barcodeTemplatePage.init());
