// 1. Connect to Hub
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/notifications")
    .configureLogging(signalR.LogLevel.Information)
    .build();

// 2. Define "ReceiveNotification" (What happens when server calls us)
connection.on("ReceiveNotification", function (data) {
    // A. Play Sound (Optional)
    // new Audio('/sounds/ping.mp3').play();

    // B. Show Toast
    if (typeof toastr !== 'undefined') {
        toastr.info(data.message, data.title);
    }

    // C. Update Badge
    updateBadgeCount(1); // Increment by 1

    // D. Add to List
    addNotificationToDropdown(data);
});

// 3. Start Connection
connection.start().catch(err => console.error(err.toString()));

// --- Helper Functions ---

function updateBadgeCount(increment) {
    const badge = document.getElementById("notificationBadge");
    let current = parseInt(badge.innerText) || 0;

    if (increment === 0) {
        badge.innerText = "0";
        badge.style.display = "none";
    } else {
        badge.innerText = current + increment;
        badge.style.display = "block";
    }
}

function addNotificationToDropdown(data) {
    const list = document.getElementById("notificationList");

    // Remove "No notifications" placeholder if exists
    if (list.children[0]?.classList.contains("text-center")) {
        list.innerHTML = "";
    }

    const item = `
        <li>
            <a class="dropdown-item d-flex align-items-start py-2 border-bottom" href="${data.url}">
                <div class="me-2 mt-1">
                    <i class="fas fa-info-circle text-primary"></i>
                </div>
                <div>
                    <h6 class="mb-0 small fw-semibold">${data.title}</h6>
                    <p class="mb-0 small text-muted text-truncate" style="max-width: 200px;">${data.message}</p>
                    <small class="text-xs text-muted">${new Date(data.created).toLocaleTimeString()}</small>
                </div>
            </a>
        </li>
    `;

    list.insertAdjacentHTML('afterbegin', item); // Add to top
}

// 4. Initial Load: Fetch unread count from API
async function loadUnreadNotifications() {
    try {
        // Uses our api.js helper
        const data = await Api.get('/api/notifications/unread');

        if (data && Array.isArray(data)) {
            // Update Badge
            const unreadCount = data.length;
            const badge = document.getElementById("notificationBadge");

            if (unreadCount > 0) {
                badge.innerText = unreadCount > 9 ? "9+" : unreadCount;
                badge.style.display = "block";

                // Clear the "No new notifications" text
                const list = document.getElementById("notificationList");
                list.innerHTML = "";

                // Populate List
                data.forEach(notification => {
                    // Re-use the function we wrote for real-time alerts
                    addNotificationToDropdown({
                        title: notification.title,
                        message: notification.message,
                        url: notification.url,
                        created: notification.created
                    });
                });
            }
        }
    } catch (err) {
        console.error("Failed to load notifications", err);
    }
}

// 5. Connect User to Hub (We need the User ID for Groups)
// We get the UserId implicitly on the server, but for the JS client
// we just start the connection. The server handles the grouping.
connection.start().catch(err => console.error(err.toString()));

// Run on load
document.addEventListener("DOMContentLoaded", loadUnreadNotifications);