using System.Windows;
using System.Windows.Media;
using AirMe.Services;
using Forms = System.Windows.Forms;

namespace AirMe;

/// <summary>Menú de la bandeja con el estilo de AirMe (se abre junto al cursor).</summary>
public partial class TrayMenu : Window
{
    private readonly App _app;
    private readonly System.Drawing.Point _cursor;
    private bool _closing;

    public TrayMenu(App app)
    {
        _app = app;
        _cursor = Forms.Cursor.Position;
        InitializeComponent();
        Left = -10000;
        Top = -10000;

        switch (app.Engine.State)
        {
            case EngineState.Streaming:
                Dot.Fill = (Brush)FindResource("Live");
                StateText.Text = app.Engine.DeviceName is { Length: > 0 } d ? $"Transmitiendo desde {d}" : "Transmitiendo";
                break;
            case EngineState.Failed:
                Dot.Fill = (Brush)FindResource("Warn");
                StateText.Text = "El receptor no pudo iniciar";
                break;
            default:
                StateText.Text = "Esperando conexión";
                break;
        }

        ContentRendered += (_, _) => PlaceNearCursor();
        Deactivated += (_, _) => SafeClose();
    }

    private void PlaceNearCursor()
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget == null) return;
        var toDip = source.CompositionTarget.TransformFromDevice;
        var area = Forms.Screen.FromPoint(_cursor).WorkingArea;

        var cursor = toDip.Transform(new Point(_cursor.X, _cursor.Y));
        var topLeft = toDip.Transform(new Point(area.Left, area.Top));
        var bottomRight = toDip.Transform(new Point(area.Right, area.Bottom));

        double left = cursor.X - ActualWidth + 24;
        double top = cursor.Y - ActualHeight + 12;
        left = Math.Max(topLeft.X, Math.Min(left, bottomRight.X - ActualWidth));
        top = Math.Max(topLeft.Y, Math.Min(top, bottomRight.Y - ActualHeight));
        Left = left;
        Top = top;
        Activate();
    }

    private void SafeClose()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        SafeClose();
        _app.ShowMainWindow();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        SafeClose();
        _app.ShowMainWindow(settings: true);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        SafeClose();
        _app.ExitApp();
    }
}
