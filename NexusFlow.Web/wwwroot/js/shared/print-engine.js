const NexusPrint = (function () {
    let currentModal = null;
    let currentPdfUrl = null;

    function createModalHtml() {
        return `
        <div class="modal fade" id="nexusPrintModal" tabindex="-1" aria-labelledby="nexusPrintModalLabel" aria-hidden="true">
            <div class="modal-dialog modal-xl modal-dialog-centered">
                <div class="modal-content" style="height: 90vh;">
                    <div class="modal-header">
                        <h5 class="modal-title" id="nexusPrintModalLabel">Print Preview</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                    </div>
                    <div class="modal-body p-0 d-flex flex-row">
                        <!-- Sidebar for Overrides -->
                        <div class="p-3 bg-light border-end" style="width: 300px; overflow-y: auto;">
                            <h6>Document Overrides</h6>
                            <form id="nexusPrintForm">
                                <div class="mb-3">
                                    <label class="form-label text-muted small">Customer Name</label>
                                    <input type="text" class="form-control form-control-sm" name="CustomerOrSupplierName" id="np_customerName">
                                </div>
                                <div class="mb-3">
                                    <label class="form-label text-muted small">Billing Address</label>
                                    <textarea class="form-control form-control-sm" name="BillingAddress" id="np_billingAddress" rows="3"></textarea>
                                </div>
                                <div class="mb-3">
                                    <label class="form-label text-muted small">Shipping Address</label>
                                    <textarea class="form-control form-control-sm" name="ShippingAddress" id="np_shippingAddress" rows="3"></textarea>
                                </div>
                                <div class="mb-3">
                                    <label class="form-label text-muted small">Notes</label>
                                    <textarea class="form-control form-control-sm" name="Notes" id="np_notes" rows="3"></textarea>
                                </div>
                                <button type="button" class="btn btn-primary btn-sm w-100 mt-2" onclick="NexusPrint.refreshPreview()">Refresh Preview</button>
                            </form>
                        </div>
                        <!-- PDF Preview Iframe -->
                        <div class="flex-grow-1 bg-secondary position-relative">
                            <div id="np_loader" class="position-absolute top-50 start-50 translate-middle text-white" style="display: none;">
                                <div class="spinner-border" role="status">
                                    <span class="visually-hidden">Loading...</span>
                                </div>
                            </div>
                            <iframe id="np_pdfFrame" style="width: 100%; height: 100%; border: none;"></iframe>
                        </div>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                        <button type="button" class="btn btn-success" onclick="NexusPrint.downloadPdf()">Download PDF</button>
                    </div>
                </div>
            </div>
        </div>`;
    }

    let dtoCache = null;

    async function openPreview(documentType, documentId) {
        // Ensure modal exists in DOM
        if (!document.getElementById('nexusPrintModal')) {
            document.body.insertAdjacentHTML('beforeend', createModalHtml());
            currentModal = new bootstrap.Modal(document.getElementById('nexusPrintModal'));
            
            document.getElementById('nexusPrintModal').addEventListener('hidden.bs.modal', function () {
                if (currentPdfUrl) {
                    URL.revokeObjectURL(currentPdfUrl);
                    currentPdfUrl = null;
                }
                document.getElementById('np_pdfFrame').src = '';
            });
        }

        currentModal.show();
        toggleLoader(true);

        try {
            // Fetch initial data
            const response = await fetch(`/api/PrintEngine/Initialize/${documentType}/${encodeURIComponent(documentId)}`);
            if (!response.ok) throw new Error(await response.text() || 'Failed to fetch document data');
            
            dtoCache = await response.json();
            
            // Populate form
            document.getElementById('np_customerName').value = dtoCache.customerOrSupplierName || '';
            document.getElementById('np_billingAddress').value = dtoCache.billingAddress || '';
            document.getElementById('np_shippingAddress').value = dtoCache.shippingAddress || '';
            document.getElementById('np_notes').value = dtoCache.notes || '';

            await renderPdf();
        } catch (error) {
            console.error('Print initialization failed:', error);
            showError(error.message || 'Failed to initialize print preview.');
        } finally {
            toggleLoader(false);
        }
    }

    async function refreshPreview() {
        if (!dtoCache) return;

        toggleLoader(true);
        
        // Update DTO with form overrides
        dtoCache.customerOrSupplierName = document.getElementById('np_customerName').value;
        dtoCache.billingAddress = document.getElementById('np_billingAddress').value;
        dtoCache.shippingAddress = document.getElementById('np_shippingAddress').value;
        dtoCache.notes = document.getElementById('np_notes').value;

        try {
            await renderPdf();
        } catch (error) {
            console.error('Failed to refresh preview:', error);
            showError(error.message || 'Failed to render PDF.');
        } finally {
            toggleLoader(false);
        }
    }

    async function renderPdf() {
        const response = await fetch('/api/PrintEngine/Render', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getToken()
            },
            body: JSON.stringify(dtoCache)
        });

        if (!response.ok) throw new Error(await response.text() || 'Render failed');

        const blob = await response.blob();
        
        if (currentPdfUrl) {
            URL.revokeObjectURL(currentPdfUrl);
        }
        
        currentPdfUrl = URL.createObjectURL(blob);
        document.getElementById('np_pdfFrame').src = currentPdfUrl;
    }

    function downloadPdf() {
        if (!currentPdfUrl || !dtoCache) return;
        const a = document.createElement('a');
        a.href = currentPdfUrl;
        a.download = `${dtoCache.documentType}_${dtoCache.documentNumber}.pdf`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    }

    function toggleLoader(show) {
        document.getElementById('np_loader').style.display = show ? 'block' : 'none';
        document.getElementById('np_pdfFrame').style.opacity = show ? '0.5' : '1';
    }

    function getToken() {
        if (window.api?.getToken) return window.api.getToken();
        return document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') || '';
    }

    function showError(message) {
        if (typeof toastr !== 'undefined') {
            toastr.error(message);
            return;
        }

        alert(message);
    }

    return {
        openPreview,
        refreshPreview,
        downloadPdf
    };
})();

window.NexusPrint = NexusPrint;
