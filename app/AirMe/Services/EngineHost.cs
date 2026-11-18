using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AirMe.Services;

public enum EngineState
{
    Stopped,
    Waiting,     // visible en la red, esperando al iPhone
    Streaming,   // ventana de video abierta
    Failed,      // el motor no arranca
}

/// <summary>
/// Arranca y vigila el motor (uxplay.exe) en segundo plano, sin consola.
/// El estado "transmitiendo" se detecta por la ventana de video que abre el motor.
/// </summary>
public sealed partial class EngineHost : IDisposable
{
    private Process? _process;
    private readonly System.Threading.Timer _watch;
    private bool _wantRunning;
    private int _quickFailures;
    private DateTime _startedAt;
    private AppSettings _settings = new();
    private StreamWriter? _log;
    private readonly object _gate = new();

    public EngineState State { get; private set; } = EngineState.Stopped;
    public string? DeviceName { get; private set; }
    public string? EnginePath { get; }
    public event Action? Changed;

    [GeneratedRegex(@"connection request from (.+?) \((.*?)\) with deviceID", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionRequest();

    public EngineHost()
    {
        EnginePath = LocateEngine();
        _watch = new System.Threading.Timer(_ => Poll(), null, 1000, 1000);
    }

    /// <summary>Busca engine\bin\uxplay.exe junto a AirMe.exe (o en dist\ durante el desarrollo).</summary>
    private static string? LocateEngine()
    {
        string baseDir = AppContext.BaseDirectory;
        string packaged = Path.Combine(baseDir, "engine", "bin", "uxplay.exe");
        if (File.Exists(packaged)) return packaged;

        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            foreach (var candidate in new[]
            {
                Path.Combine(dir.FullName, "dist", "AirMe", "engine", "bin", "uxplay.exe"),
                Path.Combine(dir.FullName, "dist", "AirMe", "bin", "uxplay.exe"),
            })
            {
                if (File.Exists(candidate)) return candidate;
            }
            dir = dir.Parent;
        }
        return null;
    }

    public void Start(AppSettings settings)
    {
        lock (_gate)
        {
            _settings = settings.Clone();
            _wantRunning = true;
            _quickFailures = 0;
            StartProcess();
        }
    }

    public void Restart(AppSettings settings)
    {
        lock (_gate)
        {
            StopProcess();
            _settings = settings.Clone();
            _wantRunning = true;
            _quickFailures = 0;
            StartProcess();
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _wantRunning = false;
            StopProcess();
            SetState(EngineState.Stopped);
        }
    }

    private void StartProcess()
    {
        if (EnginePath == null)
        {
            SetState(EngineState.Failed);
            return;
        }

        string binDir = Path.GetDirectoryName(EnginePath)!;
        string engineRoot = Path.GetDirectoryName(binDir)!;
        string plugins = Path.Combine(engineRoot, "lib", "gstreamer-1.0");
        Directory.CreateDirectory(AppSettings.Folder);

        var psi = new ProcessStartInfo(EnginePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = binDir,
        };
        psi.Environment["GST_PLUGIN_PATH"] = plugins;
        psi.Environment["GST_PLUGIN_SYSTEM_PATH"] = plugins;
        psi.Environment["GST_PLUGIN_SCANNER"] = Path.Combine(binDir, "gst-plugin-scanner.exe");
        psi.Environment["GST_REGISTRY"] = Path.Combine(AppSettings.Folder, "gst-registry.bin");
        // Sin archivo de opciones externo: todo lo decide AirMe
        psi.Environment["UXPLAYRC"] = Path.Combine(AppSettings.Folder, "uxplay.rc");
        if (!File.Exists(psi.Environment["UXPLAYRC"]!))
        {
            File.WriteAllText(psi.Environment["UXPLAYRC"]!, "# Gestionado por AirMe\n");
        }

        foreach (var arg in BuildArguments(_settings)) psi.ArgumentList.Add(arg);

        try
        {
            _log?.Dispose();
            _log = new StreamWriter(Path.Combine(AppSettings.Folder, "engine.log"), append: false, Encoding.UTF8) { AutoFlush = true };
            _log.WriteLine($"[{DateTime.Now:HH:mm:ss}] uxplay {string.Join(' ', psi.ArgumentList)}");
        }
        catch
        {
            _log = null;
        }

        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => OnLine(e.Data);
        p.ErrorDataReceived += (_, e) => OnLine(e.Data);
        try
        {
            p.Start();
            Native.KillWithParent(p.Handle);
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            _process = p;
            _startedAt = DateTime.UtcNow;
            DeviceName = null;
            SetState(EngineState.Waiting);
        }
        catch (Exception ex)
        {
            _log?.WriteLine("No se pudo iniciar el motor: " + ex.Message);
            p.Dispose();
            SetState(EngineState.Failed);
        }
    }

    public static IEnumerable<string> BuildArguments(AppSettings s)
    {
        yield return "-n";
        yield return s.ReceiverName;
        yield return "-nh";
        yield return "-vs";
        yield return "d3d11videosink";
        if (s.FullscreenOnConnect) yield return "-fs";
        if (s.RequirePin)
        {
            yield return "-pin";
            yield return s.Pin;
        }
    }

    private void StopProcess()
    {
        var p = _process;
        _process = null;
        if (p == null) return;
        try
        {
            if (!p.HasExited)
            {
                p.Kill(entireProcessTree: true);
                p.WaitForExit(3000);
            }
        }
        catch
        {
            // ya terminó
        }
        p.Dispose();
    }

    private void OnLine(string? line)
    {
        if (string.IsNullOrEmpty(line)) return;
        try { _log?.WriteLine(line); } catch { /* registro opcional */ }

        var m = ConnectionRequest().Match(line);
        if (m.Success)
        {
            DeviceName = m.Groups[1].Value.Trim();
            Changed?.Invoke();
        }
    }

    private void Poll()
    {
        lock (_gate)
        {
            var p = _process;
            if (p == null) return;

            if (p.HasExited)
            {
                _log?.WriteLine($"[{DateTime.Now:HH:mm:ss}] el motor terminó (código {p.ExitCode})");
                bool quick = DateTime.UtcNow - _startedAt < TimeSpan.FromSeconds(10);
                _quickFailures = quick ? _quickFailures + 1 : 0;
                p.Dispose();
                _process = null;
                if (_wantRunning && _quickFailures < 3)
                {
                    StartProcess();   // reinicio automático
                }
                else
                {
                    SetState(EngineState.Failed);
                }
                return;
            }

            bool streaming = Native.HasVideoWindow(p.Id);
            SetState(streaming ? EngineState.Streaming : EngineState.Waiting);
        }
    }

    private void SetState(EngineState state)
    {
        if (State == state) return;
        State = state;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _watch.Dispose();
        Stop();
        _log?.Dispose();
    }
}
