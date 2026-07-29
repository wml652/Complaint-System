const ChatWorkspace = (function () {
    let connection = null;
    let currentChatType = null;   // "complaint" or "internal"
    let currentChatId = null;
    let currentOtherUserId = null;

    function init() {
        connection = AppHub.connection;

        connection.on("ReceiveMessage", onComplaintMessageReceived);
        connection.on("ReceiveInternalMessage", onInternalMessageReceived);
        connection.on("MessagesRead", onComplaintMessagesRead);
        connection.on("InternalMessagesRead", onInternalMessagesRead);
        connection.on("UserOnline", (userId) => {
            updatePresenceUi(userId, true);
        });
        connection.on("UserOffline", (userId, lastSeenAt) => {
            updatePresenceUi(userId, false, lastSeenAt);
        });

        AppHub.ensureStarted()
            .then(async () => {
                const onlineIds = await connection.invoke("GetOnlineUserIds");
                onlineIds.forEach(id => updatePresenceUi(id, true));
                loadStudentChats();
            })
            .catch(err => console.error("SignalR connection error:", err));

        showTab('students');
    }
    function updatePresenceUi(userId, isOnline, lastSeenAt) {
        document.querySelectorAll(`.chat-list-item[data-user-id="${userId}"] .online-dot`)
            .forEach(dot => dot.style.display = isOnline ? 'block' : 'none');

        if (currentOtherUserId === userId) {
            document.getElementById('chatStatus').textContent = isOnline
                ? 'online'
                : (lastSeenAt ? `last seen ${formatTime(lastSeenAt)}` : 'offline');
        }
    }

    function showTab(tab) {
        const isStudents = tab === 'students';
        document.getElementById('listStudents').style.display = isStudents ? 'block' : 'none';
        const listStaff = document.getElementById('listStaff');
        if (listStaff) listStaff.style.display = isStudents ? 'none' : 'block';

        document.getElementById('tabStudents').classList.toggle('active', isStudents);
        const tabStaff = document.getElementById('tabStaff');
        if (tabStaff) tabStaff.classList.toggle('active', !isStudents);

        if (isStudents) {
            loadStudentChats();
        } else {
            loadTeamChats();
        }
    }

    function loadStudentChats() {
        fetch('/ChatWorkspace/GetStudentChats')
            .then(res => res.json())
            .then(chats => renderStudentList(chats))
            .catch(err => console.error("Failed to load student chats:", err));
    }

    function loadTeamChats() {
        fetch('/ChatWorkspace/GetTeamChats')
            .then(res => res.json())
            .then(chats => renderTeamList(chats))
            .catch(err => console.error("Failed to load team chats:", err));
    }

    function renderStudentList(chats) {
    const container = document.getElementById('listStudents');
    container.innerHTML = '';

    chats.forEach(chat => {
        const item = document.createElement('div');
        item.className = 'chat-list-item';
        item.dataset.userId = chat.studentId;
        item.onclick = () => openComplaintChat(chat.complaintId, chat.studentName, chat.studentId, chat.title);

        item.innerHTML = `
        <div class="chat-avatar-small">
            ${chat.isSupportTeamView ? '' : '<span class="online-dot"></span>'}
        </div>
        <div style="flex:1; min-width:0;">
            <div class="chat-list-row-top">
                <span class="chat-list-name">${escapeHtml(chat.title)}</span>
                <span class="chat-list-time">${formatTime(chat.lastMessageAt)}</span>
            </div>
            <div class="chat-list-preview">
                <span class="status-pill status-${chat.status}">${escapeHtml(chat.status)}</span>
                ${escapeHtml(chat.lastMessagePreview || chat.studentName)}
            </div>
        </div>
    ${chat.unreadCount > 0 ? `<div class="unread-badge">${chat.unreadCount}</div>` : ''}
`;
        container.appendChild(item);
    });
}

    function renderTeamList(chats) {
        const container = document.getElementById('listStaff');
        if (!container) return;
        container.innerHTML = '';

        chats.forEach(chat => {
            const item = document.createElement('div');
            item.className = 'chat-list-item';
            if (chat.otherUserId) item.dataset.userId = chat.otherUserId;
            item.onclick = () => openInternalChat(chat.id, chat.name, chat.otherUserId, chat.type === 'Group');

            const isGroup = chat.type === 'Group';
            item.innerHTML = `
                <div class="chat-avatar-small" style="${isGroup ? 'display:flex;align-items:center;justify-content:center;' : ''}">
                    ${isGroup ? '<i class="bi bi-people-fill"></i>' : '<span class="online-dot"></span>'}
                </div>
                <div style="flex:1; min-width:0;">
                    <div class="chat-list-row-top">
                        <span class="chat-list-name">${escapeHtml(chat.name)} ${isGroup ? '<i class="bi bi-pin-angle-fill" style="font-size:11px;color:#adb5bd;"></i>' : ''}</span>
                        <span class="chat-list-time">${formatTime(chat.lastMessageAt)}</span>
                    </div>
                    <div class="chat-list-preview">${escapeHtml(chat.lastMessagePreview || '')}</div>
                </div>
                ${chat.unreadCount > 0 ? `<div class="unread-badge">${chat.unreadCount}</div>` : ''}
            `;
            container.appendChild(item);
        });
    }

    function openComplaintChat(complaintId, studentName, studentId, complaintTitle) {
        currentChatType = 'complaint';
        currentChatId = complaintId;
        currentOtherUserId = studentId;

        setHeader(studentName, studentId);
        showChatUi();

        connection.invoke("JoinComplaintGroup", complaintId).catch(err => console.error(err));

        fetch(`/Complaint/GetMessages?complaintId=${complaintId}`)
            .then(res => res.json())
            .then(messages => {
                renderMessages(messages, false);
                connection.invoke("MarkAsRead", complaintId).catch(err => console.error(err));
            });
    }

    function openInternalChat(conversationId, name, otherUserId, isGroup) {
        currentChatType = 'internal';
        currentChatId = conversationId;
        currentOtherUserId = isGroup ? null : otherUserId;

        setHeader(name, otherUserId, isGroup);
        showChatUi();

        connection.invoke("JoinConversationGroup", conversationId).catch(err => console.error(err));

        fetch(`/InternalChat/GetMessages?conversationId=${conversationId}`)
            .then(res => res.json())
            .then(messages => {
                renderMessages(messages, true);
                connection.invoke("MarkInternalMessagesAsRead", conversationId).catch(err => console.error(err));
            });
    }

    function setHeader(name, userId, isGroup) {
        document.getElementById('chatHeader').style.display = 'flex';
        document.getElementById('chatInputBar').style.display = 'flex';
        document.getElementById('chatName').textContent = name;

        if (isGroup) {
            document.getElementById('chatStatus').textContent = '';
        } else if (name === 'Support Team') {
            document.getElementById('chatStatus').textContent = '';
        } else {
            document.getElementById('chatStatus').textContent = 'offline'; // updatePresenceUi jald hi isay update kar dega agar online ho
        }
    }

    function showChatUi() {
        document.getElementById('infoPanel').style.display = 'none';
    }

    function renderMessages(messages, isInternal) {
        const currentUserId = document.body.dataset.currentUserId;
        const body = document.getElementById('chatBody');
        body.innerHTML = '';

        messages.forEach(m => {
            const isOutgoing = m.senderId === currentUserId;
            const bubble = document.createElement('div');
            bubble.className = `chat-bubble ${isOutgoing ? 'outgoing' : 'incoming'}`;
            bubble.dataset.messageId = m.id;

            let ticksHtml = '';
            if (isOutgoing) {
                const seen = !!m.readAt;
                ticksHtml = `<div class="chat-bubble-ticks ${seen ? 'seen' : ''}"><i class="bi bi-check2-all"></i> ${seen ? 'seen' : 'sent'}</div>`;
            }

            bubble.innerHTML = `${escapeHtml(m.content || '')}${ticksHtml}`;
            body.appendChild(bubble);
        });

        body.scrollTop = body.scrollHeight;
    }

    function sendMessage() {
        const input = document.getElementById('messageInput');
        const content = input.value.trim();
        if (!content || !currentChatId) return;

        if (currentChatType === 'complaint') {
            connection.invoke("SendMessage", currentChatId, content).catch(err => console.error(err));
        } else {
            connection.invoke("SendInternalMessage", currentChatId, content).catch(err => console.error(err));
        }

        input.value = '';
    }

    function onComplaintMessageReceived(message) {
        if (currentChatType === 'complaint' && message.complaintId === currentChatId) {
            appendIncomingMessage(message);
            connection.invoke("MarkAsRead", currentChatId).catch(err => console.error(err));
        }
        loadStudentChats();
    }

    function onInternalMessageReceived(message) {
        if (currentChatType === 'internal' && message.conversationId === currentChatId) {
            appendIncomingMessage(message);
            connection.invoke("MarkInternalMessagesAsRead", currentChatId).catch(err => console.error(err));
        }
        loadTeamChats();
    }

    function appendIncomingMessage(message) {
        const currentUserId = document.body.dataset.currentUserId;
        const isOutgoing = message.senderId === currentUserId;
        const body = document.getElementById('chatBody');

        const bubble = document.createElement('div');
        bubble.className = `chat-bubble ${isOutgoing ? 'outgoing' : 'incoming'}`;
        bubble.dataset.messageId = message.id;
        bubble.innerHTML = escapeHtml(message.content || '');
        body.appendChild(bubble);
        body.scrollTop = body.scrollHeight;
    }

    function onComplaintMessagesRead(complaintId) {
        if (currentChatType === 'complaint' && complaintId === currentChatId) {
            markVisibleBubblesSeen();
        }
    }

    function onInternalMessagesRead(conversationId) {
        if (currentChatType === 'internal' && conversationId === currentChatId) {
            markVisibleBubblesSeen();
        }
    }

    function markVisibleBubblesSeen() {
        document.querySelectorAll('.chat-bubble.outgoing .chat-bubble-ticks').forEach(el => {
            el.classList.add('seen');
            el.innerHTML = '<i class="bi bi-check2-all"></i> seen';
        });
    }

    function toggleInfo() {
        const panel = document.getElementById('infoPanel');
        panel.style.display = panel.style.display === 'none' ? 'block' : 'none';
    }

    function formatTime(dateStr) {
        if (!dateStr) return '';
        const date = new Date(dateStr);
        const now = new Date();

        const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        const startOfDate = new Date(date.getFullYear(), date.getMonth(), date.getDate());
        const diffDays = Math.round((startOfToday - startOfDate) / (1000 * 60 * 60 * 24));

        const timeStr = date.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit', hour12: true });

        if (diffDays === 0) {
            return timeStr;
        } else if (diffDays === 1) {
            return 'Yesterday';
        } else if (diffDays > 1 && diffDays < 7) {
            return date.toLocaleDateString([], { weekday: 'long' });
        } else {
            return date.toLocaleDateString([], { day: '2-digit', month: '2-digit', year: 'numeric' });
        }
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    document.addEventListener('DOMContentLoaded', init);

    return { showTab, sendMessage, toggleInfo };
})();
