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

    const endpoint = '/api/ChatStreamAsync';

    const elements = {
        beginBtn: document.getElementById('testChatBeginBtn'),
        panel: document.getElementById('testChatPanel'),
        mobileInput: document.getElementById('testChatMobile'),
        userInfo: document.getElementById('testChatUserInfo'),
        conversationIdLabel: document.getElementById('testChatConversationId'),
        log: document.getElementById('testChatLog'),
        input: document.getElementById('testChatInput'),
        sendBtn: document.getElementById('testChatSendBtn')
    };

    let conversationId = null;
    let sending = false;

    function init() {
        if (!elements.beginBtn || !elements.panel) {
            return;
        }

        applyAuthenticatedIdentity();
        elements.beginBtn.addEventListener('click', beginNewChat);
        elements.sendBtn?.addEventListener('click', function () {
            void sendMessage();
        });

        elements.input?.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && !event.shiftKey) {
                event.preventDefault();
                void sendMessage();
            }
        });
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
        elements.input?.focus();
    }

    async function sendMessage() {
        if (sending) {
            return;
        }

        const message = (elements.input?.value || '').trim();
        if (!message) {
            return;
        }

        const mobile = resolveMobile();
        if (!mobile && !auth?.getToken?.()) {
            toast.error('Enter a mobile number or sign in so the token can supply it.');
            return;
        }

        appendMessage('user', message);
        elements.input.value = '';

        sending = true;
        setSendingState(true);

        try {
            const headers = Object.assign(
                { 'Content-Type': 'application/json', Accept: 'application/json' },
                auth?.getAuthHeaders?.() || {}
            );

            const body = {
                message: message,
                conversation_id: conversationId
            };

            if (mobile) {
                body.mobile = mobile;
            }

            const response = await fetch(endpoint, {
                method: 'POST',
                headers: headers,
                body: JSON.stringify(body)
            });

            const data = await readJson(response);
            if (!response.ok) {
                throw new Error(data.error_message || data.message || 'Chat request failed.');
            }

            conversationId = data.conversation_id || conversationId;
            setConversationIdLabel(conversationId);

            const meta = formatMeta(data);
            appendMessage('assistant', data.reply || '(empty reply)', meta);
        } catch (error) {
            appendMessage('error', error.message || 'Unable to reach the chat API.');
            toast.error(error.message || 'Unable to reach the chat API.');
        } finally {
            sending = false;
            setSendingState(false);
            elements.input?.focus();
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
