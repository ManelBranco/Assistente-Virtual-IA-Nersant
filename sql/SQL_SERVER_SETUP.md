# Guia de Instalação - SQL Server para Assistente Virtual

Este guia explica como instalar e configurar o SQL Server para usar com o projeto Assistente Virtual IA Nersant.

## 🎯 Opções de Instalação

### Opção 1: SQL Server Express (Recomendado para Desenvolvimento)

#### Download
1. Acesse: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
2. Baixe "SQL Server 2022 Express"
3. Execute o instalador

#### Instalação
- **Tipo de Instalação**: Express
- **Instância**: SQLEXPRESS (padrão)
- **Autenticação**: Modo Misto (SQL Server + Windows)
- **Senha do SA**: Defina uma senha segura

### Opção 2: SQL Server Developer (Grátis para Desenvolvimento)

#### Download
1. Acesse: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
2. Baixe "SQL Server 2022 Developer"
3. Execute o instalador

#### Instalação
- **Tipo de Instalação**: Default
- **Instância**: MSSQLSERVER (padrão)
- **Autenticação**: Modo Misto
- **Recursos**: Database Engine Services

### Opção 3: SQL Server LocalDB (Leve)

#### Instalação via Chocolatey
```bash
choco install sql-server-localdb
```

#### Ou via Visual Studio Installer
- Instalar "SQL Server Express LocalDB" através do Visual Studio Installer

## 🛠️ Ferramentas de Gestão

### SQL Server Management Studio (SSMS)
1. Download: https://docs.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms
2. Instalar e conectar ao servidor

### Azure Data Studio (Alternativa Moderna)
1. Download: https://docs.microsoft.com/en-us/sql/azure-data-studio/download-azure-data-studio
2. Mais leve e moderno que SSMS

## 🔧 Configuração da Base de Dados

### 1. Conectar ao SQL Server

**String de Conexão para Desenvolvimento:**
```
Server=localhost\SQLEXPRESS;Database=AssistenteVirtualDB;Trusted_Connection=True;TrustServerCertificate=True;
```

**Para SQL Server LocalDB:**
```
Server=(localdb)\MSSQLLocalDB;Database=AssistenteVirtualDB;Trusted_Connection=True;
```

### 2. Criar Base de Dados

Execute no SSMS/Azure Data Studio:

```sql
CREATE DATABASE AssistenteVirtualDB;
GO

USE AssistenteVirtualDB;
GO
```

### 3. Executar Schema

1. Abrir `database_schema.sql`
2. Executar todo o script
3. Verificar se todas as tabelas foram criadas

### 4. Migrar Dados

1. Abrir `migration_script.sql`
2. Executar o script de migração
3. Verificar se os dados foram migrados corretamente

## 🧪 Teste da Instalação

### Verificar Conexão
```sql
SELECT @@VERSION;
```

### Verificar Tabelas
```sql
USE AssistenteVirtualDB;
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';
```

### Verificar Dados
```sql
SELECT * FROM vw_StatisticsSummary;
SELECT * FROM vw_ConversationsWithLastMessage;
```

## 🔒 Segurança

### Para Produção
- Usar autenticação SQL Server em vez de Windows
- Criar usuário específico para a aplicação
- Configurar firewall
- Habilitar criptografia SSL/TLS

### Usuário da Aplicação
```sql
USE master;
CREATE LOGIN AssistenteUser WITH PASSWORD = 'SuaSenhaSegura123!';
CREATE USER AssistenteUser FOR LOGIN AssistenteUser;
GRANT CONNECT TO AssistenteUser;

USE AssistenteVirtualDB;
CREATE USER AssistenteUser FOR LOGIN AssistenteUser;
ALTER ROLE db_datareader ADD MEMBER AssistenteUser;
ALTER ROLE db_datawriter ADD MEMBER AssistenteUser;
```

**String de Conexão:**
```
Server=localhost\SQLEXPRESS;Database=AssistenteVirtualDB;User Id=AssistenteUser;Password=SuaSenhaSegura123!;
```

## 🚀 Integração com o Projeto

### 1. Adicionar Pacotes NuGet
```bash
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools
```

### 2. Atualizar Program.cs

Substitua a criação do DataStore:

```csharp
// Antes (JSON)
var store = new DataStore(conversationsPath, statsPath, jsonOptions);

// Depois (SQL)
var store = new DataStore(); // Usará a string de conexão do DbContext
```

### 3. Configurar Connection String

Para produção, use `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=AssistenteVirtualDB;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

E atualize o `AssistenteDbContext`:

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder options)
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
}
```

## 🔍 Troubleshooting

### Erro: "Cannot open database requested by the login"
- Verificar se a base de dados existe
- Verificar permissões do usuário

### Erro: "Login failed for user"
- Verificar credenciais
- Verificar se autenticação SQL está habilitada

### Erro: "A network-related or instance-specific error"
- Verificar se o SQL Server está rodando
- Verificar nome da instância
- Verificar firewall

### Verificar Serviços
```bash
# Windows Services
services.msc
# Procurar por "SQL Server (SQLEXPRESS)"
```

### Logs de Erro
- **SQL Server Logs**: `C:\Program Files\Microsoft SQL Server\MSSQL*\MSSQL\Log\ERRORLOG`
- **Event Viewer**: Windows Logs > Application

## 📊 Monitoramento

### Queries Úteis
```sql
-- Ver conexões ativas
SELECT * FROM sys.dm_exec_connections;

-- Ver locks
SELECT * FROM sys.dm_exec_requests WHERE blocking_session_id <> 0;

-- Ver espaço usado
EXEC sp_spaceused;
```

## 📚 Recursos Adicionais

- [Documentação SQL Server](https://docs.microsoft.com/en-us/sql/sql-server/)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [SQL Server Best Practices](https://docs.microsoft.com/en-us/sql/sql-server/sql-server-best-practices-checklist)

---

**Nota**: Para ambientes de produção, considere usar Azure SQL Database ou AWS RDS para maior escalabilidade e manutenção reduzida.