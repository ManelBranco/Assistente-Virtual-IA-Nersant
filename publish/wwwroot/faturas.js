console.log("faturas.js carregado");

let attachments = [];
let activeConversationId = null;
let conversationHistory = [];
let isSending = false;
let confirmAction = null;
let attachmentCounter = 0;

function renderBotMessage(markdownText, metaText, modelName) {
    const chat = document.getElementById("chat");
    if (!chat) return;

    const messageDiv = document.createElement("div");
    messageDiv.className = "message bot";

    let botHtml = marked.parse(markdownText || "");
    if (botHtml.includes("<a href=")) {
        const temp = document.createElement("div");
        temp.innerHTML = botHtml;
        temp.querySelectorAll("a").forEach(a => {
            a.setAttribute("target", "_blank");
            a.setAttribute("rel", "noopener noreferrer");
        });
        botHtml = temp.innerHTML;
    }

    const modelMeta = modelName
        ? `<span class="meta-separator">•</span><span class="model-name">${modelName}</span>`
        : "";
    messageDiv.innerHTML = `${botHtml}<div class="meta">${metaText}${modelMeta}</div>`;
    chat.appendChild(messageDiv);
}

function renderUserMessage(text, imageDataUrls) {
    const chat = document.getElementById("chat");
    if (!chat) return;

    const messageDiv = document.createElement("div");
    messageDiv.className = "message user";

    const textNode = document.createElement("div");
    textNode.textContent = text;
    messageDiv.appendChild(textNode);

    if (imageDataUrls && imageDataUrls.length > 0) {
        const gallery = document.createElement("div");
        gallery.className = "message-thumbs";
        imageDataUrls.forEach(url => {
            const img = document.createElement("img");
            img.src = url;
            img.className = "message-thumb";
            img.title = "Clica para ampliar";
            img.addEventListener("click", () => openLightbox(url));
            gallery.appendChild(img);
        });
        messageDiv.appendChild(gallery);
    }

    chat.appendChild(messageDiv);
}

function openLightbox(src) {
    document.getElementById("lightboxImg").src = src;
    document.getElementById("imageLightbox").classList.add("active");
}

function closeLightbox() {
    document.getElementById("imageLightbox").classList.remove("active");
    document.getElementById("lightboxImg").src = "";
}

function formatTime(ms) {
    const s = (ms || 0) / 1000;
    return s >= 60
        ? `${Math.floor(s / 60)}:${(s % 60).toFixed(2).padStart(5, "0")}s`
        : `${s.toFixed(2)}s`;
}

function openConfirmModal({ title, subtitle, message, actionLabel, action }) {
    const modal = document.getElementById("confirmModal");
    document.getElementById("confirmTitle").textContent = title;
    document.getElementById("confirmSubtitle").textContent = subtitle;
    document.getElementById("confirmMessage").textContent = message;
    document.getElementById("confirmActionButton").textContent = actionLabel;
    confirmAction = action;
    modal.classList.add("active");
}

function closeConfirmModal() {
    document.getElementById("confirmModal").classList.remove("active");
    confirmAction = null;
}

async function runConfirmAction() {
    const action = confirmAction;
    closeConfirmModal();
    if (typeof action === "function") await action();
}

function showClearHistoryConfirm() {
    openConfirmModal({
        title: "Remover histórico",
        subtitle: "Esta ação vai apagar todas as análises guardadas.",
        message: "Isto vai apagar TODAS as análises de faturas! Tens a certeza?",
        actionLabel: "Apagar",
        action: clearAllHistory
    });
}

window.addEventListener("click", (event) => {
    const m = document.getElementById("confirmModal");
    if (event.target === m) closeConfirmModal();
    const lb = document.getElementById("imageLightbox");
    if (event.target === lb) closeLightbox();
});

window.addEventListener("keydown", (event) => {
    if (event.key === "Escape") closeLightbox();
});

