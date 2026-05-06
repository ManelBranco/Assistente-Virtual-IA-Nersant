// Importar bibliotecas necessárias
const express = require("express");  // Framework para criar o servidor web
const fs = require("fs");            // Módulo para ler/escrever ficheiros
const path = require("path");        // Módulo para manipular caminhos de ficheiros

const app = express();               // Criar a aplicação Express
app.use(express.json());             // Permitir que o servidor leia JSON no corpo das requisições

// Rota principal - serve a página HTML do chat
app.get("/", (req, res) => {
    res.sendFile(path.join(__dirname, "public", "index_working.html"));
});
app.use(express.static("public"));   // Servir ficheiros estáticos (CSS, JS, imagens)

// Variáveis para guardar conversas em memória
let conversations = [];              // Array com todas as conversas
let currentId = 0;                  // ID para a próxima conversa nova
let currentConversation = {         // Conversa atual (a que está aberta)
    id: currentId,
    messages: [],
    title: "Nova conversa"
};

// Ao iniciar, carregar conversas anteriores do ficheiro conversas.json
if (fs.existsSync("conversas.json")) {
    try {
        const data = fs.readFileSync("conversas.json", "utf-8");
        conversations = JSON.parse(data);
        if (conversations.length > 0) {
            currentId = conversations[conversations.length - 1].id + 1;
        }
    } catch (error) {
        console.error("Erro ao ler conversas.json:", error);
    }
}

// Criar nova conversa atual
currentConversation = {
    id: currentId,
    messages: [],
    title: "Nova conversa"
};

// Endpoint para enviar mensagens ao modelo de IA (Ollama)
app.post("/api/chat", async (req, res) => {
    const { message, model } = req.body;  // Receber mensagem e modelo escolhido
    const systemPrompt = `Responde sempre em português de Portugal. Sê natural, direto e formal. Apenas na primeira mensagem apresenta-te como assistente virtual de Inteligência Artificial da Nersant de Torres Novas. Nas restantes mensagens não repitas a apresentação. Pergunta sempre em que podes ajudar.`;
    const fullPrompt = systemPrompt + "\n\nUtilizador: " + message;  // Juntar prompt do sistema com a mensagem
    const start = Date.now();  // Marcar início para calcular tempo de resposta

    try {
        // Adicionar mensagem do utilizador ao histórico da conversa
        currentConversation.messages.push({
            role: "user",
            text: message
        });

        // Se for a primeira mensagem, criar título automático baseado nela
        if (currentConversation.messages.length === 1) {
            currentConversation.title = message.substring(0, 30).trim();
            if (message.length > 30) {
                currentConversation.title += "...";
            }
        }

        // Chamar a API do Ollama (modelo de IA local)
        const response = await fetch("http://localhost:11434/api/generate", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                model: model,           // Modelo escolhido (granite, gemma, llama)
                prompt: fullPrompt,    // Prompt completo
                stream: false          // Não usar streaming, esperar resposta completa
            })
        });

        const data = await response.json();  // Resposta da IA
    
        const end = Date.now();
        const duration = end - start;        // Calcular tempo de resposta
        console.log("Tempo calculado:", duration, "ms");

        // Adicionar resposta do bot ao histórico
        currentConversation.messages.push({
            role: "bot",
            text: data.response
        });

        // Guardar conversa atualizada no ficheiro JSON
        const existingIndex = conversations.findIndex(c => c.id === currentConversation.id);
        
        if (existingIndex !== -1) {
            // Atualizar conversa existente
            conversations[existingIndex] = JSON.parse(JSON.stringify(currentConversation));
        } else {
            // Adicionar nova conversa
            if (currentConversation.messages.length > 0) {
                conversations.push(JSON.parse(JSON.stringify(currentConversation)));
            }
        }
        
        fs.writeFileSync("conversas.json", JSON.stringify(conversations, null, 2));  // Guardar no disco

        // Devolver resposta ao frontend
        res.json({
            reply: data.response,
            time: duration
        });

    } catch (error) {
        console.error(error);
        res.json({ reply: "Erro no servidor", time: 0 });
    }
});

// Endpoint para obter histórico de todas as conversas
app.get("/api/history", (req, res) => {
    res.json(conversations);
});

// Endpoint para criar uma nova conversa
app.post("/api/new-chat", (req, res) => {
    // Guardar conversa atual se tiver mensagens
    if (currentConversation.messages.length > 0) {
        const existingIndex = conversations.findIndex(c => c.id === currentConversation.id);
        if (existingIndex === -1) {
            conversations.push(JSON.parse(JSON.stringify(currentConversation)));
            fs.writeFileSync("conversas.json", JSON.stringify(conversations, null, 2));
        }
    }

    currentId++;  // Incrementar ID para nova conversa

    // Criar nova conversa vazia
    currentConversation = {
        id: currentId,
        messages: [],
        title: "Nova conversa"
    };

    res.json({ ok: true });
});

// Endpoint para carregar uma conversa específica pelo ID
app.get("/api/conversation/:id", (req, res) => {
    const convId = parseInt(req.params.id);
    const conversation = conversations.find(c => c.id === convId);
    
    if (conversation) {
        res.json(conversation);
    } else {
        res.status(404).json({ error: "Conversa não encontrada" });
    }
});

// Endpoint para apagar uma conversa específica
app.delete("/api/conversation/:id", (req, res) => {
    const convId = parseInt(req.params.id);
    const index = conversations.findIndex(c => c.id === convId);
    
    if (index !== -1) {
        conversations.splice(index, 1);  // Remover do array
        fs.writeFileSync("conversas.json", JSON.stringify(conversations, null, 2));  // Atualizar ficheiro
        
        // Se a conversa apagada for a atual, criar nova
        if (currentConversation.id === convId) {
            currentId++;
            currentConversation = {
                id: currentId,
                messages: [],
                title: "Nova conversa"
            };
        }
        
        res.json({ ok: true });
    } else {
        res.status(404).json({ error: "Conversa não encontrada" });
    }
});

// Endpoint para limpar TODO o histórico (apagar todas as conversas)
app.post("/api/clear-history", (req, res) => {
    conversations = [];           // Limpar array
    currentId = 0;               // Reset do ID
    currentConversation = {      // Nova conversa vazia
        id: currentId,
        messages: [],
        title: "Nova conversa"
    };
    fs.writeFileSync("conversas.json", "[]");  // Escrever array vazio no ficheiro
    res.json({ ok: true });
});

// Iniciar servidor na porta 3000
app.listen(3000, () => console.log("Servidor a correr em http://localhost:3000"));