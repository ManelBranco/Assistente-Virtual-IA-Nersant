using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

// Configuração do servidor Web e permissões CORS para permitir pedidos do navegador.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(60);
});

var app = builder.Build();
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseSession();

var publicDir = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var conversationsPath = Path.Combine(app.Environment.ContentRootPath, "data", "conversas.json");
var statsPath = Path.Combine(app.Environment.ContentRootPath, "data", "stats.json");
var contextPromptPath = Path.Combine(app.Environment.ContentRootPath, "data", "context_prompt.txt");

// Garantir que as pastas necessárias existem antes de gravar ficheiros.
Directory.CreateDirectory(publicDir);
Directory.CreateDirectory(Path.GetDirectoryName(conversationsPath)!);

// Configuração do JSON para garantir nomes em camelCase e boa leitura no ficheiro.
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

// Inicializa o armazenamento de conversas e estatísticas.
var store = new DataStore(conversationsPath, statsPath, jsonOptions);
await store.InitializeAsync();

// System prompt que será introduzido em cada mensagem
const string SYSTEM_PROMPT = @"Responde sempre em português de Portugal. Sê natural, direto e formal. Apresenta-te como assistente virtual de Inteligência Artificial da Nersant de Torres Novas. Usa sempre o histórico da conversa para manter contexto. Se o utilizador pedir para continuar uma receita, cálculo ou sequência, consulta a mensagem anterior e responde a partir daí.";

// Carregar ou criar o context prompt editável
string contextPrompt = "";
if (File.Exists(contextPromptPath))
{
    contextPrompt = await File.ReadAllTextAsync(contextPromptPath);
}
else
{
    contextPrompt = "Contexto adicional: [Edita este ficheiro com informações contextuais que serão usadas nas conversas.]";
    await File.WriteAllTextAsync(contextPromptPath, contextPrompt);
}

// Configurar ficheiros estáticos para servir o site em wwwroot.
app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(publicDir),
    RequestPath = string.Empty,
    DefaultFileNames = new List<string> { "index.html" }
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(publicDir),
    RequestPath = string.Empty
});

// Servir a página principal index.html quando o navegador aceitar a rota raiz.
app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(Path.Combine(publicDir, "index.html"));
});

// Rota para obter o histórico completo de conversas guardadas.
app.MapGet("/api/history", () => Results.Json(store.Conversations, jsonOptions));

// Rota para obter uma conversa específica pelo seu ID.
app.MapGet("/api/conversation/{id:int}", (int id) =>
{
    var conversation = store.Conversations.FirstOrDefault(c => c.Id == id);
    return conversation is not null ? Results.Json(conversation, jsonOptions) : Results.NotFound(new { error = "Conversa não encontrada" });
});

// Inicia uma nova conversa e garante que a conversa anterior fica guardada.
app.MapPost("/api/new-chat", async (HttpContext http) =>
{
    await store.SaveCurrentConversationIfNeededAsync();
    store.Stats.TotalConversations++;
    store.CurrentId++;
    var newConversation = new Conversation(store.CurrentId, "Nova conversa", new List<Message>());
    store.CurrentConversation = newConversation;
    
    // ⭐ IMPORTANTE: Adicionar a nova conversa à lista de conversas
    store.Conversations.Add(newConversation);
    http.Session.SetInt32("ActiveConversationId", newConversation.Id);
    
    await store.SaveStatsAsync();
    await store.SaveConversationsAsync();
    return Results.Json(new { ok = true, id = store.CurrentConversation.Id });
});

app.MapDelete("/api/conversation/{id:int}", async (int id) =>
{
    // Apaga a conversa com o ID fornecido.
    var removed = await store.RemoveConversationAsync(id);
    if (!removed)
    {
        return Results.NotFound(new { error = "Conversa não encontrada" });
    }

    // Se a conversa apagada era a conversa atual, criar uma nova conversa vazia.
    if (store.CurrentConversation.Id == id)
    {
        store.CurrentId++;
        store.CurrentConversation = new Conversation(store.CurrentId, "Nova conversa", new List<Message>());
    }

    await store.SaveConversationsAsync();
    return Results.Json(new { ok = true });
});

// Apaga todo o histórico de conversas e reinicia as estatísticas.
app.MapPost("/api/clear-history", async () =>
{
    try
    {
        await store.ClearHistoryAsync();
        return Results.Json(new { ok = true });
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Erro ao limpar histórico: {ex.Message}");
        return Results.Problem("Não foi possível apagar o histórico de conversas.", statusCode: 500);
    }
});

app.MapGet("/api/stats", () => Results.Json(store.Stats, jsonOptions));

