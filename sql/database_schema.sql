-- =============================================
-- Assistente Virtual IA Nersant - Schema SQL
-- Base de dados para armazenar conversas, mensagens e estatísticas
-- =============================================

USE [AssistenteVirtualDB];
GO

-- =============================================
-- Tabela: Conversations
-- Armazena as conversas do assistente
-- =============================================
CREATE TABLE Conversations (
    ConversationId INT PRIMARY KEY IDENTITY(1,1),
    Title NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME DEFAULT GETUTCDATE(),
    IsArchived BIT DEFAULT 0,
    ModelUsed NVARCHAR(50), -- Modelo usado na conversa
    TotalMessages INT DEFAULT 0
);

-- =============================================
-- Tabela: Messages
-- Armazena as mensagens individuais das conversas
-- =============================================
CREATE TABLE Messages (
    MessageId BIGINT PRIMARY KEY IDENTITY(1,1),
    ConversationId INT NOT NULL FOREIGN KEY REFERENCES Conversations(ConversationId) ON DELETE CASCADE,
    Role NVARCHAR(10) NOT NULL CHECK (Role IN ('user', 'bot')), -- "user" ou "bot"
    Content NVARCHAR(MAX) NOT NULL,
    Timestamp DATETIME DEFAULT GETUTCDATE(),
    Model NVARCHAR(50), -- Modelo usado para esta mensagem específica
    ResponseTimeMs BIGINT, -- Tempo de resposta da IA em milissegundos
    TokensUsed INT, -- Número de tokens processados (para futuro)
    IsEdited BIT DEFAULT 0,
    EditedAt DATETIME
);

-- =============================================
-- Tabela: Statistics
-- Estatísticas globais do sistema
-- =============================================
CREATE TABLE Statistics (
    StatisticId INT PRIMARY KEY IDENTITY(1,1),
    TotalThinkingTimeMs BIGINT DEFAULT 0, -- Tempo total de processamento
    MessagesSentCount INT DEFAULT 0, -- Total de mensagens enviadas
    TotalConversations INT DEFAULT 0, -- Total de conversas criadas
    CreatedAt DATETIME DEFAULT GETUTCDATE(),
    LastUpdatedAt DATETIME DEFAULT GETUTCDATE()
);

-- =============================================
-- Tabela: ConversationLogs (Auditoria)
-- Registra eventos importantes das conversas
-- =============================================
CREATE TABLE ConversationLogs (
    LogId BIGINT PRIMARY KEY IDENTITY(1,1),
    ConversationId INT FOREIGN KEY REFERENCES Conversations(ConversationId),
    EventType NVARCHAR(50), -- "created", "message_added", "archived", "deleted"
    Details NVARCHAR(MAX), -- JSON com contexto adicional
    Timestamp DATETIME DEFAULT GETUTCDATE(),
    ErrorMessage NVARCHAR(MAX)
);

-- =============================================
-- ÍNDICES PARA PERFORMANCE
-- =============================================
CREATE INDEX IX_Conversations_CreatedAt ON Conversations(CreatedAt);
CREATE INDEX IX_Conversations_IsArchived ON Conversations(IsArchived);
CREATE INDEX IX_Messages_ConversationId ON Messages(ConversationId);
CREATE INDEX IX_Messages_Timestamp ON Messages(Timestamp);
CREATE INDEX IX_Messages_Role ON Messages(Role);
CREATE INDEX IX_ConversationLogs_ConversationId ON ConversationLogs(ConversationId);
CREATE INDEX IX_ConversationLogs_Timestamp ON ConversationLogs(Timestamp);

-- =============================================
-- TRIGGERS PARA MANUTENÇÃO AUTOMÁTICA
-- =============================================

-- Trigger para atualizar UpdatedAt em Conversations
CREATE TRIGGER TR_Conversations_UpdateTimestamp
ON Conversations
AFTER UPDATE
AS
BEGIN
    UPDATE Conversations
    SET UpdatedAt = GETUTCDATE()
    WHERE ConversationId IN (SELECT ConversationId FROM inserted);
