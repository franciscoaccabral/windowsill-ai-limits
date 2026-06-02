using System.Diagnostics;
using System.IO;
using System.Text.Json;

using WindowSillAiLimits.Services;

namespace WindowSillAiLimits.Services.Codex;

public sealed class CodexAppServerClient(string commandPath, TimeSpan requestTimeout) : ICodexAppServerClient, IDisposable
{
    private readonly string _commandPath = string.IsNullOrWhiteSpace(commandPath) ? "codex" : commandPath;
    private readonly TimeSpan _requestTimeout = requestTimeout;
    private Process? _process;
    private int _nextId;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<string> SendRequestAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_requestTimeout);

            var process = EnsureProcess();
            var requestId = Interlocked.Increment(ref _nextId);
            var request = new
            {
                jsonrpc = "2.0",
                id = requestId,
                method,
                @params = parameters,
            };

            var requestJson = JsonSerializer.Serialize(request);
            await process.StandardInput.WriteLineAsync(requestJson.AsMemory(), timeout.Token);
            await process.StandardInput.FlushAsync(timeout.Token);

            while (true)
            {
                var response = await process.StandardOutput.ReadLineAsync(timeout.Token);
                if (string.IsNullOrWhiteSpace(response))
                {
                    var details = TryReadProcessError();
                    ResetProcess();
                    throw new CodexAppServerException(
                        string.IsNullOrWhiteSpace(details)
                            ? "Codex app-server closed stdout before returning a response."
                            : $"Codex app-server closed stdout before returning a response: {details}");
                }

                if (IsResponseForRequest(response, requestId))
                {
                    return response;
                }

                // Notifications do not carry the request id; keep reading until the matching response.
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ResetProcess();
            throw new TimeoutException("Codex app-server request timed out.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            ResetProcess();
            throw new CodexAppServerException($"Codex command not found or could not start: {_commandPath}", ex, commandMissing: true);
        }
        catch (IOException ex)
        {
            var details = TryReadProcessError();
            ResetProcess();
            throw new CodexAppServerException(
                string.IsNullOrWhiteSpace(details)
                    ? "Codex app-server pipe closed unexpectedly."
                    : $"Codex app-server pipe closed unexpectedly: {details}",
                ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        // Encerra o processo filho `codex app-server` para nao deixar processos orfaos
        // quando a sill e desativada/descartada.
        _gate.Wait();
        try
        {
            ResetProcess();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private Process EnsureProcess()
    {
        if (_process is { HasExited: false })
        {
            return _process;
        }

        ResetProcess();

        var startInfo = CommandStartInfoFactory.Create(_commandPath, ["app-server", "--listen", "stdio://"]);

        _process = Process.Start(startInfo) ?? throw new CodexAppServerException("Codex app-server process did not start.");
        return _process;
    }

    private void ResetProcess()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    private string? TryReadProcessError()
    {
        try
        {
            if (_process is { HasExited: true })
            {
                var error = _process.StandardError.ReadToEnd();
                return error.Length <= 240 ? error : error[..240];
            }
        }
        catch (InvalidOperationException)
        {
        }

        return null;
    }

    private static bool IsResponseForRequest(string line, int requestId)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.TryGetProperty("id", out var id) &&
                   id.ValueKind == JsonValueKind.Number &&
                   id.TryGetInt32(out var responseId) &&
                   responseId == requestId;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