// Rota para obter o context prompt editável
app.MapGet("/api/context-prompt", async () =>
{
    if (File.Exists(contextPromptPath))
    {
        var content = await File.ReadAllTextAsync(contextPromptPath);
        return Results.Json(new { contextPrompt = content }, jsonOptions);
    }
    return Results.Json(new { contextPrompt = "" }, jsonOptions);
});

// Rota para atualizar o context prompt
app.MapPost("/api/context-prompt", async (ContextPromptRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.ContextPrompt))
    {
        return Results.BadRequest(new { error = "Context prompt não pode estar vazio" });
    }
    await File.WriteAllTextAsync(contextPromptPath, request.ContextPrompt);
    contextPrompt = request.ContextPrompt;
    return Results.Json(new { ok = true });
});

app.MapPost("/api/stats/update", async (StatsUpdateRequest request) =>
{
    if (request.ThinkingTime.HasValue)
    {
        store.Stats.TotalThinkingTime += request.ThinkingTime.Value;
    }
    if (request.MessagesSent.HasValue)
    {
        store.Stats.MessagesSentCount += request.MessagesSent.Value;
    }
    await store.SaveStatsAsync();
    return Results.Json(new { ok = true });
});

// Rota de chat: recebe a mensagem do utilizador, envia ao modelo IA e devolve a resposta.
app.MapPost("/api/chat", async (HttpContext http, ChatRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Message) || string.IsNullOrWhiteSpace(request.Model))
    {
        return Results.BadRequest(new { reply = "Pedido inválido", time = 0 });
    }

    // Carregar a conversa correta do request ou da sessão do IIS
    Conversation conversation = store.CurrentConversation;
    int? requestedId = request.ConversationId;
    if (!requestedId.HasValue)
    {
        requestedId = http.Session.GetInt32("ActiveConversationId");
    }

    if (requestedId.HasValue)
    {
        var existingConversation = store.Conversations.FirstOrDefault(c => c.Id == requestedId.Value);
        if (existingConversation is not null)
        {
            conversation = existingConversation;
            store.CurrentConversation = conversation;
        }
    }

    if (request.ConversationMessages is not null && request.ConversationMessages.Any())
    {
        conversation = conversation with { Messages = request.ConversationMessages.Select(m => new Message(m.Role, m.Text)).ToList() };
        store.CurrentConversation = conversation;
    }

    if (conversation.Id != 0)
    {
        http.Session.SetInt32("ActiveConversationId", conversation.Id);
    }

    // Adicionar a mensagem do utilizador
    conversation.Messages.Add(new Message("user", request.Message));

    // Atualizar o título se for a primeira mensagem
    if (conversation.Messages.Count == 1)
    {
        var title = request.Message.Length <= 30 ? request.Message.Trim() : request.Message.Substring(0, 30).Trim() + "...";
        conversation = conversation with { Title = title };
        store.CurrentConversation = conversation;
        
        // Atualizar a conversa em store.Conversations
        var convIndex = store.Conversations.FindIndex(c => c.Id == conversation.Id);
        if (convIndex >= 0)
        {
            store.Conversations[convIndex] = conversation;
        }
        else
        {
            store.Conversations.Add(conversation);
        }
    }

    var messages = BuildMessages(conversation, contextPrompt, SYSTEM_PROMPT);

    var start = DateTime.UtcNow;

    try
    {
        using var httpClient = new HttpClient();

        const string ollamaBase = "http://localhost:11434";
        string usedModel = request.Model;
        string usedEndpoint = "v1/chat/completions";
        string usedApiUrl = $"{ollamaBase}/{usedEndpoint}";

        var response = await httpClient.PostAsJsonAsync(
            usedApiUrl,
            new { model = request.Model, messages, max_tokens = 512, temperature = 0.2, stream = false }
        );

        var content = await response.Content.ReadAsStringAsync();
        var lowerContent = content.ToLowerInvariant();

        if (!response.IsSuccessStatusCode && (lowerContent.Contains("not found") || lowerContent.Contains("invalid model") || lowerContent.Contains("invalid request") || lowerContent.Contains("unsupported") || lowerContent.Contains("internal server error")))
        {
            usedEndpoint = "api/chat";
            usedApiUrl = $"{ollamaBase}/{usedEndpoint}";
            response = await httpClient.PostAsJsonAsync(
                usedApiUrl,
                new { model = request.Model, messages, stream = false, options = new { temperature = 0.2 } }
            );
            content = await response.Content.ReadAsStringAsync();
        }

        if (!response.IsSuccessStatusCode)
        {
            return Results.Json(new { reply = "Erro na API do Ollama", time = 0, error = content, context = messages, conversationId = conversation.Id, usedModel, usedEndpoint, usedApiUrl }, statusCode: 500, options: jsonOptions);
        }

        var replyText = ExtractReplyText(content);
        var end = DateTime.UtcNow;
        var duration = (long)(end - start).TotalMilliseconds;

        // Adicionar a resposta da IA
        conversation.Messages.Add(new Message("bot", replyText));
        store.CurrentConversation = conversation;

        // Atualizar a conversa em store.Conversations
        var conversationIndex = store.Conversations.FindIndex(c => c.Id == conversation.Id);
        if (conversationIndex >= 0)
        {
            store.Conversations[conversationIndex] = conversation;
        }

        await store.SaveCurrentConversationAsync();

        return Results.Json(new { reply = replyText, time = duration, context = messages, conversationId = conversation.Id, usedModel, usedEndpoint, usedApiUrl }, jsonOptions);
    }
    catch (Exception ex)
    {
        return Results.Json(new { reply = "Erro no servidor", time = 0, error = ex.Message, context = messages, conversationId = conversation.Id }, statusCode: 500, options: jsonOptions);
    }
});

