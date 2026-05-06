
// Variáveis globais para estatísticas (agora carregadas do servidor)
let totalThinkingTime = 0;
let messagesSentCount = 0;
let conversationsCreated = 0;
let conversationHistory = [];
let isSending = false;

// Variáveis globais para estatísticas
let totalThinkingTime = 0;      // Tempo total que a IA "pensou"
let messagesSentCount = 0;      // Número total de mensagens enviadas
let conversationsCreated = 0;   // Número de conversas criadas
let conversationHistory = [];    // Array com histórico de conversas
let isSending = false;          // Evitar envios duplicados
let isCreatingChat = false;     // Evitar cliques duplicados em nova conversa


// Função para formatar o tempo de forma legível
function formatThinkingTime(ms) {
    const totalSeconds = ms / 1000;
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = (totalSeconds % 60).toFixed(2);
    
    if (minutes > 0) {
        return `${minutes}:${seconds.padStart(5, '0')}s`;
    }
    return `${seconds}s`;
}

function formatTimeDisplay(seconds) {
    if (seconds >= 60) {
        const minutes = Math.floor(seconds / 60);
        const remainingSeconds = (seconds % 60).toFixed(2);
        return `${minutes}:${remainingSeconds.padStart(5, '0')}s`;
    }
    return `${seconds.toFixed(2)}s`;
}

// Carregar estatísticas do servidor
async function loadStats() {
    try {
        const res = await fetch("/api/stats");
        const stats = await res.json();
        totalThinkingTime = stats.totalThinkingTime || 0;
        messagesSentCount = stats.messagesSentCount || 0;
        conversationsCreated = stats.totalConversations || 0;
        updateStatsDisplay();
    } catch (error) {
        console.error("Erro ao carregar estatísticas:", error);
    }
}

// Atualizar estatísticas no servidor
async function updateStatsOnServer(thinkingTime = 0, messagesSent = 0) {
    try {
        await fetch("/api/stats/update", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                thinkingTime: thinkingTime,
                messagesSent: messagesSent
            })
        });
    } catch (error) {
        console.error("Erro ao atualizar estatísticas:", error);
    }
}

// Renderiza a resposta da IA
function renderBotMessage(markdownText, metaText) {
    const chat = document.getElementById("chat");
    if (!chat) return;

    const messageDiv = document.createElement("div");
    messageDiv.className = "message bot";
    const botHtml = marked.parse(markdownText || "");
    messageDiv.innerHTML = `
        ${botHtml}
        <div class="meta">${metaText}</div>
    `;

    chat.appendChild(messageDiv);
}

// Atualizar os valores no modal
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

// Fechar modal se clicar fora
window.addEventListener("click", (event) => {
    const modal = document.getElementById("statsModal");
    if (event.target === modal) {
        closeStatsModal();
    }
});

