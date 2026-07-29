const AppHub = (function () {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/chat")
        .withAutomaticReconnect()
        .build();

    let startPromise = null;

    function ensureStarted() {
        if (!startPromise) {
            startPromise = connection.start().catch(err => {
                console.error("SignalR connection error:", err);
                startPromise = null; // retry allow karne ke liye
                throw err;
            });
        }
        return startPromise;
    }

    return { connection, ensureStarted };
})();