app.Run();

// Extrai o texto de resposta da API da IA, aceitando vários formatos de JSON possíveis.
static string ExtractReplyText(string json)
{
    try
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("response", out var responseProp) && responseProp.ValueKind == JsonValueKind.String)
        {
            return responseProp.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
        {
            return textProp.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("output", out var outputProp) && outputProp.ValueKind == JsonValueKind.Array && outputProp.GetArrayLength() > 0)
        {
            var first = outputProp[0];
            if (first.ValueKind == JsonValueKind.String)
            {
                return first.GetString() ?? string.Empty;
            }
        }

        if (root.TryGetProperty("message", out var messageRootProp) && messageRootProp.ValueKind == JsonValueKind.Object
            && messageRootProp.TryGetProperty("content", out var messageRootContent) && messageRootContent.ValueKind == JsonValueKind.String)
        {
            return messageRootContent.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("choices", out var choicesProp) && choicesProp.ValueKind == JsonValueKind.Array && choicesProp.GetArrayLength() > 0)
        {
            var firstChoice = choicesProp[0];
            if (firstChoice.TryGetProperty("message", out var messageProp) && messageProp.ValueKind == JsonValueKind.Object)
            {
                if (messageProp.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
                {
                    return contentProp.GetString() ?? string.Empty;
                }
            }

            if (firstChoice.TryGetProperty("text", out var textChoiceProp) && textChoiceProp.ValueKind == JsonValueKind.String)
            {
                return textChoiceProp.GetString() ?? string.Empty;
            }
        }
    }
    catch
    {
        // Ignorar parse errors e usar fallback
    }

    return string.Empty;
}

static List<object> BuildMessages(Conversation conversation, string contextPrompt, string systemPrompt)
{
    bool isFirstMessage = conversation.Messages.Count <= 1;

    var firstMessageInstructions = @"Esta é a primeira mensagem do utilizador. Apresenta-te de forma breve como assistente da Nersant. Cumprimenta e responde à pergunta ou pedido.";
    var subsequentMessageInstructions = @"Esta não é a primeira mensagem. Não cumprimentes o utilizador. Responde de forma direta ao que ele pede, usando sempre o histórico anterior para manter contexto e coerência. Não repitas informação que o utilizador já deu.";
    var instructions = isFirstMessage ? firstMessageInstructions : subsequentMessageInstructions;

    var messages = new List<object>
    {
        new { role = "system", content = $"{systemPrompt}\n\n{contextPrompt}\n\n{instructions}" }
    };

    foreach (var m in conversation.Messages)
    {
        messages.Add(new { role = m.Role == "bot" ? "assistant" : "user", content = m.Text.Trim() });
    }

    return messages;
}

// Classe que gerencia o armazenamento de conversas e estatísticas em ficheiros JSON.
internal class DataStore
{
    private readonly string _conversationsPath;
    private readonly string _statsPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _locker = new(1, 1);

    public List<Conversation> Conversations { get; private set; } = new();
    public GlobalStats Stats { get; private set; } = new();
    public int CurrentId { get; set; }
    public Conversation CurrentConversation { get; set; } = new(0, "Nova conversa", new List<Message>());

    public DataStore(string conversationsPath, string statsPath, JsonSerializerOptions jsonOptions)
    {
        _conversationsPath = conversationsPath;
        _statsPath = statsPath;
        _jsonOptions = jsonOptions;
    }

