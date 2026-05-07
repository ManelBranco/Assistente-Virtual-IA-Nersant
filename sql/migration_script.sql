-- =============================================
-- SCRIPT DE MIGRAÇÃO - Dados JSON para SQL
-- Migra os dados dos arquivos conversas.json e stats.json
-- =============================================

USE [AssistenteVirtualDB];
GO

-- =============================================
-- 1. MIGRAÇÃO DAS ESTATÍSTICAS
-- =============================================

-- Atualizar estatísticas com os valores atuais de stats.json
UPDATE Statistics SET
    TotalThinkingTimeMs = 0,      -- totalThinkingTime do stats.json
    MessagesSentCount = 0,        -- messagesSentCount do stats.json
    TotalConversations = 1,       -- totalConversations do stats.json
    LastUpdatedAt = GETUTCDATE()
WHERE StatisticId = 1;

PRINT '✓ Estatísticas migradas';

-- =============================================
-- 2. MIGRAÇÃO DAS CONVERSAS E MENSAGENS
-- =============================================

-- Limpar dados existentes (se necessário)
-- DELETE FROM Messages;
-- DELETE FROM Conversations;
-- DBCC CHECKIDENT ('Conversations', RESEED, 0);
-- DBCC CHECKIDENT ('Messages', RESEED, 0);

-- Conversa ID 0: "olá"
DECLARE @ConvId0 INT;
INSERT INTO Conversations (Title, CreatedAt) VALUES ('olá', GETUTCDATE());
SET @ConvId0 = SCOPE_IDENTITY();

INSERT INTO Messages (ConversationId, Role, Content, Timestamp) VALUES
(@ConvId0, 'user', 'olá', GETUTCDATE()),
(@ConvId0, 'bot', 'Olá! Sou um assistente virtual de Inteligência Artificial da Nersant de Torres Novas. Como posso ajudar você hoje?', GETUTCDATE());

PRINT '✓ Conversa 0 migrada';

-- Conversa ID 1: "olá"
DECLARE @ConvId1 INT;
INSERT INTO Conversations (Title, CreatedAt) VALUES ('olá', GETUTCDATE());
SET @ConvId1 = SCOPE_IDENTITY();

INSERT INTO Messages (ConversationId, Role, Content, Timestamp) VALUES
(@ConvId1, 'user', 'olá', GETUTCDATE()),
(@ConvId1, 'bot', 'Olá! Sou um assistente virtual de Inteligência Artificial da Nersant de Torres Novas. Como posso te ajudar hoje?', GETUTCDATE()),
(@ConvId1, 'user', 'olá', GETUTCDATE()),
(@ConvId1, 'bot', 'Olá, sou um assistente virtual de Inteligência Artificial da Nersant de Torres Novas. Como posso te ajudar hoje?', GETUTCDATE());

PRINT '✓ Conversa 1 migrada';

-- =============================================
-- 3. VERIFICAÇÃO DA MIGRAÇÃO
-- =============================================

-- Verificar conversas migradas
SELECT
    c.ConversationId,
    c.Title,
    c.TotalMessages,
    c.CreatedAt
FROM Conversations c
ORDER BY c.ConversationId;

-- Verificar mensagens migradas
SELECT
    m.MessageId,
    m.ConversationId,
    m.Role,
    LEFT(m.Content, 100) + CASE WHEN LEN(m.Content) > 100 THEN '...' ELSE '' END AS ContentPreview,
    m.Timestamp
FROM Messages m
ORDER BY m.ConversationId, m.Timestamp;

-- Verificar estatísticas
SELECT * FROM vw_StatisticsSummary;

PRINT '✓ Migração concluída com sucesso!';
PRINT 'Verifique os dados acima para confirmar a migração.';