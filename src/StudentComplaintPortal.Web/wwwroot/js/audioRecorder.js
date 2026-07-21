window.audioRecorder = (function() {
    let mediaRecorder = null;
    let audioChunks = [];

    async function start() {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            mediaRecorder = new MediaRecorder(stream, { mimeType: 'audio/webm' });

            audioChunks = [];

            mediaRecorder.ondataavailable = (event) => {
                if (event.data.size > 0) {
                    audioChunks.push(event.data);
                }
            };

            mediaRecorder.start();
            console.log('Recording started');
        } catch (error) {
            console.error('Error starting recording:', error);
            throw error;
        }
    }

    async function stop() {
        return new Promise((resolve, reject) => {
            if (!mediaRecorder || mediaRecorder.state === 'inactive') {
                reject(new Error('MediaRecorder is not recording'));
                return;
            }

            mediaRecorder.onstop = async () => {
                try {
                    const audioBlob = new Blob(audioChunks, { type: 'audio/webm' });
                    const arrayBuffer = await audioBlob.arrayBuffer();
                    const uint8Array = new Uint8Array(arrayBuffer);

                    // Stop all tracks
                    mediaRecorder.stream.getTracks().forEach(track => track.stop());

                    resolve(Array.from(uint8Array));
                } catch (error) {
                    reject(error);
                }
            };

            mediaRecorder.stop();
        });
    }

    return {
        start: start,
        stop: stop
    };
})();

function scrollToBottom(elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
}
