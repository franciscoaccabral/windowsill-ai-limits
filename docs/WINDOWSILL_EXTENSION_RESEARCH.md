# WindowSill extension research

Pesquisa feita em 2026-05-24 para guiar a implementacao do plugin AI Limits.

## Fontes oficiais consultadas

- WindowSill docs, Setup: https://getwindowsill.app/doc/articles/extension-development/getting-started/setup.html
- WindowSill docs, How to create a sill: https://getwindowsill.app/doc/articles/extension-development/getting-started/how-to-create-a-sill.html
- WindowSill docs, Creating a custom view sill: https://getwindowsill.app/doc/articles/extension-development/tutorials/creating-a-custom-view-sill.html
- WindowSill docs, Samples: https://getwindowsill.app/doc/articles/extension-development/getting-started/samples.html
- WindowSill docs, Use settings: https://getwindowsill.app/doc/articles/extension-development/guidelines/use-settings.html
- WindowSill docs, Plugin data storage: https://getwindowsill.app/doc/articles/extension-development/guidelines/plugin-data-storage.html
- WindowSill docs, Publish: https://getwindowsill.app/doc/articles/extension-development/getting-started/publish.html
- Official extensions repo: https://github.com/WindowSill-app/Extensions
- Perf Counter source: https://github.com/WindowSill-app/Extensions/tree/main/src/WindowSill.PerfCounter
- Media Control source: https://github.com/WindowSill-app/Extensions/tree/main/src/WindowSill.MediaControl
- Date source: https://github.com/WindowSill-app/Extensions/tree/main/src/WindowSill.Date

## Resultado principal

O nosso plugin deve seguir o padrao da extensao oficial `WindowSill.PerfCounter`, nao o sample inicial do template.

Motivo: `PerfCounter` e uma sill sempre visivel, com uma visualizacao customizada e dados atualizados periodicamente. Isso e o mesmo formato que precisamos para mostrar:

- OpenAI/Codex: `5h` e `7d`.
- Anthropic/Claude: `5h` e `7d`.
- Detalhes de pacing e reset em painel expandido.

## Modelo WindowSill recomendado

### Sill

Usar:

```csharp
[Export(typeof(ISill))]
[Name("AI Limits")]
[Priority(Priority.Lowest)]
[SupportMultipleMonitors]
public sealed class AiLimitsSill : ISillActivatedByDefault, ISillSingleView
```

Por que:

- `ISillActivatedByDefault`: a sill fica disponivel assim que o WindowSill inicia, sem depender de Notepad, selecao de texto ou drag-and-drop.
- `ISillSingleView`: a barra compacta precisa de layout proprio, com varios textos pequenos e cores por provider/limite.
- `SupportMultipleMonitors`: replica o comportamento das extensoes oficiais para monitores multiplos.

O sample atual do template usa `ISillActivatedByProcess` e um `NotepadProcessActivator`; isso deve ser removido quando iniciarmos a implementacao real.

### View compacta

Criar um `SillView` com `Content = new AiLimitsBarView(...)`, seguindo o padrao do `PerformanceCounterSill`.

Na barra, mostrar:

```text
OpenAI 5h 100% 7d 59% | Anthropic 5h 70% 7d 50%
```

Fallbacks por tamanho:

- `HorizontalLarge`: icone/glyph + nome + `5h` + `7d`.
- `HorizontalMedium`: glyph + `5h` + `7d`, nomes opcionais.
- `HorizontalSmall`: glyph + dois percentuais, sem nomes.
- Vertical: priorizar glyph e pior percentual; detalhes ficam no flyout/popup.

Usar `SillOrientationAndSize` e `VisualStateManager.GoToState`, com fallback manual como no `MediaControlView`, porque a propria extensao oficial registra que VisualStates podem falhar em DLL dinamica.

### Painel expandido

Ha dois mecanismos uteis:

1. `PreviewFlyoutContent`: aparece no hover. Bom para resumo detalhado rapido.
2. `SillPopup`: abre ao clicar na sill. Melhor para painel completo com refresh manual, estados e configuracoes leves.

Recomendacao para o MVP:

- Hover: mostrar resumo compacto detalhado, incluindo ultimo refresh e proximos resets.
- Clique: abrir `AiLimitsPopupContent` com o painel completo aprovado no mockup.

Esse caminho copia o padrao do `PerfCounter`, que usa `SillPopup` para o painel detalhado, e do tutorial oficial, que mostra `PreviewFlyoutContent` para detalhes no hover.

## Servicos e atualizacao

Copiar o desenho de `PerformanceMonitorService`:

```text
UsageRefreshService
  Timer
  StartMonitoring()
  StopMonitoring()
  UsageUpdated event
  evita callbacks sobrepostos
```

Detalhes importantes:

- Usar contador de assinantes/ativacoes para nao rodar refresh se a sill estiver desativada.
- Evitar refresh sobreposto com `Interlocked.CompareExchange`.
- Enviar atualizacoes para a UI com `ThreadHelper.RunOnUIThreadAsync`.
- Guardar apenas o ultimo snapshot normalizado em memoria.
- Cache persistente opcional em `IPluginInfo.GetPluginDataDirectory()`, nao em settings.

Intervalo recomendado:

- Inicial: 300 segundos.
- Minimo configuravel: 30 segundos.
- Backoff em erro: 2 a 5 minutos.

## ViewModel recomendado

