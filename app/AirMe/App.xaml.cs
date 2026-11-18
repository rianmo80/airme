using System.Threading;
using System.Windows;
using AirMe.Services;
using Forms = System.Windows.Forms;

namespace AirMe;

public partial class App : Application
{
    private const string InstanceName = "AirMe.SingleInstance.v1";
    private Mutex? _mutex;
    private EventWaitHandle? _showSignal;
    private Forms.NotifyIcon? _tray;
    private MainWindow? _main;
    private bool _toldStillRunning;
    private EngineState _lastState = EngineState.Stopped;
    private bool _minimizedForStream;

    public AppSettings Settings { get; private set; } = new();
    public EngineHost Engine { get; private set; } = null!;
    public bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Una sola instancia: si ya está abierta, se le pide que se muestre
        _mutex = new Mutex(true, InstanceName, out bool isFirst);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + ".show");
        if (!isFirst)
        {
            _showSignal.Set();
            Shutdown();
            return;
        }
        var listener = new Thread(() =>
        {
            while (_showSignal.WaitOne())
            {
                if (IsExiting) return;
                Dispatcher.BeginInvoke(() => ShowMainWindow());
            }
        }) { IsBackground = true, Name = "AirMe show listener" };
        listener.Start();

        Settings = AppSettings.Load();
        Settings.Save();
        try { SystemChecks.SetStartWithWindows(Settings.StartWithWindows); } catch { /* registro no disponible */ }

        Engine = new EngineHost();
        Engine.Changed += () => Dispatcher.BeginInvoke(OnEngineChanged);

        CreateTrayIcon();
        _main = new MainWindow(this);

        bool startInTray = e.Args.Contains("--tray", StringComparer.OrdinalIgnoreCase);
        if (!startInTray) _main.Show();

        Engine.Start(Settings);
    }

    private void CreateTrayIcon()
    {
        var iconStream = GetResourceStream(new Uri("pack://application:,,,/Assets/airme.ico"))!.Stream;
        _tray = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconStream, Forms.SystemInformation.SmallIconSize),
            Text = "AirMe",
            Visible = true,
        };
        _tray.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left) ShowMainWindow();
            else if (args.Button == Forms.MouseButtons.Right) new TrayMenu(this).Show();
        };
    }

    private void OnEngineChanged()
    {
        var current = Engine.State;
        if (current != _lastState)
        {
            if (current == EngineState.Streaming) OnStreamStarted();
            else if (_lastState == EngineState.Streaming) OnStreamEnded();
            _lastState = current;
        }

        _main?.UpdateEngineUi();
        if (_tray != null)
        {
            string state = Engine.State switch
            {
                EngineState.Streaming => "transmitiendo",
                EngineState.Waiting => "esperando al iPhone",
                EngineState.Failed => "el receptor no pudo iniciar",
                _ => "iniciando",
            };
            string text = $"{Settings.ReceiverName} — {state}";
            _tray.Text = text.Length > 63 ? text[..63] : text;
        }
    }

    /// <summary>Al conectar el iPhone: el video pasa al frente y la ventana de AirMe se minimiza.</summary>
    private void OnStreamStarted()
    {
        Native.BringToFront(Engine.VideoWindow);
        if (_main is { IsVisible: true } && _main.WindowState != WindowState.Minimized)
        {
            _main.WindowState = WindowState.Minimized;
            _minimizedForStream = true;
        }
    }

    /// <summary>Al desconectar: si AirMe se minimizó por la transmisión, vuelve a mostrarse.</summary>
    private void OnStreamEnded()
    {
        if (_minimizedForStream && _main is { IsVisible: true } && _main.WindowState == WindowState.Minimized)
        {
            _main.WindowState = WindowState.Normal;
        }
        _minimizedForStream = false;
    }

    public void ShowMainWindow(bool settings = false)
    {
        if (_main == null) return;
        if (settings) _main.ShowSettings();
        if (!_main.IsVisible) _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    public void ApplySettings(AppSettings updated)
    {
        updated.Normalize();
        var previous = Settings;
        Settings = updated;
        Settings.Save();
        if (previous.StartWithWindows != updated.StartWithWindows)
        {
            try { SystemChecks.SetStartWithWindows(updated.StartWithWindows); } catch { /* sin acceso al registro */ }
        }
        if (previous.EngineOptionsDiffer(updated)) Engine.Restart(updated);
        OnEngineChanged();
    }

    public void NotifyStillRunningOnce()
    {
        if (_toldStillRunning || _tray == null) return;
        _toldStillRunning = true;
        _tray.ShowBalloonTip(3000, "AirMe sigue activo",
            "Tu iPhone puede seguir conectándose. Abre AirMe desde la bandeja del sistema.", Forms.ToolTipIcon.None);
    }

    public void ExitApp()
    {
        if (IsExiting) return;
        IsExiting = true;
        if (_main != null && PresentationSource.FromVisual(_main) != null) _main.Close();
        Engine?.Dispose();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _showSignal?.Set();   // libera el hilo que escucha
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _mutex?.ReleaseMutex(); } catch { /* no era el propietario */ }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
