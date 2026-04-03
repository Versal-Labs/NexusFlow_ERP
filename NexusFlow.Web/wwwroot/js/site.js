// Nexus ERP Global Utilities
// v2.2 - Robust Error Parsing (Handles 'errors', 'messages', and 'message')

// Nexus ERP Global Utilities
// v3.0 - Stripped auto-toasting for business logic. UI handles its own state.
const api = {
    // Helper: Get Anti-Forgery Token
    getToken: () => document.querySelector('meta[name="csrf-token"]')?.getAttribute('content'),

    // Core Request Handler
    request: async (url, method, data = null) => {
        const headers = {
            'Accept': 'application/json',
            'RequestVerificationToken': document.querySelector('meta[name="csrf-token"]')?.getAttribute('content')
        };

        if (data && method !== 'GET') {
            headers['Content-Type'] = 'application/json';
        }

        const options = { method: method, headers: headers };
        if (data && method !== 'GET') options.body = JSON.stringify(data);

        try {
            const response = await fetch(url, options);

            // 1. Handle Session Timeout (401)
            if (response.status === 401) {
                window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(window.location.pathname);
                return { succeeded: false, messages: ["Session expired. Redirecting..."] };
            }

            // 2. Try parse JSON
            let result = null;
            try { result = await response.json(); } catch (e) { result = null; }

            // === HELPER: Smart Error Extractor ===
            const getErrorMsg = (res) => {
                if (!res) return null;
                if (res.messages && res.messages.length > 0) return res.messages[0];
                if (res.errors && Array.isArray(res.errors) && res.errors.length > 0) return res.errors[0];
                if (res.errors && typeof res.errors === 'object' && !Array.isArray(res.errors)) {
                    const firstKey = Object.keys(res.errors)[0];
                    return res.errors[firstKey];
                }
                if (res.message) return res.message;
                return null;
            };

            // 3. Handle Hard HTTP Errors (400, 404, 500)
            if (!response.ok) {
                console.error("API Error:", response.status, result);
                let errorMsg = getErrorMsg(result) || `System Error (${response.status})`;
                
                // We keep global toasts for catastrophic HTTP failures
                if (typeof toastr !== 'undefined') toastr.error(errorMsg);
                return result || { succeeded: false, messages: [errorMsg] };
            }

            // 4. Return to Caller (REMOVED GLOBAL TOASTR.SUCCESS)
            // We now rely on the calling function (e.g. orderApp.save) to display success/warning toasts.
            return result;

        } catch (error) {
            console.error("Network Error:", error);
            if (typeof toastr !== 'undefined') toastr.error("Network connection failed.");
            return { succeeded: false, messages: [error.message] };
        }
    },

    // Shorthand Methods
    get: (url) => api.request(url, 'GET'),
    post: (url, data) => api.request(url, 'POST', data),
    put: (url, data) => api.request(url, 'PUT', data),
    delete: (url) => api.request(url, 'DELETE')
};

// Global Init
document.addEventListener("DOMContentLoaded", function () {
    if (typeof toastr !== 'undefined') {
        toastr.options = {
            "closeButton": true,
            "progressBar": true,
            "positionClass": "toast-top-right",
            "timeOut": "3000"
        };
    }
});

$(document).ready(function () {

    var tooltipList = [];

    // --- 1. Tooltip Management ---
    function enableSidebarTooltips() {
        // Select items that have a 'title' attribute (top-level links)
        // We exclude items inside .collapse (sub-menus) because they are hidden in mini mode
        var sidebarItems = document.querySelectorAll('#sidebar-wrapper .list-group-item-action:not(.collapse .nav-link)');

        sidebarItems.forEach(function (el) {
            // Get text content if title is missing, or use existing title
            var title = el.getAttribute('title');
            if (!title) {
                // Fallback: try to grab the text inside the span
                var textSpan = el.querySelector('.fw-semibold');
                if (textSpan) title = textSpan.innerText;
            }

            if (title) {
                el.setAttribute('title', title); // Ensure title attr exists for Bootstrap
                el.setAttribute('data-bs-toggle', 'tooltip');
                el.setAttribute('data-bs-placement', 'right');

                var t = new bootstrap.Tooltip(el);
                tooltipList.push(t);
            }
        });
    }

    function disableSidebarTooltips() {
        tooltipList.forEach(function (tooltip) {
            tooltip.dispose();
        });
        tooltipList = [];

        // Cleanup attributes so they don't interfere with normal hover
        $('#sidebar-wrapper .list-group-item-action').removeAttr('title').removeAttr('data-bs-toggle').removeAttr('data-bs-placement');
    }

    // --- 2. Sidebar Toggle Logic ---
    $("#sidebarToggle").click(function (e) {
        e.preventDefault();

        var body = $("body");
        var isCollapsing = !body.hasClass("sb-sidenav-toggled");

        body.toggleClass("sb-sidenav-toggled");

        if (isCollapsing) {
            // === GOING MINI ===

            // 1. Close all open accordions (sub-menus)
            // This prevents weird floating menus when hidden
            $('.collapse.show').each(function () {
                var collapseInstance = bootstrap.Collapse.getInstance(this);
                if (collapseInstance) {
                    collapseInstance.hide();
                } else {
                    $(this).removeClass('show'); // Fallback
                }

                // Reset the chevron icon rotation
                var trigger = $(`[href="#${this.id}"]`);
                trigger.attr('aria-expanded', 'false');
            });

            // 2. Enable Tooltips (so user can see what icons mean)
            enableSidebarTooltips();

        } else {
            // === GOING FULL ===

            // 1. Disable Tooltips (Text is visible now)
            disableSidebarTooltips();

            // Optional: Re-open the menu of the current page
            // (You can add logic here to re-open #menuInventory if on Product page)
        }
    });

    // --- 3. Auto-Active Link & Auto-Open Menu (On Page Load) ---
    const currentPath = window.location.pathname.toLowerCase();
    const links = document.querySelectorAll('#sidebar-wrapper a');

    links.forEach(link => {
        const linkPath = link.getAttribute('href').toLowerCase();

        // Check if this link matches current URL
        if (linkPath !== '/' && currentPath.startsWith(linkPath) || (linkPath === '/' && currentPath === '/')) {

            // Highlight Link
            link.classList.add('active-page');

            // If it's a sub-menu item, open the parent Accordion
            const parentCollapse = link.closest('.collapse');
            if (parentCollapse) {
                // Ensure body isn't toggled (mini) before opening
                if (!$("body").hasClass("sb-sidenav-toggled")) {
                    new bootstrap.Collapse(parentCollapse, { toggle: true });

                    // Rotate the arrow on the parent trigger
                    const triggerBtn = document.querySelector(`[href="#${parentCollapse.id}"]`);
                    if (triggerBtn) triggerBtn.setAttribute('aria-expanded', 'true');
                }
            }
        }
    });
});