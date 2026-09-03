/* TEST-ONLY CHAT WIDGET
   Safe to delete this file when the test chat widget is removed.
   Also remove its <script> reference and the marked markup block from
   Conversations/Index.cshtml, plus test-chat-widget.css. */
(function () {
    'use strict';

    const auth = window.HrmsAdminAuth;
    const toast = window.AdminToast || {
        error: function (message) { window.alert(message); }
    };

    const VOICE_DISABLED_MESSAGE = 'Voice feature is disabled for you.';
    const chatEndpoint = '/api/ChatStreamAsync';
    const transcribeEndpoint = '/api/TranscribeVoiceAsync';

    const elements = {
        beginBtn: document.getElementById('testChatBeginBtn'),
        panel: document.getElementById('testChatPanel'),
        mobileInput: document.getElementById('testChatMobile'),
        userInfo: document.getElementById('testChatUserInfo'),
        conversationIdLabel: document.getElementById('testChatConversationId'),
        log: document.getElementById('testChatLog'),
        input: document.getElementById('testChatInput'),
        sendBtn: document.getElementById('testChatSendBtn'),
        micBtn: document.getElementById('testChatMicBtn')
    };

    let conversationId = null;
    let sending = false;
    let voiceEnabled = false;
    let voiceDisabledMessage = VOICE_DISABLED_MESSAGE;
    let isRecording = false;
    let isTranscribing = false;
    let recorder = null;
    let sendAbort = null;

    function init() {
        if (!elements.beginBtn || !elements.panel) {
            return;
        }

        applyAuthenticatedIdentity();
        elements.beginBtn.addEventListener('click', beginNewChat);
        elements.sendBtn?.addEventListener('click', function () {
            void sendMessage();
        });

        elements.micBtn?.addEventListener('click', function (event) {
            event.preventDefault();
            toggleVoiceRecording();
        });

        elements.mobileInput?.addEventListener('change', function () {
            void refreshVoiceAccess();
        });

        elements.mobileInput?.addEventListener('blur', function () {
            void refreshVoiceAccess();
        });

        elements.input?.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && !event.shiftKey) {
                event.preventDefault();
                void sendMessage();
            }
        });

        updateMicButtonState();
    }

    function getDefaultMobile() {
        const profile = auth?.getUserProfile?.();
        if (profile?.mobile) {
            return String(profile.mobile).trim();
        }

        const claims = auth?.parseTokenClaims?.() || {};
        return String(claims.Mobile || claims.mobile || '').trim();
    }

    function resolveMobile() {
        const entered = (elements.mobileInput?.value || '').trim();
        if (entered) {
            return entered;
        }

        return getDefaultMobile();
    }

    function applyAuthenticatedIdentity() {
        const profile = auth?.getUserProfile?.();
        const claims = auth?.parseTokenClaims?.() || {};
        const userId = profile?.user_id || claims.UserId || '';
        const fullName = profile?.full_name || claims.UserName || '';
        const defaultMobile = getDefaultMobile();

        if (elements.userInfo) {
            if (userId || fullName) {
                elements.userInfo.textContent = [fullName, userId ? `(${userId})` : ''].filter(Boolean).join(' ');
            } else {
                elements.userInfo.textContent = 'Not signed in (anonymous test mode)';
            }
        }

        if (!elements.mobileInput) {
            return;
        }

        if (defaultMobile) {
            elements.mobileInput.value = defaultMobile;
        }

        elements.mobileInput.readOnly = false;
        if (!elements.mobileInput.value) {
            elements.mobileInput.placeholder = 'Enter mobile number';
        }
    }

    function beginNewChat() {
        conversationId = null;
        setConversationIdLabel(null);

        if (elements.log) {
            elements.log.innerHTML = '';
        }

        if (elements.input) {
            elements.input.value = '';
        }

        applyAuthenticatedIdentity();
        elements.panel.hidden = false;
        void refreshVoiceAccess();
        elements.input?.focus();
    }

    async function refreshVoiceAccess() {
        const mobile = resolveMobile();
        if (!mobile) {
            voiceEnabled = false;
            voiceDisabledMessage = VOICE_DISABLED_MESSAGE;
            updateMicButtonState();
            return;
        }

        try {
            const response = await fetch('/api/GetVoiceInputEnabledAsync?mobile=' + encodeURIComponent(mobile));
            if (!response.ok) {
                throw new Error('HTTP ' + response.status);
            }

            const data = await response.json();
            voiceEnabled = !!(data && data.voice_enabled);
            voiceDisabledMessage = (data && data.disabled_message) || VOICE_DISABLED_MESSAGE;
        } catch {
            voiceEnabled = false;
            voiceDisabledMessage = VOICE_DISABLED_MESSAGE;
        }

        updateMicButtonState();
    }

    function updateMicButtonState() {
        if (!elements.micBtn) {
            return;
        }

        elements.micBtn.classList.toggle('test-chat-mic-recording', isRecording);
        elements.micBtn.classList.toggle('test-chat-mic-transcribing', isTranscribing);
        elements.micBtn.classList.toggle('test-chat-mic-disabled', !voiceEnabled);

        const blocked = !voiceEnabled || sending || isTranscribing;
        elements.micBtn.disabled = blocked && !isRecording;
        elements.micBtn.title = voiceEnabled
            ? (isRecording ? 'Tap to stop recording' : 'Tap to record voice message')
            : voiceDisabledMessage;
    }

    async function sendMessage() {
        const message = (elements.input?.value || '').trim();
        if (!message) {
            return;
        }

        elements.input.value = '';
        await sendChatTurn(message);
    }

    async function sendChatTurn(message) {
        if (sending) {
            return;
        }

        const mobile = resolveMobile();
        if (!mobile && !auth?.getToken?.()) {
            toast.error('Enter a mobile number or sign in so the token can supply it.');
            return;
        }

        appendMessage('user', message);

        sending = true;
        setSendingState(true);

        const assistantBubble = document.createElement('div');
        assistantBubble.className = 'test-chat-message assistant';
        const assistantBody = document.createElement('span');
        assistantBody.textContent = '';
        assistantBubble.appendChild(assistantBody);
        elements.log?.appendChild(assistantBubble);
        elements.log && (elements.log.scrollTop = elements.log.scrollHeight);

        if (sendAbort) {
            sendAbort.abort();
        }
        sendAbort = new AbortController();

        try {
            const headers = Object.assign(
                { 'Content-Type': 'application/json', Accept: 'application/x-ndjson' },
                auth?.getAuthHeaders?.() || {}
            );

            const body = {
                message: message,
                conversation_id: conversationId
            };

            if (mobile) {
                body.mobile = mobile;
            }

            const response = await fetch(chatEndpoint, {
                method: 'POST',
                headers: headers,
                body: JSON.stringify(body),
                signal: sendAbort.signal
            });

            if (!response.ok) {
                const data = await readJson(response);
                throw new Error(data.error_message || data.message || 'Chat request failed.');
            }

            if (!response.body || !response.body.getReader) {
                throw new Error('Streaming is not supported in this browser.');
            }

            const reader = response.body.getReader();
            const decoder = new TextDecoder();
            let buffer = '';
            let finalData = null;

            while (true) {
                const result = await reader.read();
                if (result.done) {
                    break;
                }

                buffer += decoder.decode(result.value, { stream: true });
                const lines = buffer.split('\n');
                buffer = lines.pop() || '';

                for (const line of lines) {
                    if (!line.trim()) {
                        continue;
                    }

                    const chunk = JSON.parse(line);
                    if (chunk.type === 'delta' && chunk.text) {
                        assistantBody.textContent += chunk.text;
                        elements.log && (elements.log.scrollTop = elements.log.scrollHeight);
                    } else if (chunk.type === 'done') {
                        finalData = chunk;
                    } else if (chunk.type === 'error') {
                        throw new Error(chunk.message || 'Chat stream failed.');
                    }
                }
            }

            if (!finalData) {
                throw new Error('Chat stream ended without a final response.');
            }

            conversationId = finalData.conversation_id || conversationId;
            setConversationIdLabel(conversationId);

            const meta = formatMeta(finalData);
            if (meta) {
                const metaEl = document.createElement('span');
                metaEl.className = 'test-chat-message-meta';
                metaEl.textContent = meta;
                assistantBubble.appendChild(metaEl);
            }

            if (!assistantBody.textContent) {
                assistantBody.textContent = finalData.reply || '(empty reply)';
            }
        } catch (error) {
            if (error.name === 'AbortError') {
                return;
            }

            assistantBubble.remove();
            appendMessage('error', error.message || 'Unable to reach the chat API.');
            toast.error(error.message || 'Unable to reach the chat API.');
        } finally {
            sending = false;
            setSendingState(false);
            elements.input?.focus();
        }
    }

    function encodeWav(samples, sampleRate) {
        const buffer = new ArrayBuffer(44 + samples.length * 2);
        const view = new DataView(buffer);

        function writeString(offset, str) {
            for (let i = 0; i < str.length; i++) {
                view.setUint8(offset + i, str.charCodeAt(i));
            }
        }

        writeString(0, 'RIFF');
        view.setUint32(4, 36 + samples.length * 2, true);
        writeString(8, 'WAVE');
        writeString(12, 'fmt ');
        view.setUint32(16, 16, true);
        view.setUint16(20, 1, true);
        view.setUint16(22, 1, true);
        view.setUint32(24, sampleRate, true);
        view.setUint32(28, sampleRate * 2, true);
        view.setUint16(32, 2, true);
        view.setUint16(34, 16, true);
        writeString(36, 'data');
        view.setUint32(40, samples.length * 2, true);

        let offset = 44;
        for (let j = 0; j < samples.length; j++) {
            const s = Math.max(-1, Math.min(1, samples[j]));
            view.setInt16(offset, s < 0 ? s * 0x8000 : s * 0x7fff, true);
            offset += 2;
        }

        return new Blob([view], { type: 'audio/wav' });
    }

    function createVoiceRecorder() {
        let audioContext = null;
        let mediaStream = null;
        let processor = null;
        let source = null;
        let chunks = [];

        return {
            start: function () {
                chunks = [];
                return navigator.mediaDevices.getUserMedia({ audio: true })
                    .then(function (stream) {
                        mediaStream = stream;
                        audioContext = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: 16000 });
                        source = audioContext.createMediaStreamSource(stream);
                        processor = audioContext.createScriptProcessor(4096, 1, 1);

                        processor.onaudioprocess = function (event) {
                            chunks.push(new Float32Array(event.inputBuffer.getChannelData(0)));
                        };

                        source.connect(processor);
                        processor.connect(audioContext.destination);
                    });
            },

            stop: function () {
                if (processor) {
                    processor.disconnect();
                    processor.onaudioprocess = null;
                }
                if (source) {
                    source.disconnect();
                }
                if (mediaStream) {
                    mediaStream.getTracks().forEach(function (track) { track.stop(); });
                }

                const sampleRate = audioContext ? audioContext.sampleRate : 16000;
                if (audioContext) {
                    audioContext.close();
                }

                const totalLength = chunks.reduce(function (sum, chunk) { return sum + chunk.length; }, 0);
                const samples = new Float32Array(totalLength);
                let offset = 0;
                chunks.forEach(function (chunk) {
                    samples.set(chunk, offset);
                    offset += chunk.length;
                });

                mediaStream = null;
                audioContext = null;
                processor = null;
                source = null;
                chunks = [];

                return encodeWav(samples, sampleRate);
            }
        };
    }

    async function transcribeVoice(mobile, wavBlob) {
        const formData = new FormData();
        formData.append('mobile', mobile);
        formData.append('audio', wavBlob, 'voice.wav');

        const response = await fetch(transcribeEndpoint, {
            method: 'POST',
            body: formData,
            signal: sendAbort?.signal
        });

        const data = await readJson(response);
        if (!response.ok) {
            throw new Error(data.error_message || data.message || 'Transcription failed.');
        }

        return data;
    }

    async function toggleVoiceRecording() {
        if (!voiceEnabled || isTranscribing || sending) {
            return;
        }

        const mobile = resolveMobile();
        if (!mobile) {
            toast.error('Enter a mobile number before using voice input.');
            return;
        }

        if (!isRecording) {
            recorder = createVoiceRecorder();
            try {
                await recorder.start();
                isRecording = true;
                updateMicButtonState();
            } catch {
                isRecording = false;
                recorder = null;
                updateMicButtonState();
                toast.error('Microphone access is required for voice input.');
            }
            return;
        }

        isRecording = false;
        updateMicButtonState();

        const activeRecorder = recorder;
        recorder = null;
        if (!activeRecorder) {
            return;
        }

        const wavBlob = activeRecorder.stop();
        if (!wavBlob || wavBlob.size <= 44) {
            updateMicButtonState();
            return;
        }

        isTranscribing = true;
        updateMicButtonState();

        if (sendAbort) {
            sendAbort.abort();
        }
        sendAbort = new AbortController();

        try {
            const data = await transcribeVoice(mobile, wavBlob);
            const englishText = (data && data.english_text) || '';
            if (!englishText.trim()) {
                throw new Error('Could not transcribe the audio.');
            }

            await sendChatTurn(englishText.trim());
        } catch (error) {
            if (error.name === 'AbortError') {
                return;
            }

            appendMessage('error', error.message || 'Voice transcription failed.');
            toast.error(error.message || 'Voice transcription failed.');
        } finally {
            isTranscribing = false;
            updateMicButtonState();
        }
    }

    function formatMeta(data) {
        const parts = [];
        if (data.last_speaker) {
            parts.push(`speaker: ${data.last_speaker}`);
        }
        if (Array.isArray(data.enabled_agents) && data.enabled_agents.length) {
            parts.push(`enabled: ${data.enabled_agents.join(', ')}`);
        }
        return parts.join(' \u2022 ');
    }

    function appendMessage(role, text, meta) {
        if (!elements.log) {
            return;
        }

        const bubble = document.createElement('div');
        bubble.className = `test-chat-message ${role}`;

        const body = document.createElement('span');
        body.textContent = text;
        bubble.appendChild(body);

        if (meta) {
            const metaEl = document.createElement('span');
            metaEl.className = 'test-chat-message-meta';
            metaEl.textContent = meta;
            bubble.appendChild(metaEl);
        }

        elements.log.appendChild(bubble);
        elements.log.scrollTop = elements.log.scrollHeight;
    }

    function setConversationIdLabel(id) {
        if (elements.conversationIdLabel) {
            elements.conversationIdLabel.textContent = id ? `conversation_id: ${id}` : 'conversation_id: (new)';
        }
    }

    function setSendingState(isSending) {
        if (elements.sendBtn) {
            elements.sendBtn.disabled = isSending;
        }
        if (elements.input) {
            elements.input.disabled = isSending;
        }
        updateMicButtonState();
    }

    async function readJson(response) {
        const text = await response.text();
        if (!text) {
            return {};
        }

        try {
            return JSON.parse(text);
        } catch {
            return { error_message: text };
        }
    }

    init();
})();
