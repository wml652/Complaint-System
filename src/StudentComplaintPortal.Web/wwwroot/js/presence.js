const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/chat")
    .withAutomaticReconnect()
    .build();

connection.on("UserOnline", (userId) => {
    document.querySelectorAll(`[data-user-id="${userId}"] .online-dot`)
        .forEach(dot => dot.style.display = "block");
});

connection.on("UserOffline", (userId, lastSeenAt) => {
    document.querySelectorAll(`[data-user-id="${userId}"] .online-dot`)
        .forEach(dot => dot.style.display = "none");
    // lastSeenAt ko "last seen" text update karne ke liye use kar sakte ho
});

connection.start().then(async () => {
    const onlineUserIds = await connection.invoke("GetOnlineUserIds");
    onlineUserIds.forEach(userId => {
        document.querySelectorAll(`[data-user-id="${userId}"] .online-dot`)
            .forEach(dot => dot.style.display = "block");
    });
});