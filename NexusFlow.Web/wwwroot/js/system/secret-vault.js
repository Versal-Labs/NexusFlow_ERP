(function () {
    const apiBase = '/api/system/secrets';

    const state = {
        status: null
    };

    function getToken() {
        return document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') || '';
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#039;');
    }

    function keyId(key) {
        return key.replace(/[^a-z0-9]/gi, '-').toLowerCase();
    }

    async function request(url, options) {
        const response = await fetch(url, options);
        const contentType = response.headers.get('content-type') || '';
        const payload = contentType.includes('application/json')
            ? await response.json()
            : { message: await response.text() };

        if (!response.ok || payload.succeeded === false) {
            const message = payload.message || (payload.errors || []).join(', ') || 'Request failed.';
            throw new Error(message);
        }

        return payload;
    }

    async function postJson(path, body) {
        return request(`${apiBase}${path}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getToken()
            },
            body: JSON.stringify(body)
        });
    }

    async function loadStatus() {
        const result = await request(apiBase, { method: 'GET' });
        state.status = result.data;
        renderStatus();
    }

    function renderStatus() {
        if (!state.status) return;

        document.querySelector('[data-summary="instanceId"]').textContent = state.status.instanceId || '-';
        document.querySelector('[data-summary="deploymentProfile"]').textContent = state.status.deploymentProfile || '-';
        document.querySelector('[data-summary="storageMode"]').textContent = state.status.storageMode || '-';
        document.querySelector('[data-summary="azureBlobContainer"]').textContent = state.status.azureBlobContainer || '-';

        const restartBanner = document.getElementById('restartBanner');
        const restartButton = document.getElementById('btnRestartApp');
        if (state.status.restartRequired) {
            restartBanner.classList.remove('d-none');
            restartBanner.innerHTML = `<strong>Restart required.</strong> ${escapeHtml(state.status.restartRequiredReason || 'A runtime secret changed.')} Saved at ${escapeHtml(state.status.restartRequiredAtUtc || 'unknown time')}.`;
            restartButton.classList.remove('d-none');
        } else {
            restartBanner.classList.add('d-none');
            restartButton.classList.add('d-none');
        }

        const container = document.getElementById('secretCards');
        const grouped = state.status.items.reduce((acc, item) => {
            acc[item.category] = acc[item.category] || [];
            acc[item.category].push(item);
            return acc;
        }, {});

        container.innerHTML = Object.entries(grouped).map(([category, items]) => `
            <div class="col-12">
                <h6 class="text-uppercase text-muted fw-semibold small mb-2 mt-2">${escapeHtml(category)}</h6>
            </div>
            ${items.map(renderSecretCard).join('')}
        `).join('');
    }

    function renderSecretCard(item) {
        const id = keyId(item.key);
        const statusBadge = item.configured
            ? '<span class="badge bg-success-subtle text-success border border-success-subtle">Configured</span>'
            : '<span class="badge bg-danger-subtle text-danger border border-danger-subtle">Missing</span>';
        const sourceBadge = `<span class="badge bg-secondary-subtle text-secondary border border-secondary-subtle">${escapeHtml(item.source)}</span>`;
        const overrideWarning = item.warning
            ? `<div class="alert alert-warning py-2 px-3 small mb-3">${escapeHtml(item.warning)}</div>`
            : '';
        const removeButton = item.canRemove
            ? `<button type="button" class="btn btn-outline-danger btn-sm" data-action="remove" data-key="${escapeHtml(item.key)}"><i class="fa-solid fa-trash-can me-1"></i> Clear</button>`
            : '';
        const rotateButton = item.canRotate
            ? `<button type="button" class="btn btn-outline-warning btn-sm" data-action="rotate-jwt"><i class="fa-solid fa-shuffle me-1"></i> Rotate</button>`
            : '';
        const placeholder = item.kind === 'Hangfire'
            ? 'Optional. Leave blank and save to inherit DefaultConnection.'
            : 'Paste the new value to test or save. Current value is never displayed.';

        return `
            <div class="col-lg-6">
                <div class="card border-0 shadow-sm h-100 secret-card" data-key="${escapeHtml(item.key)}">
                    <div class="card-header bg-white border-0 pt-3 pb-0">
                        <div class="d-flex justify-content-between align-items-start gap-2">
                            <div>
                                <h5 class="card-title mb-1">${escapeHtml(item.displayName)}</h5>
                                <div class="small text-muted">${escapeHtml(item.description)}</div>
                            </div>
                            <div class="text-end">${statusBadge}<br>${sourceBadge}</div>
                        </div>
                    </div>
                    <div class="card-body">
                        ${overrideWarning}
                        <dl class="row small mb-3">
                            <dt class="col-4 text-muted">Fingerprint</dt>
                            <dd class="col-8 font-monospace">${escapeHtml(item.fingerprint || '-')}</dd>
                            <dt class="col-4 text-muted">Last audit</dt>
                            <dd class="col-8">${escapeHtml(item.lastAuditAtUtc || '-')}</dd>
                        </dl>
                        <label class="form-label small fw-semibold" for="secret-${id}">New value</label>
                        <textarea class="form-control font-monospace secret-input" id="secret-${id}" rows="4" autocomplete="off" autocapitalize="off" spellcheck="false" placeholder="${escapeHtml(placeholder)}"></textarea>
                    </div>
                    <div class="card-footer bg-white border-0 d-flex justify-content-end gap-2 pb-3">
                        ${rotateButton}
                        ${removeButton}
                        <button type="button" class="btn btn-outline-primary btn-sm" data-action="test" data-key="${escapeHtml(item.key)}"><i class="fa-solid fa-vial me-1"></i> Test</button>
                        <button type="button" class="btn btn-primary btn-sm" data-action="save" data-key="${escapeHtml(item.key)}"><i class="fa-solid fa-floppy-disk me-1"></i> Save</button>
                    </div>
                </div>
            </div>
        `;
    }

    async function askPassword(title) {
        const result = await Swal.fire({
            title,
            input: 'password',
            inputLabel: 'Confirm your current SuperAdmin password',
            inputPlaceholder: 'Current password',
            inputAttributes: {
                autocomplete: 'current-password',
                autocapitalize: 'off',
                spellcheck: 'false'
            },
            showCancelButton: true,
            confirmButtonText: 'Confirm',
            preConfirm: value => {
                if (!value) {
                    Swal.showValidationMessage('Current password is required.');
                    return false;
                }
                return value;
            }
        });

        return result.isConfirmed ? result.value : null;
    }

    function getCardValue(key) {
        const card = document.querySelector(`.secret-card[data-key="${CSS.escape(key)}"]`);
        return card?.querySelector('.secret-input')?.value || '';
    }

    function clearCardValue(key) {
        const card = document.querySelector(`.secret-card[data-key="${CSS.escape(key)}"]`);
        const input = card?.querySelector('.secret-input');
        if (input) input.value = '';
    }

    async function testSecret(key) {
        const value = getCardValue(key);
        try {
            const result = await postJson('/test', { key, value });
            const warnings = result.data?.warnings?.length
                ? `<div class="text-start small mt-2">${result.data.warnings.map(w => `&bull; ${escapeHtml(w)}`).join('<br>')}</div>`
                : '';
            await Swal.fire({
                icon: 'success',
                title: 'Validation passed',
                html: `${escapeHtml(result.data?.message || result.message || 'Secret validation succeeded.')}${warnings}`
            });
        } catch (error) {
            await Swal.fire('Validation failed', error.message, 'error');
        }
    }

    async function saveSecret(key) {
        const value = getCardValue(key);
        const password = await askPassword('Save secret');
        if (!password) return;

        try {
            const result = await postJson('/save', { key, value, currentPassword: password });
            clearCardValue(key);
            toastr.success(result.data?.message || result.message || 'Secret saved.');
            await loadStatus();
        } catch (error) {
            Swal.fire('Save failed', error.message, 'error');
        }
    }

    async function removeSecret(key) {
        const confirmed = await Swal.fire({
            icon: 'warning',
            title: 'Clear this secret?',
            text: 'The current value will be removed from the writable NexusFlow secret store. A platform value may still override it.',
            showCancelButton: true,
            confirmButtonText: 'Clear secret',
            confirmButtonColor: '#dc3545'
        });
        if (!confirmed.isConfirmed) return;

        const password = await askPassword('Clear secret');
        if (!password) return;

        try {
            const result = await postJson('/remove', { key, currentPassword: password });
            clearCardValue(key);
            toastr.success(result.data?.message || result.message || 'Secret cleared.');
            await loadStatus();
        } catch (error) {
            Swal.fire('Clear failed', error.message, 'error');
        }
    }

    async function rotateJwt() {
        const confirmed = await Swal.fire({
            icon: 'warning',
            title: 'Rotate JWT secret?',
            text: 'Existing API and realtime JWT tokens will be invalid after restart.',
            showCancelButton: true,
            confirmButtonText: 'Rotate JWT secret',
            confirmButtonColor: '#ffc107'
        });
        if (!confirmed.isConfirmed) return;

        const password = await askPassword('Rotate JWT secret');
        if (!password) return;

        try {
            const result = await postJson('/rotate-jwt', { currentPassword: password });
            toastr.success(result.data?.message || result.message || 'JWT secret rotated.');
            await loadStatus();
        } catch (error) {
            Swal.fire('Rotate failed', error.message, 'error');
        }
    }

    async function restartApplication() {
        const confirmed = await Swal.fire({
            icon: 'warning',
            title: 'Restart NexusFlow?',
            text: 'The current request will return before the hosting platform starts the application again.',
            showCancelButton: true,
            confirmButtonText: 'Restart application',
            confirmButtonColor: '#dc3545'
        });
        if (!confirmed.isConfirmed) return;

        const password = await askPassword('Restart application');
        if (!password) return;

        try {
            await postJson('/restart', { currentPassword: password });
            await Swal.fire('Restart requested', 'NexusFlow is restarting. Reload this page in a few seconds.', 'info');
        } catch (error) {
            await Swal.fire('Restart may be in progress', error.message, 'info');
        }

        setTimeout(() => window.location.reload(), 8000);
    }

    document.addEventListener('click', event => {
        const button = event.target.closest('[data-action]');
        if (!button) return;

        const action = button.getAttribute('data-action');
        const key = button.getAttribute('data-key');
        if (action === 'test') testSecret(key);
        if (action === 'save') saveSecret(key);
        if (action === 'remove') removeSecret(key);
        if (action === 'rotate-jwt') rotateJwt();
    });

    document.getElementById('btnRestartApp')?.addEventListener('click', restartApplication);

    loadStatus().catch(error => {
        document.getElementById('secretCards').innerHTML = `
            <div class="col-12">
                <div class="alert alert-danger shadow-sm">${escapeHtml(error.message)}</div>
            </div>
        `;
    });
})();
