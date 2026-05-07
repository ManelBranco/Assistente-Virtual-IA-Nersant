# 🧪 Guia de Testes - Migração SQL

Este documento fornece um plano de testes completo para validar a migração da base de dados JSON para SQL.

## 🎯 Objetivos dos Testes

- Validar que todos os dados foram migrados corretamente
- Garantir que todas as funcionalidades continuam funcionando
- Verificar performance e estabilidade
- Testar cenários de erro e recuperação

## 📋 Pré-requisitos

1. SQL Server instalado e configurado
2. Base de dados `AssistenteVirtualDB` criada
3. Scripts `database_schema.sql` e `migration_script.sql` executados
4. Código C# atualizado para usar SQL
5. Aplicação compilada com sucesso

## 🧪 Plano de Testes

### Teste 1: Validação da Migração de Dados

#### Objetivo
Verificar se os dados JSON foram migrados corretamente para SQL.

#### Passos
1. Verificar contagem de conversas:
   ```sql
   SELECT COUNT(*) FROM Conversations WHERE IsArchived = 0;
   -- Deve retornar 2 (conversas existentes)
   ```

2. Verificar contagem de mensagens:
   ```sql
   SELECT COUNT(*) FROM Messages;
   -- Deve retornar 4 (mensagens existentes)
   ```

3. Verificar estatísticas:
   ```sql
   SELECT * FROM vw_StatisticsSummary;
   -- Deve mostrar TotalConversations = 1
   ```

4. Verificar estrutura das conversas:
   ```sql
   SELECT c.ConversationId, c.Title, COUNT(m.MessageId) as MessageCount
   FROM Conversations c
   LEFT JOIN Messages m ON c.ConversationId = m.ConversationId
   GROUP BY c.ConversationId, c.Title;
   ```

#### Resultado Esperado
- 2 conversas ativas
- 4 mensagens no total
- Estatísticas corretas

### Teste 2: Funcionalidades da Aplicação

#### Objetivo
Testar todas as funcionalidades da UI para garantir compatibilidade.

#### Cenários de Teste

1. **Carregar Histórico**
   - Abrir aplicação
   - Verificar se conversas aparecem na sidebar
   - Clicar em uma conversa existente
   - Verificar se mensagens carregam corretamente

2. **Criar Nova Conversa**
   - Clicar "Nova conversa"
   - Verificar se nova conversa é criada
   - Enviar mensagem
   - Verificar se título é gerado automaticamente

3. **Enviar Mensagem**
   - Digitar mensagem
   - Selecionar modelo
   - Enviar
   - Verificar resposta da IA
   - Verificar tempo de resposta

4. **Estatísticas**
   - Abrir modal de estatísticas
   - Verificar valores
   - Fechar modal

5. **Deletar Conversa**
   - Selecionar conversa
   - Deletar
   - Verificar se desaparece da lista
   - Verificar se foi arquivada (soft delete)

6. **Limpar Histórico**
   - Clicar "Remover histórico"
   - Confirmar
   - Verificar se todas as conversas foram arquivadas

#### Resultado Esperado
- Todas as funcionalidades funcionam normalmente
- Dados são salvos no SQL
- UI responde corretamente

### Teste 3: Performance

#### Objetivo
Verificar que a performance não regrediu com SQL.

#### Métricas
- Tempo de carregamento da página
- Tempo de resposta das APIs
- Tempo de salvamento de mensagens

#### Testes
1. **Carregamento Inicial**
   - Medir tempo para carregar histórico
   - Comparar com versão JSON

2. **Envio de Mensagens**
   - Medir tempo de resposta da IA
   - Medir tempo de salvamento no banco

3. **Consulta de Estatísticas**
   - Medir tempo de carregamento do modal

#### Resultado Esperado
- Performance similar ou melhor que JSON
- Sem delays perceptíveis

### Teste 4: Concorrência

#### Objetivo
Testar acesso simultâneo à aplicação.

#### Cenários
1. **Múltiplas Abas**
   - Abrir aplicação em 2 abas
   - Criar conversa em uma aba
   - Verificar se aparece na outra aba após refresh

2. **Múltiplas Instâncias**
   - Se possível, rodar 2 instâncias da aplicação
   - Testar isolamento de dados

#### Resultado Esperado
- Dados consistentes entre sessões
- Sem conflitos de concorrência

