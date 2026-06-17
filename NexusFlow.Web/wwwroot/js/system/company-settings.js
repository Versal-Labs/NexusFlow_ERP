window.companySettings = {
    documentTemplates: [],
    documentTemplateModal: null,

    init: function () {
        const modal = document.getElementById('documentTemplateModal');
        if (modal) {
            this.documentTemplateModal = new bootstrap.Modal(modal);
        }

        const templatesTab = document.getElementById('templates-tab');
        if (templatesTab) {
            templatesTab.addEventListener('shown.bs.tab', () => this.loadDocumentTemplates());
        }

        if (window.location.hash === '#document-templates' && templatesTab) {
            bootstrap.Tab.getOrCreateInstance(templatesTab).show();
        }

        this.loadDocumentTemplates();
    },

    loadDocumentTemplates: async function () {
        const result = await api.get('/api/document-templates');
        this.documentTemplates = Array.isArray(result?.data) ? result.data : [];
        this.renderDocumentTemplates();
    },

    renderDocumentTemplates: function () {
        const body = $('#documentTemplateRows');
        body.empty();

        if (this.documentTemplates.length === 0) {
            body.append('<tr><td colspan="7" class="text-center text-muted py-3">No document templates configured. Fallback PDFs will be used.</td></tr>');
            return;
        }

        this.documentTemplates.forEach(template => {
            body.append(`
                <tr>
                    <td>${this.escape(template.documentTypeName)}</td>
                    <td class="fw-semibold">${this.escape(template.templateName)}</td>
                    <td>${this.escape(template.taxProfileName)}</td>
                    <td><span class="text-muted small">${this.escape(this.shortFileReference(template.blobUrl))}</span></td>
                    <td class="text-center">${template.isDefault ? '<span class="badge bg-success">Default</span>' : `<button class="btn btn-sm btn-outline-success" onclick="companySettings.setDefault(${template.id})">Set Default</button>`}</td>
                    <td class="text-center">
                        <div class="form-check form-switch d-inline-block">
                            <input class="form-check-input" type="checkbox" ${template.isActive ? 'checked' : ''} onchange="companySettings.setActive(${template.id}, this.checked)">
                        </div>
                    </td>
                    <td class="text-end">
                        <button class="btn btn-sm btn-outline-primary me-1" onclick="companySettings.openDocumentTemplateModal(${template.id})"><i class="fa-solid fa-pen"></i></button>
                        <button class="btn btn-sm btn-outline-danger" onclick="companySettings.deleteDocumentTemplate(${template.id})"><i class="fa-solid fa-trash"></i></button>
                    </td>
                </tr>`);
        });
    },

    openDocumentTemplateModal: function (id) {
        const form = document.getElementById('documentTemplateForm');
        form.reset();
        form.classList.remove('was-validated');
        $('#documentTemplateId').val(0);
        $('#documentTemplateIsActive').prop('checked', true);

        const template = this.documentTemplates.find(x => x.id === id);
        if (template) {
            $('#documentTemplateId').val(template.id);
            $('#documentTemplateType').val(template.documentType);
            $('#documentTemplateTaxProfile').val(template.taxProfile);
            $('#documentTemplateName').val(template.templateName);
            $('#documentTemplateIsDefault').prop('checked', template.isDefault);
            $('#documentTemplateIsActive').prop('checked', template.isActive);
        }

        this.documentTemplateModal.show();
    },

    saveDocumentTemplate: async function (event) {
        const form = document.getElementById('documentTemplateForm');
        if (!form.checkValidity()) {
            form.classList.add('was-validated');
            return;
        }

        const id = parseInt($('#documentTemplateId').val()) || 0;
        const fileInput = document.getElementById('documentTemplateFile');
        if (id === 0 && fileInput.files.length === 0) {
            toastr.error('Upload a .docx file for the new document template.');
            return;
        }

        const formData = new FormData();
        formData.append('DocumentType', $('#documentTemplateType').val());
        formData.append('TaxProfile', $('#documentTemplateTaxProfile').val());
        formData.append('TemplateName', $('#documentTemplateName').val());
        formData.append('IsDefault', $('#documentTemplateIsDefault').is(':checked'));
        formData.append('IsActive', $('#documentTemplateIsActive').is(':checked'));
        if (fileInput.files.length > 0) {
            formData.append('TemplateFile', fileInput.files[0]);
        }

        const button = event.currentTarget;
        button.disabled = true;
        const result = await this.sendForm(id > 0 ? `/api/document-templates/${id}` : '/api/document-templates', id > 0 ? 'PUT' : 'POST', formData);
        button.disabled = false;

        if (result?.succeeded) {
            toastr.success(result.message || 'Document template saved.');
            this.documentTemplateModal.hide();
            await this.loadDocumentTemplates();
        }
    },

    setDefault: async function (id) {
        const result = await api.post(`/api/document-templates/${id}/default`, {});
        if (result?.succeeded) {
            toastr.success(result.message || 'Default template updated.');
            await this.loadDocumentTemplates();
        }
    },

    setActive: async function (id, isActive) {
        const result = await api.post(`/api/document-templates/${id}/active`, { isActive });
        if (result?.succeeded) {
            toastr.success(result.message || 'Document template updated.');
            await this.loadDocumentTemplates();
        } else {
            await this.loadDocumentTemplates();
        }
    },

    deleteDocumentTemplate: async function (id) {
        const confirmation = await Swal.fire({
            title: 'Delete document template?',
            text: 'The stored Word file will be removed. Existing documents are not changed.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Delete'
        });
        if (!confirmation.isConfirmed) return;

        const result = await api.delete(`/api/document-templates/${id}`);
        if (result?.succeeded) {
            toastr.success(result.message || 'Document template deleted.');
            await this.loadDocumentTemplates();
        }
    },

    sendForm: async function (url, method, formData) {
        try {
            const response = await fetch(url, {
                method,
                headers: {
                    'Accept': 'application/json',
                    'RequestVerificationToken': api.getToken()
                },
                body: formData
            });

            const result = await response.json().catch(() => null);
            if (!response.ok) {
                const message = this.firstError(result) || `System Error (${response.status})`;
                toastr.error(message);
                return result || { succeeded: false, errors: [message] };
            }

            return result;
        } catch (error) {
            toastr.error('Network connection failed.');
            return { succeeded: false, errors: [error.message] };
        }
    },

    firstError: function (result) {
        if (!result) return null;
        if (Array.isArray(result.errors) && result.errors.length > 0) return result.errors[0];
        if (result.message) return result.message;
        return null;
    },

    shortFileReference: function (value) {
        if (!value) return 'No file';
        const normalized = value.replace(/\\/g, '/');
        const parts = normalized.split('/');
        return parts[parts.length - 1] || value;
    },

    escape: function (value) {
        return $('<div>').text(value || '').html();
    }
};

$(document).ready(() => companySettings.init());
