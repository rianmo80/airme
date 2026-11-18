using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AirMe.Services;

namespace AirMe;

public partial class MainWindow : Window
{
    private readonly App _app;
    private NetworkStatus _net = NetworkStatus.Unknown;
    private FirewallStatus _fw = FirewallStatus.Unknown;
    private bool _checking;
    private bool _loadingSettings;
    private readonly System.Windows.Threading.DispatcherTimer _checkTimer;

    public MainWindow(App app)
    {
        _app = app;
        InitializeComponent();
        VersionRun.Text = "AirMe " + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "");

        _checkTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _checkTimer.Tick += (_, _) => RefreshChecks();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { RefreshChecks(); _checkTimer.Start(); }
            else _checkTimer.Stop();
        };
        Activated += (_, _) => RefreshChecks();

        UpdateEngineUi();
    }

    // ---------------- Estado del motor ----------------

    public void UpdateEngineUi()
    {
        var s = _app.Settings;
        var engine = _app.Engine;
        StepChooseTitle.Text = $"Elige {s.ReceiverName}";

        switch (engine.State)
        {
            case EngineState.Streaming:
                SetPill("Live", "LiveSoft", engine.DeviceName is { Length: > 0 } d ? $"Transmitiendo desde {d}" : "Transmitiendo");
                HeroTitle.Text = "Duplicando tu iPhone";
                HeroText.Text = "La imagen se muestra en la ventana " + s.ReceiverName + ". Pulsa Alt + Enter para pantalla completa.";
                break;
            case EngineState.Failed:
                SetPill("Warn", "WarnSoft", "El receptor no pudo iniciar");
                HeroTitle.Text = "No se pudo iniciar el receptor";
                HeroText.Text = engine.EnginePath == null
                    ? "Falta la carpeta \"engine\" junto a AirMe.exe. Vuelve a descargar AirMe."
                    : "Puede que el antivirus lo haya bloqueado. Prueba \"Reiniciar receptor\" en ¿No aparece AirMe?";
                break;
            case EngineState.Waiting:
                SetPill("Accent", "AccentSoft", "Esperando al iPhone");
                HeroTitle.Text = "Listo para duplicar tu iPhone";
                HeroText.Text = "Tu PC ya aparece en la red como receptor AirPlay. Solo falta elegirlo desde el iPhone.";
                break;
            default:
                SetPill("Accent", "AccentSoft", "Iniciando…");
                break;
        }

        PinText.Text = s.RequirePin ? s.Pin : "Desactivado";
        PinText.FontFamily = s.RequirePin ? new FontFamily("Consolas") : (FontFamily)FindResource("UiFont");
        PinText.FontSize = s.RequirePin ? 16 : 14;
        PinToggle.Content = s.RequirePin ? "Desactivar" : "Activar";
    }

    private void SetPill(string fgKey, string bgKey, string text)
    {
        var fg = (Brush)FindResource(fgKey);
        StatusPill.Background = (Brush)FindResource(bgKey);
        StatusDot.Fill = fg;
        StatusText.Foreground = fg;
        StatusText.Text = text;
    }

    // ---------------- Red y Firewall ----------------

    private async void RefreshChecks()
    {
        if (_checking) return;
        _checking = true;
        string? enginePath = _app.Engine.EnginePath;
        var (net, fw) = await Task.Run(() => (SystemChecks.GetNetworkStatus(), SystemChecks.GetFirewallStatus(enginePath)));
        _net = net;
        _fw = fw;
        _checking = false;

        switch (_net)
        {
            case NetworkStatus.Private: SetCheck(NetIcon, NetText, true, "Privada"); NetFix.Visibility = Visibility.Collapsed; break;
            case NetworkStatus.Public: SetCheck(NetIcon, NetText, false, "Pública"); NetFix.Visibility = Visibility.Visible; break;
            case NetworkStatus.Disconnected: SetCheck(NetIcon, NetText, false, "Sin conexión"); NetFix.Visibility = Visibility.Collapsed; break;
            default: SetUnknown(NetIcon, NetText); NetFix.Visibility = Visibility.Collapsed; break;
        }
        switch (_fw)
        {
            case FirewallStatus.Allowed: SetCheck(FwIcon, FwText, true, "Acceso permitido"); FwFix.Visibility = Visibility.Collapsed; break;
            case FirewallStatus.NotAllowed: SetCheck(FwIcon, FwText, false, "Sin permiso"); FwFix.Visibility = Visibility.Visible; break;
            default: SetUnknown(FwIcon, FwText); FwFix.Visibility = Visibility.Collapsed; break;
        }
    }

    private void SetCheck(System.Windows.Controls.TextBlock icon, System.Windows.Controls.TextBlock text, bool ok, string label)
    {
        icon.Text = ok ? "" : "";   // círculo con check / advertencia
        var brush = (Brush)FindResource(ok ? "Ok" : "Warn");
        icon.Foreground = brush;
        text.Foreground = ok ? (Brush)FindResource("Ink") : brush;
        text.Text = label;
    }

    private void SetUnknown(System.Windows.Controls.TextBlock icon, System.Windows.Controls.TextBlock text)
    {
        icon.Text = "";
        icon.Foreground = (Brush)FindResource("InkSoft");
        text.Foreground = (Brush)FindResource("InkSoft");
        text.Text = "No se pudo comprobar";
    }

    private void NetFix_Click(object sender, RoutedEventArgs e) => SystemChecks.OpenNetworkSettings();

    private async void FwFix_Click(object sender, RoutedEventArgs e)
    {
        string? path = _app.Engine.EnginePath;
        if (path == null) return;
        FwFix.IsEnabled = false;
        await Task.Run(() => SystemChecks.AllowThroughFirewall(path));
        FwFix.IsEnabled = true;
        RefreshChecks();
    }

    private void PinToggle_Click(object sender, RoutedEventArgs e)
    {
        var updated = _app.Settings.Clone();
        updated.RequirePin = !updated.RequirePin;
        _app.ApplySettings(updated);
    }

    // ---------------- Navegación ----------------

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        bool showHelp = HelpPanel.Visibility != Visibility.Visible;
        HelpPanel.Visibility = showHelp ? Visibility.Visible : Visibility.Collapsed;
        StepsPanel.Visibility = showHelp ? Visibility.Collapsed : Visibility.Visible;
        HelpButtonText.Text = showHelp ? "Cómo conectar" : "¿No aparece AirMe?";
        if (showHelp) RefreshChecks();
    }

    private void RestartEngine_Click(object sender, RoutedEventArgs e) => _app.Engine.Restart(_app.Settings);

    private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings();

    public void ShowSettings()
    {
        _loadingSettings = true;
        var s = _app.Settings;
        NameBox.Text = s.ReceiverName;
        StartupSwitch.IsChecked = s.StartWithWindows;
        TraySwitch.IsChecked = s.MinimizeToTrayOnClose;
        FullscreenSwitch.IsChecked = s.FullscreenOnConnect;
        PinSwitch.IsChecked = s.RequirePin;
        UpdatePinSettingText();
        _loadingSettings = false;

        HomeView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Visible;
        Title = "AirMe — Ajustes";
    }

    public void ShowHome()
    {
        SettingsView.Visibility = Visibility.Collapsed;
        HomeView.Visibility = Visibility.Visible;
        Title = "AirMe";
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        CommitName();
        ShowHome();
    }

    // ---------------- Ajustes ----------------

    private void Switch_Click(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        var updated = _app.Settings.Clone();
        updated.StartWithWindows = StartupSwitch.IsChecked == true;
        updated.MinimizeToTrayOnClose = TraySwitch.IsChecked == true;
        updated.FullscreenOnConnect = FullscreenSwitch.IsChecked == true;
        updated.RequirePin = PinSwitch.IsChecked == true;
        _app.ApplySettings(updated);
        UpdatePinSettingText();
    }

    private void UpdatePinSettingText()
    {
        var s = _app.Settings;
        PinSettingText.Text = s.RequirePin
            ? $"El iPhone pedirá este código la primera vez: {s.Pin}"
            : "Código de 4 dígitos al conectar un iPhone nuevo";
    }

    private void NameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitName();
    }

    private void NameBox_Commit(object sender, KeyboardFocusChangedEventArgs e) => CommitName();

    private void CommitName()
    {
        var updated = _app.Settings.Clone();
        updated.ReceiverName = NameBox.Text;
        updated.Normalize();
        NameBox.Text = updated.ReceiverName;
        if (updated.ReceiverName != _app.Settings.ReceiverName) _app.ApplySettings(updated);
    }

    // ---------------- Cerrar ----------------

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_app.IsExiting && _app.Settings.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            CommitName();
            Hide();
            _app.NotifyStillRunningOnce();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!_app.IsExiting) Dispatcher.BeginInvoke(_app.ExitApp);
    }
}
