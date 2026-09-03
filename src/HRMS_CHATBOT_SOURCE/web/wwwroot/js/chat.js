(function () {
    "use strict";

    // ---------------------------------------------------------------
    // API services — thin wrappers around the existing Chat endpoints.
    // ---------------------------------------------------------------

    var MobileDirectoryApiService = {
        /**
         * Populates the top-left dropdown. Backed by GET api/GetActiveMobileNumbersAsync,
         * which reads dbo.usp_GetActiveMobileNumbers (active user_profile rows) —
         * no hardcoded numbers.
         */
        getActiveMobileNumbers: function (signal) {
            return fetch("api/GetActiveMobileNumbersAsync", { method: "GET", signal: signal })
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error("HTTP " + response.status);
                    }
                    return response.json();
                });
        }
    };

    var ChatApiService = {
        /**
         * Wraps the existing ChatStreamAsync endpoint (POST api/ChatStreamAsync).
         * Despite its name, the current backend returns one complete
         * ChatTurnResponse (not chunked/IAsyncEnumerable) — so this resolves once
         * with the full reply rather than yielding partial tokens. The UI still
         * renders it into a single progressively-updated bubble so it is a
         * drop-in replacement the day the backend starts truly streaming.
         */
        sendMessage: function (mobile, message, conversationId, signal) {
            return fetch("api/ChatStreamAsync", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                signal: signal,
                body: JSON.stringify({
                    mobile: mobile,
                    message: message,
                    conversation_id: conversationId
                })
            }).then(function (response) {
                if (!response.ok) {
                    throw new Error("HTTP " + response.status);
                }
                return response.json();
            });
        }
    };

    // ---------------------------------------------------------------
    // State
    // ---------------------------------------------------------------

    var state = {
        mobileNumbers: [],
        selectedMobile: null,   // dropdown value, before Start Chat is clicked
        activeConversation: null, // mobile number of the currently open conversation row
        conversations: {},      // mobile -> { conversationId, messages: [] }
        conversationOrder: [],  // mobile numbers, most-recently-active first
        loadAbort: null,
        sendAbort: null
    };

    // ---------------------------------------------------------------
    // Elements
    // ---------------------------------------------------------------

    var els = {
        mobileSelect: document.getElementById("waMobileSelect"),
        refreshBtn: document.getElementById("waRefreshBtn"),
        startChatBtn: document.getElementById("waStartChatBtn"),
        searchInput: document.getElementById("waSearchInput"),
        list: document.getElementById("waList"),
        chatPlaceholder: document.getElementById("waChatPlaceholder"),
        chatActive: document.getElementById("waChatActive"),
        activeAvatar: document.getElementById("waActiveAvatar"),
        activeName: document.getElementById("waActiveName"),
        activeStatus: document.getElementById("waActiveStatus"),
        messages: document.getElementById("waMessages"),
        input: document.getElementById("waInput"),
        sendBtn: document.getElementById("waSendBtn")
    };

    function initials(name) {
        return (name || "?").trim().charAt(0).toUpperCase();
    }

    function formatTime(date) {
        return date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
    }

    function escapeHtml(text) {
        var div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
    }

    // ---------------------------------------------------------------
    // Mobile number dropdown (Step 1-3)
    // ---------------------------------------------------------------

    function loadMobileNumbers() {
        els.mobileSelect.disabled = true;
        els.mobileSelect.innerHTML = '<option value="">Loading mobile numbers…</option>';
        els.startChatBtn.hidden = true;

        if (state.loadAbort) state.loadAbort.abort();
        state.loadAbort = new AbortController();

        MobileDirectoryApiService.getActiveMobileNumbers(state.loadAbort.signal)
            .then(function (data) {
                state.mobileNumbers = (data && data.mobile_numbers) || [];
                populateMobileSelect();
            })
            .catch(function (err) {
                if (err.name === "AbortError") return;
                els.mobileSelect.innerHTML = '<option value="">Couldn’t load numbers — retry ↻</option>';
                els.mobileSelect.disabled = true;
            });
    }

    function populateMobileSelect() {
        els.mobileSelect.innerHTML = "";

        if (state.mobileNumbers.length === 0) {
            var emptyOpt = document.createElement("option");
            emptyOpt.value = "";
            emptyOpt.textContent = "No active mobile numbers found";
            els.mobileSelect.appendChild(emptyOpt);
            els.mobileSelect.disabled = true;
            return;
        }

        var placeholder = document.createElement("option");
        placeholder.value = "";
        placeholder.textContent = "Select Mobile Number";
        els.mobileSelect.appendChild(placeholder);

        state.mobileNumbers.forEach(function (mobile) {
            var option = document.createElement("option");
            option.value = mobile;
            option.textContent = mobile;
            els.mobileSelect.appendChild(option);
        });

        els.mobileSelect.disabled = false;
    }

    function onMobileSelectChanged() {
        state.selectedMobile = els.mobileSelect.value || null;
        var alreadyStarted = state.selectedMobile && state.conversationOrder.indexOf(state.selectedMobile) !== -1;
        els.startChatBtn.hidden = !state.selectedMobile || alreadyStarted;

        if (alreadyStarted) {
            openConversation(state.selectedMobile);
        }
    }

    // ---------------------------------------------------------------
    // Conversation rows (Step 4) — created ONLY by Start Chat
    // ---------------------------------------------------------------

    function conversationFor(mobile) {
        if (!state.conversations[mobile]) {
            state.conversations[mobile] = { conversationId: null, messages: [] };
        }
        return state.conversations[mobile];
    }

    function startChat() {
        var mobile = state.selectedMobile;
        if (!mobile) return;

        if (state.conversationOrder.indexOf(mobile) === -1) {
            state.conversationOrder.unshift(mobile);
        }
        conversationFor(mobile);

        els.startChatBtn.hidden = true;
        renderList(els.searchInput.value.trim());
        openConversation(mobile);
    }

    function renderEmpty(message) {
        els.list.innerHTML = '<div class="wa-empty-state"><p>' + escapeHtml(message) + "</p></div>";
    }

    function renderList(filterText) {
        var mobiles = state.conversationOrder.filter(function (mobile) {
            return !filterText || mobile.toLowerCase().indexOf(filterText.toLowerCase()) !== -1;
        });

        if (mobiles.length === 0) {
            renderEmpty(filterText
                ? "No conversations match your search."
                : "Select a mobile number above and click Start Chat to begin.");
            return;
        }

        els.list.innerHTML = "";
        mobiles.forEach(function (mobile) {
            var convo = conversationFor(mobile);
            var lastMessage = convo.messages[convo.messages.length - 1];

            var item = document.createElement("div");
            item.className = "wa-list-item" + (mobile === state.activeConversation ? " active" : "");
            item.setAttribute("role", "button");
            item.setAttribute("tabindex", "0");

            item.innerHTML =
                '<div class="wa-avatar">' + escapeHtml(initials(mobile)) + "</div>" +
                '<div class="wa-list-item-body">' +
                '<div class="wa-list-item-top">' +
                '<span class="wa-list-item-name">' + escapeHtml(mobile) + "</span>" +
                '<span class="wa-list-item-time">' + (lastMessage ? formatTime(lastMessage.time) : "") + "</span>" +
                "</div>" +
                '<div class="wa-list-item-preview">' +
                (lastMessage ? escapeHtml(lastMessage.preview) : "Start chatting…") +
                "</div>" +
                "</div>";

            item.addEventListener("click", function () { openConversation(mobile); });
            item.addEventListener("keydown", function (e) {
                if (e.key === "Enter" || e.key === " ") { e.preventDefault(); openConversation(mobile); }
            });

            els.list.appendChild(item);
        });
    }

    function bumpConversationToTop(mobile) {
        var idx = state.conversationOrder.indexOf(mobile);
        if (idx > 0) {
            state.conversationOrder.splice(idx, 1);
            state.conversationOrder.unshift(mobile);
        }
    }

    // ---------------------------------------------------------------
    // Chat panel (Step 5-7)
    // ---------------------------------------------------------------

    function openConversation(mobile) {
        state.activeConversation = mobile;
        renderList(els.searchInput.value.trim());

        els.chatPlaceholder.hidden = true;
        els.chatActive.hidden = false;
        els.activeAvatar.textContent = initials(mobile);
        els.activeName.textContent = mobile;
        els.activeStatus.textContent = "online";

        renderMessages();
        els.input.focus();
    }

    function renderMessages() {
        var convo = conversationFor(state.activeConversation);
        els.messages.innerHTML = "";
        convo.messages.forEach(function (msg) {
            appendBubbleElement(msg);
        });
        scrollToBottom();
    }

    function appendBubbleElement(msg) {
        var row = document.createElement("div");
        row.className = "wa-bubble-row " + msg.kind;

        var bubble = document.createElement("div");
        bubble.className = "wa-bubble " + msg.kind + (msg.isError ? " error" : "");
        bubble.dataset.msgId = msg.id;

        if (msg.pending) {
            bubble.innerHTML = '<span class="wa-typing-dots"><span></span><span></span><span></span></span>';
        } else {
            bubble.textContent = msg.text;
            if (msg.kind !== "system") {
                var meta = document.createElement("span");
                meta.className = "wa-bubble-meta";
                meta.textContent = formatTime(msg.time);
                bubble.appendChild(meta);
            }
        }

        row.appendChild(bubble);
        els.messages.appendChild(row);
        return bubble;
    }

    function scrollToBottom() {
        els.messages.scrollTop = els.messages.scrollHeight;
    }

    var msgCounter = 0;
    function nextId() { return "m" + (++msgCounter); }

    function setSending(isSending) {
        els.sendBtn.disabled = isSending;
        if (!isSending) {
            els.sendBtn.classList.toggle("active", els.input.value.trim().length > 0);
        } else {
            els.sendBtn.classList.remove("active");
        }
    }

    function sendMessage() {
        var text = els.input.value.trim();
        var mobile = state.activeConversation;
        if (!text || !mobile) return;

        var convo = conversationFor(mobile);

        var userMsg = { id: nextId(), kind: "out", text: text, time: new Date(), preview: text };
        convo.messages.push(userMsg);

        var pendingMsg = { id: nextId(), kind: "in", text: "", time: new Date(), pending: true };
        convo.messages.push(pendingMsg);

        els.input.value = "";
        autosizeInput();
        els.sendBtn.classList.remove("active");

        if (state.activeConversation === mobile) {
            appendBubbleElement(userMsg);
            appendBubbleElement(pendingMsg);
            scrollToBottom();
        }

        bumpConversationToTop(mobile);
        renderList(els.searchInput.value.trim());

        setSending(true);
        if (state.sendAbort) state.sendAbort.abort();
        state.sendAbort = new AbortController();

        ChatApiService.sendMessage(mobile, text, convo.conversationId, state.sendAbort.signal)
            .then(function (data) {
                convo.conversationId = data.conversation_id || convo.conversationId;
                pendingMsg.pending = false;
                pendingMsg.text = data.reply || "(no reply)";
                pendingMsg.time = new Date();
                pendingMsg.preview = pendingMsg.text;

                if (state.activeConversation === mobile) {
                    replaceBubble(pendingMsg);
                }
                renderList(els.searchInput.value.trim());
            })
            .catch(function (err) {
                if (err.name === "AbortError") return;
                pendingMsg.pending = false;
                pendingMsg.isError = true;
                pendingMsg.text = "Message failed to send. Please try again.";
                pendingMsg.time = new Date();

                if (state.activeConversation === mobile) {
                    replaceBubble(pendingMsg);
                }
            })
            .finally(function () {
                setSending(false);
                els.input.focus();
            });
    }

    function replaceBubble(msg) {
        var bubble = els.messages.querySelector('[data-msg-id="' + msg.id + '"]');
        if (!bubble) return;

        bubble.classList.toggle("error", !!msg.isError);
        bubble.innerHTML = "";
        bubble.appendChild(document.createTextNode(msg.text));

        var meta = document.createElement("span");
        meta.className = "wa-bubble-meta";
        meta.textContent = formatTime(msg.time);
        bubble.appendChild(meta);

        scrollToBottom();
    }

    function autosizeInput() {
        els.input.style.height = "auto";
        els.input.style.height = Math.min(els.input.scrollHeight, 120) + "px";
    }

    // ---------------------------------------------------------------
    // Wiring
    // ---------------------------------------------------------------

    els.mobileSelect.addEventListener("change", onMobileSelectChanged);
    els.refreshBtn.addEventListener("click", loadMobileNumbers);
    els.startChatBtn.addEventListener("click", startChat);

    els.searchInput.addEventListener("input", function () {
        renderList(els.searchInput.value.trim());
    });

    els.sendBtn.addEventListener("click", function (e) {
        e.preventDefault();
        sendMessage();
    });

    els.input.addEventListener("input", function () {
        autosizeInput();
        els.sendBtn.classList.toggle("active", els.input.value.trim().length > 0);
    });

    els.input.addEventListener("keydown", function (e) {
        if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });

    loadMobileNumbers();
}());
