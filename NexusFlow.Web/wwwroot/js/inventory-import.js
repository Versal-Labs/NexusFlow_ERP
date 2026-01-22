// Global State
let currentJobId = null;
let gridApi = null;
let importedHeaders = [];

// System Target Fields (Hardcoded for Mock)
const targetFields = [
    { value: "ProductName", label: "Product Name" },
    { value: "StyleCode", label: "Style Code" },
    { value: "Color", label: "Color" },
    { value: "Size", label: "Size" },
    { value: "SKU", label: "SKU (Barcode)" },
    { value: "Quantity", label: "Quantity" },
    { value: "CostPrice", label: "Cost Price" },
    { value: "SellingPrice", label: "Selling Price" }
];

document.addEventListener('DOMContentLoaded', function () {

    // --- Step 1: Drag & Drop ---
    const dropzone = document.getElementById('dropzone');
    const fileInput = document.getElementById('fileInput');

    dropzone.addEventListener('click', () => fileInput.click());

    dropzone.addEventListener('dragover', (e) => {
        e.preventDefault();
        dropzone.classList.add('border-primary');
    });

    dropzone.addEventListener('dragleave', () => dropzone.classList.remove('border-primary'));

    dropzone.addEventListener('drop', (e) => {
        e.preventDefault();
        dropzone.classList.remove('border-primary');
        handleFileUpload(e.dataTransfer.files[0]);
    });

    fileInput.addEventListener('change', (e) => handleFileUpload(e.target.files[0]));

    // --- Navigation Buttons ---
    document.getElementById('btnToStep2').addEventListener('click', () => showStep(2));

    document.getElementById('btnToStep3').addEventListener('click', function () {
        saveMapping(); // Simulates saving and triggers Step 3 load
    });

    document.getElementById('btnCommit').addEventListener('click', function () {
        commitImport();
    });
});

function handleFileUpload(file) {
    if (!file) return;

    // Show Preview UI
    document.getElementById('fileName').innerText = file.name;
    document.getElementById('uploadPreview').style.display = 'block';

    const formData = new FormData();
    formData.append('file', file);

    fetch('/InventoryImport/Upload', {
        method: 'POST',
        body: formData
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                currentJobId = data.jobId;
                importedHeaders = data.headers;

                // Enable Next Button
                document.getElementById('btnToStep2').disabled = false;

                // Pre-build Mapping Table
                buildMappingTable();
            }
        });
}

function buildMappingTable() {
    const tbody = document.querySelector('#mappingTable tbody');
    tbody.innerHTML = '';

    importedHeaders.forEach((header, index) => {
        const tr = document.createElement('tr');

        // Auto-Match Logic (Simple string contains)
        let selectedValue = "";
        const lowerHeader = header.toLowerCase();

        if (lowerHeader.includes("style")) selectedValue = "StyleCode";
        else if (lowerHeader.includes("color")) selectedValue = "Color";
        else if (lowerHeader.includes("size")) selectedValue = "Size";
        else if (lowerHeader.includes("qty") || lowerHeader.includes("quantity")) selectedValue = "Quantity";
        else if (lowerHeader.includes("cost")) selectedValue = "CostPrice";
        else if (lowerHeader.includes("price")) selectedValue = "SellingPrice";
        else if (lowerHeader.includes("barcode")) selectedValue = "SKU";
        else if (lowerHeader.includes("name")) selectedValue = "ProductName";

        // Build Select Options
        let optionsHtml = '<option value="">-- Ignore Column --</option>';
        targetFields.forEach(field => {
            const isSelected = field.value === selectedValue ? 'selected' : '';
            optionsHtml += `<option value="${field.value}" ${isSelected}>${field.label}</option>`;
        });

        tr.innerHTML = `
            <td><strong>${header}</strong></td>
            <td class="text-center"><i class="fas fa-arrow-right text-muted"></i></td>
            <td>
                <select class="form-select mapping-select" data-source="${header}">
                    ${optionsHtml}
                </select>
            </td>
        `;
        tbody.appendChild(tr);
    });
}

