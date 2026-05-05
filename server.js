const express = require("express");
const fs = require("fs");

const app = express();
app.use(express.json());
app.use(express.static("public"));

let conversations = [];
let currentId = 0;
let currentConversation = {
    id: currentId,
    messages: [],
    title: "Nova conversa"
};

// Restaurar conversas ao iniciar
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

currentConversation = {
    id: currentId,
    messages: [],
    title: "Nova conversa"
};

app.post("/api/chat", async (req, res) => {
    const { message, model } = req.body;
    const systemPrompt = `Responde sempre em português de Portugal. Sê natural, direto e formal. Apenas na primeira mensagem apresenta-te como assistente virtual de Inteligência Artificial da Nersant de Torres Novas. Nas restantes mensagens não repitas a apresentação. Pergunta sempre em que podes ajudar.`;
    const fullPrompt = systemPrompt + "\n\nUtilizador: " + message;
    const start = Date.now();

    try {
        currentConversation.messages.push({
            role: "user",
            text: message
        });

        // Gerar título automático com base na primeira mensagem
        if (currentConversation.messages.length === 1) {
            currentConversation.title = message.substring(0, 30).trim();
            if (message.length > 30) {
                currentConversation.title += "...";
            }
        }

        const response = await fetch("http://localhost:11434/api/generate", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                model: model,
                prompt: fullPrompt,
                stream: false
            })
        });

        const data = await response.json();
    
        const end = Date.now();
        const duration = end - start;
        console.log("Tempo calculado:", duration, "ms");

        currentConversation.messages.push({
            role: "bot",
            text: data.response
        });

        // ✅ GUARDAR CONVERSA ATUALIZADA NO FICHEIRO A CADA MENSAGEM
        // Verificar se já existe uma conversa com este ID no array conversations
        const existingIndex = conversations.findIndex(c => c.id === currentConversation.id);
        
        if (existingIndex !== -1) {
            // Atualizar conversa existente
            conversations[existingIndex] = JSON.parse(JSON.stringify(currentConversation));
        } else {
            // Adicionar nova conversa (apenas se tiver pelo menos 1 mensagem)
            if (currentConversation.messages.length > 0) {
                conversations.push(JSON.parse(JSON.stringify(currentConversation)));
            }
        }
        
        // Guardar no ficheiro
        fs.writeFileSync("conversas.json", JSON.stringify(conversations, null, 2));

        res.json({
            reply: data.response,
            time: duration
        });

    } catch (error) {
        console.error(error);
        res.json({ reply: "Erro no servidor", time: 0 });
    }
});

app.get("/api/history", (req, res) => {
    res.json(conversations);
});

app.post("/api/new-chat", (req, res) => {
    // Guardar conversa atual se tiver mensagens e ainda não estiver guardada
    if (currentConversation.messages.length > 0) {
        const existingIndex = conversations.findIndex(c => c.id === currentConversation.id);
        if (existingIndex === -1) {
            conversations.push(JSON.parse(JSON.stringify(currentConversation)));
            fs.writeFileSync("conversas.json", JSON.stringify(conversations, null, 2));
        }
    }

    currentId++;

    currentConversation = {
        id: currentId,
        messages: [],
        title: "Nova conversa"
    };

    res.json({ ok: true });
});

app.get("/api/conversation/:id", (req, res) => {
    const convId = parseInt(req.params.id);
    const conversation = conversations.find(c => c.id === convId);
    
    if (conversation) {
        res.json(conversation);
    } else {
        res.status(404).json({ error: "Conversa não encontrada" });
    }
});

app.delete("/api/conversation/:id", (req, res) => {
    const convId = parseInt(req.params.id);
    const index = conversations.findIndex(c => c.id === convId);
    
    if (index !== -1) {
        conversations.splice(index, 1);
        fs.writeFileSync("conversas.json", JSON.stringify(conversations, null, 2));
        
        // Se a conversa apagada for a atual, criar nova conversa
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

app.post("/api/clear-history", (req, res) => {
    conversations = [];
    currentId = 0;
    currentConversation = {
        id: currentId,
        messages: [],
        title: "Nova conversa"
    };
    // Escrever array vazio no ficheiro
    fs.writeFileSync("conversas.json", "[]");
    res.json({ ok: true });
});

app.listen(3000, () => console.log("Servidor a correr em http://localhost:3000"));