END;
GO

-- Trigger para atualizar TotalMessages em Conversations
CREATE TRIGGER TR_Messages_UpdateConversationCount
ON Messages
AFTER INSERT, DELETE
AS
BEGIN
    UPDATE c
    SET TotalMessages = (
        SELECT COUNT(*)
        FROM Messages m
        WHERE m.ConversationId = c.ConversationId
    )
    FROM Conversations c
    WHERE c.ConversationId IN (
        SELECT DISTINCT ConversationId FROM inserted
        UNION
        SELECT DISTINCT ConversationId FROM deleted
    );
END;
GO

-- Trigger para atualizar LastUpdatedAt em Statistics
CREATE TRIGGER TR_Statistics_UpdateTimestamp
ON Statistics
AFTER UPDATE
AS
BEGIN
    UPDATE Statistics
    SET LastUpdatedAt = GETUTCDATE()
    WHERE StatisticId IN (SELECT StatisticId FROM inserted);
END;
GO

-- =============================================
-- DADOS INICIAIS
-- =============================================

-- Inserir estatísticas iniciais (se não existir)
IF NOT EXISTS (SELECT 1 FROM Statistics WHERE StatisticId = 1)
BEGIN
    INSERT INTO Statistics (StatisticId, TotalThinkingTimeMs, MessagesSentCount, TotalConversations)
    VALUES (1, 0, 0, 0);
END
GO

-- =============================================
-- VIEWS ÚTEIS
-- =============================================

-- View para estatísticas resumidas
CREATE VIEW vw_StatisticsSummary AS
SELECT
    s.TotalConversations,
    s.MessagesSentCount,
    s.TotalThinkingTimeMs,
    CAST(s.TotalThinkingTimeMs / 1000.0 AS DECIMAL(10,2)) AS TotalThinkingTimeSeconds,
    CASE
        WHEN s.TotalConversations > 0 THEN CAST(s.MessagesSentCount AS DECIMAL(10,2)) / s.TotalConversations
        ELSE 0
    END AS AvgMessagesPerConversation,
    s.LastUpdatedAt
FROM Statistics s
WHERE s.StatisticId = 1;
GO

-- View para conversas com última mensagem
CREATE VIEW vw_ConversationsWithLastMessage AS
SELECT
    c.ConversationId,
    c.Title,
    c.CreatedAt,
    c.UpdatedAt,
    c.IsArchived,
    c.ModelUsed,
    c.TotalMessages,
    m.Timestamp AS LastMessageAt,
    m.Content AS LastMessageContent,
    m.Role AS LastMessageRole
FROM Conversations c
LEFT JOIN (
    SELECT ConversationId, Timestamp, Content, Role,
           ROW_NUMBER() OVER (PARTITION BY ConversationId ORDER BY Timestamp DESC) AS rn
    FROM Messages
) m ON c.ConversationId = m.ConversationId AND m.rn = 1;
GO

-- =============================================
-- STORED PROCEDURES ÚTEIS
-- =============================================

-- Procedure para criar nova conversa
CREATE PROCEDURE sp_CreateConversation
    @Title NVARCHAR(MAX),
    @ModelUsed NVARCHAR(50) = NULL
AS
BEGIN
    INSERT INTO Conversations (Title, ModelUsed)
    VALUES (@Title, @ModelUsed);

    SELECT SCOPE_IDENTITY() AS ConversationId;
END;
GO

-- Procedure para adicionar mensagem
CREATE PROCEDURE sp_AddMessage
    @ConversationId INT,
    @Role NVARCHAR(10),
    @Content NVARCHAR(MAX),
    @Model NVARCHAR(50) = NULL,
    @ResponseTimeMs BIGINT = NULL,
    @TokensUsed INT = NULL
