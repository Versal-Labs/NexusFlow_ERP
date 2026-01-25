// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// A global API utility for your internal app
const api = {
    // Generic request handler
    request: async function (url, method = 'GET', data = null) {

        // 1. Get the token from the meta tag
        const csrfToken = document.querySelector('meta[name="csrf-token"]').getAttribute('content');

        const headers = {
            'Accept': 'application/json',
            // 2. Automatically attach the Anti-Forgery Token
            'RequestVerificationToken': csrfToken
        };

        // If sending JSON data, set content type
        if (data && method !== 'GET') {
            headers['Content-Type'] = 'application/json';
        }

        const options = {
            method: method,
            headers: headers
        };

        if (data && method !== 'GET') {
            options.body = JSON.stringify(data);
        }

        const response = await fetch(url, options);

        // 3. Centralized 401 Handling (Session Timeout)
        if (response.status === 401) {
            // Redirect to login if cookie expires
            window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(window.location.pathname);
            return null;
        }

        // 4. Handle other errors globally (Optional)
        if (!response.ok) {
            console.error("API Error:", response.statusText);
            // You could show a global toast notification here
            throw new Error(response.statusText);
        }

        return await response.json();
    },

    // Shorthand methods
    get: (url) => api.request(url, 'GET'),
    post: (url, data) => api.request(url, 'POST', data),
    put: (url, data) => api.request(url, 'PUT', data),
    delete: (url) => api.request(url, 'DELETE')
};