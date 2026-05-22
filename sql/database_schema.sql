-- =============================================
-- Assistente Virtual IA Nersant - Schema MySQL
-- =============================================

CREATE DATABASE IF NOT EXISTS AssistenteVirtualDB
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE AssistenteVirtualDB;

-- =============================================
-- Tabela: Conversations
-- =============================================
CREATE TABLE IF NOT EXISTS Conversations (
    ConversationId INT          PRIMARY KEY AUTO_INCREMENT,
    Title          LONGTEXT     NOT NULL,
    Type           VARCHAR(20)  NOT NULL DEFAULT 'chat',   -- 'chat' ou 'invoice'
    CreatedAt      DATETIME     NOT NULL DEFAULT (UTC_TIMESTAMP()),
    UpdatedAt      DATETIME     NOT NULL DEFAULT (UTC_TIMESTAMP()),
    IsArchived     TINYINT(1)   NOT NULL DEFAULT 0,
    ModelUsed      VARCHAR(100),
    TotalMessages  INT          NOT NULL DEFAULT 0
);

-- =============================================
-- Tabela: Messages
-- =============================================
CREATE TABLE IF NOT EXISTS Messages (
    MessageId      BIGINT       PRIMARY KEY AUTO_INCREMENT,
    ConversationId INT          NOT NULL,
    Role           VARCHAR(10)  NOT NULL,
    Content        LONGTEXT     NOT NULL,
    Timestamp      DATETIME     NOT NULL DEFAULT (UTC_TIMESTAMP()),
    Model          VARCHAR(100),
    ResponseTimeMs BIGINT,
    TokensUsed     INT,
    IsEdited       TINYINT(1)   NOT NULL DEFAULT 0,
    EditedAt       DATETIME,

    CONSTRAINT CHK_Messages_Role CHECK (Role IN ('user', 'bot')),
    CONSTRAINT FK_Messages_Conversations
        FOREIGN KEY (ConversationId)
        REFERENCES Conversations(ConversationId)
        ON DELETE CASCADE
);

-- =============================================
-- Tabela: Statistics
-- Linha única (StatisticId = 1) com totais globais
-- =============================================
CREATE TABLE IF NOT EXISTS Statistics (
    StatisticId          INT    PRIMARY KEY AUTO_INCREMENT,
    TotalThinkingTimeMs  BIGINT NOT NULL DEFAULT 0,
    MessagesSentCount    INT    NOT NULL DEFAULT 0,
    TotalConversations   INT    NOT NULL DEFAULT 0,
    CreatedAt            DATETIME NOT NULL DEFAULT (UTC_TIMESTAMP()),
    LastUpdatedAt        DATETIME NOT NULL DEFAULT (UTC_TIMESTAMP())
);

-- =============================================
-- Tabela: Settings
-- Configurações persistentes da aplicação
-- (substitui ficheiros como context_prompt.txt)
-- =============================================
CREATE TABLE IF NOT EXISTS Settings (
    SettingKey   VARCHAR(100) PRIMARY KEY,
    SettingValue LONGTEXT,
    UpdatedAt    DATETIME NOT NULL DEFAULT (UTC_TIMESTAMP())
);

-- =============================================
-- ÍNDICES
-- =============================================
CREATE INDEX IX_Conversations_CreatedAt  ON Conversations(CreatedAt);
CREATE INDEX IX_Conversations_IsArchived ON Conversations(IsArchived);
CREATE INDEX IX_Conversations_Type       ON Conversations(Type);
CREATE INDEX IX_Messages_ConversationId  ON Messages(ConversationId);
CREATE INDEX IX_Messages_Timestamp       ON Messages(Timestamp);

-- =============================================
-- TRIGGERS
-- =============================================
DELIMITER $$

-- Atualiza UpdatedAt automaticamente ao editar uma conversa
CREATE TRIGGER TR_Conversations_UpdateTimestamp
BEFORE UPDATE ON Conversations
FOR EACH ROW
BEGIN
    SET NEW.UpdatedAt = UTC_TIMESTAMP();
END$$

-- Recalcula TotalMessages ao inserir uma mensagem
CREATE TRIGGER TR_Messages_IncrementCount
AFTER INSERT ON Messages
FOR EACH ROW
BEGIN
    UPDATE Conversations
    SET TotalMessages = (
        SELECT COUNT(*) FROM Messages WHERE ConversationId = NEW.ConversationId
    )
    WHERE ConversationId = NEW.ConversationId;
END$$

-- Recalcula TotalMessages ao apagar uma mensagem
CREATE TRIGGER TR_Messages_DecrementCount
AFTER DELETE ON Messages
FOR EACH ROW
BEGIN
    UPDATE Conversations
    SET TotalMessages = (
        SELECT COUNT(*) FROM Messages WHERE ConversationId = OLD.ConversationId
    )
    WHERE ConversationId = OLD.ConversationId;
END$$

-- Atualiza LastUpdatedAt automaticamente ao editar estatísticas
CREATE TRIGGER TR_Statistics_UpdateTimestamp
BEFORE UPDATE ON Statistics
FOR EACH ROW
BEGIN
    SET NEW.LastUpdatedAt = UTC_TIMESTAMP();
