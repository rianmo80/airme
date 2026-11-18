using System.Diagnostics;
using Microsoft.Win32;

namespace AirMe.Services;

public enum NetworkStatus { Unknown, Private, Public, Disconnected }

public enum FirewallStatus { Unknown, Allowed, NotAllowed }

/// <summary>Comprobaciones de Windows que afectan a si el iPhone ve AirMe.</summary>
public static class SystemChecks
{
    /// <summary>Categoría de las redes conectadas (Network List Manager).</summary>
    public static NetworkStatus GetNetworkStatus()
    {
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B"));
            if (type == null) return NetworkStatus.Unknown;
            dynamic nlm = Activator.CreateInstance(type)!;
            bool any = false, anyPublic = false;
            foreach (dynamic network in nlm.GetNetworks(1 /* NLM_ENUM_NETWORK_CONNECTED */))
            {
                any = true;
                int category = network.GetCategory(); // 0 pública, 1 privada, 2 dominio
                if (category == 0) anyPublic = true;
            }
            if (!any) return NetworkStatus.Disconnected;
            return anyPublic ? NetworkStatus.Public : NetworkStatus.Private;
        }
        catch
        {
            return NetworkStatus.Unknown;
        }
    }

    /// <summary>¿Hay una regla de entrada habilitada que permita el motor?</summary>
    public static FirewallStatus GetFirewallStatus(string? enginePath)
    {
        if (enginePath == null) return FirewallStatus.Unknown;
        try
        {
            var type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            if (type == null) return FirewallStatus.Unknown;
            dynamic policy = Activator.CreateInstance(type)!;
            foreach (dynamic rule in policy.Rules)
            {
                string? app = rule.ApplicationName;
                if (app == null || !string.Equals(app, enginePath, StringComparison.OrdinalIgnoreCase)) continue;
                // Direction 1 = entrada, Action 1 = permitir; Profiles: 2 = privada, 0x7FFFFFFF = todas
                if (rule.Enabled && rule.Direction == 1 && rule.Action == 1 && (rule.Profiles & 2) != 0)
                {
                    return FirewallStatus.Allowed;
                }
            }
            return FirewallStatus.NotAllowed;
        }
        catch
        {
            return FirewallStatus.Unknown;
        }
    }

    /// <summary>Crea la regla del Firewall (pide permiso de administrador con UAC).</summary>
    public static bool AllowThroughFirewall(string enginePath)
    {
        try
        {
            var psi = new ProcessStartInfo("netsh")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = $"advfirewall firewall add rule name=\"AirMe\" dir=in action=allow program=\"{enginePath}\" enable=yes profile=private",
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(15000);
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;   // el usuario canceló el UAC
        }
    }

    public static void OpenNetworkSettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:network-status") { UseShellExecute = true }); }
        catch { /* sin Configuración */ }
    }

    // ---- Iniciar con Windows ----

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void SetStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue("AirMe", $"\"{Environment.ProcessPath}\" --tray");
        }
        else
        {
            key.DeleteValue("AirMe", throwOnMissingValue: false);
        }
    }
}