async function loadHistory() {
    const res = await fetch("/api/invoice-history");
    if (!res.ok) {
        console.error("Erro a carregar histórico de faturas:", res.status);
        return;
    }
    conversationHistory = await res.json();

    const box = document.getElementById("conversations");
    if (!box) return;
    box.innerHTML = "";

    conversationHistory.forEach(c => {
        const convDiv = document.createElement("div");
        convDiv.className = "conv";

        const titleDiv = document.createElement("div");
        titleDiv.className = "conv-title";
        titleDiv.textContent = c.title || "Análise sem título";

        const del = document.createElement("button");
        del.type = "button";
        del.className = "conv-delete";
        del.textContent = "X";
        del.addEventListener("click", (e) => {
            e.preventDefault();
            e.stopPropagation();
            deleteConversation(c.id);
        });

        convDiv.appendChild(titleDiv);
        convDiv.appendChild(del);
        convDiv.onclick = () => loadConversation(c.id);
        box.appendChild(convDiv);
    });
}

async function loadConversation(id) {
    activeConversationId = id;
    const res = await fetch(`/api/invoice-conversation/${id}`);
    if (!res.ok) return;

    const conversation = await res.json();
    const chat = document.getElementById("chat");
    chat.innerHTML = "";

    conversation.messages.forEach(msg => {
        const div = document.createElement("div");
        if (msg.role === "bot") {
            div.className = "message bot";
            div.innerHTML = marked.parse(msg.text || "");
        } else {
            div.className = "message user";
            div.textContent = msg.text;
        }
        chat.appendChild(div);
    });
    chat.scrollTop = chat.scrollHeight;
}

function deleteConversation(id) {
    openConfirmModal({
        title: "Apagar análise",
        subtitle: "Esta análise será removida permanentemente.",
        message: "Tens a certeza que queres apagar esta análise?",
        actionLabel: "Apagar",
        action: async () => {
            await fetch(`/api/invoice-conversation/${id}`, { method: "DELETE" });
            if (activeConversationId === id) {
                activeConversationId = null;
                document.getElementById("chat").innerHTML = "";
            }
            await loadHistory();
        }
    });
}

async function clearAllHistory() {
    const res = await fetch("/api/invoice-clear-history", { method: "POST" });
    if (!res.ok) {
        alert("Não foi possível apagar o histórico.");
        return;
    }
    activeConversationId = null;
    conversationHistory = [];
    document.getElementById("chat").innerHTML = "";
    await loadHistory();
}

async function newAnalysis() {
    const res = await fetch("/api/invoice-new-chat", { method: "POST" });
    if (!res.ok) return;
    const data = await res.json();
    activeConversationId = data.id ?? null;
    clearAttachments();
    document.getElementById("input").value = "";
    document.getElementById("chat").innerHTML =
        `<div class="empty-chat"><p>Arrasta um ficheiro para começar</p><p>ou clica num dos prompts rápidos abaixo</p></div>`;
    await loadHistory();
}

function filterConversations() {
    const q = (document.getElementById("search").value || "").toLowerCase();
    document.querySelectorAll(".conv").forEach(conv => {
        const title = conv.querySelector(".conv-title")?.textContent.toLowerCase() || "";
        conv.style.display = title.includes(q) ? "flex" : "none";
    });
}

function renderAttachments() {
    const box = document.getElementById("attachments");
    if (!box) return;
    box.innerHTML = "";
    attachments.forEach(a => {
        const chip = document.createElement("div");
        chip.className = "attachment-chip";
        chip.innerHTML = `
            <img src="${a.thumbnailUrl}" alt="thumb">
            <span class="attachment-name" title="${a.name}">${a.name}</span>
            <button type="button" class="attachment-remove" data-id="${a.id}">×</button>
        `;
        box.appendChild(chip);
    });
    box.querySelectorAll(".attachment-remove").forEach(btn => {
        btn.addEventListener("click", () => {
            const id = parseInt(btn.dataset.id, 10);
            attachments = attachments.filter(x => x.id !== id);
            renderAttachments();
        });
    });
}