END$$

-- Atualiza UpdatedAt em Settings ao editar
CREATE TRIGGER TR_Settings_UpdateTimestamp
BEFORE UPDATE ON Settings
FOR EACH ROW
BEGIN
    SET NEW.UpdatedAt = UTC_TIMESTAMP();
END$$

DELIMITER ;

-- =============================================
-- DADOS INICIAIS
-- =============================================

-- Registo único de estatísticas globais
INSERT INTO Statistics (StatisticId, TotalThinkingTimeMs, MessagesSentCount, TotalConversations)
VALUES (1, 0, 0, 0)
ON DUPLICATE KEY UPDATE StatisticId = StatisticId;

-- Prompt de contexto por omissão (vazio)
INSERT INTO Settings (SettingKey, SettingValue)
VALUES ('context_prompt', '')
ON DUPLICATE KEY UPDATE SettingKey = SettingKey;

-- =============================================
-- VIEWS
-- =============================================

CREATE OR REPLACE VIEW vw_StatisticsSummary AS
SELECT
    s.TotalConversations,
    s.MessagesSentCount,
    s.TotalThinkingTimeMs,
    CAST(s.TotalThinkingTimeMs / 1000.0 AS DECIMAL(10,2)) AS TotalThinkingTimeSeconds,
    CASE
        WHEN s.TotalConversations > 0
            THEN CAST(s.MessagesSentCount AS DECIMAL(10,2)) / s.TotalConversations
        ELSE 0
    END AS AvgMessagesPerConversation,
    s.LastUpdatedAt
FROM Statistics s
WHERE s.StatisticId = 1;

CREATE OR REPLACE VIEW vw_ConversationsWithLastMessage AS
SELECT
    c.ConversationId,
    c.Title,
    c.Type,
    c.CreatedAt,
    c.UpdatedAt,
    c.IsArchived,
    c.ModelUsed,
    c.TotalMessages,
    last_msg.Timestamp      AS LastMessageAt,
    last_msg.Content        AS LastMessageContent,
    last_msg.Role           AS LastMessageRole
FROM Conversations c
LEFT JOIN (
    SELECT ConversationId, Timestamp, Content, Role,
           ROW_NUMBER() OVER (PARTITION BY ConversationId ORDER BY Timestamp DESC) AS rn
    FROM Messages
) AS last_msg ON c.ConversationId = last_msg.ConversationId AND last_msg.rn = 1;

-- =============================================
-- STORED PROCEDURES
-- =============================================
DELIMITER $$

-- Criar nova conversa
CREATE PROCEDURE sp_CreateConversation(
    IN p_Title     LONGTEXT,
    IN p_ModelUsed VARCHAR(100),
    IN p_Type      VARCHAR(20)
)
BEGIN
    INSERT INTO Conversations (Title, ModelUsed, Type)
    VALUES (p_Title, p_ModelUsed, IFNULL(p_Type, 'chat'));

    SELECT LAST_INSERT_ID() AS ConversationId;
END$$

-- Adicionar mensagem a uma conversa
CREATE PROCEDURE sp_AddMessage(
    IN p_ConversationId INT,
    IN p_Role           VARCHAR(10),
    IN p_Content        LONGTEXT,
    IN p_Model          VARCHAR(100),
    IN p_ResponseTimeMs BIGINT,
    IN p_TokensUsed     INT
)
BEGIN
    INSERT INTO Messages (ConversationId, Role, Content, Model, ResponseTimeMs, TokensUsed)
    VALUES (p_ConversationId, p_Role, p_Content, p_Model, p_ResponseTimeMs, p_TokensUsed);
END$$

-- Atualizar estatísticas globais (incremento)
CREATE PROCEDURE sp_UpdateStatistics(
    IN p_ThinkingTimeMs BIGINT,
    IN p_MessagesSent   INT,
    IN p_Conversations  INT
)
BEGIN
    UPDATE Statistics
    SET
        TotalThinkingTimeMs = TotalThinkingTimeMs + IFNULL(p_ThinkingTimeMs, 0),
        MessagesSentCount   = MessagesSentCount   + IFNULL(p_MessagesSent, 0),
        TotalConversations  = TotalConversations  + IFNULL(p_Conversations, 0)
    WHERE StatisticId = 1;
END$$

-- Arquivar (soft-delete) uma conversa
CREATE PROCEDURE sp_ArchiveConversation(
    IN p_ConversationId INT
)
BEGIN
    UPDATE Conversations
    SET IsArchived = 1
    WHERE ConversationId = p_ConversationId;
END$$

-- Ler ou atualizar uma definição (Settings)
CREATE PROCEDURE sp_SetSetting(
    IN p_Key   VARCHAR(100),
    IN p_Value LONGTEXT
)
BEGIN
    INSERT INTO Settings (SettingKey, SettingValue)
    VALUES (p_Key, p_Value)
    ON DUPLICATE KEY UPDATE SettingValue = p_Value;
END$$

DELIMITER ;

SELECT 'Schema MySQL criado com sucesso!' AS Result;
