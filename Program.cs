using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Adicionar serviços
builder.Services.AddCors();
builder.Services.AddHttpClient();

var app = builder.Build();

// Middleware
app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseDefaultFiles();
app.UseStaticFiles();

// Models
public class ChatRequest
{
    public string Message { get; set; } = "";
    public string Model { get; set; } = "llama3.2:latest";
}

public class OllamaRequest
{
    public string Model { get; set; } = "";
    public string Prompt { get; set; } = "";
    public bool Stream { get; set; } = false;
}

public class Message
{
    public string Role { get; set; } = "";
    public string Text { get; set; } = "";
}

public class Conversation
{
    public int Id { get; set; }
    public string Title { get; set; } = "Nova conversa";
    public List<Message> Messages { get; set; } = new();
}

// Variáveis globais
var conversationsFile = "conversas.json";
var conversations = new List<Conversation>();
var currentId = 0;
Conversation currentConversation;

// Carregar conversas existentes
if (File.Exists(conversationsFile))
{
    var json = File.ReadAllText(conversationsFile);
    conversations = JsonSerializer.Deserialize<List<Conversation>>(json) ?? new();
    if (conversations.Any())
        currentId = conversations.Max(c => c.Id) + 1;
}

currentConversation = new Conversation { Id = currentId, Messages = new(), Title = "Nova conversa" };

// ENDPOINTS

// Chat endpoint
app.MapPost("/api/chat", async (ChatRequest request) =>
{
    var startTime = DateTime.Now;

    // Adicionar mensagem do utilizador
    currentConversation.Messages.Add(new Message { Role = "user", Text = request.Message });

    // Criar título automático
    if (currentConversation.Messages.Count == 1)
    {
        currentConversation.Title = request.Message.Length > 30 
            ? request.Message[..30] + "..." 
            : request.Message;
    }

    try
    {
        // Preparar prompt do sistema
        var systemPrompt = "Responde sempre em português de Portugal. Sê natural, direto e formal. " +
                          "Apenas na primeira mensagem apresenta-te como assistente virtual de Inteligência Artificial da Nersant de Torres Novas. " +
                          "Nas restantes mensagens não repitas a apresentação.";

        var fullPrompt = systemPrompt + "\n\nUtilizador: " + request.Message;

        // Chamar Ollama
        using var httpClient = new HttpClient();
        var ollamaRequest = new OllamaRequest
        {
            Model = request.Model,
            Prompt = fullPrompt,
            Stream = false
        };

        var content = new StringContent(
            JsonSerializer.Serialize(ollamaRequest),
            Encoding.UTF8,
            "application/json"
        );

        var response = await httpClient.PostAsync("http://localhost:11434/api/generate", content);
        var responseString = await response.Content.ReadAsStringAsync();
        var ollamaResponse = JsonSerializer.Deserialize<Dictionary<string, string>>(responseString);
        
        var reply = ollamaResponse?.GetValueOrDefault("response") ?? "Sem resposta da IA";
        var endTime = DateTime.Now;
        var duration = (endTime - startTime).TotalMilliseconds;

        // Adicionar resposta do bot
        currentConversation.Messages.Add(new Message { Role = "bot", Text = reply });

        // Guardar conversa
        var existingIndex = conversations.FindIndex(c => c.Id == currentConversation.Id);
        if (existingIndex != -1)
        {
            conversations[existingIndex] = JsonSerializer.Deserialize<Conversation>(
                JsonSerializer.Serialize(currentConversation))!;
        }
        else if (currentConversation.Messages.Any())
        {
            conversations.Add(JsonSerializer.Deserialize<Conversation>(
                JsonSerializer.Serialize(currentConversation))!);
        }

        // Salvar no ficheiro
        File.WriteAllText(conversationsFile, JsonSerializer.Serialize(conversations, 
            new JsonSerializerOptions { WriteIndented = true }));

        return Results.Json(new { reply, time = duration });
    }
    catch (Exception ex)
    {
        return Results.Json(new { reply = $"Erro ao contactar IA: {ex.Message}", time = 0 });
    }
});

