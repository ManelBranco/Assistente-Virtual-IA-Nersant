const express = require("express");
const fs = require("fs");
const path = require("path");

const app = express();
app.use(express.json());

app.get("/", (req, res) => {
    res.sendFile(path.join(__dirname, "public", "index.html"));
});
app.use(express.static("public"));

let conversations = [];
let currentId = 0;
let currentConversation = {
    id: currentId,
    messages: [],
    title: "Nova conversa"
};

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
    const systemPrompt = `Responde sempre em português de Portugal. Sê natural, direto e formal. Apenas na primeira mensagem apresenta-te como assistente virtual de Inteligência Artificial da Nersant de Torres Novas. Nas restantes mensagens não repitas a apresentação. Apenas na primeira mensagem pergunta em que podes ajudar.`;
    const fullPrompt = systemPrompt + "\n\nUtilizador: " + message;
    const start = Date.now();

    try {
        currentConversation.messages.push({
            role: "user",
            text: message
        });

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

        if (!response.ok) {
            throw new Error(`Ollama API falhou: ${response.status}`);
        }

        const replyText = data.response || "Sem resposta do modelo.";
        const end = Date.now();
        const duration = end - start;

        currentConversation.messages.push({
            role: "bot",
            text: replyText
        });

        const existingIndex = conversations.findIndex(c => c.id === currentConversation.id);
        
        if (existingIndex !== -1) {
            conversations[existingIndex] = JSON.parse(JSON.stringify(currentConversation));
        } else {
            if (currentConversation.messages.length > 0) {
                conversations.push(JSON.parse(JSON.stringify(currentConversation)));
            }
        }
        
        fs.writeFileSync("conversas.json", JSON.stringify(conversations, null, 2));

        res.json({
            reply: replyText,
            time: duration
        });

    } catch (error) {
        console.error(error);
        res.status(500).json({ reply: "Erro no servidor", time: 0, error: error.message });
    }
});

app.get("/api/history", (req, res) => {
    res.json(conversations);
});

app.post("/api/new-chat", (req, res) => {
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
    
    fs.writeFileSync("conversas.json", "[]");
    res.json({ ok: true });
});

app.listen(3000, () => console.log("Servidor a correr em http://localhost:3000"));