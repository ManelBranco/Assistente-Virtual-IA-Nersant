-- =============================================
-- SCRIPT DE MIGRAÇÃO - Dados JSON para MySQL
-- Corre APÓS o database_schema.sql
-- =============================================

USE AssistenteVirtualDB;

-- =============================================
-- 1. LIMPAR DADOS DE TESTES (opcional)
-- Descomentar se quiseres começar do zero
-- =============================================
-- DELETE FROM Messages;
-- DELETE FROM Conversations;
-- DELETE FROM Statistics;
-- ALTER TABLE Messages     AUTO_INCREMENT = 1;
-- ALTER TABLE Conversations AUTO_INCREMENT = 1;

-- =============================================
-- 2. MIGRAÇÃO DAS ESTATÍSTICAS
-- Substituir os valores pelos do teu stats.json
-- =============================================

INSERT INTO Statistics (StatisticId, TotalThinkingTimeMs, MessagesSentCount, TotalConversations)
VALUES (1, 0, 0, 0)
ON DUPLICATE KEY UPDATE
    TotalThinkingTimeMs = VALUES(TotalThinkingTimeMs),
    MessagesSentCount   = VALUES(MessagesSentCount),
    TotalConversations  = VALUES(TotalConversations);

SELECT 'Estatísticas migradas' AS Result;

-- =============================================
-- 3. MIGRAÇÃO DO CONTEXT PROMPT
-- Substituir pelo conteúdo do teu context_prompt.txt
-- =============================================

INSERT INTO Settings (SettingKey, SettingValue)
VALUES ('context_prompt', '')
ON DUPLICATE KEY UPDATE SettingValue = VALUES(SettingValue);

SELECT 'Context prompt migrado' AS Result;

-- =============================================
-- 4. MIGRAÇÃO DAS CONVERSAS (conversas.json)
-- Para cada conversa do JSON, adicionar um bloco
-- como o exemplo abaixo.
-- Type = 'chat' para conversas normais
-- =============================================

-- Exemplo: Conversa 1
INSERT INTO Conversations (Title, Type, CreatedAt, UpdatedAt)
VALUES ('olá', 'chat', UTC_TIMESTAMP(), UTC_TIMESTAMP());

SET @conv1 = LAST_INSERT_ID();

INSERT INTO Messages (ConversationId, Role, Content, Timestamp) VALUES
(@conv1, 'user', 'olá', UTC_TIMESTAMP()),
(@conv1, 'bot',  'Olá! Sou um assistente virtual de Inteligência Artificial da Nersant de Torres Novas. Como posso ajudar você hoje?', UTC_TIMESTAMP());

-- Exemplo: Conversa 2
INSERT INTO Conversations (Title, Type, CreatedAt, UpdatedAt)
VALUES ('olá', 'chat', UTC_TIMESTAMP(), UTC_TIMESTAMP());

SET @conv2 = LAST_INSERT_ID();

INSERT INTO Messages (ConversationId, Role, Content, Timestamp) VALUES
(@conv2, 'user', 'olá', UTC_TIMESTAMP()),
(@conv2, 'bot',  'Olá! Sou um assistente virtual de Inteligência Artificial da Nersant de Torres Novas. Como posso te ajudar hoje?', UTC_TIMESTAMP()),
(@conv2, 'user', 'olá', UTC_TIMESTAMP()),
(@conv2, 'bot',  'Olá, sou um assistente virtual de Inteligência Artificial da Nersant de Torres Novas. Como posso te ajudar hoje?', UTC_TIMESTAMP());

SELECT 'Conversas (chat) migradas' AS Result;

-- =============================================
-- 5. MIGRAÇÃO DAS FATURAS (faturas.json)
-- Igual ao bloco acima mas com Type = 'invoice'
-- =============================================

-- Exemplo: Conversa de fatura 1
-- INSERT INTO Conversations (Title, Type, CreatedAt, UpdatedAt)
-- VALUES ('Análise de fatura', 'invoice', UTC_TIMESTAMP(), UTC_TIMESTAMP());
-- SET @fatura1 = LAST_INSERT_ID();
-- INSERT INTO Messages (ConversationId, Role, Content, Timestamp) VALUES
-- (@fatura1, 'user', '...', UTC_TIMESTAMP()),
-- (@fatura1, 'bot',  '...', UTC_TIMESTAMP());

-- =============================================
-- 6. VERIFICAÇÃO DA MIGRAÇÃO
-- =============================================

SELECT
    c.ConversationId,
    c.Title,
    c.Type,
    c.TotalMessages,
    c.CreatedAt
FROM Conversations c
ORDER BY c.ConversationId;

SELECT
    m.MessageId,
    m.ConversationId,
    m.Role,
    LEFT(m.Content, 80) AS ContentPreview,
    m.Timestamp
FROM Messages m
ORDER BY m.ConversationId, m.Timestamp;

SELECT * FROM vw_StatisticsSummary;

SELECT 'Migração concluída. Verifique os dados acima.' AS Result;