    // Inicializa o DataStore: lê ficheiros existentes ou cria valores iniciais e garante que o JSON está gravado.
    public async Task InitializeAsync()
    {
        await _locker.WaitAsync();
        try
        {
            if (File.Exists(_conversationsPath))
            {
                var content = await File.ReadAllTextAsync(_conversationsPath);
                var conversations = JsonSerializer.Deserialize<List<Conversation>>(content, _jsonOptions);
                if (conversations is not null)
                {
                    Conversations = conversations;
                    CurrentId = Conversations.Any() ? Conversations.Max(c => c.Id) + 1 : 0;
                }
            }

            if (File.Exists(_statsPath))
            {
                var content = await File.ReadAllTextAsync(_statsPath);
                var stats = JsonSerializer.Deserialize<GlobalStats>(content, _jsonOptions);
                if (stats is not null)
                {
                    Stats = stats;
                }
            }

            CurrentConversation = new Conversation(CurrentId, "Nova conversa", new List<Message>());
            await SaveConversationsInternalAsync();
            await SaveStatsInternalAsync();
        }
        finally
        {
            _locker.Release();
        }
    }

    // Guarda a conversa atual apenas se já existirem mensagens nela.
    // Isto evita criar conversas vazias sem conteúdo.
    public async Task SaveCurrentConversationIfNeededAsync()
    {
        await _locker.WaitAsync();
        try
        {
            if (CurrentConversation.Messages.Any())
            {
                var existingIndex = Conversations.FindIndex(c => c.Id == CurrentConversation.Id);
                if (existingIndex >= 0)
                {
                    Conversations[existingIndex] = CloneConversation(CurrentConversation);
                }
                else
                {
                    Conversations.Add(CloneConversation(CurrentConversation));
                }
                await SaveConversationsInternalAsync();
            }
        }
        finally
        {
            _locker.Release();
        }
    }

    // Guarda a conversa atual no histórico, criando um novo registo se necessário.
    public async Task SaveCurrentConversationAsync()
    {
        await _locker.WaitAsync();
        try
        {
            var existingIndex = Conversations.FindIndex(c => c.Id == CurrentConversation.Id);
            if (existingIndex >= 0)
            {
                Conversations[existingIndex] = CloneConversation(CurrentConversation);
            }
            else
            {
                if (CurrentConversation.Messages.Any())
                {
                    Conversations.Add(CloneConversation(CurrentConversation));
                }
            }
            await SaveConversationsInternalAsync();
        }
        finally
        {
            _locker.Release();
        }
    }

    // Remove uma conversa do histórico pelo ID.
    public async Task<bool> RemoveConversationAsync(int id)
    {
        await _locker.WaitAsync();
        try
        {
            var index = Conversations.FindIndex(c => c.Id == id);
            if (index < 0) return false;
            Conversations.RemoveAt(index);
            return true;
        }
        finally
        {
            _locker.Release();
        }
    }

    // Limpa todo o histórico de conversas e zera as estatísticas.
    public async Task ClearHistoryAsync()
    {
        await _locker.WaitAsync();
        try
        {
            Conversations = new List<Conversation>();
            CurrentId = 0;
            CurrentConversation = new Conversation(0, "Nova conversa", new List<Message>());
            Stats = new GlobalStats();
            await SaveConversationsInternalAsync();
            await SaveStatsInternalAsync();
        }
        finally
        {
            _locker.Release();
        }
    }

    // Guarda o ficheiro de conversas no disco.
    public async Task SaveConversationsAsync()
    {
        await _locker.WaitAsync();
        try
        {
            await SaveConversationsInternalAsync();
        }
        finally
        {
            _locker.Release();
        }
    }

    // Guarda o ficheiro de estatísticas no disco.
    public async Task SaveStatsAsync()
    {
        await _locker.WaitAsync();
        try
        {
            await SaveStatsInternalAsync();
        }
        finally
        {
            _locker.Release();
        }
    }

    private async Task SaveConversationsInternalAsync()
    {
        await File.WriteAllTextAsync(_conversationsPath, JsonSerializer.Serialize(Conversations, _jsonOptions));
    }

    private async Task SaveStatsInternalAsync()
    {
        await File.WriteAllTextAsync(_statsPath, JsonSerializer.Serialize(Stats, _jsonOptions));
    }

    // Cria uma cópia profunda de uma conversa para evitar alterações acidentais no estado original.
    private static Conversation CloneConversation(Conversation conversation)
    {
        return new Conversation(conversation.Id, conversation.Title, conversation.Messages.Select(m => new Message(m.Role, m.Text)).ToList());
    }
}

internal sealed record Conversation(int Id, string Title, List<Message> Messages);
internal sealed record Message(string Role, string Text);
internal sealed record ChatRequest(string Message, string Model, int? ConversationId = null, List<Message>? ConversationMessages = null);
internal sealed record ChatResponse(string Reply, long Time);
internal sealed record StatsUpdateRequest(long? ThinkingTime, int? MessagesSent);
internal sealed record ContextPromptRequest(string ContextPrompt);

internal sealed class GlobalStats
{
    public long TotalThinkingTime { get; set; }
    public int MessagesSentCount { get; set; }
    public int TotalConversations { get; set; }
}
