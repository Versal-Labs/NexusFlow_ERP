const NexusPrint = (function () {
    let currentModal = null;
    let currentPdfUrl = null;
    let documentType = null;
    let documentId = null;
    let documentInfo = null;

    function createModalHtml() {
        return `
        <div class="modal fade" id="nexusPrintModal" tabindex="-1" aria-labelledby="nexusPrintModalLabel" aria-hidden="true">
            <div class="modal-dialog modal-xl modal-dialog-centered">
                <div class="modal-content" style="height: 92vh;">
                    <div class="modal-header py-2">
                        <div>
                            <h5 class="modal-title" id="nexusPrintModalLabel">Document Preview</h5>
                            <div class="small text-muted" id="np_documentNumber"></div>
                        </div>
                        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                    </div>
                    <div class="modal-body p-0 d-flex flex-row overflow-hidden">
                        <div class="p-3 bg-light border-end" style="width: 310px; overflow-y: auto;">
                            <h6 class="mb-3">Print Details</h6>
                            <form id="nexusPrintForm">
                                <div class="mb-3">
                                    <label class="form-label text-muted small">Party Name</label>
                                    <input type="text" class="form-control form-control-sm" id="np_customerName">
                                </div>
                                <div class="mb-3">
                                    <label class="form-label text-muted small">Billing Address</label>
                                    <textarea class="form-control form-control-sm" id="np_billingAddress" rows="3"></textarea>
                                </div>
                                <div class="mb-3">
                                    <label class="form-label text-muted small">Shipping Address</label>
                                    <textarea class="form-control form-control-sm" id="np_shippingAddress" rows="3"></textarea>
                                </div>
                                <div class="mb-3">
                                    <label class="form-label text-muted small">Notes</label>
                                    <textarea class="form-control form-control-sm" id="np_notes" rows="3"></textarea>
                                </div>
                                <button type="button" class="btn btn-primary btn-sm w-100" onclick="NexusPrint.refreshPreview()">
                                    <i class="fa-solid fa-rotate me-1"></i> Refresh Preview
                                </button>
                            </form>
                            <hr>
                            <div class="d-flex align-items-center justify-content-between mb-2">
                                <h6 class="mb-0">Generated History</h6>
                                <button type="button" class="btn btn-sm btn-link p-0" onclick="NexusPrint.loadHistory()" title="Refresh history"><i class="fa-solid fa-rotate"></i></button>
                            </div>
                            <div id="np_history" class="small text-muted">No generated documents.</div>
                        </div>
                        <div class="flex-grow-1 bg-secondary position-relative">
                            <div id="np_loader" class="position-absolute top-50 start-50 translate-middle text-white" style="display:none; z-index:2;">
                                <div class="spinner-border" role="status"><span class="visually-hidden">Loading...</span></div>
                            </div>
                            <iframe id="np_pdfFrame" title="PDF preview" style="width:100%; height:100%; border:0;"></iframe>
                        </div>
                    </div>
                    <div class="modal-footer py-2">
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                        <button type="button" class="btn btn-outline-dark" onclick="NexusPrint.printPdf()"><i class="fa-solid fa-print me-1"></i> Print</button>
                        <button type="button" class="btn btn-success" onclick="NexusPrint.downloadPdf()"><i class="fa-solid fa-file-arrow-down me-1"></i> Download PDF</button>
                    </div>
                </div>
            </div>
        </div>`;
    }

    async function openPreview(type, id) {
        ensureModal();
        documentType = type;
        documentId = String(id);
        documentInfo = null;
        currentModal.show();
        toggleLoader(true);

        try {
            const response = await fetch(`/api/PrintEngine/Initialize/${encodeURIComponent(type)}/${encodeURIComponent(id)}`);
            if (!response.ok) throw new Error(await response.text() || 'Failed to load document.');
            documentInfo = await response.json();
            $('#np_documentNumber').text(documentInfo.documentNumber || '');
            $('#np_customerName').val(documentInfo.customerOrSupplierName || '');
            $('#np_billingAddress').val(documentInfo.billingAddress || '');
            $('#np_shippingAddress').val(documentInfo.shippingAddress || '');
            $('#np_notes').val(documentInfo.notes || '');
            await Promise.all([renderPdf(), loadHistory()]);
        } catch (error) {
            showError(error.message || 'Failed to initialize print preview.');
        } finally {
            toggleLoader(false);
        }
    }

    function ensureModal() {
        if (document.getElementById('nexusPrintModal')) return;
        document.body.insertAdjacentHTML('beforeend', createModalHtml());
        currentModal = new bootstrap.Modal(document.getElementById('nexusPrintModal'));
        document.getElementById('nexusPrintModal').addEventListener('hidden.bs.modal', function () {
            releasePdfUrl();
            document.getElementById('np_pdfFrame').src = '';
        });
    }

    async function refreshPreview() {
        toggleLoader(true);
        try { await renderPdf(); }
        catch (error) { showError(error.message || 'Failed to render PDF.'); }
        finally { toggleLoader(false); }
    }

    async function renderPdf() {
        const response = await postPdf('/api/PrintEngine/Render', 'Preview');
        setPreview(await response.blob());
    }

    async function printPdf() {
        toggleLoader(true);
        try {
            const response = await postPdf('/api/PrintEngine/Finalize', 'Print');
            const blob = await response.blob();
            releasePdfUrl();
            currentPdfUrl = URL.createObjectURL(blob);
            const frame = document.getElementById('np_pdfFrame');
            frame.onload = () => {
                frame.contentWindow.focus();
                frame.contentWindow.print();
                frame.onload = null;
            };
            frame.src = currentPdfUrl;
            await loadHistory();
        } catch (error) { showError(error.message || 'Failed to print document.'); }
        finally { toggleLoader(false); }
    }

    async function downloadPdf() {
        toggleLoader(true);
        try {
            const response = await postPdf('/api/PrintEngine/Finalize', 'Download');
            const blob = await response.blob();
            setPreview(blob);
            const link = document.createElement('a');
            link.href = currentPdfUrl;
            link.download = `${documentInfo.documentType}_${documentInfo.documentNumber}.pdf`;
            document.body.appendChild(link);
            link.click();
            link.remove();
            await loadHistory();
        } catch (error) { showError(error.message || 'Failed to download PDF.'); }
        finally { toggleLoader(false); }
    }

    async function postPdf(url, outputAction) {
        if (!documentType || !documentId) throw new Error('No document selected.');
        const response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getToken() },
            body: JSON.stringify({
                documentType,
                documentId,
                outputAction,
                overrides: {
                    customerOrSupplierName: $('#np_customerName').val(),
                    billingAddress: $('#np_billingAddress').val(),
                    shippingAddress: $('#np_shippingAddress').val(),
                    notes: $('#np_notes').val()
                }
            })
        });
        if (!response.ok) throw new Error(await response.text() || 'PDF generation failed.');
        return response;
    }

    async function loadHistory() {
        if (!documentType || !documentId) return;
        try {
            const response = await fetch(`/api/PrintEngine/History/${encodeURIComponent(documentType)}/${encodeURIComponent(documentId)}`);
            if (!response.ok) throw new Error('History unavailable.');
            const rows = await response.json();
            if (!rows.length) {
                $('#np_history').html('<span class="text-muted">No final output generated yet.</span>');
                return;
            }
            $('#np_history').html(rows.map(row => `
                <a class="d-block text-decoration-none border-bottom py-2" href="/api/PrintEngine/Generated/${row.id}">
                    <span class="fw-semibold text-dark">${escapeHtml(row.outputAction)}</span>
                    ${row.hasOverrides ? '<span class="badge bg-warning text-dark ms-1">Edited</span>' : ''}
                    <br><span class="text-muted">${new Date(row.generatedAtUtc).toLocaleString()}</span>
                    <br><span class="font-monospace text-muted" title="SHA-256">${row.sha256Hash.substring(0, 16)}...</span>
                </a>`).join(''));
        } catch { $('#np_history').html('<span class="text-danger">History unavailable.</span>'); }
    }

    async function printDocument(type, id) {
        await finalizeDirect(type, id, 'Print');
    }

    async function downloadDocument(type, id) {
        await finalizeDirect(type, id, 'Download');
    }

    async function finalizeDirect(type, id, action) {
        try {
            const response = await fetch('/api/PrintEngine/Finalize', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getToken() },
                body: JSON.stringify({ documentType: type, documentId: String(id), outputAction: action, overrides: {} })
            });
            if (!response.ok) throw new Error(await response.text() || 'PDF generation failed.');
            const url = URL.createObjectURL(await response.blob());
            if (action === 'Download') {
                const link = document.createElement('a');
                link.href = url;
                link.download = `${type}-${id}.pdf`;
                document.body.appendChild(link);
                link.click();
                link.remove();
                setTimeout(() => URL.revokeObjectURL(url), 1000);
                return;
            }

            const frame = document.createElement('iframe');
            frame.style.display = 'none';
            frame.onload = () => {
                frame.contentWindow.focus();
                frame.contentWindow.print();
                setTimeout(() => { URL.revokeObjectURL(url); frame.remove(); }, 1000);
            };
            frame.src = url;
            document.body.appendChild(frame);
        } catch (error) { showError(error.message || 'Unable to generate document.'); }
    }

    function setPreview(blob) {
        releasePdfUrl();
        currentPdfUrl = URL.createObjectURL(blob);
        document.getElementById('np_pdfFrame').src = currentPdfUrl;
    }

    function releasePdfUrl() {
        if (!currentPdfUrl) return;
        URL.revokeObjectURL(currentPdfUrl);
        currentPdfUrl = null;
    }

    function toggleLoader(show) {
        const loader = document.getElementById('np_loader');
        const frame = document.getElementById('np_pdfFrame');
        if (loader) loader.style.display = show ? 'block' : 'none';
        if (frame) frame.style.opacity = show ? '0.5' : '1';
    }

    function getToken() {
        if (window.api?.getToken) return window.api.getToken();
        return document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') || '';
    }

    function escapeHtml(value) {
        return $('<div>').text(value || '').html();
    }

    function showError(message) {
        if (typeof toastr !== 'undefined') toastr.error(message);
        else alert(message);
    }

    return { openPreview, refreshPreview, printPdf, downloadPdf, loadHistory, printDocument, downloadDocument };
})();

window.NexusPrint = NexusPrint;
