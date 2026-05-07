using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors();

var app = builder.Build();
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

var publicDir = Path.Combine(app.Environment.ContentRootPath, "public");
var conversationsPath = Path.Combine(app.Environment.ContentRootPath, "conversas.json");
var statsPath = Path.Combine(app.Environment.ContentRootPath, "stats.json");

Directory.CreateDirectory(publicDir);

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

var store = new DataStore(conversationsPath, statsPath, jsonOptions);
await store.InitializeAsync();

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

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(Path.Combine(publicDir, "index.html"));
});

app.MapGet("/api/history", () => Results.Json(store.Conversations, jsonOptions));

app.MapGet("/api/conversation/{id:int}", (int id) =>
{
    var conversation = store.Conversations.FirstOrDefault(c => c.Id == id);
    return conversation is not null ? Results.Json(conversation, jsonOptions) : Results.NotFound(new { error = "Conversa não encontrada" });
});

app.MapPost("/api/new-chat", async () =>
{
    await store.SaveCurrentConversationIfNeededAsync();
    store.Stats.TotalConversations++;
    store.CurrentId++;
    store.CurrentConversation = new Conversation(store.CurrentId, "Nova conversa", new List<Message>());
    await store.SaveStatsAsync();
    return Results.Json(new { ok = true });
});

app.MapDelete("/api/conversation/{id:int}", async (int id) =>
{
    var removed = await store.RemoveConversationAsync(id);
    if (!removed)
    {
        return Results.NotFound(new { error = "Conversa não encontrada" });
    }

    if (store.CurrentConversation.Id == id)
    {
        store.CurrentId++;
        store.CurrentConversation = new Conversation(store.CurrentId, "Nova conversa", new List<Message>());
    }

    await store.SaveConversationsAsync();
    return Results.Json(new { ok = true });
});

app.MapPost("/api/clear-history", async () =>
{
    await store.ClearHistoryAsync();
    return Results.Json(new { ok = true });
});

app.MapGet("/api/stats", () => Results.Json(store.Stats, jsonOptions));

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

app.MapPost("/api/chat", async (ChatRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Message) || string.IsNullOrWhiteSpace(request.Model))
    {
        return Results.BadRequest(new { reply = "Pedido inválido", time = 0 });
    }

    store.CurrentConversation.Messages.Add(new Message("user", request.Message));

    if (store.CurrentConversation.Messages.Count == 1)
    {
        var title = request.Message.Length <= 30 ? request.Message.Trim() : request.Message.Substring(0, 30).Trim() + "...";
        store.CurrentConversation = store.CurrentConversation with { Title = title };
    }

    var systemPrompt = "Responde sempre em português de Portugal. Sê natural, direto e formal. Apenas na primeira mensagem apresenta-te como assistente virtual de Inteligência Artificial da Nersant de Torres Novas. Nas restantes mensagens não repitas a apresentação. Apenas na primeira mensagem pergunta em que podes ajudar. Nas restantes mensagens não repitas";
    var fullPrompt = $"{systemPrompt}\n\nUtilizador: {request.Message}";

    var start = DateTime.UtcNow;

    try
    {

        using var httpClient = new HttpClient();
        var response = await httpClient.PostAsJsonAsync(
            "http://localhost:11434/api/generate",
            new { model = request.Model, prompt = fullPrompt, stream = false }
        );

        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return Results.Json(new { reply = "Erro na API do Ollama", time = 0, error = content }, statusCode: 500, options: jsonOptions);
        }

        var replyText = ExtractReplyText(content);
        var end = DateTime.UtcNow;
        var duration = (long)(end - start).TotalMilliseconds;

        store.CurrentConversation.Messages.Add(new Message("bot", replyText));
        await store.SaveCurrentConversationAsync();

        return Results.Json(new ChatResponse(replyText, duration), jsonOptions);
    }
    catch (Exception ex)
    {
        return Results.Json(new { reply = "Erro no servidor", time = 0, error = ex.Message }, statusCode: 500, options: jsonOptions);
    }
});

app.Run();

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

    private static Conversation CloneConversation(Conversation conversation)
    {
        return new Conversation(conversation.Id, conversation.Title, conversation.Messages.Select(m => new Message(m.Role, m.Text)).ToList());
    }
}

internal sealed record Conversation(int Id, string Title, List<Message> Messages);
internal sealed record Message(string Role, string Text);
internal sealed record ChatRequest(string Message, string Model);
internal sealed record ChatResponse(string Reply, long Time);
internal sealed record StatsUpdateRequest(long? ThinkingTime, int? MessagesSent);

internal sealed class GlobalStats
{
    public long TotalThinkingTime { get; set; }
    public int MessagesSentCount { get; set; }
    public int TotalConversations { get; set; }
}
