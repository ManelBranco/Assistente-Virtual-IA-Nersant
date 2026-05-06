// Variáveis globais para estatísticas
let totalThinkingTime = 0;      // Tempo total que a IA "pensou"
let messagesSentCount = 0;      // Número total de mensagens enviadas
let conversationsCreated = 0;   // Número de conversas criadas
let conversationHistory = [];    // Array com histórico de conversas
let isSending = false;          // Evitar envios duplicados

// Função para formatar o tempo de forma legível (segundos ou minutos:segundos)
function formatThinkingTime(ms) {
    const totalSeconds = ms / 1000;
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = (totalSeconds % 60).toFixed(2);
    
    if (minutes > 0) {
        return `${minutes}:${seconds.padStart(5, '0')}s`;  // Ex: 1:23.45s
    }
    return `${seconds}s`;  // Ex: 45.67s
}

// Formatar tempo para exibição (versão mais simples)
function formatTimeDisplay(seconds) {
    if (seconds >= 60) {
        const minutes = Math.floor(seconds / 60);
        const remainingSeconds = (seconds % 60).toFixed(2);
        return `${minutes}:${remainingSeconds.padStart(5, '0')}s`;
    }
    return `${seconds.toFixed(2)}s`;
}

// Renderiza a resposta da IA em Markdown convertido para HTML
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

// Atualizar os valores no modal de estatísticas
function updateStatsDisplay() {
    const modalThinking = document.getElementById("modalThinking");
    const modalMessages = document.getElementById("modalMessages");
    const modalConversations = document.getElementById("modalConversations");
    
    if (modalThinking) modalThinking.textContent = formatThinkingTime(totalThinkingTime);
    if (modalMessages) modalMessages.textContent = messagesSentCount;
    if (modalConversations) modalConversations.textContent = conversationsCreated;
}

// Abrir o modal de estatísticas
function showStatsModal() {
    updateStatsDisplay();                    // Atualizar valores antes de mostrar
    const modal = document.getElementById("statsModal");
    if (modal) {
        modal.classList.add("active");       // Adicionar classe que mostra o modal
    }
}

// Fechar o modal de estatísticas
function closeStatsModal() {
    const modal = document.getElementById("statsModal");
    if (modal) {
        modal.classList.remove("active");    // Remover classe que mostra o modal
    }
}

// Fechar modal se clicar fora do conteúdo (no fundo escuro)
window.addEventListener("click", (event) => {
    const modal = document.getElementById("statsModal");
    if (event.target === modal) {            // Se clicou no fundo (não no conteúdo)
        closeStatsModal();
    }
});

// Carregar histórico de conversas do servidor
async function loadHistory() {
    const res = await fetch("/api/history");     // Buscar histórico
    const data = await res.json();

    conversationHistory = data;
    conversationsCreated = conversationHistory.length + 1;  // +1 para conversa atual
    updateStatsDisplay();

    const box = document.getElementById("conversations");
    if (!box) return;

    box.innerHTML = "";  // Limpar lista

    // Criar elemento para cada conversa no histórico
    conversationHistory.forEach((c) => {
        const convDiv = document.createElement("div");
        convDiv.className = "conv";
        convDiv.innerHTML = `
            <div class="conv-title">${c.title || "Conversa sem título"}</div>
            <button class="conv-delete" onclick="deleteConversation(${c.id}, event)">X</button>
        `;
        convDiv.onclick = () => loadConversation(c.id);  // Clicar na conversa carrega-a
        box.appendChild(convDiv);
    });
}

