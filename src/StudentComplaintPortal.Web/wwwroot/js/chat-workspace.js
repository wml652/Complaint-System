const ChatWorkspace = (function () {
    let connection = null;
    let currentChatType = null;   // "complaint" or "internal"
    let currentChatId = null;
    let currentOtherUserId = null;
    let currentComplaintStatus = null;

    // Client-side cache: jab bhi list re-render ho, isse dubara apply kar sakein
    const onlineUserIds = new Set();
    const lastSeenCache = new Map(); // userId -> lastSeenAt string

    // Typing indicator tracking
    let typingTimeout = null;
    const typingSet = new Set();

    function init() {
        connection = AppHub.connection;

        connection.on("ReceiveMessage", onComplaintMessageReceived);
        connection.on("ReceiveInternalMessage", onInternalMessageReceived);
        connection.on("MessagesRead", onComplaintMessagesRead);
        connection.on("InternalMessagesRead", onInternalMessagesRead);
        connection.on("UserTyping", (userId, userName) => {
            showTypingIndicator(userName);
        });
        connection.on("UserStoppedTyping", (userId) => {
            hideTypingIndicator();
        });
        connection.on("UserOnline", (userId) => {
            onlineUserIds.add(userId);
            updatePresenceUi(userId, true);
        });
        connection.on("UserOffline", (userId, lastSeenAt) => {
            onlineUserIds.delete(userId);
            lastSeenCache.set(userId, lastSeenAt);
            updatePresenceUi(userId, false, lastSeenAt);
        });
        connection.on("MessageEdited", (updatedMessage) => {
            const msgEl = document.querySelector(`[data-message-id="${updatedMessage.id}"]`);
            if (msgEl) {
                const textEl = msgEl.querySelector('.message-text') || msgEl.querySelector('p');
                if (textEl) {
                    textEl.textContent = updatedMessage.content;
                    if (!textEl.querySelector('.edited-badge')) {
                        const badge = document.createElement('small');
                        badge.className = 'text-muted ms-1 edited-badge';
                        badge.style.fontSize = '0.7rem';
                        badge.textContent = '(edited)';
                        textEl.appendChild(badge);
                    }
                }
            }
        });
        connection.on("MessageDeleted", (messageId) => {
            const msgEl = document.querySelector(`[data-message-id="${messageId}"]`);
            if (msgEl) {
                const textEl = msgEl.querySelector('.message-text') || msgEl.querySelector('p');
                if (textEl) {
                    textEl.innerHTML = '<span class="text-muted fst-italic">[Message deleted]</span>';
                }
                const actionsEl = msgEl.querySelector('.message-actions');
                if (actionsEl) actionsEl.remove();
            }
        });

        AppHub.ensureStarted()
            .then(async () => {
                const onlineIds = await connection.invoke("GetOnlineUserIds");
                onlineIds.forEach(id => onlineUserIds.add(id));
                applyPresenceToList();
                loadStudentChats();
            })
            .catch(err => console.error("SignalR connection error:", err));

        setupMessageInputToggle();

        const micBtn = document.getElementById('micButton');
        if (micBtn) {
            micBtn.onclick = () => startRecording();
        }
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

    // Fresh render hone ke baad cached online state ko dobara dots par apply karta hai
    function applyPresenceToList() {
        document.querySelectorAll('.chat-list-item[data-user-id]').forEach(item => {
            const uid = item.dataset.userId;
            const dot = item.querySelector('.online-dot');
            if (dot) dot.style.display = onlineUserIds.has(uid) ? 'block' : 'none';
        });
    }
    function showTab(tab) {
        const isStudents = tab === 'students';
        document.getElementById('listStudents').style.display = isStudents ? 'block' : 'none';
        const listStaff = document.getElementById('listStaff');
        if (listStaff) listStaff.style.display = isStudents ? 'none' : 'block';

        const newChatFab = document.getElementById('newChatFab');
        if (newChatFab) newChatFab.style.display = isStudents ? 'none' : 'flex';

        document.getElementById('tabStudents').classList.toggle('active', isStudents);
        const tabStaff = document.getElementById('tabStaff');
        if (tabStaff) tabStaff.classList.toggle('active', !isStudents);

        if (isStudents) { loadStudentChats(); } else { loadTeamChats(); }
    }

    function loadStudentChats() {
        fetch('/ChatWorkspace/GetStudentChats')
            .then(res => res.json())
            .then(chats => {
                renderStudentList(chats);
                maybeOpenPreselectedComplaint(chats);
            })
            .catch(err => console.error("Failed to load student chats:", err));
    }

    function maybeOpenPreselectedComplaint(chats) {
        const wrapper = document.querySelector('.chat-workspace');
        const preselectId = wrapper?.dataset.preselectComplaintId;
        if (!preselectId || preselectId === '') return;

        const match = chats.find(c => c.complaintId == preselectId);
        if (match) {
            openComplaintChat(match.complaintId, match.studentName, match.studentId, match.title);
        }

        wrapper.dataset.preselectComplaintId = '';
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
        applyPresenceToList();
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
        setInfoPanelLoading();

        connection.invoke("JoinComplaintGroup", complaintId)
            .then(() => {
                fetch(`/Complaint/GetMessages?complaintId=${complaintId}`)
                    .then(res => res.json())
                    .then(messages => {
                        renderMessages(messages, false);
                        connection.invoke("MarkAsRead", complaintId)
                            .then(() => loadStudentChats())
                            .catch(err => console.error(err));
                    });
            })
            .catch(err => console.error(err));

        fetch(`/ChatWorkspace/GetComplaintDetails?complaintId=${complaintId}`)
            .then(res => res.json())
            .then(details => renderInfoPanel(details))
            .catch(err => console.error("Failed to load complaint details:", err));
    }

    function openInternalChat(conversationId, name, otherUserId, isGroup) {
        currentChatType = 'internal';
        currentChatId = conversationId;
        currentOtherUserId = isGroup ? null : otherUserId;

        setHeader(name, otherUserId, isGroup);
        showChatUi();
        updateChatInputForStatus(null);
        setInfoPanelLoading();

        connection.invoke("JoinConversationGroup", conversationId).catch(err => console.error(err));

        fetch(`/InternalChat/GetMembers?conversationId=${conversationId}`)
            .then(res => res.json())
            .then(members => renderMembersPanel(members))
            .catch(err => console.error("Failed to load group members:", err));

        fetch(`/InternalChat/GetMessages?conversationId=${conversationId}`)
            .then(res => res.json())
            .then(messages => {
                renderMessages(messages, true);
                connection.invoke("MarkInternalMessagesAsRead", conversationId)
                    .then(() => loadTeamChats())
                    .catch(err => console.error(err));
            });
    }

    function setHeader(name, userId, isGroup) {
        document.getElementById('chatHeader').style.display = 'flex';
        document.getElementById('chatInputBar').style.display = 'flex';
        document.getElementById('chatName').textContent = name;

        if (isGroup || name === 'Support Team') {
            document.getElementById('chatStatus').textContent = '';
            return;
        }

        // Pehle jo humein pata hai wahi turant dikhao (flash of wrong status na ho)
        if (onlineUserIds.has(userId)) {
            document.getElementById('chatStatus').textContent = 'online';
        } else {
            const cachedLastSeen = lastSeenCache.get(userId);
            document.getElementById('chatStatus').textContent = cachedLastSeen
                ? `last seen ${formatTime(cachedLastSeen)}`
                : 'offline';
        }

        // Phir server se authoritative/live status confirm kar lo
        connection.invoke("GetUserPresence", userId)
            .then(presence => {
                if (currentOtherUserId !== userId) return; // user ne is dauran chat badal li ho
                if (presence.isOnline) {
                    onlineUserIds.add(userId);
                    document.getElementById('chatStatus').textContent = 'online';
                } else {
                    onlineUserIds.delete(userId);
                    if (presence.lastSeenAt) lastSeenCache.set(userId, presence.lastSeenAt);
                    document.getElementById('chatStatus').textContent = presence.lastSeenAt
                        ? `last seen ${formatTime(presence.lastSeenAt)}`
                        : 'offline';
                }
            })
            .catch(err => console.error("GetUserPresence error:", err));
    }

    function showChatUi() {
        document.getElementById('infoPanel').style.display = 'none';
    }

    function renderMessages(messages, isInternal) {
    const currentUserId = document.body.dataset.currentUserId;
    const userRole = document.body.dataset.userRole;
    const isAdmin = userRole === 'Admin';
    const body = document.getElementById('chatBody');
    body.innerHTML = '';

    messages.forEach((m, index) => {
        const isOutgoing = m.senderId === currentUserId;
        const canEditDelete = isOutgoing || isAdmin;
        const bubble = document.createElement('div');
        bubble.className = `chat-bubble ${isOutgoing ? 'outgoing' : 'incoming'}`;
        bubble.dataset.messageId = m.id;

        let attachmentsHtml = '';
        if (m.attachments && m.attachments.length > 0) {
            attachmentsHtml = m.attachments.map(renderAttachment).join('');
        }

        let ticksHtml = '';
        if (isOutgoing) {
         const seen = !!m.readAt;
         const isLastMessage = index === messages.length - 1;
         const icon = seen ? 'bi-check2-all' : 'bi-check';
         const label = isLastMessage ? `<span class="tick-label">${seen ? 'seen' : 'sent'}</span>` : '';
         ticksHtml = `<div class="chat-bubble-ticks ${seen ? 'seen' : ''}"><i class="bi ${icon}"></i>${label}</div>`;
    }

        let contentHtml = escapeHtml(m.content || '');
        let editBadgeHtml = m.isEdited ? '<small class="text-muted ms-1 edited-badge" style="font-size: 0.7rem;">(edited)</small>' : '';

        let actionsHtml = '';
        if (canEditDelete && !m.deletedAt) {
            actionsHtml = `
                <div class="message-actions" style="margin-top: 4px; display: flex; gap: 6px;">
                    <button type="button" class="btn btn-sm btn-outline-secondary" style="font-size: 0.75rem; padding: 2px 6px;" onclick="ChatWorkspace.editMessage(${m.id}, ${currentChatId}, '${escapeHtml(m.content || '').replace(/'/g, "\\'")}')">
                        <i class="bi bi-pencil"></i> Edit
                    </button>
                    <button type="button" class="btn btn-sm btn-outline-danger" style="font-size: 0.75rem; padding: 2px 6px;" onclick="ChatWorkspace.deleteMessage(${m.id}, ${currentChatId})">
                        <i class="bi bi-trash"></i> Delete
                    </button>
                </div>
            `;
        }

        let messageContent = `<p class="message-text mb-0">${contentHtml}${editBadgeHtml}</p>${actionsHtml}`;
        if (m.deletedAt) {
            messageContent = `<p class="message-text mb-0"><span class="text-muted fst-italic">[Message deleted]</span></p>`;
        }

        bubble.innerHTML = `${attachmentsHtml}${messageContent}${ticksHtml}`;
        body.appendChild(bubble);
    });

    body.scrollTop = body.scrollHeight;
    if (window.voicePlayer) voicePlayer.setup();
}

    function renderAttachment(attachment) {
    if (attachment.fileType === 'Photo') {
        return `<img src="${attachment.fileUrl}" class="attachment-media" alt="Photo" />`;
    } else if (attachment.fileType === 'Video') {
        return `<video controls class="attachment-media"><source src="${attachment.fileUrl}" type="video/mp4" /></video>`;
    } else if (attachment.fileType === 'VoiceNote' || attachment.fileType === 'Audio' || attachment.fileUrl.match(/\.(webm|mp3|mp4|ogg|wav)$/i)) {
        return `<audio controls class="attachment-media" style="min-width: 250px; max-width: 100%; margin-top: 8px;"><source src="${attachment.fileUrl}" /></audio>`;
    }
    return '';
}

    function toggleAttachMenu() {
        const menu = document.getElementById('attachMenu');
        menu.style.display = menu.style.display === 'none' ? 'block' : 'none';
    }
    document.addEventListener('click', (e) => {
    const menu = document.getElementById('attachMenu');
    const attachBtn = document.getElementById('attachButton');
    if (!menu || menu.style.display === 'none') return;

    if (!menu.contains(e.target) && e.target !== attachBtn && !attachBtn.contains(e.target)) {
        menu.style.display = 'none';
    }
});

    function handleFileSelected(inputEl, fileType) {
        const file = inputEl.files[0];
        if (!file) return;

        uploadAttachment(file, fileType);
        inputEl.value = ''; // reset, so that same file can not be selected in future
        document.getElementById('attachMenu').style.display = 'none';
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

    function uploadAttachment(file, fileType) {
    const formData = new FormData();
    formData.append('File', file);
    formData.append('FileType', fileType);

    fetch(`/api/v1/complaints/${currentChatId}/attachments`, {
        method: 'POST',
        body: formData
    })
        .then(res => {
            if (!res.ok) {
                console.error('Attachment upload failed, status:', res.status);
            }
            // ReceiveMessage SignalR event will add msg in chat — no need to manually render
        })
        .catch(err => console.error('Attachment upload error:', err));
}

    function setupMessageInputToggle() {
        const input = document.getElementById('messageInput');
        if (!input) return;

        input.addEventListener('input', () => {
            const hasText = input.value.trim().length > 0;

            // Check if user role allows voice recording
            const userRole = document.body.dataset.userRole;
            const canRecord = userRole === 'Admin' || userRole === 'Staff';

            // Show mic button only if user can record AND no text input
            const micBtn = document.getElementById('micButton');
            if (micBtn && canRecord) {
                micBtn.style.display = hasText ? 'none' : 'flex';
            } else if (micBtn) {
                micBtn.style.display = 'none';
            }

            document.getElementById('sendButton').style.display = hasText ? 'flex' : 'none';

            // Notify typing
            notifyTyping();
        });

        input.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
                // Stop typing indicator after send
                if (currentChatId) {
                    connection.invoke("UserStoppedTyping", currentChatId).catch(err => console.error(err));
                }
            }
        });
    }

    function notifyTyping() {
        if (currentChatId) {
            connection.invoke("UserStartedTyping", currentChatId).catch(err => console.error(err));
        }

        clearTimeout(typingTimeout);
        typingTimeout = setTimeout(() => {
            if (currentChatId) {
                connection.invoke("UserStoppedTyping", currentChatId).catch(err => console.error(err));
            }
        }, 3000);
    }

    function showTypingIndicator(userName) {
        typingSet.add(userName);
        const typingNames = Array.from(typingSet).join(", ");
        const indicator = document.getElementById('typingIndicator');
        if (indicator) {
            document.getElementById('typingText').textContent =
                typingNames + (typingSet.size === 1 ? " is typing..." : " are typing...");
            indicator.style.display = 'block';
        }
    }

    function hideTypingIndicator() {
        typingSet.clear();
        const indicator = document.getElementById('typingIndicator');
        if (indicator) {
            indicator.style.display = 'none';
        }
    }

    function onComplaintMessageReceived(message) {
        if (currentChatType === 'complaint' && message.complaintId === currentChatId) {
            appendIncomingMessage(message);

            const currentUserId = document.body.dataset.currentUserId;
            // Sirf doosre banday ka message "read" karo - apna hi wapas aaya
            // hua message dobara "read" mat karo, warna apna hi last-sent
            // message turant "seen" ban jata hai (asal bug yehi tha).
            if (message.senderId !== currentUserId) {
                connection.invoke("MarkAsRead", currentChatId).catch(err => console.error(err));
            }
        }
        loadStudentChats();
    }
    function onInternalMessageReceived(message) {
        if (currentChatType === 'internal' && message.conversationId === currentChatId) {
            appendIncomingMessage(message);

            const currentUserId = document.body.dataset.currentUserId;
            if (message.senderId !== currentUserId) {
                connection.invoke("MarkInternalMessagesAsRead", currentChatId).catch(err => console.error(err));
            }
        }
        loadTeamChats();
    }

    function appendIncomingMessage(message) {
    const currentUserId = document.body.dataset.currentUserId;
    const isOutgoing = message.senderId === currentUserId;
        const body = document.getElementById('chatBody');

        const allLabels = body.querySelectorAll('.chat-bubble.outgoing .tick-label');
        if (allLabels.length > 0) {
            allLabels[allLabels.length - 1].remove();
        }

    const bubble = document.createElement('div');
    bubble.className = `chat-bubble ${isOutgoing ? 'outgoing' : 'incoming'}`;
    bubble.dataset.messageId = message.id;

    let attachmentsHtml = '';
    if (message.attachments && message.attachments.length > 0) {
        attachmentsHtml = message.attachments.map(renderAttachment).join('');
    }

        let ticksHtml = '';
        if (isOutgoing) {
            const seen = !!message.readAt;
            const icon = seen ? 'bi-check2-all' : 'bi-check';
            ticksHtml = `<div class="chat-bubble-ticks ${seen ? 'seen' : ''}"><i class="bi ${icon}"></i><span class="tick-label">${seen ? 'seen' : 'sent'}</span></div>`;
        }

        bubble.innerHTML = `${attachmentsHtml}${escapeHtml(message.content || '')}${ticksHtml}`;
    body.appendChild(bubble);
    body.scrollTop = body.scrollHeight;
    if (window.voicePlayer) voicePlayer.setup();
}

    function onComplaintMessagesRead(complaintId) {
        if (currentChatType === 'complaint' && complaintId === currentChatId) {
            markVisibleBubblesSeen();
        }
    }

    function onInternalMessagesRead(conversationId) {
        if (currentChatType === 'internal' && conversationId === currentChatId) {
            refreshInternalTicks(conversationId);
        }
    }

    function refreshInternalTicks(conversationId) {
        fetch(`/InternalChat/GetMessages?conversationId=${conversationId}`)
            .then(res => res.json())
            .then(messages => {
                if (currentChatType !== 'internal' || currentChatId !== conversationId) return;

                const currentUserId = document.body.dataset.currentUserId;
                const lastMessage = messages[messages.length - 1];

                messages.forEach(m => {
                    if (m.senderId !== currentUserId) return;

                    const bubble = document.querySelector(`.chat-bubble[data-message-id="${m.id}"]`);
                    const ticks = bubble && bubble.querySelector('.chat-bubble-ticks');
                    if (!ticks) return;

                    const seen = !!m.readAt;
                    const isLastOverall = lastMessage && lastMessage.id === m.id;
                    const icon = seen ? 'bi-check2-all' : 'bi-check';
                    const label = isLastOverall ? `<span class="tick-label">${seen ? 'seen' : 'sent'}</span>` : '';

                    ticks.className = `chat-bubble-ticks ${seen ? 'seen' : ''}`;
                    ticks.innerHTML = `<i class="bi ${icon}"></i>${label}`;
                });
            })
            .catch(err => console.error("refreshInternalTicks error:", err));
    }

    function markVisibleBubblesSeen() {
        const allTicks = document.querySelectorAll('.chat-bubble.outgoing .chat-bubble-ticks');

        // Sab outgoing messages ka icon double-tick ho jayega (sab padh liye gaye)
        allTicks.forEach(el => {
            el.classList.add('seen');
            el.innerHTML = '<i class="bi bi-check2-all"></i>';
        });

        // "seen" word sirf tab lagega jab POORI CHAT ka sabse aakhri message
        // (incoming ya outgoing, dono mila kar) khud outgoing ho - warna label
        // kisi purani (ab last na rahi) outgoing bubble pe wapas chipak jata tha.
        const body = document.getElementById('chatBody');
        const allBubbles = body.querySelectorAll('.chat-bubble');
        const lastBubble = allBubbles[allBubbles.length - 1];

        if (lastBubble && lastBubble.classList.contains('outgoing')) {
            const ticks = lastBubble.querySelector('.chat-bubble-ticks');
            if (ticks) {
                ticks.innerHTML += '<span class="tick-label">seen</span>';
            }
        }
    }

    function toggleInfo() {
        const panel = document.getElementById('infoPanel');
        panel.style.display = panel.style.display === 'none' ? 'block' : 'none';
    }

    function openNewChatPicker() {
        document.getElementById('contactPicker').style.display = 'flex';
        document.getElementById('contactList').innerHTML = '<div class="chat-list-item">Loading...</div>';

        fetch('/InternalChat/GetContacts')
            .then(res => res.json())
            .then(contacts => renderContactList(contacts))
            .catch(err => console.error("Failed to load contacts:", err));
    }

    function closeNewChatPicker() {
        document.getElementById('contactPicker').style.display = 'none';
    }

    function renderContactList(contacts) {
        const container = document.getElementById('contactList');
        container.innerHTML = '';

        if (contacts.length === 0) {
            container.innerHTML = '<div class="chat-list-item">No contacts found</div>';
            return;
        }

        contacts.forEach(c => {
            const item = document.createElement('div');
            item.className = 'chat-list-item';
            item.innerHTML = `
            <div class="chat-avatar-small"></div>
            <div class="chat-list-name">${escapeHtml(c.fullName)}</div>
        `;
            item.onclick = () => startNewChat(c.userId, c.fullName);
            container.appendChild(item);
        });
    }

    function startNewChat(userId, name) {
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

        fetch('/InternalChat/StartDirectConversation', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: `otherUserId=${encodeURIComponent(userId)}&__RequestVerificationToken=${encodeURIComponent(token)}`
        })
            .then(res => res.json())
            .then(data => {
                closeNewChatPicker();
                loadTeamChats();
                openInternalChat(data.conversationId, name, userId, false);
            })
            .catch(err => console.error("Failed to start conversation:", err));
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


function setInfoPanelLoading() {
    document.getElementById('infoPanel').innerHTML = `<div class="chat-info-panel-field">Loading...</div>`;
}

    function renderInfoPanel(details) {
        const canChangeStatus = document.body.dataset.canChangeStatus === 'true';

        const statusFieldHtml = canChangeStatus
            ? `<select id="statusSelect" class="form-select form-select-sm status-select">
    <option value="Open" ${details.status === 'Open' ? 'selected' : ''}>Open</option>
    <option value="InProgress" ${details.status === 'InProgress' ? 'selected' : ''}>In Progress</option>
    <option value="Resolved" ${details.status === 'Resolved' ? 'selected' : ''}>Resolved</option>
    <option value="Closed" ${details.status === 'Closed' ? 'selected' : ''}>Closed</option>
</select>`
            : `<span class="status-pill status-${details.status}">${escapeHtml(details.status)}</span>`;

        document.getElementById('infoPanel').innerHTML = `
        <div class="chat-info-panel-title">Complaint details</div>
        <div class="chat-info-panel-field">
            <div class="chat-info-panel-label">Title</div>
            <div class="chat-info-panel-value">${escapeHtml(details.title)}</div>
        </div>
        <div class="chat-info-panel-field">
            <div class="chat-info-panel-label">Description</div>
            <div class="chat-info-panel-value" style="color:#6c757d;">${escapeHtml(details.description || 'No description provided')}</div>
        </div>
        <div class="chat-info-panel-field">
            <div class="chat-info-panel-label">Status</div>
            ${statusFieldHtml}
        </div>
        ${canChangeStatus ? '<button class="btn btn-sm btn-primary" onclick="ChatWorkspace.updateStatus()">Update Status</button>' : ''}
    `;
        updateChatInputForStatus(details.status);
    }

    function renderMembersPanel(members) {
        const rows = members.map(m => {
            const isOnline = onlineUserIds.has(m.userId);
            const statusText = isOnline
                ? 'online'
                : (m.lastReadAt ? `last read ${formatTime(m.lastReadAt)}` : 'not read yet');

            return `
        <div class="chat-info-panel-field">
            <div class="chat-info-panel-value">${escapeHtml(m.fullName)}</div>
            <div style="color:#6c757d; font-size:12px;">${isOnline ? '🟢 online' : statusText}</div>
        </div>`;
        }).join('');

        document.getElementById('infoPanel').innerHTML = `
        <div class="chat-info-panel-title">Members (${members.length})</div>
        ${rows}
    `;
    }

    function updateChatInputForStatus(status) {
        currentComplaintStatus = status;
        const inputBar = document.getElementById('chatInputBar');
        const closedBanner = document.getElementById('closedBanner');
        if (!inputBar || !closedBanner) return;

        if (currentChatType === 'complaint' && status === 'Closed') {
            inputBar.style.display = 'none';
            closedBanner.style.display = 'flex';
        } else {
            closedBanner.style.display = 'none';
            inputBar.style.display = 'flex';
        }
    }
        function startRecording() {
        const userRole = document.body.dataset.userRole;

        // Final authorization check (fail-safe)
        if (userRole !== 'Admin' && userRole !== 'Staff') {
            alert('Only Admin and Staff members can send voice messages.');
            return;
        }

        if (!currentChatId || currentChatType !== 'complaint') {
            alert('Please select a complaint chat to send a voice message.');
            return;
        }

        audioRecorder.start()
            .then(() => {
                document.getElementById('messageInput').style.display = 'none';
                document.getElementById('attachButton').style.display = 'none';
                document.getElementById('recordingBar').style.display = 'flex';

                const micBtn = document.getElementById('micButton');
                micBtn.onclick = () => stopAndSendRecording();
                micBtn.innerHTML = '<i class="bi bi-send"></i>';
                micBtn.title = 'Stop recording and send';
            })
            .catch(err => {
                console.error('Failed to start recording:', err);
                alert('Microphone access denied or unavailable. Please check your browser permissions.');
            });
    }

    function cancelRecording() {
        // For now, we'll stop and discard
        audioRecorder.stop()
            .then(() => {
                resetRecordingUi();
            })
            .catch(err => {
                console.error('Error canceling recording:', err);
                resetRecordingUi();
            });
    }

    async function stopAndSendRecording() {
        try {
            const recordingData = await audioRecorder.stop();
            resetRecordingUi();

            // Show upload progress
            const micBtn = document.getElementById('micButton');
            micBtn.disabled = true;
            micBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';

            // Upload voice message using the new endpoint
            const messageDto = await audioRecorder.uploadVoiceMessage(currentChatId, recordingData.blob);

            console.log('Voice message sent successfully');
            micBtn.disabled = false;
            micBtn.innerHTML = '<i class="bi bi-mic"></i>';
        } catch (err) {
            console.error('Failed to send voice message:', err);
            alert(`Error sending voice message: ${err.message}`);
            resetRecordingUi();
        }
    }

    function resetRecordingUi() {
        document.getElementById('messageInput').style.display = 'block';
        document.getElementById('attachButton').style.display = 'flex';
        document.getElementById('recordingBar').style.display = 'none';

        const micBtn = document.getElementById('micButton');
        micBtn.style.display = 'flex';
        micBtn.onclick = () => startRecording();
        micBtn.innerHTML = '<i class="bi bi-mic"></i>';
        micBtn.title = 'Record voice message';
        micBtn.disabled = false;
    }

function updateStatus() {
    const select = document.getElementById('statusSelect');
    const newStatus = select.value;
    if (newStatus === 'Closed' && !confirm('Are you sure you want to close this complaint? The chat will become read-only while it stays closed.')) {
        select.value = currentComplaintStatus || select.value;
        return;
    }
    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

    fetch('/Complaint/UpdateStatus', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'X-Requested-With': 'XMLHttpRequest'
        },
        body: `id=${currentChatId}&newStatus=${newStatus}&__RequestVerificationToken=${encodeURIComponent(token)}`
    })
        .then(res => {
            if (res.ok) {
                updateChatInputForStatus(newStatus);
                loadStudentChats();
            } else {
                console.error('Failed to update status, HTTP status:', res.status);
            }
        })
        .catch(err => console.error(err));
}
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    async function editMessage(messageId, complaintId, currentContent) {
        const newContent = prompt('Edit message:', currentContent);
        if (newContent === null || newContent.trim() === '') return;

        try {
            const response = await fetch(`/api/v1/complaints/${complaintId}/messages/${messageId}`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${localStorage.getItem('authToken') || ''}`
                },
                body: JSON.stringify({ content: newContent.trim() })
            });

            if (!response.ok) {
                alert(`Error editing message: ${response.status}`);
                return;
            }

            const updatedMessage = await response.json();
            console.log('Message edited:', updatedMessage);
        } catch (err) {
            console.error('Error editing message:', err);
            alert('Failed to edit message');
        }
    }

    async function deleteMessage(messageId, complaintId) {
        if (!confirm('Are you sure you want to delete this message?')) return;

        try {
            const response = await fetch(`/api/v1/complaints/${complaintId}/messages/${messageId}`, {
                method: 'DELETE',
                headers: {
                    'Authorization': `Bearer ${localStorage.getItem('authToken') || ''}`
                }
            });

            if (!response.ok) {
                alert(`Error deleting message: ${response.status}`);
                return;
            }

            console.log('Message deleted:', messageId);
        } catch (err) {
            console.error('Error deleting message:', err);
            alert('Failed to delete message');
        }
    }

    document.addEventListener('DOMContentLoaded', init);

    return { showTab, sendMessage, toggleInfo, updateStatus, toggleAttachMenu, handleFileSelected, startRecording, cancelRecording, openNewChatPicker, closeNewChatPicker, editMessage, deleteMessage };
})();
