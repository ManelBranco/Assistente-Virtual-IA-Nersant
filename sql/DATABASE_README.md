# Base de Dados - Assistente Virtual IA Nersant

Este documento explica como configurar e usar a base de dados SQL para o projeto Assistente Virtual IA Nersant.

## 📋 Visão Geral

A migração de JSON para SQL permite:
- Melhor performance com grandes volumes de dados
- Consultas mais eficientes e complexas
- Relacionamentos estruturados entre dados
- Auditoria e logs de atividades
- Escalabilidade para múltiplos usuários

## 🗄️ Estrutura da Base de Dados

### Tabelas Principais

| Tabela | Descrição |
|--------|-----------|
| `Conversations` | Armazena as conversas do assistente |
| `Messages` | Armazena as mensagens individuais |
| `Statistics` | Estatísticas globais do sistema |
| `ConversationLogs` | Logs de auditoria e eventos |

### Relacionamentos

```
Conversations (1) ──── (N) Messages
Conversations (1) ──── (N) ConversationLogs
```

## 🚀 Configuração Inicial

### 1. Criar Base de Dados

```sql
CREATE DATABASE AssistenteVirtualDB;
GO
USE AssistenteVirtualDB;
GO
```

### 2. Executar Schema

Execute o arquivo `database_schema.sql` para criar todas as tabelas, índices, triggers e procedures.

### 3. Migrar Dados Existentes

Execute o arquivo `migration_script.sql` para migrar os dados dos arquivos JSON atuais.

## 📊 Consultas Úteis

### Ver Todas as Conversas
```sql
SELECT * FROM vw_ConversationsWithLastMessage
ORDER BY UpdatedAt DESC;
```

### Ver Mensagens de uma Conversa
```sql
SELECT Role, Content, Timestamp
FROM Messages
WHERE ConversationId = 1
ORDER BY Timestamp;
```

### Estatísticas Gerais
```sql
SELECT * FROM vw_StatisticsSummary;
```

### Conversas por Modelo
```sql
SELECT ModelUsed, COUNT(*) as TotalConversas
FROM Conversations
WHERE ModelUsed IS NOT NULL
GROUP BY ModelUsed
ORDER BY TotalConversas DESC;
```

## 🔧 Stored Procedures

### Criar Nova Conversa
```sql
EXEC sp_CreateConversation @Title = 'Nova Conversa', @ModelUsed = 'llama3.1:latest';
```

### Adicionar Mensagem
```sql
EXEC sp_AddMessage
    @ConversationId = 1,
    @Role = 'user',
    @Content = 'Olá, como vai?',
    @Model = 'llama3.1:latest',
    @ResponseTimeMs = 1500;
```

### Atualizar Estatísticas
```sql
EXEC sp_UpdateStatistics @ThinkingTimeMs = 1500, @MessagesSent = 1;
```

### Arquivar Conversa
```sql
EXEC sp_ArchiveConversation @ConversationId = 1;
```

## 📈 Monitoramento e Manutenção

### Ver Logs de Auditoria
```sql
SELECT cl.*, c.Title
FROM ConversationLogs cl
LEFT JOIN Conversations c ON cl.ConversationId = c.ConversationId
ORDER BY cl.Timestamp DESC;
```

### Limpeza de Conversas Antigas
```sql
-- Arquivar conversas com mais de 6 meses
UPDATE Conversations
SET IsArchived = 1
WHERE CreatedAt < DATEADD(MONTH, -6, GETUTCDATE())
AND IsArchived = 0;
```

### Backup Regular
```sql
BACKUP DATABASE AssistenteVirtualDB
TO DISK = 'C:\Backup\AssistenteVirtualDB.bak'
WITH FORMAT, INIT;
```

## 🔄 Próximos Passos

### Migração do Código C#

Para migrar o código C# para usar SQL em vez de JSON:

1. **Adicionar Entity Framework Core**
   ```bash
   dotnet add package Microsoft.EntityFrameworkCore.SqlServer
   dotnet add package Microsoft.EntityFrameworkCore.Tools
   ```

2. **Criar DbContext**
   ```csharp
   public class AssistenteDbContext : DbContext
   {
       public DbSet<Conversation> Conversations { get; set; }
       public DbSet<Message> Messages { get; set; }
       public DbSet<GlobalStats> Statistics { get; set; }

       protected override void OnConfiguring(DbContextOptionsBuilder options)
           => options.UseSqlServer("Server=.;Database=AssistenteVirtualDB;Trusted_Connection=True;");
   }
   ```

3. **Atualizar DataStore**
   - Substituir operações de arquivo JSON por operações Entity Framework
   - Usar LINQ para consultas
   - Implementar transações para consistência

### Funcionalidades Futuras

- **Multi-usuário**: Adicionar tabela `Users`
- **Preferências**: Tabela `ModelPreferences`
- **Feedback**: Avaliação de respostas da IA
- **Analytics**: Relatórios avançados de uso
- **API REST**: Expor dados via API

## 📋 Checklist de Migração

- [ ] Criar base de dados
- [ ] Executar `database_schema.sql`
- [ ] Executar `migration_script.sql`
- [ ] Verificar integridade dos dados
- [ ] Atualizar código C# para usar SQL
- [ ] Testar todas as funcionalidades
- [ ] Fazer backup dos arquivos JSON originais
- [ ] Remover dependência de arquivos JSON

## 🆘 Troubleshooting

### Problemas Comuns

1. **Erro de Conexão**: Verificar string de conexão
2. **Dados Não Aparecem**: Verificar se migration foi executada
3. **Performance Lenta**: Verificar índices criados
4. **Triggers Não Funcionam**: Verificar se foram criados corretamente

### Logs de Erro
```sql
SELECT * FROM ConversationLogs
WHERE ErrorMessage IS NOT NULL
ORDER BY Timestamp DESC;
```

---

**Nota**: Este schema foi projetado para ser escalável e preparado para futuras expansões do sistema.