// Carregar uma conversa específica pelo ID
async function loadConversation(id) {
    const res = await fetch(`/api/conversation/${id}`);
    const conversation = await res.json();
    
    const chat = document.getElementById("chat");
    if (!chat) return;
    
    chat.innerHTML = "";  // Limpar chat atual
    
    // Mostrar todas as mensagens da conversa
    conversation.messages.forEach((msg) => {
        const messageClass = msg.role === "user" ? "user" : "bot";
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
    
    chat.scrollTop = chat.scrollHeight;  // Scroll para o fundo
}

// Apagar uma conversa específica
async function deleteConversation(id, event) {
    event.stopPropagation();  // Evitar que o clique no botão também clique na conversa
    
    if (confirm("Tens a certeza que queres apagar esta conversa?")) {
        await fetch(`/api/conversation/${id}`, { method: "DELETE" });  // Pedir ao servidor para apagar
        await loadHistory();  // Recarregar histórico
    }
}

// Limpar TODO o histórico (apagar todas as conversas)
async function clearAllHistory() {
    if (confirm("Isto vai apagar TODAS as conversas! Tens a certeza?")) {
        await fetch("/api/clear-history", { method: "POST" });
        const chat = document.getElementById("chat");
        if (chat) {
            chat.innerHTML = "";  // Limpar chat
        }
        // Reset das estatísticas
        totalThinkingTime = 0;
        messagesSentCount = 0;
        conversationsCreated = 1;
        updateStatsDisplay();
        await loadHistory();  // Recarregar histórico (vazio)
    }
}

// Criar uma nova conversa
async function newChat() {
    await fetch("/api/new-chat", { method: "POST" });

    const chat = document.getElementById("chat");
    if (chat) chat.innerHTML = "";  // Limpar chat
    messagesSentCount = 0;
    conversationsCreated += 1;      // Incrementar contador
    updateStatsDisplay();

    await loadHistory();  // Recarregar histórico
}

// Filtrar conversas na sidebar (pesquisa)
function filterConversations() {
    const searchInput = document.getElementById("search");
    if (!searchInput) return;

    const query = searchInput.value.toLowerCase();
    const convs = document.querySelectorAll(".conv");

    convs.forEach((conv) => {
        const title = conv.querySelector(".conv-title")?.textContent.toLowerCase() || "";
        // Mostrar apenas conversas cujo título contenha o texto pesquisado
        conv.style.display = title.includes(query) ? "flex" : "none";
    });
}

// Enviar mensagem para a IA
async function send() {
    // Evitar envios múltiplos
    if (isSending) return;
    
    const input = document.getElementById("input");
    const chat = document.getElementById("chat");
    const modelSelect = document.getElementById("model");

    if (!input || !chat || !modelSelect) return;

    const model = modelSelect.value;
    const message = input.value.trim();
    if (!message) return;

    isSending = true;
    
    // ⭐ LIMPAR O INPUT IMEDIATAMENTE (antes de qualquer outra ação)
    input.value = "";

    // Mostrar mensagem do utilizador no chat
    const userMessageDiv = document.createElement("div");
    userMessageDiv.className = "message user";
    userMessageDiv.textContent = message;
    chat.appendChild(userMessageDiv);
    
    // Mostrar indicador de "A pensar..." com temporizador
    const loadingDiv = document.createElement("div");
    loadingDiv.className = "message bot";
    loadingDiv.id = "loading";
    loadingDiv.innerHTML = `A pensar... <span id="timer">0.00s</span>`;
    chat.appendChild(loadingDiv);
    
    const startTime = Date.now();  // Marcar início
    
    // Atualizar temporizador a cada 100ms com formato minutos:segundos
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
    
    // Enviar mensagem ao servidor
    const res = await fetch("/api/chat", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({ message, model })
    });

    const data = await res.json();  // Receber resposta
    
    clearInterval(timerInterval);   // Parar temporizador
    const loadingElement = document.getElementById("loading");
    if (loadingElement) loadingElement.remove();  // Remover indicador de "A pensar..."
    
    // Atualizar estatísticas com tempo formatado
    totalThinkingTime += data.time || 0;
    messagesSentCount += 1;
    updateStatsDisplay();
    
    // Formatar tempo final para exibição
    const totalSeconds = data.time ? (data.time / 1000) : 0;
    const timeText = formatTimeDisplay(totalSeconds);
    
    // Mostrar resposta da IA no chat com Markdown convertido para HTML
    renderBotMessage(data.reply, timeText);

    chat.scrollTop = chat.scrollHeight;  // Scroll para o fundo

    await loadHistory();  // Recarregar histórico (para mostrar nova conversa na sidebar)
    
    isSending = false;
}

// Configurar eventos quando a página carregar
window.addEventListener("DOMContentLoaded", () => {
    loadHistory();
    updateStatsDisplay();
    
    // Configurar evento de pesquisa em tempo real
    const searchInput = document.getElementById("search");
    if (searchInput) {
        searchInput.addEventListener("input", filterConversations);
    }
    
    // Enviar mensagem com tecla Enter
    const messageInput = document.getElementById("input");
    if (messageInput) {
        messageInput.addEventListener("keypress", function(event) {
            // Verificar se a tecla pressionada é o Enter
            if (event.key === "Enter") {
                event.preventDefault();  // Evitar comportamento padrão (como enviar formulário)
                send();                  // Chamar a função de enviar mensagem
            }
        });
    }
});