const Api = {
    // 1. Generic GET Request
    get: async (url) => {
        try {
            // Show a small loading indicator if you have one (optional)
            // document.body.style.cursor = 'wait';

            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'Accept': 'application/json'
                }
            });

            // document.body.style.cursor = 'default';

            if (!response.ok) {
                // Handle 401 Unauthorized (Redirect to login)
                if (response.status === 401) {
                    window.location.href = '/Account/Login';
                    return null;
                }
                throw new Error(`HTTP Error: ${response.status}`);
            }

            const result = await response.json();

            // Check your Result<T> wrapper
            if (result.succeeded) {
                return result.data;
            } else {
                // Use Toastr if available, otherwise alert
                if (typeof toastr !== 'undefined') {
                    toastr.error(result.message || "Operation failed");
                } else {
                    alert("Error: " + (result.message || "Operation failed"));
                }
                return null;
            }
        } catch (error) {
            console.error("API GET Error:", error);
            if (typeof toastr !== 'undefined') {
                toastr.error("System Error. Please check console.");
            }
            return null;
        }
    },

    // 2. Generic POST Request
    post: async (url, payload) => {
        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                    // CSRF Tokens can be added here if needed later
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                if (response.status === 401) {
                    window.location.href = '/Account/Login';
                    return null;
                }
                // Try to read the error message from the response body
                let errorMsg = `HTTP Error: ${response.status}`;
                try {
                    const errorJson = await response.json();
                    if (errorJson.message) errorMsg = errorJson.message;
                } catch (e) { /* ignore */ }

                throw new Error(errorMsg);
            }

            const result = await response.json();

            if (result.succeeded) {
                // Success!
                if (typeof toastr !== 'undefined' && result.messages && result.messages.length > 0) {
                    toastr.success(result.messages[0]);
                }
                return result; // Return full result (might need ID)
            } else {
                // Business Logic Failure
                if (typeof toastr !== 'undefined') {
                    toastr.error(result.messages ? result.messages[0] : "Operation failed");
                } else {
                    alert("Error: " + result.message);
                }
                return null;
            }
        } catch (error) {
            console.error("API POST Error:", error);
            if (typeof toastr !== 'undefined') {
                toastr.error(error.message || "System Error.");
            }
            return null;
        }
    }
};