// Histórico de conversas
app.MapGet("/api/history", () =>
{
    return Results.Json(conversations);
});

// Nova conversa
app.MapPost("/api/new-chat", () =>
{
    // Guardar conversa atual se tiver mensagens
    if (currentConversation.Messages.Any())
    {
        var existingIndex = conversations.FindIndex(c => c.Id == currentConversation.Id);
        if (existingIndex == -1)
        {
            conversations.Add(JsonSerializer.Deserialize<Conversation>(
                JsonSerializer.Serialize(currentConversation))!);
            File.WriteAllText(conversationsFile, JsonSerializer.Serialize(conversations, 
                new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    currentId++;
    currentConversation = new Conversation { Id = currentId, Messages = new(), Title = "Nova conversa" };
    
    return Results.Json(new { ok = true });
});

// Carregar conversa específica
app.MapGet("/api/conversation/{id}", (int id) =>
{
    var conversation = conversations.Find(c => c.Id == id);
    if (conversation == null)
        return Results.NotFound();
    
    currentConversation = JsonSerializer.Deserialize<Conversation>(
        JsonSerializer.Serialize(conversation))!;
    
    return Results.Json(conversation);
});

// Apagar conversa
app.MapDelete("/api/conversation/{id}", (int id) =>
{
    var index = conversations.FindIndex(c => c.Id == id);
    if (index == -1)
        return Results.NotFound();
    
    conversations.RemoveAt(index);
    File.WriteAllText(conversationsFile, JsonSerializer.Serialize(conversations, 
        new JsonSerializerOptions { WriteIndented = true }));
    
    if (currentConversation.Id == id)
    {
        currentId++;
        currentConversation = new Conversation { Id = currentId, Messages = new(), Title = "Nova conversa" };
    }
    
    return Results.Json(new { ok = true });
});

// Limpar todo o histórico
app.MapPost("/api/clear-history", () =>
{
    conversations.Clear();
    currentId = 0;
    currentConversation = new Conversation { Id = currentId, Messages = new(), Title = "Nova conversa" };
    File.WriteAllText(conversationsFile, "[]");
    File.WriteAllText("stats.json", "{\"totalThinkingTime\":0,\"messagesSentCount\":0,\"totalConversations\":0}");
    
    return Results.Json(new { ok = true });
});

// Estatísticas
app.MapGet("/api/stats", () =>
{
    if (File.Exists("stats.json"))
    {
        var json = File.ReadAllText("stats.json");
        var stats = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        return Results.Json(stats ?? new Dictionary<string, object>());
    }
    return Results.Json(new { totalThinkingTime = 0, messagesSentCount = 0, totalConversations = 0 });
});

app.MapPost("/api/stats/update", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    var stats = JsonSerializer.Deserialize<Dictionary<string, int>>(body);
    
    if (stats != null)
    {
        var currentStats = new Dictionary<string, int>();
        if (File.Exists("stats.json"))
        {
            var json = File.ReadAllText("stats.json");
            currentStats = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
        }
        
        if (stats.ContainsKey("thinkingTime"))
            currentStats["totalThinkingTime"] = currentStats.GetValueOrDefault("totalThinkingTime") + stats["thinkingTime"];
        if (stats.ContainsKey("messagesSent"))
            currentStats["messagesSentCount"] = currentStats.GetValueOrDefault("messagesSentCount") + stats["messagesSent"];
        
        File.WriteAllText("stats.json", JsonSerializer.Serialize(currentStats, 
            new JsonSerializerOptions { WriteIndented = true }));
    }
    
    return Results.Json(new { ok = true });
});

// Servir ficheiros estáticos (fallback para SPA)
app.MapFallbackToFile("index.html");

app.Run();