// Carregar histórico
async function loadHistory() {
    const res = await fetch("/api/history");
    const data = await res.json();

    conversationHistory = data;

    // Não atualizar conversationsCreated aqui pois já vem do servidor
    

    conversationsCreated = conversationHistory.length;  // Contar apenas conversas guardadas
    updateStatsDisplay();


    const box = document.getElementById("conversations");
    if (!box) return;

    box.innerHTML = "";

    conversationHistory.forEach((c) => {
        const convDiv = document.createElement("div");
        convDiv.className = "conv";
        convDiv.innerHTML = `
            <div class="conv-title">${c.title || "Conversa sem título"}</div>
            <button class="conv-delete" onclick="deleteConversation(${c.id}, event)">X</button>
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
        if (msg.role === "bot") {
            const botHtml = marked.parse(msg.text || "");
            chat.innerHTML += `<div class="message bot">${botHtml}</div>`;
        } else {
            const userDiv = document.createElement("div");
            userDiv.className = "message user";
            userDiv.textContent = msg.text;
            chat.appendChild(userDiv);
        }
    });
    
    chat.scrollTop = chat.scrollHeight;
}

async function deleteConversation(id, event) {
    event.stopPropagation();
    
    if (confirm("Tens a certeza que queres apagar esta conversa?")) {
        await fetch(`/api/conversation/${id}`, { method: "DELETE" });
        await loadHistory();
        await loadStats(); // Recarregar estatísticas
    }
}

async function clearAllHistory() {
    if (confirm("Isto vai apagar TODAS as conversas! Tens a certeza?")) {
        await fetch("/api/clear-history", { method: "POST" });
        const chat = document.getElementById("chat");
        if (chat) {
            chat.innerHTML = "";
        }

        
        await loadStats(); // Recarregar estatísticas (serão zero)
        await loadHistory();

        // Reset das estatísticas
        totalThinkingTime = 0;
        messagesSentCount = 0;
        conversationsCreated = 0;
        updateStatsDisplay();
        await loadHistory();  // Recarregar histórico (vazio)

    }
}

async function newChat() {
    if (isCreatingChat) return;
    isCreatingChat = true;


    const chat = document.getElementById("chat");
    if (chat) chat.innerHTML = "";
    
    await loadStats(); // Recarregar estatísticas (conversasCreated já incrementou no servidor)
    await loadHistory();

    try {
        await fetch("/api/new-chat", { method: "POST" });

        const chat = document.getElementById("chat");
        if (chat) chat.innerHTML = "";  // Limpar chat
        messagesSentCount = 0;
        updateStatsDisplay();

        await loadHistory();  // Recarregar histórico
    } finally {
        isCreatingChat = false;
    }

}

function filterConversations() {
    const searchInput = document.getElementById("search");
    if (!searchInput) return;

    const query = searchInput.value.toLowerCase();
    const convs = document.querySelectorAll(".conv");

    convs.forEach((conv) => {
        const title = conv.querySelector(".conv-title")?.textContent.toLowerCase() || "";
        conv.style.display = title.includes(query) ? "flex" : "none";
    });
}

async function send() {
    if (isSending) return;
    
    const input = document.getElementById("input");
    const chat = document.getElementById("chat");
    const modelSelect = document.getElementById("model");

    if (!input || !chat || !modelSelect) return;

    const model = modelSelect.value;
    const message = input.value.trim();
    if (!message) return;

    isSending = true;
    
    input.value = "";

    // Mostrar mensagem do utilizador
    const userMessageDiv = document.createElement("div");
    userMessageDiv.className = "message user";
    userMessageDiv.textContent = message;
    chat.appendChild(userMessageDiv);
    
    // Mostrar indicador de "A pensar..."
    const loadingDiv = document.createElement("div");
    loadingDiv.className = "message bot";
    loadingDiv.id = "loading";
    loadingDiv.innerHTML = `A pensar... <span id="timer">0.00s</span>`;
    chat.appendChild(loadingDiv);
    
    const startTime = Date.now();
    
    const timerInterval = setInterval(() => {
        const elapsed = Date.now() - startTime;
        const totalSeconds = elapsed / 1000;
        const minutes = Math.floor(totalSeconds / 60);
        const seconds = (totalSeconds % 60).toFixed(2);
        
        let timeText;
        if (minutes > 0) {
            timeText = `${minutes}:${seconds.padStart(5, '0')}s`;
        } else {
            timeText = `${seconds}s`;
        }
        
        const timerElement = document.getElementById("timer");
        if (timerElement) {
            timerElement.textContent = timeText;
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
    const loadingElement = document.getElementById("loading");
    if (loadingElement) loadingElement.remove();
    
    // Atualizar estatísticas globais
    if (data.time) {
        totalThinkingTime += data.time;
        messagesSentCount += 1;
        
        // Enviar atualização para o servidor
        await updateStatsOnServer(data.time, 1);
        updateStatsDisplay();
    }
    
    const totalSeconds = data.time ? (data.time / 1000) : 0;
    const timeText = formatTimeDisplay(totalSeconds);
    
    renderBotMessage(data.reply, timeText);

    chat.scrollTop = chat.scrollHeight;

    await loadHistory();
    
    isSending = false;
}

// Inicializar tudo quando a página carregar
window.addEventListener("DOMContentLoaded", async () => {
    await loadStats();
    await loadHistory();
    
    const searchInput = document.getElementById("search");
    if (searchInput) {
        searchInput.addEventListener("input", filterConversations);
    }
    
    const messageInput = document.getElementById("input");
    if (messageInput) {
        messageInput.addEventListener("keypress", function(event) {
            if (event.key === "Enter") {
                event.preventDefault();
                send();
            }
        });
    }
});