AS
BEGIN
    INSERT INTO Messages (ConversationId, Role, Content, Model, ResponseTimeMs, TokensUsed)
    VALUES (@ConversationId, @Role, @Content, @Model, @ResponseTimeMs, @TokensUsed);

    -- Log do evento
    INSERT INTO ConversationLogs (ConversationId, EventType, Details)
    VALUES (@ConversationId, 'message_added', '{"role":"' + @Role + '","length":' + CAST(LEN(@Content) AS NVARCHAR) + '}');
END;
GO

-- Procedure para atualizar estatísticas
CREATE PROCEDURE sp_UpdateStatistics
    @ThinkingTimeMs BIGINT = NULL,
    @MessagesSent INT = NULL,
    @Conversations INT = NULL
AS
BEGIN
    UPDATE Statistics
    SET
        TotalThinkingTimeMs = ISNULL(TotalThinkingTimeMs, 0) + ISNULL(@ThinkingTimeMs, 0),
        MessagesSentCount = ISNULL(MessagesSentCount, 0) + ISNULL(@MessagesSent, 0),
        TotalConversations = ISNULL(TotalConversations, 0) + ISNULL(@Conversations, 0)
    WHERE StatisticId = 1;
END;
GO

-- Procedure para arquivar conversa
CREATE PROCEDURE sp_ArchiveConversation
    @ConversationId INT
AS
BEGIN
    UPDATE Conversations
    SET IsArchived = 1
    WHERE ConversationId = @ConversationId;

    INSERT INTO ConversationLogs (ConversationId, EventType, Details)
    VALUES (@ConversationId, 'archived', 'Conversation archived');
END;
GO

-- =============================================
-- SCRIPT DE MIGRAÇÃO DOS DADOS JSON EXISTENTES
-- =============================================

-- Este script deve ser executado APÓS criar as tabelas
-- Substitua os valores pelos dados dos seus arquivos JSON

-- 1. Migrar estatísticas
-- UPDATE Statistics SET
--     TotalThinkingTimeMs = 0,  -- valor de stats.json
--     MessagesSentCount = 0,    -- valor de stats.json
--     TotalConversations = 1    -- valor de stats.json
-- WHERE StatisticId = 1;

-- 2. Migrar conversas e mensagens
-- DECLARE @ConversationId INT;
-- DECLARE @MessageId BIGINT;

-- -- Conversa 1
-- INSERT INTO Conversations (Title, CreatedAt) VALUES ('olá', GETUTCDATE());
-- SET @ConversationId = SCOPE_IDENTITY();

-- INSERT INTO Messages (ConversationId, Role, Content, Timestamp) VALUES
-- (@ConversationId, 'user', 'olá', GETUTCDATE()),
-- (@ConversationId, 'bot', 'Olá! Sou um assistente virtual de Inteligência Artificial da Nersant de Torres Novas. Como posso ajudar você hoje?', GETUTCDATE());

-- -- Conversa 2
-- INSERT INTO Conversations (Title, CreatedAt) VALUES ('olá', GETUTCDATE());
-- SET @ConversationId = SCOPE_IDENTITY();

-- INSERT INTO Messages (ConversationId, Role, Content, Timestamp) VALUES
-- (@ConversationId, 'user', 'olá', GETUTCDATE()),
-- (@ConversationId, 'bot', 'Olá! Sou um assistente virtual de Inteligência Artificial da Nersant de Torres Novas. Como posso te ajudar hoje?', GETUTCDATE()),
-- (@ConversationId, 'user', 'olá', GETUTCDATE()),
-- (@ConversationId, 'bot', 'Olá, sou um assistente virtual de Inteligência Artificial da Nersant de Torres Novas. Como posso te ajudar hoje?', GETUTCDATE());

PRINT 'Schema SQL criado com sucesso!';
PRINT 'Execute o script de migração dos dados JSON existentes.';
GO