```text
AiLimitsViewModel : ObservableObject, IDisposable
  Snapshot
  OpenAiFiveHourText
  OpenAiSevenDayText
  ClaudeFiveHourText
  ClaudeSevenDayText
  WorstStatus
  LastUpdatedText
  IsStale
  RefreshCommand
```

ViewModels devem assinar eventos do service e cancelar no `Dispose`, como o `PerformanceCounterViewModel`.

## Settings

Usar `ISettingsProvider` e `SettingDefinition<T>` para valores pequenos:

- `RefreshIntervalSeconds`
- `CodexCommandPath`
- `ClaudeCommandPath`
- `ShowProviderNamesInBar`
- `ShowPreviewFlyout`

Nao usar settings para payloads grandes. A doc limita settings a 8 KB por valor e 64 KB no total do WindowSill. Para cache historico/snapshot, usar `IPluginInfo`.

## Empacotamento e instalacao local

O caminho oficial e:

1. `dotnet build -c Release`
2. Gerar `.nupkg`
3. Renomear `.nupkg` para `.wsext`
4. Dar duplo clique no `.wsext` para instalar no WindowSill

Cuidados:

- Nao ativar trimming nem Native AOT; as extensoes sao descobertas por MEF/reflection.
- Manter `PackageId`, `Version`, `Title`, `Description`, `PackageIcon`, `PackageLicenseFile` no `.csproj`.
- Incluir `CHANGELOGS.md`, `LICENSE.md`, assets e screenshots no pacote.
- Para a barra compacta, usar assets SVG empacotados para as marcas dos providers
  (`Assets/openai-mark.svg` e `Assets/anthropic-mark.svg`) em vez de glyphs de
  texto ou formas desenhadas manualmente.

### Regra para manutencao de icones

Sempre que uma marca/icone de provider for alterada, atualizar e validar todos
os pontos que consomem o mesmo asset:

- Asset fonte em `src/WindowSillAiLimits/Assets/*.svg`.
- Barra compacta (`AiLimitsBarView`) e hover/preview (`AiLimitsPreviewContent`)
  devem continuar lendo o mesmo SVG empacotado, sem copias visuais divergentes.
- Se o nome ou caminho do arquivo mudar, atualizar o `.csproj`,
  `scripts/validate-package.ps1` e os testes que verificam assets empacotados.
- Se houver mockup/preview local em `artifacts/mockups/`, atualizar esse preview
  antes de promover o asset real.
- Rodar validacao local, inspecionar o `.wsext`, reinstalar a extensao e
  reiniciar o WindowSill antes de considerar o ajuste visual concluido.

## Exemplos oficiais relevantes

### `WindowSill.PerfCounter`

Usar como base principal.

Padroes encontrados:

- `PerformanceCounterSill : ISillActivatedByDefault, ISillSingleView`
- injecao MEF via `[ImportingConstructor]`
- `SillView.Content = new PerformanceCounterView(...)`
- `SillPopup` no clique para detalhe
- service singleton-like com `Timer`
- update de UI via `ThreadHelper.RunOnUIThreadAsync`
- settings por `ISettingsProvider`
- icone carregado com `IPluginInfo.GetPluginContentDirectory()`

### `WindowSill.MediaControl`

Usar como referencia para layout responsivo.

Padroes encontrados:

- `SillOrientationAndSize`
- `ShouldAppearInSill`
- `PreviewFlyoutContent`
- fallback manual quando `VisualStateManager.GoToState` nao aplica estado

### `WindowSill.Date`

Usar como referencia se precisarmos misturar itens na barra, popups e adaptadores dinamicos.

Padroes encontrados:

- `ISillActivatedByDefault`
- `ISillListView`
- multiplas settings pages
- view list dinamica
- refresh quando popup fecha

Para o nosso caso, `Date` e inspiracao secundaria. A barra do AI Limits e mais adequada como `ISillSingleView`.

## Decisoes para o nosso projeto

- Substituir `MySill.cs` por `AiLimitsSill.cs`.
- Remover `NotepadProcessActivator.cs` quando a sill real entrar.
- Criar:
  - `Views/AiLimitsBarView.xaml`
  - `Views/AiLimitsPopupContent.xaml`
  - `ViewModels/AiLimitsViewModel.cs`
  - `Services/UsageRefreshService.cs`
  - `Services/CodexUsageProbe.cs`
  - `Services/ClaudeUsageProbe.cs`
  - `Models/UsageSnapshot.cs`
  - `Settings/Settings.cs`
  - `Settings/SettingsView.xaml`
- Manter o contrato de dados ja definido para o produto.
- Comecar com dados mockados no `UsageRefreshService`, validar UI no WindowSill, depois plugar Codex e Claude.

## Riscos conhecidos

- APIs de consumo do Codex e Claude podem mudar; os probes devem falhar de forma isolada e manter a UI funcional.
- O Claude pode exigir reutilizacao cuidadosa de auth local; nao persistir tokens.
- A UI da barra precisa se adaptar bem a `HorizontalSmall`; nao depender de texto longo.
- Se `SillPopup` ficar grande demais, a popup deve ter largura fixa e conteudo rolavel.
- O build Release deve preservar MEF/reflection.

## Proximo passo recomendado

Implementar primeiro uma vertical slice somente com mock data:

1. `AiLimitsSill` sempre ativa.
2. Barra compacta real no WindowSill.
3. Popup detalhado no clique.
4. Timer atualizando mock data.
5. Build `.wsext` e instalacao local.

Depois disso, trocar o mock pelo `CodexUsageProbe` e `ClaudeUsageProbe` com muito menos risco visual.
