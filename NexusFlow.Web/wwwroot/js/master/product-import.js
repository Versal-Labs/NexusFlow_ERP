/**
 * Nexus ERP - Migration Wizard
 * Handles client-side validation against Master Data before submission.
 */
var importApp = (function () {

    // Cache Master Data for Validation
    let _masterData = { brands: [], categories: [], units: [] };
    let _stagingData = []; // The rows from the CSV
    let _modal;

    // Init
    function init() {
        const el = document.getElementById('importModal');
        if (el) _modal = new bootstrap.Modal(el);

        // Setup File Reader
        $('#fileUpload').on('change', function (e) {
            _handleFile(e.target.files[0]);
        });
    }

    function openWizard() {
        _loadMasterData().then(() => {
            _modal.show();
            _resetUI();
        });
    }

    async function _loadMasterData() {
        // Fetch fresh lists to validate against
        const [b, c, u] = await Promise.all([
            api.get('/api/Brand'),
            api.get('/api/Category'),
            api.get('/api/UnitOfMeasure')
        ]);
        _masterData.brands = b.data;
        _masterData.categories = c.data;
        _masterData.units = u.data;
    }

    // --- Step 1: Parse File (Client Side CSV Parse) ---
    function _handleFile(file) {
        if (!file) return;

        const reader = new FileReader();
        reader.onload = function (e) {
            const text = e.target.result;
            _parseCSV(text);
        };
        reader.readAsText(file);
    }

    function _parseCSV(csvText) {
        // Simple Parser (Assumes: Name,Brand,Category,Unit,Price)
        const lines = csvText.split('\n');
        const headers = lines[0].split(','); // Naive split

        _stagingData = [];

        for (let i = 1; i < lines.length; i++) {
            if (!lines[i].trim()) continue;
            const cols = lines[i].split(',');

            // Map CSV columns to Object
            _stagingData.push({
                id: i, // temp id
                name: cols[0]?.trim(),
                brand: cols[1]?.trim(),
                category: cols[2]?.trim(),
                unit: cols[3]?.trim(),
                price: parseFloat(cols[4]) || 0,
                isValid: true,
                errors: []
            });
        }

        _validateData();
        _renderValidationGrid();
        _showStep2();
    }

    // --- Step 2: Validate Logic ---
    function _validateData() {
        let errorCount = 0;

        _stagingData.forEach(row => {
            row.errors = [];
            row.matchedBrandId = null;
            row.matchedCatId = null;
            row.matchedUnitId = null;

            // 1. Validate Brand
            const brand = _masterData.brands.find(b => b.name.toLowerCase() === row.brand?.toLowerCase());
            if (brand) row.matchedBrandId = brand.id;
            else row.errors.push("Brand not found");

            // 2. Validate Category
            const cat = _masterData.categories.find(c => c.name.toLowerCase() === row.category?.toLowerCase());
            if (cat) row.matchedCatId = cat.id;
            else row.errors.push("Category not found");

            // 3. Validate Unit
            const unit = _masterData.units.find(u => u.symbol.toLowerCase() === row.unit?.toLowerCase() || u.name.toLowerCase() === row.unit?.toLowerCase());
            if (unit) row.matchedUnitId = unit.id;
            else row.errors.push("Unit not found");

            row.isValid = row.errors.length === 0;
            if (!row.isValid) errorCount++;
        });

        $('#errorCountBadge').text(`${errorCount} Errors Found`);
        $('#btnCommitImport').prop('disabled', errorCount > 0); // Strict mode: Fix all before import
    }

    function _renderValidationGrid() {
        const tbody = $('#validationBody');
        tbody.empty();

        _stagingData.forEach((row, index) => {
            const statusClass = row.isValid ? 'text-success' : 'text-danger fw-bold';
            const statusIcon = row.isValid ? '<i class="bi bi-check-circle"></i>' : '<i class="bi bi-exclamation-circle"></i>';

            // Editable Cells logic could be complex; here we use contenteditable for simplicity or prompts
            // For robust editing, we usually replace the text with an <input> on click.
            // Here, we highlight the invalid cells.

            const brandClass = !row.matchedBrandId ? 'bg-danger bg-opacity-10 text-danger' : '';
            const catClass = !row.matchedCatId ? 'bg-danger bg-opacity-10 text-danger' : '';

            const html = `
                <tr data-idx="${index}">
                    <td class="${statusClass}">${statusIcon}</td>
                    <td contenteditable="true" onblur="importApp.updateCell(${index}, 'name', this.innerText)">${row.name}</td>
                    <td class="${brandClass}" contenteditable="true" onblur="importApp.updateCell(${index}, 'brand', this.innerText)">${row.brand}</td>
                    <td class="${catClass}" contenteditable="true" onblur="importApp.updateCell(${index}, 'category', this.innerText)">${row.category}</td>
                    <td contenteditable="true" onblur="importApp.updateCell(${index}, 'unit', this.innerText)">${row.unit}</td>
                    <td>${row.price}</td>
                </tr>
            `;
            tbody.append(html);
        });
    }

    // --- Step 3: Commit ---
    function updateCell(index, field, value) {
        _stagingData[index][field] = value.trim();
        _validateData(); // Re-validate
        _renderValidationGrid(); // Re-render to update colors
    }

    async function commit() {
        // Convert Staging Data to API Commands
        const commands = _stagingData.map(row => ({
            Name: row.name,
            BrandId: row.matchedBrandId,
            CategoryId: row.matchedCatId,
            UnitOfMeasureId: row.matchedUnitId,
            Type: 1, // Default Finished Good
            Variants: [
                { SKU: row.name.substring(0, 5).toUpperCase() + '-001', SellingPrice: row.price, Size: 'STD', Color: 'N/A' } // Auto-gen dummy variant
            ]
        }));

        const payload = { Products: commands };

        // Call the Bulk API
        // Note: You need to add [HttpPost("bulk")] to ProductController for this
        // For now, we simulate looping or need a bulk endpoint.
        // Assuming we added BulkImportProductsCommand to backend:
        // await api.post('/api/Product/bulk', payload);

        // Since user asked for robust logic, let's use loop if bulk endpoint missing, 
        // OR ideally use the Bulk endpoint I defined in Part 1.

        // Let's assume endpoint: /api/Product/bulk
        const res = await api.post('/api/Product/bulk', payload);

        if (res && res.succeeded) {
            _modal.hide();
            toastr.success(res.messages[0]);
            productApp.init(); // Refresh main grid
        }
    }

    // Navigation Helpers
    function _showStep2() {
        $('#step1').addClass('d-none');
        $('#step2').removeClass('d-none');
        $('#wizardProgress').css('width', '100%');
    }

    function _resetUI() {
        $('#step1').removeClass('d-none');
        $('#step2').addClass('d-none');
        $('#wizardProgress').css('width', '33%');
        $('#fileUpload').val('');
    }

    return {
        init: init,
        openWizard: openWizard,
        updateCell: updateCell, // Exposed for inline editing
        commit: commit,
        reset: _resetUI
    };
})();

$(document).ready(function () {
    importApp.init();
});