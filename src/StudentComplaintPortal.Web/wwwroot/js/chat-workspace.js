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
    let typingStopTimeout = null;
    const typingUsers = new Map(); // userName -> timeout

    function init() {
        connection = AppHub.connection;

        connection.on("ReceiveMessage", onComplaintMessageReceived);
        connection.on("ReceiveInternalMessage", onInternalMessageReceived);
        connection.on("MessagesRead", onComplaintMessagesRead);
        connection.on("InternalMessagesRead", onInternalMessagesRead);
        connection.on("UserTyping", (userName, isTyping) => {
            if (isTyping) {
                showTypingIndicator(userName);
            } else {
                hideTypingIndicatorForUser(userName);
            }
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

        // FIX 3: Updated MessageEdited handler to maintain dropdown UI
        connection.on("MessageEdited", (updatedMessage) => {
            const msgEl = document.querySelector(`[data-message-id="${updatedMessage.id}"]`);
            if (msgEl) {
                const textEl = msgEl.querySelector('.message-text');
                if (textEl) {
                    // Update content while preserving dropdown structure
                    const contentSpan = textEl.querySelector('.message-content');
                    if (contentSpan) {
                        contentSpan.textContent = updatedMessage.content;
                    } else {
                        textEl.textContent = updatedMessage.content;
                    }

                    // Add or update edited badge
                    let editedBadge = textEl.querySelector('.edited-badge');
                    if (!editedBadge) {
                        editedBadge = document.createElement('small');
                        editedBadge.className = 'text-muted ms-1 edited-badge';
                        editedBadge.style.fontSize = '0.7rem';
                        editedBadge.textContent = '(edited)';
                        textEl.appendChild(editedBadge);
                    }
                }
            }
        });

        // FIX 3: Updated MessageDeleted handler to properly remove dropdown menu
        connection.on("MessageDeleted", (messageId) => {
            const msgEl = document.querySelector(`[data-message-id="${messageId}"]`);
            if (msgEl) {
                const textEl = msgEl.querySelector('.message-text');
                if (textEl) {
                    textEl.innerHTML = '<span class="text-muted fst-italic">[Message deleted]</span>';
                }
                // Remove the entire message options dropdown container
                const optionsMenu = msgEl.querySelector('.message-options-container');
                if (optionsMenu) optionsMenu.remove();
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
        setupDropdownClickOutside();

        const micBtn = document.getElementById('micButton');
        if (micBtn) {
            micBtn.onclick = () => startRecording();
        }
        showTab('students');
    }

    // FIX 3: Close dropdown when clicking outside
    function setupDropdownClickOutside() {
        document.addEventListener('click', (e) => {
            if (!e.target.closest('.message-options-container')) {
                document.querySelectorAll('.message-options-menu.show').forEach(menu => {
                    menu.classList.remove('show');
                });
            }
        });
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

    // FIX 3: Completely rewritten renderMessages with WhatsApp-style dropdown
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

            // FIX 3: WhatsApp-style dropdown menu instead of inline buttons
            let optionsHtml = '';
            if (canEditDelete && !m.deletedAt) {
                const escapedContent = escapeHtml(m.content || '').replace(/'/g, "\\'").replace(/"/g, '&quot;');
                optionsHtml = `
                    <div class="message-options-container">
                        <button type="button" class="message-options-btn" onclick="ChatWorkspace.toggleMessageOptions(event, ${m.id})">
                            <i class="bi bi-three-dots-vertical"></i>
                        </button>
                        <div class="message-options-menu" id="options-${m.id}">
                            <div class="message-option-item" onclick="ChatWorkspace.editMessage(${m.id}, ${currentChatId}, '${escapedContent}')">
                                <i class="bi bi-pencil"></i>
                                <span>Edit</span>
                            </div>
                            <div class="message-option-item message-option-delete" onclick="ChatWorkspace.deleteMessage(${m.id}, ${currentChatId})">
                                <i class="bi bi-trash"></i>
                                <span>Delete</span>
                            </div>
                        </div>
                    </div>
                `;
            }

            let messageContent = `
                <div class="message-text-wrapper">
                    <p class="message-text mb-0">
                        <span class="message-content">${contentHtml}</span>${editBadgeHtml}
                    </p>
                    ${optionsHtml}
                </div>
            `;

            if (m.deletedAt) {
                messageContent = `<p class="message-text mb-0"><span class="text-muted fst-italic">[Message deleted]</span></p>`;
            }

            bubble.innerHTML = `${attachmentsHtml}${messageContent}${ticksHtml}`;
            body.appendChild(bubble);
        });

        body.scrollTop = body.scrollHeight;
        if (window.voicePlayer) voicePlayer.setup();
    }

    // FIX 3: Toggle dropdown menu
    function toggleMessageOptions(event, messageId) {
        event.stopPropagation();
        const menu = document.getElementById(`options-${messageId}`);
        const wasShown = menu.classList.contains('show');

        // Close all other open menus
        document.querySelectorAll('.message-options-menu.show').forEach(m => {
            m.classList.remove('show');
        });

        // Toggle this menu
        if (!wasShown) {
            menu.classList.add('show');
        }
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
        if (!currentChatId) return;

        // Send typing started on first keystroke
        if (!typingTimeout) {
            connection.invoke("UserStartedTyping", currentChatId).catch(err => console.error(err));
        }

        // Clear existing timeout
        clearTimeout(typingTimeout);

        // Set new timeout to notify typing stopped after 1.5 seconds of inactivity
        typingTimeout = setTimeout(() => {
            connection.invoke("UserStoppedTyping", currentChatId).catch(err => console.error(err));
            typingTimeout = null;
        }, 1500);
    }

    function showTypingIndicator(userName) {
        const indicator = document.getElementById('typingIndicator');
        if (!indicator) return;

        // Clear existing timeout for this user if any
        if (typingUsers.has(userName)) {
            clearTimeout(typingUsers.get(userName));
        }

        // Add user to typing set
        typingUsers.set(userName, null);

        // Update display
        updateTypingDisplay();

        // Show indicator
        indicator.style.display = 'block';

        // Set auto-hide timeout (3 seconds) in case we don't get UserTyping(false) event
        const timeout = setTimeout(() => {
            typingUsers.delete(userName);
            updateTypingDisplay();
        }, 3000);

        typingUsers.set(userName, timeout);
    }

    function hideTypingIndicatorForUser(userName) {
        if (typingUsers.has(userName)) {
            clearTimeout(typingUsers.get(userName));
            typingUsers.delete(userName);
        }
        updateTypingDisplay();
    }

    function updateTypingDisplay() {
        const indicator = document.getElementById('typingIndicator');
        const typingText = document.getElementById('typingText');
        if (!indicator || !typingText) return;

        if (typingUsers.size === 0) {
            indicator.style.display = 'none';
            return;
        }

        const userNames = Array.from(typingUsers.keys());
        const text = userNames.length === 1
            ? `${userNames[0]} is typing...`
            : `${userNames.join(', ')} are typing...`;

        typingText.textContent = text;
        indicator.style.display = 'block';
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
        const userRole = document.body.dataset.userRole;
        const isAdmin = userRole === 'Admin';
        const isOutgoing = message.senderId === currentUserId;
        const body = document.getElementById('chatBody');

        // Remove "sent/seen" label from previous last message
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

        // FIX 3: Add WhatsApp-style dropdown for newly appended messages
        const canEditDelete = isOutgoing || isAdmin;
        let optionsHtml = '';
        if (canEditDelete && !message.deletedAt) {
            const escapedContent = escapeHtml(message.content || '').replace(/'/g, "\\'").replace(/"/g, '&quot;');
            optionsHtml = `
                <div class="message-options-container">
                    <button type="button" class="message-options-btn" onclick="ChatWorkspace.toggleMessageOptions(event, ${message.id})">
                        <i class="bi bi-three-dots-vertical"></i>
                    </button>
                    <div class="message-options-menu" id="options-${message.id}">
                        <div class="message-option-item" onclick="ChatWorkspace.editMessage(${message.id}, ${currentChatId}, '${escapedContent}')">
                            <i class="bi bi-pencil"></i>
                            <span>Edit</span>
                        </div>
                        <div class="message-option-item message-option-delete" onclick="ChatWorkspace.deleteMessage(${message.id}, ${currentChatId})">
                            <i class="bi bi-trash"></i>
                            <span>Delete</span>
                        </div>
                    </div>
                </div>
            `;
        }

        let messageContent = `
            <div class="message-text-wrapper">
                <p class="message-text mb-0">
                    <span class="message-content">${escapeHtml(message.content || '')}</span>
                </p>
                ${optionsHtml}
            </div>
        `;

        if (message.deletedAt) {
            messageContent = `<p class="message-text mb-0"><span class="text-muted fst-italic">[Message deleted]</span></p>`;
        }

        bubble.innerHTML = `${attachmentsHtml}${messageContent}${ticksHtml}`;
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

        // FIX 4: Build complaint details fields dynamically
        let complaintFieldsHtml = `
            <div class="chat-info-panel-field">
                <div class="chat-info-panel-label">Title</div>
                <div class="chat-info-panel-value">${escapeHtml(details.title)}</div>
            </div>
            <div class="chat-info-panel-field">
                <div class="chat-info-panel-label">Description</div>
                <div class="chat-info-panel-value" style="color:#6c757d;">${escapeHtml(details.description || 'No description provided')}</div>
            </div>
            <div class="chat-info-panel-field">
                <div class="chat-info-panel-label">Category</div>
                <div class="chat-info-panel-value">${escapeHtml(details.category || 'N/A')}</div>
            </div>
            <div class="chat-info-panel-field">
                <div class="chat-info-panel-label">Status</div>
                ${statusFieldHtml}
            </div>
        `;

        // FIX 4: Dynamically render student info fields - loops through all keys
        let studentInfoHtml = '';
        if (details.studentInfo && Object.keys(details.studentInfo).length > 0) {
            studentInfoHtml = '<div class="chat-info-panel-divider"></div>';
            studentInfoHtml += '<div class="chat-info-panel-title" style="font-size: 14px; margin-top: 16px; margin-bottom: 12px;">Student Information</div>';

            // Loop through all student info fields dynamically
            for (const [key, value] of Object.entries(details.studentInfo)) {
                studentInfoHtml += `
                    <div class="chat-info-panel-field">
                        <div class="chat-info-panel-label">${escapeHtml(key)}</div>
                        <div class="chat-info-panel-value">${escapeHtml(value)}</div>
                    </div>
                `;
            }
        }

        document.getElementById('infoPanel').innerHTML = `
            <div class="chat-info-panel-title">Complaint Details</div>
            ${complaintFieldsHtml}
            ${canChangeStatus ? '<button class="btn btn-sm btn-primary" onclick="ChatWorkspace.updateStatus()">Update Status</button>' : ''}
            ${studentInfoHtml}
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
        // Show input and attach button again
        const messageInput = document.getElementById('messageInput');
        const attachButton = document.getElementById('attachButton');
        const recordingBar = document.getElementById('recordingBar');
        const micBtn = document.getElementById('micButton');
        const sendBtn = document.getElementById('sendButton');

        messageInput.style.display = 'block';
        attachButton.style.display = 'flex';
        recordingBar.style.display = 'none';

        // Reset mic button properties
        micBtn.onclick = () => startRecording();
        micBtn.innerHTML = '<i class="bi bi-mic"></i>';
        micBtn.title = 'Record voice message';
        micBtn.disabled = false;

        // FIX 5: Check actual input state to determine which button to show
        const userRole = document.body.dataset.userRole;
        const canRecord = userRole === 'Admin' || userRole === 'Staff';
        const hasText = messageInput.value.trim().length > 0;

        if (hasText) {
            // Input has text - show send button, hide mic
            sendBtn.style.display = 'flex';
            if (canRecord) {
                micBtn.style.display = 'none';
            }
        } else {
            // Input is empty - show mic button (if allowed), hide send
            sendBtn.style.display = 'none';
            if (canRecord) {
                micBtn.style.display = 'flex';
            } else {
                micBtn.style.display = 'none';
            }
        }
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

    return {
        showTab,
        sendMessage,
        toggleInfo,
        updateStatus,
        toggleAttachMenu,
        handleFileSelected,
        startRecording,
        cancelRecording,
        openNewChatPicker,
        closeNewChatPicker,
        editMessage,
        deleteMessage,
        toggleMessageOptions  // FIX 3: Export new function
    };
})();
