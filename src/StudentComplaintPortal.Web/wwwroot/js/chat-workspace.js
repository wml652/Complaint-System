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
                        connection.invoke("MarkAsRead", complaintId).catch(err => console.error(err));
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

        let attachmentsHtml = '';
        if (m.attachments && m.attachments.length > 0) {
            attachmentsHtml = m.attachments.map(renderAttachment).join('');
        }

        let ticksHtml = '';
        if (isOutgoing) {
            const seen = !!m.readAt;
            ticksHtml = `<div class="chat-bubble-ticks ${seen ? 'seen' : ''}"><i class="bi bi-check2-all"></i> ${seen ? 'seen' : 'sent'}</div>`;
        }

        bubble.innerHTML = `${attachmentsHtml}${escapeHtml(m.content || '')}${ticksHtml}`;
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
    } else if (attachment.fileType === 'VoiceNote') {
        return `<div class="voice-bubble">
            <button type="button" class="voice-play-btn"><i class="bi bi-play-fill"></i></button>
            <div class="voice-progress-track"><div class="voice-progress-fill"></div></div>
            <span class="voice-duration">0:00</span>
            <audio src="${attachment.fileUrl}" preload="metadata" style="display:none;"></audio>
        </div>`;
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
            document.getElementById('micButton').style.display = hasText ? 'none' : 'flex';
            document.getElementById('sendButton').style.display = hasText ? 'flex' : 'none';
        });

        input.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
            }
        });
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

    let attachmentsHtml = '';
    if (message.attachments && message.attachments.length > 0) {
        attachmentsHtml = message.attachments.map(renderAttachment).join('');
    }

    bubble.innerHTML = `${attachmentsHtml}${escapeHtml(message.content || '')}`;
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


function setInfoPanelLoading() {
    document.getElementById('infoPanel').innerHTML = `<div class="chat-info-panel-field">Loading...</div>`;
}

function renderInfoPanel(details) {
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
            <select id="statusSelect" class="form-select form-select-sm status-select">
    <option value="Open" ${details.status === 'Open' ? 'selected' : ''}>Open</option>
    <option value="InProgress" ${details.status === 'InProgress' ? 'selected' : ''}>In Progress</option>
    <option value="Resolved" ${details.status === 'Resolved' ? 'selected' : ''}>Resolved</option>
    <option value="Closed" ${details.status === 'Closed' ? 'selected' : ''}>Closed</option>
</select>
        </div>
        <button class="btn btn-sm btn-primary" onclick="ChatWorkspace.updateStatus()">Update Status</button>
    `;
}

    function startRecording() {
    audioRecorder.startCapture()
        .then(() => {
            document.getElementById('messageInput').style.display = 'none';
            document.getElementById('attachButton').style.display = 'none';
            document.getElementById('recordingBar').style.display = 'flex';

            const micBtn = document.getElementById('micButton');
            micBtn.onclick = () => stopAndSendRecording();
            micBtn.innerHTML = '<i class="bi bi-send"></i>';

            setTimeout(() => {
                audioRecorder.attachVisualizer('waveformBars', 'recordingTimer');
            }, 50);
        })
        .catch(err => {
            console.error('Failed to start recording:', err);
            alert('Microphone access denied or unavailable.');
        });
}

function cancelRecording() {
    audioRecorder.cancel();
    resetRecordingUi();
}

function stopAndSendRecording() {
    audioRecorder.stop()
        .then(audioBytes => {
            resetRecordingUi();
            const blob = new Blob([audioBytes], { type: 'audio/webm' });
            const file = new File([blob], `voice-${Date.now()}.webm`, { type: 'audio/webm' });
            uploadAttachment(file, 'VoiceNote');
        })
        .catch(err => {
            console.error('Failed to stop recording:', err);
            resetRecordingUi();
        });
}

function resetRecordingUi() {
    document.getElementById('messageInput').style.display = 'block';
    document.getElementById('attachButton').style.display = 'flex';
    document.getElementById('recordingBar').style.display = 'none';

    const micBtn = document.getElementById('micButton');
    micBtn.style.display = 'flex';
    micBtn.onclick = () => startRecording();
    micBtn.innerHTML = '<i class="bi bi-mic"></i>';
}

function updateStatus() {
    const select = document.getElementById('statusSelect');
    const newStatus = select.value;
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

    document.addEventListener('DOMContentLoaded', init);

    return { showTab, sendMessage, toggleInfo, updateStatus, toggleAttachMenu, handleFileSelected, startRecording, cancelRecording };
})();
