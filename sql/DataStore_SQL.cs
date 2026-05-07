// =============================================
// DataStore Atualizado - Versão SQL
// Substitui o armazenamento JSON por SQL Server
// =============================================

using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

internal class AssistenteDbContext : DbContext
{
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<GlobalStats> Statistics { get; set; }
    public DbSet<ConversationLog> ConversationLogs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        // ⚠️  IMPORTANTE: Substitua pela sua string de conexão
        options.UseSqlServer("Server=localhost;Database=AssistenteVirtualDB;Trusted_Connection=True;TrustServerCertificate=True;");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configurações adicionais do modelo
        modelBuilder.Entity<GlobalStats>()
            .HasKey(s => s.StatisticId);

        modelBuilder.Entity<ConversationLog>()
            .HasKey(l => l.LogId);
    }
}

// Entidades atualizadas
internal class Conversation
{
    public int ConversationId { get; set; }
    public string Title { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsArchived { get; set; }
    public string? ModelUsed { get; set; }
    public int TotalMessages { get; set; }
    public List<Message> Messages { get; set; } = new();
}

internal class Message
{
    public long MessageId { get; set; }
    public int ConversationId { get; set; }
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string? Model { get; set; }
    public long? ResponseTimeMs { get; set; }
    public int? TokensUsed { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
}

internal class GlobalStats
{
    public int StatisticId { get; set; } = 1;
    public long TotalThinkingTimeMs { get; set; }
    public int MessagesSentCount { get; set; }
    public int TotalConversations { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}

internal class ConversationLog
{
    public long LogId { get; set; }
    public int? ConversationId { get; set; }
    public string EventType { get; set; } = "";
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
    public string? ErrorMessage { get; set; }
}

internal class DataStore
{
    private readonly AssistenteDbContext _context;
    private readonly SemaphoreSlim _locker = new(1, 1);

    public List<Conversation> Conversations { get; private set; } = new();
    public GlobalStats Stats { get; private set; } = new();
    public int CurrentId { get; set; }
    public Conversation CurrentConversation { get; set; } = new();

    public DataStore()
    {
        _context = new AssistenteDbContext();
    }

    public async Task InitializeAsync()
    {
        await _locker.WaitAsync();
        try
        {
            // Carregar conversas (não arquivadas)
            Conversations = await _context.Conversations
                .Where(c => !c.IsArchived)
                .Include(c => c.Messages.OrderBy(m => m.Timestamp))
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();

            // Carregar estatísticas
            Stats = await _context.Statistics.FirstOrDefaultAsync() ?? new GlobalStats();

            // Definir CurrentId baseado nas conversas existentes
            CurrentId = Conversations.Any() ? Conversations.Max(c => c.ConversationId) + 1 : 0;

            // Criar conversa atual vazia
            CurrentConversation = new Conversation
            {
                ConversationId = CurrentId,
                Title = "Nova conversa",
                Messages = new List<Message>()
            };
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
                var existing = await _context.Conversations
                    .FirstOrDefaultAsync(c => c.ConversationId == CurrentConversation.ConversationId);

                if (existing == null)
                {
                    // Nova conversa
                    _context.Conversations.Add(CurrentConversation);
                }
                else
                {
                    // Atualizar conversa existente
                    existing.Title = CurrentConversation.Title;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.Messages = CurrentConversation.Messages;
                }

                await _context.SaveChangesAsync();
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
            var existing = await _context.Conversations
                .FirstOrDefaultAsync(c => c.ConversationId == CurrentConversation.ConversationId);

            if (existing == null)
            {
                _context.Conversations.Add(CurrentConversation);
            }
            else
            {
                existing.Title = CurrentConversation.Title;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.Messages = CurrentConversation.Messages;
            }

            await _context.SaveChangesAsync();
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
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.ConversationId == id);

            if (conversation == null) return false;

            // Soft delete - arquivar em vez de deletar
            conversation.IsArchived = true;
            await _context.SaveChangesAsync();

            // Log do evento
            await LogEventAsync(id, "archived", "Conversation archived by user");

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
            // Arquivar todas as conversas
            await _context.Conversations
                .Where(c => !c.IsArchived)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.IsArchived, true));

            // Reset estatísticas
            Stats = new GlobalStats();
            _context.Statistics.Update(Stats);

            CurrentId = 0;
            CurrentConversation = new Conversation
            {
                ConversationId = 0,
                Title = "Nova conversa",
                Messages = new List<Message>()
            };

            await _context.SaveChangesAsync();
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
            await _context.SaveChangesAsync();
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
            _context.Statistics.Update(Stats);
            await _context.SaveChangesAsync();
        }
        finally
        {
            _locker.Release();
        }
    }

    public async Task LogEventAsync(int? conversationId, string eventType, string details, string? errorMessage = null)
    {
        var log = new ConversationLog
        {
            ConversationId = conversationId,
            EventType = eventType,
            Details = details,
            ErrorMessage = errorMessage
        };

        _context.ConversationLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    // Método auxiliar para conversões (se necessário)
    private Conversation CloneConversation(Conversation conversation)
    {
        return new Conversation
        {
            ConversationId = conversation.ConversationId,
            Title = conversation.Title,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt,
            IsArchived = conversation.IsArchived,
            ModelUsed = conversation.ModelUsed,
            TotalMessages = conversation.TotalMessages,
            Messages = conversation.Messages.Select(m => new Message
            {
                MessageId = m.MessageId,
                ConversationId = m.ConversationId,
                Role = m.Role,
                Content = m.Content,
                Timestamp = m.Timestamp,
                Model = m.Model,
                ResponseTimeMs = m.ResponseTimeMs,
                TokensUsed = m.TokensUsed,
                IsEdited = m.IsEdited,
                EditedAt = m.EditedAt
            }).ToList()
        };
    }
}

// =============================================
// INSTRUÇÕES PARA IMPLEMENTAÇÃO
// =============================================

/*
1. Adicionar pacotes NuGet:
   dotnet add package Microsoft.EntityFrameworkCore.SqlServer
   dotnet add package Microsoft.EntityFrameworkCore.Tools

2. Atualizar Program.cs:
   - Substituir DataStore jsonPath por DataStore()
   - Adicionar migration inicial se necessário

3. Configurar connection string:
   - Atualizar OnConfiguring no AssistenteDbContext
   - Usar appsettings.json para produção

4. Executar migrations:
   - Add-Migration InitialCreate
   - Update-Database

5. Testar:
   - Verificar se dados são salvos/carregados corretamente
   - Testar todas as funcionalidades da UI
*/