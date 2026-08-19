window.audioRecorder = (function () {
    let mediaRecorder = null;
    let audioChunks = [];
    let recordingStartTime = null;
    let recordingTimeout = null;
    let tickInterval = null;
    let activeStream = null;
    let onTickCallback = null;
    let onMaxDurationCallback = null;
    const MAX_RECORDING_DURATION = 2 * 60 * 1000; // 2 minutes

    // onTick(elapsedSeconds) — har-second-call-hota-hai taake UI-live-timer-update-kar-sake
    // onMaxDuration() — jab-2-min-ki-limit-poori-ho-jaye, recording-auto-stop-hone-se-pehle-call-hota-hai
    async function start(onTick, onMaxDuration) {
        try {
            onTickCallback = onTick || null;
            onMaxDurationCallback = onMaxDuration || null;

            activeStream = await navigator.mediaDevices.getUserMedia({ audio: true });
            // Do not force webm. Let the browser use its native format.
            mediaRecorder = new MediaRecorder(activeStream);
            audioChunks = [];
            recordingStartTime = Date.now();

            mediaRecorder.ondataavailable = (event) => {
                if (event.data && event.data.size > 0) audioChunks.push(event.data);
            };

            mediaRecorder.start(250);

            tickInterval = setInterval(() => {
                const elapsedSeconds = Math.floor((Date.now() - recordingStartTime) / 1000);
                if (onTickCallback) onTickCallback(elapsedSeconds);
            }, 1000);

            recordingTimeout = setTimeout(() => {
                stop()
                    .then((recordingData) => {
                        if (onMaxDurationCallback) onMaxDurationCallback(recordingData);
                    })
                    .catch(console.error);
            }, MAX_RECORDING_DURATION);
        } catch (error) {
            console.error('Microphone error:', error);
            alert('Could not access microphone. Please check browser permissions.');
            throw error;
        }
    }

    async function stop() {
        return new Promise((resolve, reject) => {
            if (recordingTimeout) clearTimeout(recordingTimeout);
            if (tickInterval) clearInterval(tickInterval);
            if (!mediaRecorder || mediaRecorder.state === 'inactive') return reject(new Error('Not recording'));

            mediaRecorder.onstop = () => {
                const audioBlob = new Blob(audioChunks, { type: mediaRecorder.mimeType });
                if (activeStream) activeStream.getTracks().forEach(track => track.stop());
                resolve({ blob: audioBlob, mimeType: mediaRecorder.mimeType });
            };
            mediaRecorder.stop();
        });
    }

    async function uploadVoiceMessage(chatId, audioData, chatType) {
        const blobToUpload = audioData.blob ? audioData.blob : audioData;

        // Note: We intentionally DO NOT set 'Content-Type' here.
        // The browser will automatically set the correct boundary and mime type for the Blob.
        let url;
        if (chatType === 'internal' || chatType === 'query') {
            url = `/ChatWorkspace/UploadInternalVoiceMessage?conversationId=${chatId}&chatType=${chatType}`;
        } else {
            url = `/api/v1/complaints/${chatId}/voice-message`;
        }

        const response = await fetch(url, {
            method: 'POST',
            body: blobToUpload
        });

        if (!response.ok) throw new Error(`Upload failed with status ${response.status}`);
        return await response.json();
    }

    return {
        start: start,
        stop: stop,
        uploadVoiceMessage: uploadVoiceMessage
    };
})();
