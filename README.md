# Assistente-Virtual-IA-Nersant

Este projeto agora inclui um backend em C# para correr no IIS ou como aplicação ASP.NET Core. A interface atual em `public/` mantém todas as funcionalidades existentes.

## Como executar com .NET

1. Instalar o SDK .NET 8
2. Abrir uma linha de comandos na pasta do projeto
3. Executar:

```bash
dotnet run --project AssistenteVirtualIA.csproj
```

4. Aceder a `http://localhost:5000`

> Se a porta `5000` estiver ocupada, podes usar outra porta com:
>
> ```bash
> dotnet run --project AssistenteVirtualIA.csproj --urls "http://127.0.0.1:5001"
> ```

## Publicação para IIS

1. Publicar o projeto:

```bash
dotnet publish AssistenteVirtualIA.csproj -c Release -o publish
```

2. Configurar o site no IIS para apontar para a pasta `publish` (não para `publish/public`).
3. Definir o binding do site para `ia.localhost` se esse for o nome que queres usar. O site pode manter esse nome no IIS, desde que tenhas o `hosts` configurado para apontar `ia.localhost` para `127.0.0.1`.
4. Garantir que o `web.config` está presente e que o ASP.NET Core Hosting Bundle está instalado.

> Se não estiver instalado, o IIS não conhece o módulo `AspNetCoreModuleV2` usado pelo `web.config` e dá erro 500.19.
>
> Antes de publicar de novo, apaga a pasta `publish` antiga para evitar cópias internas recursivas.
