# faturas.js — Documentação

Ficheiro JavaScript da página de análise de faturas (`faturas.html`).  
Vanilla JS sem framework nem build step. Depende de dois scripts externos carregados na página:

- **marked.js** — converte Markdown das respostas do assistente em HTML.
- **pdf.js** (Mozilla) — renderiza páginas de PDF num `<canvas>` para as converter em imagens.

---

## Estado global

| Variável | Tipo | Propósito |
|---|---|---|
| `attachments` | `Array` | Ficheiros pendentes de envio: `{ id, name, dataUrl, mime, thumbnailUrl }`. Cada página de um PDF gera uma entrada separada. |
| `activeConversationId` | `number \| null` | ID da conversa activa no backend. `null` enquanto não existe nenhuma conversa criada. |
| `conversationHistory` | `Array` | Cache local das conversas carregadas da sidebar. |
| `isSending` | `boolean` | Semáforo que impede envios simultâneos. |
| `confirmAction` | `Function \| null` | Callback guardado pelo modal de confirmação até o utilizador confirmar. |
| `attachmentCounter` | `number` | Contador crescente para IDs únicos de anexos, independente do índice do array. |

---

## Secções do ficheiro

### Renderização de mensagens

#### `renderBotMessage(markdownText, metaText, modelName)`
Cria e insere no chat um `<div class="message bot">` com:
- Markdown convertido em HTML via `marked.parse()`.
- Todos os links com `target="_blank"` e `rel="noopener noreferrer"` por segurança.
- Rodapé com tempo de resposta e nome do modelo.

#### `renderUserMessage(text, imageDataUrls)`
Cria e insere um `<div class="message user">` com o texto em nó de texto puro (sem HTML injection) e, opcionalmente, uma galeria de miniaturas clicáveis que abrem o lightbox.

---

### Lightbox

#### `openLightbox(src)` / `closeLightbox()`
Mostra/oculta o overlay de imagem a tamanho completo.  
- Fechar: clique no overlay, botão de fechar ou tecla **Escape**.
- `closeLightbox()` limpa o `src` para libertar memória.

---

### Formatação de tempo

#### `formatTime(ms)`
Converte milissegundos em string legível:
- `< 60 s` → `"3.45s"`
- `≥ 60 s` → `"1:03.45s"`

---

### Modal de confirmação

Modal genérico reutilizável para qualquer ação destrutiva.

#### `openConfirmModal({ title, subtitle, message, actionLabel, action })`
Popula os campos do modal e guarda `action` em `confirmAction`.

#### `closeConfirmModal()`
Fecha e descarta o callback pendente.

#### `runConfirmAction()`
Executa `confirmAction` (se existir) após fechar o modal.

#### `showClearHistoryConfirm()`
Atalho que abre o modal configurado para apagar todo o histórico.

---

### Histórico (sidebar)

#### `loadHistory()`
`GET /api/invoice-history` → reconstrói a lista de conversas na sidebar.  
Cada item tem título clicável e botão "X" (com `stopPropagation` para não activar o clique no item pai).

#### `loadConversation(id)`
`GET /api/invoice-conversation/{id}` → carrega todas as mensagens no chat e actualiza `activeConversationId`.

#### `deleteConversation(id)`
Pede confirmação via modal; se aceite faz `DELETE /api/invoice-conversation/{id}`.  
Se era a conversa activa, limpa também o chat.

#### `clearAllHistory()`
`POST /api/invoice-clear-history` → apaga tudo e repõe o estado local.

#### `newAnalysis()`
`POST /api/invoice-new-chat` → cria nova conversa vazia no backend, limpa anexos, input e chat.

---

### Pesquisa

#### `filterConversations()`
Filtra em tempo real os itens da sidebar por título (case-insensitive), ocultando os que não correspondem.

---

### Anexos

#### `renderAttachments()`
Re-renderiza os chips de anexo a partir do array `attachments`. Liga os botões de remoção após inserção no DOM.

#### `clearAttachments()`
Esvazia `attachments` e actualiza o UI.

#### `handleFiles(fileList)`
Entry point para processamento de ficheiros (upload via input ou drag-drop):
- **PDF** → `convertPdfToImages()`
- **Imagem** → lida diretamente com `readFileAsDataUrl()`
- **Outro** → alerta e ignora

#### `readFileAsDataUrl(file)` → `Promise<string>`
Lê um `File` como data URL usando `FileReader`. Resolve com a string `data:...`.

#### `convertPdfToImages(file)`
Converte cada página do PDF num JPEG via pdf.js:

| Parâmetro | Valor |
|---|---|
| Limite de páginas | 8 (para não exceder tokens do modelo) |
| Escala base | 1.5× |
| Largura máxima | 1600 px (reduz escala proporcionalmente se necessário) |
| Qualidade JPEG | 85 % |

Cada página origina uma entrada em `attachments` com nome `"ficheiro.pdf — pág. N"`.

#### `setupDropZone()`
Configura o elemento `#dropZone` e o `#fileInput` escondido:
- `change` no input → `handleFiles()`
- `dragenter`/`dragover` → adiciona classe `is-dragover`
- `dragleave`/`drop` → remove `is-dragover`
- `drop` → `handleFiles()` com os ficheiros do evento

---

### Prompts rápidos

#### `setupQuickPrompts()`
Liga cada `.quick-prompts .chip` ao `#input`: ao clicar, o `data-prompt` do chip é copiado para o campo de texto e o foco é movido.

---

### Envio

#### `send()`
Função principal de envio — fluxo completo:

```
1. Verificar semáforo (isSending)
2. Validar: mensagem ou pelo menos um anexo
3. Criar conversa no backend se activeConversationId === null
4. Bloquear botão + activar semáforo
5. Renderizar mensagem do utilizador imediatamente (optimistic UI)
6. Mostrar indicador "A analisar..." com temporizador ao vivo (100 ms)
7. POST /api/invoice-chat com { message, model, conversationId, images }
8. Remover indicador + renderizar resposta com renderBotMessage()
9. Se sem erro: limpar anexos
10. Actualizar sidebar + desbloquear botão
```

**Payload enviado:**
```json
{
  "message": "texto do utilizador",
  "model": "valor do selector de modelo",
  "conversationId": 42,
  "images": ["data:image/jpeg;base64,...", "..."]
}
```

**Resposta esperada:**
```json
{
  "reply": "resposta em Markdown",
  "conversationId": 42,
  "time": 3450
}
```

---

### Inicialização

`DOMContentLoaded` executa, por ordem:

1. `setupDropZone()`
2. `setupQuickPrompts()`
3. Listener de pesquisa no `#search`
4. Listener **Ctrl+Enter** / **Cmd+Enter** no `#input` → `send()`
5. `loadHistory()` + abertura automática da análise mais recente

---

## Endpoints do backend usados

| Método | Endpoint | Descrição |
|---|---|---|
| `POST` | `/api/invoice-new-chat` | Criar nova análise |
| `POST` | `/api/invoice-chat` | Enviar mensagem + imagens |
| `GET` | `/api/invoice-history` | Listar análises |
| `GET` | `/api/invoice-conversation/{id}` | Obter mensagens de uma análise |
| `DELETE` | `/api/invoice-conversation/{id}` | Apagar uma análise |
| `POST` | `/api/invoice-clear-history` | Apagar todo o histórico |
