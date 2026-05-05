let totalThinkingTime = 0;
let messagesSentCount = 0;
let conversationsCreated = 0;
let conversationHistory = [];

function formatThinkingTime(ms) {
    const seconds = ms / 1000;
    if (seconds < 60) {
        return `${seconds.toFixed(2)}s`;
    }
    const minutes = Math.floor(seconds / 60);
    const remainder = (seconds % 60).toFixed(2).padStart(5, '0');
    return `${minutes}:${remainder}s`;
}

function updateStatsDisplay() {
    const modalThinking = document.getElementById("modalThinking");
    const modalMessages = document.getElementById("modalMessages");
    const modalConversations = document.getElementById("modalConversations");
    
    if (modalThinking) modalThinking.textContent = formatThinkingTime(totalThinkingTime);
    if (modalMessages) modalMessages.textContent = messagesSentCount;
    if (modalConversations) modalConversations.textContent = conversationsCreated;
}

function showStatsModal() {
    updateStatsDisplay();
    const modal = document.getElementById("statsModal");
    if (modal) {
        modal.classList.add("active");
    }
}

function closeStatsModal() {
    const modal = document.getElementById("statsModal");
    if (modal) {
        modal.classList.remove("active");
    }
}

// Fechar modal ao clicar fora do conteúdo
window.addEventListener("click", (event) => {
    const modal = document.getElementById("statsModal");
    if (event.target === modal) {
        closeStatsModal();
    }
});

async function loadHistory() {
    const res = await fetch("/api/history");
    const data = await res.json();

    conversationHistory = data;
    conversationsCreated = conversationHistory.length + 1;
    updateStatsDisplay();

    const box = document.getElementById("conversations");
    if (!box) {
        return;
    }

    box.innerHTML = "";

    conversationHistory.forEach((c) => {
        const convDiv = document.createElement("div");
        convDiv.className = "conv";
        convDiv.innerHTML = `
            <div class="conv-title">${c.title || "Conversa sem título"}</div>
            <button class="conv-delete" onclick="deleteConversation(${c.id}, event)">🗑️</button>
        `;
        convDiv.onclick = () => loadConversation(c.id);
        box.appendChild(convDiv);
    });
}

async function loadConversation(id) {
    const res = await fetch(`/api/conversation/${id}`);
    const conversation = await res.json();
    
    const chat = document.getElementById("chat");
    if (!chat) return;
    
    chat.innerHTML = "";
    
    conversation.messages.forEach((msg) => {
        const messageClass = msg.role === "user" ? "user" : "bot";
        chat.innerHTML += `<div class="message ${messageClass}">${msg.text}</div>`;
    });
    
    chat.scrollTop = chat.scrollHeight;
}

async function deleteConversation(id, event) {
    event.stopPropagation();
    
    if (confirm("Tens a certeza que queres apagar esta conversa?")) {
        await fetch(`/api/conversation/${id}`, { method: "DELETE" });
        await loadHistory();
    }
}

async function clearAllHistory() {
    if (confirm("Isto vai apagar TODAS as conversas! Tens a certeza?")) {
        await fetch("/api/clear-history", { method: "POST" });
        const chat = document.getElementById("chat");
        if (chat) {
            chat.innerHTML = "";
        }
        totalThinkingTime = 0;
        messagesSentCount = 0;
        conversationsCreated = 1;
        updateStatsDisplay();
        await loadHistory();
    }
}

async function newChat() {
    await fetch("/api/new-chat", { method: "POST" });

    const chat = document.getElementById("chat");
    if (chat) chat.innerHTML = "";
    messagesSentCount = 0;
    conversationsCreated += 1;
    updateStatsDisplay();

    await loadHistory();
}

function filterConversations() {
    const searchInput = document.getElementById("search");
    if (!searchInput) {
        return;
    }

    const query = searchInput.value.toLowerCase();
    const convs = document.querySelectorAll(".conv");

    convs.forEach((conv) => {
        const title = conv.querySelector(".conv-title")?.textContent.toLowerCase() || "";
        conv.style.display = title.includes(query) ? "flex" : "none";
    });
}

async function send() {
    const input = document.getElementById("input");
    const chat = document.getElementById("chat");
    const modelSelect = document.getElementById("model");

    if (!input || !chat || !modelSelect) {
        return;
    }

    const model = modelSelect.value;
    const message = input.value.trim();
    if (!message) {
        return;
    }

    chat.innerHTML += `<div class="message user">${message}</div>`;
    
    chat.innerHTML += `<div class="message bot" id="loading">A pensar... <span id="timer">0.00</span>s</div>`;
    
    const startTime = Date.now();
    
    const timerInterval = setInterval(() => {
        const elapsed = Date.now() - startTime;
        const seconds = (elapsed / 1000).toFixed(2);
        const timerElement = document.getElementById("timer");
        if (timerElement) {
            timerElement.textContent = seconds;
        }
    }, 100);
    
    const res = await fetch("/api/chat", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({ message, model })
    });

    const data = await res.json();
    
    clearInterval(timerInterval);
    document.getElementById("loading").remove();
    
    const totalSeconds = data.time ? (data.time / 1000).toFixed(2) : "0.00";
    totalThinkingTime += data.time || 0;
    messagesSentCount += 1;
    updateStatsDisplay();
    
    chat.innerHTML += `
    <div class="message bot">
        ${data.reply}
        <div class="meta"> ${totalSeconds}s</div>
    </div>
    `;

    input.value = "";
    chat.scrollTop = chat.scrollHeight;

    await loadHistory();
}

// Configurar pesquisa ao carregar a página
window.addEventListener("DOMContentLoaded", () => {
    loadHistory();
    updateStatsDisplay();
    
    // Adicionar evento de pesquisa
    const searchInput = document.getElementById("search");
    if (searchInput) {
        searchInput.addEventListener("input", filterConversations);
    }
});