### Teste 5: Recuperação de Erros

#### Objetivo
Testar comportamento em cenários de erro.

#### Cenários
1. **Erro de Conexão SQL**
   - Desligar SQL Server
   - Tentar usar aplicação
   - Verificar tratamento de erro

2. **Dados Corrompidos**
   - Simular dados inválidos
   - Verificar logs de erro

3. **Timeout de Operação**
   - Configurar timeout curto
   - Testar operações longas

#### Resultado Esperado
- Erros tratados graciosamente
- Logs adequados
- Aplicação não quebra

## 🔍 Validações Técnicas

### Queries de Verificação

```sql
-- Verificar integridade referencial
SELECT c.ConversationId, COUNT(m.MessageId) as Messages
FROM Conversations c
LEFT JOIN Messages m ON c.ConversationId = m.ConversationId
GROUP BY c.ConversationId
ORDER BY c.ConversationId;

-- Verificar logs de auditoria
SELECT EventType, COUNT(*) as Count
FROM ConversationLogs
GROUP BY EventType;

-- Verificar estatísticas em tempo real
SELECT
    (SELECT COUNT(*) FROM Conversations WHERE IsArchived = 0) as ActiveConversations,
    (SELECT COUNT(*) FROM Messages) as TotalMessages,
    (SELECT TotalConversations FROM Statistics WHERE StatisticId = 1) as StatsConversations;

-- Verificar performance de queries
SET STATISTICS TIME ON;
SELECT * FROM vw_ConversationsWithLastMessage;
SET STATISTICS TIME OFF;
```

### Logs da Aplicação

Verificar logs do ASP.NET Core:
- Conexões com SQL Server
- Erros de Entity Framework
- Warnings de performance

### Monitoramento do SQL Server

```sql
-- Verificar conexões ativas
SELECT * FROM sys.dm_exec_connections WHERE database_id = DB_ID('AssistenteVirtualDB');

-- Verificar locks
SELECT * FROM sys.dm_exec_requests WHERE database_id = DB_ID('AssistenteVirtualDB');

-- Verificar uso de espaço
EXEC sp_spaceused 'Conversations';
EXEC sp_spaceused 'Messages';
```

## 📊 Relatório de Testes

### Template de Relatório

```
DATA: ________
TESTADOR: ________
VERSÃO: ________

TESTE 1 - MIGRAÇÃO DE DADOS: [PASS/FAIL]
- Contagem conversas: ____ / ____
- Contagem mensagens: ____ / ____
- Estatísticas corretas: [SIM/NÃO]
- Observações: ________

TESTE 2 - FUNCIONALIDADES: [PASS/FAIL]
- Carregar histórico: [OK/ERRO]
- Criar conversa: [OK/ERRO]
- Enviar mensagem: [OK/ERRO]
- Estatísticas: [OK/ERRO]
- Deletar conversa: [OK/ERRO]
- Limpar histórico: [OK/ERRO]

TESTE 3 - PERFORMANCE: [PASS/FAIL]
- Tempo carregamento: ____ ms
- Tempo resposta IA: ____ ms
- Tempo salvamento: ____ ms

TESTE 4 - CONCORRÊNCIA: [PASS/FAIL]
- Múltiplas abas: [OK/ERRO]
- Isolamento: [OK/ERRO]

TESTE 5 - ERROS: [PASS/FAIL]
- Conexão SQL: [OK/ERRO]
- Dados inválidos: [OK/ERRO]
- Timeout: [OK/ERRO]

CONCLUSÃO: [APROVADO/REPROVADO]
OBSERVAÇÕES GERAIS: ________
```

## 🚨 Plano de Rollback

Se testes falharem, seguir estes passos para voltar à versão JSON:

1. **Backup dos Dados SQL** (se necessário)
2. **Restaurar arquivos JSON originais**
3. **Reverter código C#** para usar DataStore JSON
4. **Remover dependências SQL** do projeto
5. **Testar funcionalidade** com JSON

## ✅ Critérios de Aprovação

- [ ] Todos os dados migrados corretamente
- [ ] Todas as funcionalidades funcionando
- [ ] Performance adequada
- [ ] Sem erros críticos
- [ ] Logs funcionando
- [ ] Concorrência segura

---

**Nota**: Documentar qualquer problema encontrado e suas soluções para referência futura.