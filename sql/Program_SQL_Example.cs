// =============================================
// Program.cs Atualizado - Versão com SQL
// Modificações necessárias para usar base de dados SQL
// =============================================

// Adicionar using necessários
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

// ... código existente ...

var builder = WebApplication.CreateBuilder(args);

// ⚠️  IMPORTANTE: Adicionar configuração do DbContext
builder.Services.AddDbContext<AssistenteDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
        "Server=localhost\\SQLEXPRESS;Database=AssistenteVirtualDB;Trusted_Connection=True;TrustServerCertificate=True;"));

builder.Services.AddCors();

// ... código existente ...

var app = builder.Build();

// ⚠️  IMPORTANTE: Remover caminhos de arquivo JSON (não são mais necessários)
// var conversationsPath = Path.Combine(app.Environment.ContentRootPath, "data", "conversas.json");
// var statsPath = Path.Combine(app.Environment.ContentRootPath, "data", "stats.json");

app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

// ⚠️  IMPORTANTE: Usar DataStore SQL em vez de JSON
// var store = new DataStore(conversationsPath, statsPath, jsonOptions);
var store = new DataStore();

// ... resto do código permanece igual ...

// =============================================
// APIS QUE PRECISAM DE AJUSTES MÍNIMOS
// =============================================

// API para obter conversa por ID - precisa ajustar para carregar do SQL
app.MapGet("/api/conversation/{id:int}", async (int id) =>
{
    // ⚠️  IMPORTANTE: Agora carrega do SQL
    var conversation = await store.Context.Conversations
        .Include(c => c.Messages.OrderBy(m => m.Timestamp))
        .FirstOrDefaultAsync(c => c.ConversationId == id && !c.IsArchived);

    return conversation is not null ? Results.Json(conversation, jsonOptions) : Results.NotFound(new { error = "Conversa não encontrada" });
});

// API para nova chat - permanece similar
app.MapPost("/api/new-chat", async () =>
{
    await store.SaveCurrentConversationIfNeededAsync();
    store.Stats.TotalConversations++;
    store.CurrentId++;
    store.CurrentConversation = new Conversation
    {
        ConversationId = store.CurrentId,
        Title = "Nova conversa",
        Messages = new List<Message>(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    await store.SaveStatsAsync();
    return Results.Json(new { ok = true });
});

// API para deletar conversa - agora faz soft delete
app.MapDelete("/api/conversation/{id:int}", async (int id) =>
{
    var removed = await store.RemoveConversationAsync(id);
    if (!removed)
    {
        return Results.NotFound(new { error = "Conversa não encontrada" });
    }

    // ⚠️  NOTA: CurrentConversation não precisa ser resetada aqui
    // pois RemoveConversationAsync já trata disso

    await store.SaveConversationsAsync();
    return Results.Json(new { ok = true });
});

// API para limpar histórico - permanece similar
app.MapGet("/api/stats", () => Results.Json(store.Stats, jsonOptions));

// API para atualizar estatísticas - permanece similar
app.MapPost("/api/stats/update", async (StatsUpdateRequest request) =>
{
    if (request.ThinkingTime.HasValue)
    {
        store.Stats.TotalThinkingTimeMs += request.ThinkingTime.Value;
    }
    if (request.MessagesSent.HasValue)
    {
        store.Stats.MessagesSentCount += request.MessagesSent.Value;
    }
    await store.SaveStatsAsync();
    return Results.Json(new { ok = true });
});

// API para chat - adicionar logging e métricas
app.MapPost("/api/chat", async (ChatRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Message) || string.IsNullOrWhiteSpace(request.Model))
    {
        return Results.BadRequest(new { reply = "Pedido inválido", time = 0 });
    }

    var message = new Message
    {
        Role = "user",
        Content = request.Message,
        Timestamp = DateTime.UtcNow,
        Model = request.Model
    };
    store.CurrentConversation.Messages.Add(message);

    if (store.CurrentConversation.Messages.Count == 1)
    {
        var title = request.Message.Length <= 30 ? request.Message.Trim() : request.Message.Substring(0, 30).Trim() + "...";
        store.CurrentConversation.Title = title;
        store.CurrentConversation.ModelUsed = request.Model;
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
            // ⚠️  IMPORTANTE: Log de erro
            await store.LogEventAsync(store.CurrentConversation.ConversationId, "api_error",
                $"Status: {response.StatusCode}", content);

            return Results.Json(new { reply = "Erro na API do Ollama", time = 0, error = content }, statusCode: 500, options: jsonOptions);
        }

        var replyText = ExtractReplyText(content);
        var end = DateTime.UtcNow;
        var duration = (long)(end - start).TotalMilliseconds;

        var botMessage = new Message
        {
            Role = "bot",
            Content = replyText,
            Timestamp = DateTime.UtcNow,
            Model = request.Model,
            ResponseTimeMs = duration
        };
        store.CurrentConversation.Messages.Add(botMessage);

        await store.SaveCurrentConversationAsync();

        // ⚠️  IMPORTANTE: Log de sucesso
        await store.LogEventAsync(store.CurrentConversation.ConversationId, "message_added",
            $"{{\"user_message_length\": {request.Message.Length}, \"response_time_ms\": {duration}}}");

        return Results.Json(new ChatResponse(replyText, duration), jsonOptions);
    }
    catch (Exception ex)
    {
        // ⚠️  IMPORTANTE: Log de erro
        await store.LogEventAsync(store.CurrentConversation.ConversationId, "exception",
            ex.Message, ex.StackTrace);

        return Results.Json(new { reply = "Erro no servidor", time = 0, error = ex.Message }, statusCode: 500, options: jsonOptions);
    }
});

// ... resto do código permanece igual ...

// =============================================
// CLASSES DE DADOS - LEGACY (manter para compatibilidade)
// =============================================

// Manter estas classes para compatibilidade com APIs existentes
internal sealed record ChatRequest(string Message, string Model);
internal sealed record ChatResponse(string Reply, long Time);
internal sealed record StatsUpdateRequest(long? ThinkingTime, int? MessagesSent);

// =============================================
// APPSSETTINGS.JSON EXEMPLO
// =============================================

/*
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=AssistenteVirtualDB;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
*/

// =============================================
// INSTRUÇÕES DE MIGRAÇÃO
// =============================================

/*
PASSO A PASSO PARA MIGRAÇÃO:

1. Instalar SQL Server (ver SQL_SERVER_SETUP.md)

2. Criar base de dados e executar scripts:
   - database_schema.sql
   - migration_script.sql

3. Adicionar pacotes NuGet:
   dotnet add package Microsoft.EntityFrameworkCore.SqlServer
   dotnet add package Microsoft.EntityFrameworkCore.Tools

4. Criar appsettings.json com connection string

5. Substituir DataStore no Program.cs

6. Testar aplicação:
   - Verificar se conecta ao SQL
   - Testar criação de conversas
   - Testar envio de mensagens
   - Verificar estatísticas

7. Backup dos arquivos JSON (opcional)

8. Remover código JSON não utilizado
*/