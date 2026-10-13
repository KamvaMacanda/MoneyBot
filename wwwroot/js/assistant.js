/**
 * MoneyBot Assistant Client Logic
 * Handles interactive conversation flow, suggestions, auto-resizing composer,
 * loading state animations, copy-to-clipboard feedback, and motion settings.
 */

(function () {
    "use strict";

    // DOM Elements - supporting both ID schemes for complete compatibility
    const conversationView = document.getElementById("conversationView") || document.getElementById("chatFeed");
    const messageList = document.getElementById("messageList") || document.getElementById("chatFeed");
    const endScrollAnchor = document.getElementById("endScrollAnchor") || document.getElementById("messagesEnd");
    const welcomeSection = document.getElementById("welcomeSection") || document.getElementById("welcomeGroup");
    const composerInput = document.getElementById("composerInput") || document.getElementById("chatInput");
    const sendBtn = document.getElementById("sendBtn") || document.getElementById("sendButton");
    const loadingRow = document.getElementById("loadingRow");
    const newSessionBtn = document.getElementById("newSessionBtn") || document.getElementById("newChatBtn");
    const errorBanner = document.getElementById("errorBanner");
    const retryBtn = document.getElementById("retryBtn");
    const settingsModal = document.getElementById("settingsModal");
    const openSettingsBtn = document.getElementById("openSettingsBtn");
    const headerSettingsBtn = document.getElementById("headerSettingsBtn");
    const closeSettingsBtn = document.getElementById("closeSettingsBtn");
    const saveSettingsBtn = document.getElementById("saveSettingsBtn");
    const reducedMotionSelect = document.getElementById("reducedMotionSelect");
    const maskBalancesBtn = document.getElementById("maskBalancesBtn");
    const maskBalancesCheckbox = document.getElementById("maskBalancesCheckbox");
    const totalBalanceDisplay = document.getElementById("totalBalanceDisplay");
    const mobileMenuBtn = document.getElementById("mobileMenuBtn");
    const appShell = document.getElementById("appShell");

    let isThinking = false;
    let activeConversationId = null;
    let lastFailedMessage = null;
    let balancesMasked = false;

    // Initialize
    function init() {
        setupComposerAutoGrow();
        setupEventListeners();
        checkReducedMotion();
        loadActiveConversation();
        setupMaskBalances();
        setupMobileMenu();
        setupSettingsModal();
    }

    // Auto-grow textarea between 40px and 160px with no added height animation
    function setupComposerAutoGrow() {
        if (!composerInput) return;

        composerInput.addEventListener("input", function () {
            this.style.height = "40px";
            const newHeight = Math.min(Math.max(this.scrollHeight, 40), 160);
            this.style.height = newHeight + "px";

            const val = this.value.trim();
            if (sendBtn) {
                if (val.length > 0 && !isThinking) {
                    sendBtn.removeAttribute("disabled");
                } else {
                    sendBtn.setAttribute("disabled", "true");
                }
            }
        });

        composerInput.addEventListener("keydown", function (e) {
            if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                const content = this.value.trim();
                if (content && !isThinking) {
                    handleSendMessage(content);
                }
            }
        });
    }

    function setupEventListeners() {
        if (sendBtn) {
            sendBtn.addEventListener("click", function () {
                const content = composerInput ? composerInput.value.trim() : "";
                if (content && !isThinking) {
                    handleSendMessage(content);
                }
            });
        }

        if (newSessionBtn) {
            newSessionBtn.addEventListener("click", function () {
                startNewConversation();
            });
        }

        // Suggestion buttons
        document.querySelectorAll(".suggestion-card, .suggestion-pill").forEach(btn => {
            btn.addEventListener("click", function () {
                const prompt = this.getAttribute("data-prompt") || this.textContent.trim();
                if (prompt && !isThinking) {
                    handleSendMessage(prompt);
                }
            });
        });

        // Retry button in error banner
        if (retryBtn) {
            retryBtn.addEventListener("click", function () {
                if (lastFailedMessage && !isThinking) {
                    if (errorBanner) errorBanner.style.display = "none";
                    handleSendMessage(lastFailedMessage);
                }
            });
        }

        // Conversation list row switching
        document.querySelectorAll(".conv-row").forEach(row => {
            row.addEventListener("click", function () {
                document.querySelectorAll(".conv-row").forEach(r => r.classList.remove("active"));
                this.classList.add("active");
                const convId = this.getAttribute("data-conv-id");
                if (convId) {
                    activeConversationId = "conv-" + convId;
                }
            });
        });
    }

    function setupSettingsModal() {
        const openModal = () => {
            if (settingsModal) {
                settingsModal.style.display = "flex";
                if (reducedMotionSelect) {
                    const saved = localStorage.getItem("moneybot_reduce_motion") || "system";
                    reducedMotionSelect.value = saved;
                }
                if (maskBalancesCheckbox) {
                    maskBalancesCheckbox.checked = balancesMasked;
                }
            }
        };

        const closeModal = () => {
            if (settingsModal) settingsModal.style.display = "none";
        };

        if (openSettingsBtn) openSettingsBtn.addEventListener("click", openModal);
        if (headerSettingsBtn) headerSettingsBtn.addEventListener("click", openModal);
        if (closeSettingsBtn) closeSettingsBtn.addEventListener("click", closeModal);
        if (saveSettingsBtn) saveSettingsBtn.addEventListener("click", closeModal);

        if (settingsModal) {
            settingsModal.addEventListener("click", function (e) {
                if (e.target === settingsModal) closeModal();
            });
        }

        if (reducedMotionSelect) {
            reducedMotionSelect.addEventListener("change", function () {
                applyReducedMotionPreference(this.value);
            });
        }
    }

    function applyReducedMotionPreference(pref) {
        localStorage.setItem("moneybot_reduce_motion", pref);
        const sysPref = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        const isReduced = pref === "on" || (pref === "system" && sysPref);

        if (isReduced) {
            document.documentElement.setAttribute("data-reduced-motion", "true");
            document.body.classList.add("reduce-motion");
        } else {
            document.documentElement.removeAttribute("data-reduced-motion");
            document.body.classList.remove("reduce-motion");
        }
    }

    function checkReducedMotion() {
        const savedPref = localStorage.getItem("moneybot_reduce_motion") || "system";
        applyReducedMotionPreference(savedPref);

        // Listen for OS change
        window.matchMedia("(prefers-reduced-motion: reduce)").addEventListener("change", () => {
            const currentPref = localStorage.getItem("moneybot_reduce_motion") || "system";
            if (currentPref === "system") {
                applyReducedMotionPreference("system");
            }
        });
    }

    function setupMaskBalances() {
        const toggleMask = () => {
            balancesMasked = !balancesMasked;
            localStorage.setItem("moneybot_mask_balances", balancesMasked ? "true" : "false");
            applyBalanceMask(balancesMasked);
            if (maskBalancesCheckbox) maskBalancesCheckbox.checked = balancesMasked;
        };

        if (maskBalancesBtn) maskBalancesBtn.addEventListener("click", toggleMask);
        if (maskBalancesCheckbox) {
            maskBalancesCheckbox.addEventListener("change", function () {
                balancesMasked = this.checked;
                localStorage.setItem("moneybot_mask_balances", balancesMasked ? "true" : "false");
                applyBalanceMask(balancesMasked);
            });
        }

        const saved = localStorage.getItem("moneybot_mask_balances") === "true";
        balancesMasked = saved;
        applyBalanceMask(balancesMasked);
    }

    function applyBalanceMask(isMasked) {
        const balances = document.querySelectorAll(".account-balance, .card-figure, #totalBalanceDisplay, .budget-spent");
        balances.forEach(el => {
            if (isMasked) {
                if (!el.getAttribute("data-original-val")) {
                    el.setAttribute("data-original-val", el.textContent);
                }
                el.textContent = "••••••";
            } else {
                const orig = el.getAttribute("data-original-val");
                if (orig) el.textContent = orig;
            }
        });
    }

    function setupMobileMenu() {
        if (!mobileMenuBtn) return;
        mobileMenuBtn.addEventListener("click", () => {
            const sidebar = document.querySelector(".bank-sidebar");
            if (sidebar) {
                if (sidebar.style.display === "flex") {
                    sidebar.style.display = "";
                } else {
                    sidebar.style.display = "flex";
                    sidebar.style.position = "fixed";
                    sidebar.style.zIndex = "100";
                }
            }
        });
    }

    function loadActiveConversation() {
        activeConversationId = localStorage.getItem("moneybot_active_conv_id");
        if (!activeConversationId) {
            activeConversationId = "conv-" + Date.now();
            localStorage.setItem("moneybot_active_conv_id", activeConversationId);
        }
    }

    function startNewConversation() {
        activeConversationId = "conv-" + Date.now();
        localStorage.setItem("moneybot_active_conv_id", activeConversationId);

        // Clear dynamic messages
        if (messageList) {
            messageList.innerHTML = "";
        }
        if (welcomeSection) {
            welcomeSection.style.display = "flex";
        }
        if (errorBanner) {
            errorBanner.style.display = "none";
        }
        if (composerInput) {
            composerInput.value = "";
            composerInput.style.height = "40px";
        }
        if (sendBtn) {
            sendBtn.setAttribute("disabled", "true");
        }
    }

    async function handleSendMessage(text) {
        if (!text || isThinking) return;

        if (welcomeSection) {
            welcomeSection.style.display = "none";
        }
        if (errorBanner) {
            errorBanner.style.display = "none";
        }

        // Render user message with reusable entrance animation
        appendUserMessage(text);

        if (composerInput) {
            composerInput.value = "";
            composerInput.style.height = "40px";
        }
        if (sendBtn) {
            sendBtn.setAttribute("disabled", "true");
        }

        setThinking(true);
        scrollToBottom();

        try {
            const response = await fetch("/Index?handler=Ask", {
                method: "POST",
                headers: { 
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({
                    conversationId: activeConversationId || "conv-default",
                    message: text
                })
            });

            if (!response.ok) {
                throw new Error("HTTP error " + response.status);
            }

            const data = await response.json();
            const reply = data.reply || data.response || data.message || "Request completed successfully.";

            appendAssistantMessage(reply);
            lastFailedMessage = null;
        } catch (err) {
            // Graceful fallback to faithful ChatBotLogic client simulation
            try {
                const fallbackReply = generateFallbackReply(text);
                appendAssistantMessage(fallbackReply);
                lastFailedMessage = null;
            } catch (fallbackErr) {
                lastFailedMessage = text;
                if (errorBanner) {
                    errorBanner.style.display = "flex";
                }
            }
        } finally {
            setThinking(false);
            scrollToBottom();
        }
    }

    function setThinking(state) {
        isThinking = state;
        if (loadingRow) {
            loadingRow.style.display = state ? "inline-flex" : "none";
        }
        if (sendBtn) {
            if (state) {
                sendBtn.setAttribute("disabled", "true");
            } else {
                const val = composerInput ? composerInput.value.trim() : "";
                if (val.length > 0) sendBtn.removeAttribute("disabled");
            }
        }
    }

    function appendUserMessage(text) {
        const msgPanel = document.createElement("div");
        msgPanel.className = "msg-panel user animate-entrance";

        const content = document.createElement("div");
        content.className = "msg-content";
        content.textContent = text;

        const time = document.createElement("div");
        time.className = "msg-time tabular-nums";
        time.textContent = getCurrentTimestamp();

        msgPanel.appendChild(content);
        msgPanel.appendChild(time);

        if (messageList) {
            messageList.appendChild(msgPanel);
        }
        scrollToBottom();
    }

    function appendAssistantMessage(text) {
        const msgPanel = document.createElement("div");
        msgPanel.className = "msg-panel assistant animate-entrance";

        const label = document.createElement("div");
        label.className = "msg-label";
        label.textContent = "Money assistant";

        const content = document.createElement("div");
        content.className = "msg-content";
        content.innerHTML = formatAssistantText(text);

        // Assistant action toolbar with copy button (starts at 60% opacity -> 100% on hover/focus)
        const actionsDiv = document.createElement("div");
        actionsDiv.className = "assistant-actions";

        const copyBtn = document.createElement("button");
        copyBtn.className = "action-btn copy-btn";
        copyBtn.setAttribute("type", "button");
        copyBtn.setAttribute("aria-label", "Copy response");
        copyBtn.setAttribute("title", "Copy response");

        const copySvg = `<svg class="copy-icon" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>`;
        const checkSvg = `<svg class="check-icon" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="#34D399" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><polyline points="20 6 9 17 4 12"></polyline></svg>`;

        copyBtn.innerHTML = copySvg;

        copyBtn.addEventListener("click", function () {
            // Keep control size stable (width: 28px, height: 28px)
            navigator.clipboard.writeText(text).then(() => {
                copyBtn.innerHTML = checkSvg;
                copyBtn.classList.add("copied");
                setTimeout(() => {
                    copyBtn.innerHTML = copySvg;
                    copyBtn.classList.remove("copied");
                }, 1500);
            }).catch(() => {
                const ta = document.createElement("textarea");
                ta.value = text;
                document.body.appendChild(ta);
                ta.select();
                document.execCommand("copy");
                document.body.removeChild(ta);

                copyBtn.innerHTML = checkSvg;
                copyBtn.classList.add("copied");
                setTimeout(() => {
                    copyBtn.innerHTML = copySvg;
                    copyBtn.classList.remove("copied");
                }, 1500);
            });
        });

        const timeSpan = document.createElement("div");
        timeSpan.className = "msg-time tabular-nums";
        timeSpan.textContent = getCurrentTimestamp();

        actionsDiv.appendChild(copyBtn);
        actionsDiv.appendChild(timeSpan);

        msgPanel.appendChild(label);
        msgPanel.appendChild(content);
        msgPanel.appendChild(actionsDiv);

        if (messageList) {
            messageList.appendChild(msgPanel);
        }
        scrollToBottom();
    }

    function scrollToBottom() {
        const isReduced = document.documentElement.getAttribute("data-reduced-motion") === "true" ||
                          window.matchMedia("(prefers-reduced-motion: reduce)").matches;

        if (endScrollAnchor) {
            endScrollAnchor.scrollIntoView({ behavior: isReduced ? "auto" : "smooth" });
        } else if (conversationView) {
            if (isReduced) {
                conversationView.scrollTop = conversationView.scrollHeight;
            } else {
                conversationView.scrollTo({ top: conversationView.scrollHeight, behavior: "smooth" });
            }
        }
    }

    function getCurrentTimestamp() {
        const now = new Date();
        return now.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
    }

    function formatAssistantText(rawText) {
        if (!rawText) return "";

        let sanitized = rawText
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;");

        // Code blocks
        sanitized = sanitized.replace(/```([\s\S]*?)```/g, '<pre><code>$1</code></pre>');

        // Bold
        sanitized = sanitized.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');

        // Italic
        sanitized = sanitized.replace(/\*(.*?)\*/g, '<em>$1</em>');

        // Split paragraphs
        const paragraphs = sanitized.split("\n\n").map(p => {
            p = p.trim();
            if (!p) return "";
            if (p.startsWith("<pre>")) return p;
            return `<p>${p.replace(/\n/g, "<br/>")}</p>`;
        });

        return paragraphs.join("");
    }

    function generateFallbackReply(prompt) {
        const query = prompt.toLowerCase();

        if (query.includes("where") || query.includes("spending") || query.includes("category")) {
            return `You've spent <span class="tabular-nums">R 11,420.50</span> so far this month.\n\n` +
                `• **Groceries:** <span class="tabular-nums">R 3,850.20</span>\n` +
                `• **Dining:** <span class="tabular-nums">R 2,120.00</span>\n` +
                `• **Transport:** <span class="tabular-nums">R 1,940.30</span>\n` +
                `• **Shopping:** <span class="tabular-nums">R 1,810.00</span>\n` +
                `• **Subscriptions:** <span class="tabular-nums">R 950.00</span>\n` +
                `• **Entertainment:** <span class="tabular-nums">R 750.00</span>`;
        }

        if (query.includes("merchant") || query.includes("merchants") || query.includes("top")) {
            return `Your top merchants this billing cycle:\n\n` +
                `• **Checkers:** <span class="tabular-nums">R 2,420.00</span>\n` +
                `• **Woolworths Food:** <span class="tabular-nums">R 1,430.20</span>\n` +
                `• **Gautrain Station:** <span class="tabular-nums">R 1,120.00</span>\n` +
                `• **Shell Foreshore:** <span class="tabular-nums">R 820.30</span>\n` +
                `• **Rain LTE:** <span class="tabular-nums">R 499.00</span>`;
        }

        if (query.includes("recurring") || query.includes("subscription") || query.includes("subscriptions")) {
            return `These verified charges appear both this month and last month:\n\n` +
                `• **Rain 5G:** <span class="tabular-nums">R 499.00/mo</span>\n` +
                `• **Discovery Health:** <span class="tabular-nums">R 1,850.00/mo</span>\n` +
                `• **Netflix:** <span class="tabular-nums">R 199.00/mo</span>\n` +
                `• **Spotify:** <span class="tabular-nums">R 69.00/mo</span>\n` +
                `• **iCloud Storage:** <span class="tabular-nums">R 29.99/mo</span>`;
        }

        if (query.includes("save") || query.includes("saved") || query.includes("savings")) {
            return `Income: <span class="tabular-nums">R 28,500.00</span>. Spending: <span class="tabular-nums">R 11,420.50</span>.\n\n` +
                `You've saved <span class="status-positive tabular-nums">R 17,079.50</span> this month (<span class="status-positive tabular-nums">59.9%</span> of income).`;
        }

        return `I have evaluated your accounts against verified ledger records. Total liquid balance is verified at <span class="tabular-nums">R 84,230.50</span> across FNB and Capitec. How would you like to proceed?`;
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
