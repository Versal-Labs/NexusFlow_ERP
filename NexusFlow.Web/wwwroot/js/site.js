// Nexus ERP Global Utilities
// v2.2 - Robust Error Parsing (Handles 'errors', 'messages', and 'message')

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
                // Check 'messages' (Result<T> standard)
                if (res.messages && res.messages.length > 0) return res.messages[0];
                // Check 'errors' (ASP.NET Validation standard)
                if (res.errors && Array.isArray(res.errors) && res.errors.length > 0) return res.errors[0];
                // Check 'errors' object (FluentValidation dictionary)
                if (res.errors && typeof res.errors === 'object' && !Array.isArray(res.errors)) {
                    const firstKey = Object.keys(res.errors)[0];
                    return res.errors[firstKey];
                }
                // Check 'message' (General exception)
                if (res.message) return res.message;
                return null;
            };

            // 3. Handle HTTP Errors (400, 404, 500)
            if (!response.ok) {
                console.error("API Error:", response.status, result);

                let errorMsg = getErrorMsg(result) || `System Error (${response.status})`;

                if (typeof toastr !== 'undefined') toastr.error(errorMsg);
                return result || { succeeded: false, messages: [errorMsg] };
            }

            // 4. Handle Success Logic (200 OK)
            if (result && typeof result.succeeded !== 'undefined') {
                if (!result.succeeded) {
                    // Business Logic Failure (e.g. "Domain validation failed")
                    let failMsg = getErrorMsg(result) || "Operation failed";
                    if (typeof toastr !== 'undefined') toastr.error(failMsg);
                } else {
                    // SUCCESS: Show Toast for Non-GET requests
                    if (method !== 'GET' && typeof toastr !== 'undefined') {
                        // Use backend message OR generic fallback
                        const successMsg = getErrorMsg(result) || "Saved successfully.";
                        toastr.success(successMsg);
                    }
                }
            }

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