function saveMapping() {
    // Collect Mapping Data
    const mapping = {};
    document.querySelectorAll('.mapping-select').forEach(select => {
        if (select.value) {
            mapping[select.dataset.source] = select.value;
        }
    });

    fetch('/InventoryImport/SaveMapping', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ jobId: currentJobId, columnMapping: mapping })
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                showStep(3);
                initGrid(); // Load AG Grid
            }
        });
}

function showStep(stepNumber) {
    // Hide all steps
    document.querySelectorAll('.wizard-step').forEach(el => el.style.display = 'none');

    // Show target step
    document.getElementById(`step${stepNumber}`).style.display = 'block';

    // Update Progress Bar
    const progressMap = { 1: 0, 2: 33, 3: 66, 4: 100 };
    document.getElementById('wizardProgress').style.width = `${progressMap[stepNumber]}%`;

    // Update Indicators
    document.querySelectorAll('.step-indicator').forEach((btn, idx) => {
        if (idx + 2 <= stepNumber) { // +2 because indicators start from step 2 button idx 0
            btn.classList.add('btn-primary');
            btn.classList.remove('btn-secondary');
        } else {
            btn.classList.remove('btn-primary');
            btn.classList.add('btn-secondary');
        }
    });
}

function initGrid() {
    const gridDiv = document.querySelector('#myGrid');
    gridDiv.innerHTML = ''; // Clear previous if any

    const columnDefs = [
        { field: "id", headerName: "ID", width: 70 },
        { field: "productName", headerName: "Product Name", editable: true },
        { field: "styleCode", headerName: "Style", editable: true },
        { field: "color", headerName: "Color", editable: true },
        { field: "size", headerName: "Size", editable: true, width: 80 },
        { field: "sku", headerName: "SKU", editable: true },
        { field: "quantity", headerName: "Qty", editable: true, width: 100 },
        { field: "costPrice", headerName: "Cost", editable: true, width: 100 },
        { field: "sellingPrice", headerName: "Price", editable: true, width: 100 },
        {
            field: "errorMessage",
            headerName: "Issues",
            width: 200,
            cellStyle: { color: 'red' }
        }
    ];

    const gridOptions = {
        columnDefs: columnDefs,
        rowData: [],
        defaultColDef: {
            resizable: true,
            sortable: true,
            filter: true
        },
        getRowStyle: params => {
            if (params.data.errorMessage) {
                return { background: '#ffe6e6' }; // RED/Light Red for ERROR
            }
            return { background: 'white' }; // WHITE for VALID
        },
        onCellValueChanged: (params) => {
            // Re-validate row logic (Mock)
            if (params.colDef.field === "quantity" && parseInt(params.data.quantity) < 0) {
                params.data.errorMessage = "Invalid Quantity";
            } else if (params.data.sku !== "") {
                params.data.errorMessage = ""; // Clear error if fixed
            }

            // Refresh row to update color
            params.api.applyTransaction({ update: [params.data] });
            updateStats(params.api);
        }
    };

    gridApi = agGrid.createGrid(gridDiv, gridOptions);

    // Fetch Mock Data
    fetch(`/InventoryImport/GetStagingData?jobId=${currentJobId}`)
        .then(res => res.json())
        .then(data => {
            gridOptions.api.setRowData(data.data);
            updateStats(gridOptions.api);
        });
}

function updateStats(api) {
    let errorCount = 0;
    let validCount = 0;

    api.forEachNode(node => {
        if (node.data.errorMessage) errorCount++;
        else validCount++;
    });

    document.getElementById('errorCountBadge').innerText = `${errorCount} Errors`;
    document.getElementById('validCountBadge').innerText = `${validCount} Valid`;

    // Disable commit if errors? (Optional requirement, usually we skip errors)
}

function commitImport() {
    showStep(4);

    // Simulate Async Process
    setTimeout(() => {
        fetch(`/InventoryImport/Commit?jobId=${currentJobId}`, { method: 'POST' })
            .then(res => res.json())
            .then(data => {
                document.getElementById('processingState').style.display = 'none';
                document.getElementById('successState').style.display = 'block';
            });
    }, 2000);
}