function clearAttachments() {
    attachments = [];
    renderAttachments();
}

async function handleFiles(fileList) {
    const files = Array.from(fileList || []);
    for (const file of files) {
        try {
            if (file.type === "application/pdf" || file.name.toLowerCase().endsWith(".pdf")) {
                await convertPdfToImages(file);
            } else if (file.type.startsWith("image/")) {
                const dataUrl = await readFileAsDataUrl(file);
                attachments.push({
                    id: ++attachmentCounter,
                    name: file.name,
                    dataUrl,
                    thumbnailUrl: dataUrl,
                    mime: file.type
                });
            } else {
                alert(`Tipo de ficheiro não suportado: ${file.name}\nUsa PDF, JPG, PNG ou WEBP.`);
            }
        } catch (err) {
            console.error("Erro a processar ficheiro", file.name, err);
            alert(`Não foi possível processar ${file.name}: ${err.message || err}`);
        }
    }
    renderAttachments();
}

function readFileAsDataUrl(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result);
        reader.onerror = reject;
        reader.readAsDataURL(file);
    });
}

async function convertPdfToImages(file) {
    if (!window.pdfjsLib) {
        throw new Error("pdf.js não carregado");
    }
    const arrayBuffer = await file.arrayBuffer();
    const pdf = await pdfjsLib.getDocument({ data: arrayBuffer }).promise;
    // Máximo 8 páginas para não exceder o limite de tokens do modelo.
    const maxPages = Math.min(pdf.numPages, 8);

    for (let p = 1; p <= maxPages; p++) {
        const page = await pdf.getPage(p);
        const viewport = page.getViewport({ scale: 1.5 });
        const scale = viewport.width > 1600 ? 1600 / viewport.width : 1;
        const finalViewport = page.getViewport({ scale: 1.5 * scale });

        const canvas = document.createElement("canvas");
        const ctx = canvas.getContext("2d");
        canvas.width = finalViewport.width;
        canvas.height = finalViewport.height;

        await page.render({ canvasContext: ctx, viewport: finalViewport }).promise;

        const dataUrl = canvas.toDataURL("image/jpeg", 0.85);
        attachments.push({
            id: ++attachmentCounter,
            name: `${file.name} — pág. ${p}`,
            dataUrl,
            thumbnailUrl: dataUrl,
            mime: "image/jpeg"
        });
    }

    if (pdf.numPages > maxPages) {
        alert(`O PDF tem ${pdf.numPages} páginas — apenas as primeiras ${maxPages} serão enviadas para análise.`);
    }
}

function setupDropZone() {
    const dz = document.getElementById("dropZone");
    const fi = document.getElementById("fileInput");
    if (!dz || !fi) return;

    // Limpar após upload para permitir re-selecionar o mesmo ficheiro.
    fi.addEventListener("change", () => {
        handleFiles(fi.files);
        fi.value = "";
    });

    ["dragenter", "dragover"].forEach(ev => {
        dz.addEventListener(ev, (e) => {
            e.preventDefault();
            e.stopPropagation();
            dz.classList.add("is-dragover");
        });
    });

    ["dragleave", "drop"].forEach(ev => {
        dz.addEventListener(ev, (e) => {
            e.preventDefault();
            e.stopPropagation();
            dz.classList.remove("is-dragover");
        });
    });

    dz.addEventListener("drop", (e) => {
        const files = e.dataTransfer?.files;
        if (files && files.length > 0) handleFiles(files);
    });
}

function setupQuickPrompts() {
    document.querySelectorAll(".quick-prompts .chip").forEach(btn => {
        btn.addEventListener("click", () => {
            const text = btn.dataset.prompt || "";
            const input = document.getElementById("input");
            input.value = text;
            input.focus();
        });
    });
}

