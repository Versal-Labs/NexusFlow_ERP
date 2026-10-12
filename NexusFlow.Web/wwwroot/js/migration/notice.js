(() => {
    const notice = document.getElementById('migrationProvisionalNotice');
    if (notice) fetch('/api/migration/status', { credentials: 'same-origin' }).then(r => r.ok ? r.json() : null).then(s => { if (s?.provisional) notice.classList.remove('d-none'); }).catch(() => {});
})();
