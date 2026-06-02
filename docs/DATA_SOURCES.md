# Fontes de dados

## Codex

Fonte preferencial: `codex app-server`.

Fluxo esperado:

1. Abrir `codex app-server --listen stdio://`.
2. Enviar `initialize` com um objeto `params`.
3. Chamar `account/read` para conferir conta e plano.
4. Chamar `account/rateLimits/read`.

Resposta esperada pelo schema local do Codex:

- `rateLimits.primary.usedPercent`
- `rateLimits.primary.windowDurationMins`
- `rateLimits.primary.resetsAt`
- `rateLimits.secondary.usedPercent`
- `rateLimits.secondary.windowDurationMins`
- `rateLimits.secondary.resetsAt`
- `rateLimitsByLimitId.codex`, quando disponivel
- `rateLimitReachedType`, quando houver bloqueio

Observacao: o probe do projeto validou `initialize`, `account/read` e `account/rateLimits/read` nesta maquina usando JSON por linha em stdio. As chamadas sem argumentos tambem precisam enviar `params: {}`.

## Claude Code

Fonte preferencial: endpoint OAuth usado pelo proprio Claude Code, conforme
validado contra a implementacao local e comparado com o projeto
`akitaonrails/ai-usagebar`:

```text
GET https://api.anthropic.com/api/oauth/usage
```

A extensao le `~/.claude/.credentials.json` para obter o access token local ja
autenticado, o prazo de expiracao e o refresh token mantido pelo Claude Code.
Quando o access token esta expirado ou perto de expirar, a extensao pode renovar
o OAuth pelo endpoint do Claude Code e atualizar somente esse mesmo arquivo de
credenciais. Ela nao cria armazenamento proprio de tokens, nao grava tokens no
cache do plugin e nao registra headers ou payloads brutos de erro.

Refresh OAuth:

- `POST https://platform.claude.com/v1/oauth/token`
- `grant_type=refresh_token`
- `client_id=9d1c250a-e61b-44d9-88ed-5944d1962f5e`
- `anthropic-beta=oauth-2025-04-20`
- lock local antes de atualizar `~/.claude/.credentials.json`
- escrita atomica preservando campos desconhecidos

Credencial ausente, refresh token ausente, refresh token revogado ou erro de
assinatura continua virando `unavailable` com mensagem curta para rodar
`claude auth login`.

Campos esperados:

- `five_hour.utilization`
- `five_hour.resets_at`
- `seven_day.utilization`
- `seven_day.resets_at`
- `anthropic-ratelimit-unified-5h-utilization`
- `anthropic-ratelimit-unified-5h-reset`
- `anthropic-ratelimit-unified-7d-utilization`
- `anthropic-ratelimit-unified-7d-reset`
- `seven_day_sonnet.utilization`
- `seven_day_opus.utilization`, quando disponivel
- `extra_usage`

Os headers unificados seguem o comportamento observado no codigo local do
Claude Code: `utilization` vem como fracao `0..1` e `reset` vem como Unix epoch
em segundos. A extensao captura apenas headers com prefixo
`anthropic-ratelimit-unified-` e normaliza para percentual `0..100`.

Fonte secundaria implementada para parsing: JSON estilo statusline com
`rate_limits`. Ela permite reaproveitar uma fonte local futura sem mudar o
modelo normalizado.

Campos de statusline conhecidos:

- `rate_limits.five_hour.used_percentage`
- `rate_limits.five_hour.resets_at`
- `rate_limits.seven_day.used_percentage`
- `rate_limits.seven_day.resets_at`

## OpenAI / ChatGPT fallback futuro

O projeto `akitaonrails/ai-usagebar` usa tambem o endpoint
`https://chatgpt.com/backend-api/wham/usage`. Esse endpoint fica registrado
apenas como pesquisa para fallback futuro.

Regra atual:

- Fonte primaria de Codex/OpenAI continua sendo `codex app-server`.
- Nao ler nem persistir tokens de `~/.codex/auth.json` para implementar esse
  fallback nesta fatia.
- Qualquer uso futuro de `wham/usage` precisa de sanitizacao
  de payloads e justificativa para nao usar apenas o app-server local.

## Smoke tests ao vivo

Smoke tests contra provedores reais devem ser opt-in por flag explicita, como
`--live-codex` e `--live-claude`.

Saida permitida:

- status normalizado
- plano/rotulo nao sensivel
- nomes das janelas
- percentuais
- reset timestamps
- mensagens curtas sanitizadas

Saida proibida:

- access tokens
- refresh tokens
- auth headers
- payload HTTP bruto
- conteudo bruto de arquivos de credenciais
- identificadores de conta quando nao forem necessarios para validar limites

## Regra de produto

O plugin deve mostrar:

- "5h" para janela de 5 horas.
- "7d" para janela semanal de 7 dias.

Nao tratar o limite semanal como "7 horas".