async function send() {
    if (isSending) return;

    const input = document.getElementById("input");
    const modelSelect = document.getElementById("model");
    const chat = document.getElementById("chat");
    const sendBtn = document.getElementById("sendBtn");
    if (!input || !modelSelect || !chat) return;

    const message = (input.value || "").trim();
    const images = attachments.map(a => a.dataUrl);

    if (!message && images.length === 0) {
        alert("Escreve uma pergunta ou anexa um ficheiro.");
        return;
    }

    if (activeConversationId === null) {
        const res = await fetch("/api/invoice-new-chat", { method: "POST" });
        if (res.ok) {
            const data = await res.json();
            activeConversationId = data.id ?? null;
        }
    }

    isSending = true;
    sendBtn.disabled = true;
    sendBtn.textContent = "A enviar...";

    chat.querySelector(".empty-chat")?.remove();

    const userTextShown = message || "(documento anexado)";
    renderUserMessage(userTextShown, images);
    input.value = "";

    const loading = document.createElement("div");
    loading.className = "message bot";
    loading.id = "loading";
    loading.innerHTML = `A analisar... <span id="timer">0.00s</span>`;
    chat.appendChild(loading);
    chat.scrollTop = chat.scrollHeight;

    const start = Date.now();
    const timerInterval = setInterval(() => {
        const s = (Date.now() - start) / 1000;
        const t = document.getElementById("timer");
        if (t) {
            t.textContent = s >= 60
                ? `${Math.floor(s / 60)}:${(s % 60).toFixed(2).padStart(5, "0")}s`
                : `${s.toFixed(2)}s`;
        }
    }, 100);

    let data;
    try {
        const res = await fetch("/api/invoice-chat", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                message,
                model: modelSelect.value,
                conversationId: activeConversationId,
                images
            })
        });
        const text = await res.text();
        try { data = JSON.parse(text); } catch { data = { error: text }; }
        if (!res.ok) console.error("Erro /api/invoice-chat:", res.status, data);
    } catch (err) {
        data = { error: err.message };
    }

    clearInterval(timerInterval);
    document.getElementById("loading")?.remove();

    if (data?.conversationId) activeConversationId = data.conversationId;

    const replyText = data?.reply || (data?.error ? `Erro: ${data.error}` : "Resposta vazia");
    const modelName = modelSelect.options[modelSelect.selectedIndex]?.text || modelSelect.value;
    renderBotMessage(replyText, formatTime(data?.time), modelName);

    if (!data?.error) {
        clearAttachments();
    }

    chat.scrollTop = chat.scrollHeight;
    await loadHistory();

    sendBtn.disabled = false;
    sendBtn.textContent = "Enviar";
    isSending = false;
}

async function loadModels() {
    const select = document.getElementById("model");
    if (!select) return;

    try {
        const res = await fetch("/api/models");
        if (!res.ok) return;
        const data = await res.json();
        if (!data.models || data.models.length === 0) return;

        const current = select.value;
        select.innerHTML = "";
        data.models.forEach(name => {
            const option = document.createElement("option");
            option.value = name;
            option.textContent = name;
            select.appendChild(option);
        });
        if ([...select.options].some(o => o.value === current)) {
            select.value = current;
        }
    } catch {
        console.warn("Não foi possível carregar modelos do Ollama — a usar lista estática.");
    }
}

window.addEventListener("DOMContentLoaded", async () => {
    await loadModels();
    setupDropZone();
    setupQuickPrompts();

    document.getElementById("search")?.addEventListener("input", filterConversations);

    document.getElementById("input")?.addEventListener("keydown", (e) => {
        if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) {
            e.preventDefault();
            send();
        }
    });

    await loadHistory();
    if (conversationHistory.length > 0) {
        loadConversation(conversationHistory[conversationHistory.length - 1].id);
    }
});
