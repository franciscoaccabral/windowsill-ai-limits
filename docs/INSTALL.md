# Instalacao de dependencias

Este projeto precisa de ferramentas para duas partes:

1. Desenvolver a extensao WindowSill em .NET/C#.
2. Consultar as fontes locais Codex e Claude Code.

## 1. Verificar o que ja existe

```powershell
dotnet --info
codex --version
claude --version
node --version
npm --version
git --version
```

Na verificacao inicial desta maquina, o runtime .NET 8 existia, mas o SDK nao.

## 2. Instalar .NET SDK

Opcao via winget:

```powershell
winget install Microsoft.DotNet.SDK.8
```

Se quiser usar SDK mais novo e o WindowSill aceitar, tambem pode instalar:

```powershell
winget install Microsoft.DotNet.SDK.9
```

Depois feche e abra o terminal e confirme:

```powershell
dotnet --list-sdks
dotnet --info
```

## 3. Instalar template WindowSill

Depois que o SDK estiver instalado:

```powershell
dotnet new install WindowSill.Extension.Template
dotnet new list windowsill
```

Se o nome do template mudar, consultar a documentacao oficial:

```text
https://getwindowsill.app/extensions
```

## 4. Criar o projeto da extensao

Quando o template estiver disponivel:

```powershell
cd .\src
dotnet new windowsill-ext --name WindowSillAiLimits
```

Depois:

```powershell
dotnet build
```

## 5. Dependencias de consulta

Codex:

```powershell
codex --version
codex app-server --help
```

Claude:

```powershell
claude --version
```

Opcional para comparacao local de logs:

```powershell
npm install -g ccusage
ccusage --help
```

O plugin nao deve armazenar tokens. Ele deve chamar as ferramentas locais ja autenticadas ou endpoints locais documentados.
