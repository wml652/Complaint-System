(function () {
    const connection = AppHub.connection;

    let notificationCount = 0;

    connection.on("ReceiveNotification", function (notification) {
        notificationCount++;
        updateNotificationBadge();
        if (notification.message) {
            showToast(notification.message);
        }
    });

    AppHub.ensureStarted()
        .then(() => console.log("Notification hub connected"))
        .catch(err => console.error("Error connecting to notification hub:", err));

    function updateNotificationBadge() {
        const badge = document.getElementById('notificationCount');
        if (badge) {
            if (notificationCount > 0) {
                badge.textContent = notificationCount;
                badge.style.display = 'inline-block';
            } else {
                badge.style.display = 'none';
            }
        }
    }

    function showToast(message) {
        const toast = document.createElement('div');
        toast.className = 'position-fixed top-0 end-0 m-3 p-3 bg-primary text-white rounded shadow';
        toast.style.zIndex = '9999';
        toast.textContent = message;
        document.body.appendChild(toast);
        setTimeout(() => toast.remove(), 3000);
    }

    document.getElementById('notificationBell')?.addEventListener('click', function (e) {
        e.preventDefault();
        notificationCount = 0;
        updateNotificationBadge();
    });
})();