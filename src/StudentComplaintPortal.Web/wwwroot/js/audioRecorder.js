// =====================================================================
// audioRecorder — captures mic audio, drives a live waveform while
// recording, and returns the finished clip as bytes for Blazor.
// =====================================================================
window.audioRecorder = (function () {
    let mediaRecorder = null;
    let audioChunks = [];
    let stream = null;

    let audioContext = null;
    let analyser = null;
    let sourceNode = null;
    let animationFrameId = null;
    let waveBars = [];

    let timerInterval = null;
    let recordStartTime = null;

    // Step 1: ask for mic access, start recording, start analysing audio.
    // Does NOT touch the DOM — call attachVisualizer() after Blazor renders.
    async function startCapture() {
        try {
            stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            mediaRecorder = new MediaRecorder(stream, { mimeType: 'audio/webm' });
            audioChunks = [];

            mediaRecorder.ondataavailable = (event) => {
                if (event.data.size > 0) {
                    audioChunks.push(event.data);
                }
            };

            mediaRecorder.start(100);
            recordStartTime = Date.now();

            audioContext = new (window.AudioContext || window.webkitAudioContext)();
            analyser = audioContext.createAnalyser();
            analyser.fftSize = 64;
            sourceNode = audioContext.createMediaStreamSource(stream);
            sourceNode.connect(analyser);

            console.log('Voice recording started');
        } catch (error) {
            console.error('Error starting recording:', error);
            throw error;
        }
    }

    // Step 2: called after Blazor has rendered the waveform bars + timer span.
    function attachVisualizer(waveformContainerId, timerElementId) {
        const container = document.getElementById(waveformContainerId);
        if (!container || !analyser) return;

        waveBars = Array.from(container.querySelectorAll('.wave-bar'));
        const dataArray = new Uint8Array(analyser.frequencyBinCount);

        function render() {
            if (!analyser) return;
            analyser.getByteFrequencyData(dataArray);

            const step = Math.max(1, Math.floor(dataArray.length / waveBars.length));
            waveBars.forEach((bar, i) => {
                const value = dataArray[i * step] || 0;
                const height = Math.max(4, Math.min(32, (value / 255) * 32));
                bar.style.height = height + 'px';
            });

            animationFrameId = requestAnimationFrame(render);
        }
        render();

        const timerEl = document.getElementById(timerElementId);
        timerInterval = setInterval(() => {
            if (!timerEl || !recordStartTime) return;
            const elapsed = Math.floor((Date.now() - recordStartTime) / 1000);
            const mins = Math.floor(elapsed / 60);
            const secs = elapsed % 60;
            timerEl.textContent = mins + ':' + secs.toString().padStart(2, '0');
        }, 250);
    }

    function cleanup() {
        if (animationFrameId) {
            cancelAnimationFrame(animationFrameId);
            animationFrameId = null;
        }
        if (timerInterval) {
            clearInterval(timerInterval);
            timerInterval = null;
        }
        if (sourceNode) {
            try { sourceNode.disconnect(); } catch { }
            sourceNode = null;
        }
        if (audioContext) {
            try { audioContext.close(); } catch { }
            audioContext = null;
        }
        analyser = null;
        waveBars = [];
        recordStartTime = null;
    }

    // Stops recording, cleans up, and resolves with the finished clip bytes.
    async function stop() {
        return new Promise((resolve, reject) => {
            cleanup();

            if (!mediaRecorder || mediaRecorder.state === 'inactive') {
                reject(new Error('MediaRecorder is not active'));
                return;
            }

            mediaRecorder.onstop = async () => {
                try {
                    const audioBlob = new Blob(audioChunks, { type: 'audio/webm' });
                    const arrayBuffer = await audioBlob.arrayBuffer();
                    mediaRecorder.stream.getTracks().forEach(track => track.stop());
                    resolve(new Uint8Array(arrayBuffer));
                } catch (error) {
                    reject(error);
                }
            };

            mediaRecorder.stop();
        });
    }

    // Stops recording and throws the audio away (used for the trash/cancel button).
    function cancel() {
        cleanup();
        if (mediaRecorder && mediaRecorder.state !== 'inactive') {
            try {
                mediaRecorder.onstop = () => {
                    mediaRecorder.stream.getTracks().forEach(t => t.stop());
                };
                mediaRecorder.stop();
            } catch { }
        }
        audioChunks = [];
    }

    return {
        startCapture: startCapture,
        attachVisualizer: attachVisualizer,
        stop: stop,
        cancel: cancel
    };
})();

// =====================================================================
// voicePlayer — turns each hidden <audio> inside a .voice-bubble into a
// custom play/pause button + progress bar + duration, WhatsApp-style.
// setup() is idempotent — safe to call after every render.
// =====================================================================
window.voicePlayer = (function () {
    let currentlyPlaying = null;

    function formatTime(seconds) {
        if (!isFinite(seconds) || seconds < 0) return '0:00';
        const mins = Math.floor(seconds / 60);
        const secs = Math.floor(seconds % 60);
        return mins + ':' + secs.toString().padStart(2, '0');
    }

    function setup() {
        document.querySelectorAll('.voice-bubble').forEach(bubble => {
            if (bubble.dataset.bound === 'true') return;
            bubble.dataset.bound = 'true';

            const btn = bubble.querySelector('.voice-play-btn');
            const audio = bubble.querySelector('audio');
            const fill = bubble.querySelector('.voice-progress-fill');
            const durationEl = bubble.querySelector('.voice-duration');
            const track = bubble.querySelector('.voice-progress-track');
            if (!btn || !audio) return;

            audio.addEventListener('loadedmetadata', () => {
                if (isFinite(audio.duration)) durationEl.textContent = formatTime(audio.duration);
            });

            btn.addEventListener('click', () => {
                if (audio.paused) {
                    if (currentlyPlaying && currentlyPlaying !== audio) {
                        currentlyPlaying.pause();
                    }
                    audio.play();
                    currentlyPlaying = audio;
                } else {
                    audio.pause();
                }
            });

            audio.addEventListener('play', () => btn.classList.add('playing'));
            audio.addEventListener('pause', () => btn.classList.remove('playing'));

            audio.addEventListener('timeupdate', () => {
                if (!isFinite(audio.duration) || audio.duration === 0) return;
                const pct = (audio.currentTime / audio.duration) * 100;
                fill.style.width = pct + '%';
                durationEl.textContent = formatTime(audio.duration - audio.currentTime);
            });

            audio.addEventListener('ended', () => {
                fill.style.width = '0%';
                durationEl.textContent = formatTime(audio.duration);
                btn.classList.remove('playing');
                if (currentlyPlaying === audio) currentlyPlaying = null;
            });

            if (track) {
                track.addEventListener('click', (e) => {
                    if (!isFinite(audio.duration)) return;
                    const rect = track.getBoundingClientRect();
                    const pct = (e.clientX - rect.left) / rect.width;
                    audio.currentTime = Math.max(0, Math.min(1, pct)) * audio.duration;
                });
            }
        });
    }

    return { setup: setup };
})();

// Scroll Helper
function scrollToBottom(elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
}

// Enter Key Press Event Listener for Textarea
window.attachEnterKeyHandler = function (elementId, dotnetHelper) {
    const el = document.getElementById(elementId);
    if (!el) return;

    el.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            dotnetHelper.invokeMethodAsync('SubmitMessageFromEnter');
        }
    });
};