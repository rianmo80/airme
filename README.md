# AirMe

Duplica la pantalla de tu iPhone en tu PC con Windows, por WiFi y sin instalar nada en el iPhone.

En el iPhone: **Centro de Control → Duplicar pantalla → AirMe**.

## Cómo funciona

AirMe hace que tu PC aparezca en la red como un receptor AirPlay (igual que un Apple TV).
La app `AirMe.exe` (.NET/WPF) arranca en segundo plano el motor de recepción,
[UxPlay](https://github.com/FDH2/UxPlay) (GPL-3.0), compilado en la nube con GitHub Actions.
En tu PC no hace falta instalar nada: todo viene en la carpeta `AirMe`.

## Uso

1. Ve a la pestaña **Actions** del repositorio, abre la última compilación exitosa y
   descarga el artefacto `AirMe-windows-x64`.
2. Descomprímelo donde quieras.
3. Abre `AirMe.exe`.
4. Si Windows pregunta por el Firewall, permite el acceso en **redes privadas**
   (o pulsa **Permitir** en la ventana de AirMe).
5. En el iPhone (misma red WiFi): Centro de Control → Duplicar pantalla → **AirMe**.

- La ventana del video cambia de forma sola: vertical al duplicar la pantalla y horizontal con fotos o videos.
- `Alt+Enter` alterna pantalla completa.
- Al cerrar la ventana de AirMe sigue activo en la **bandeja del sistema** (clic derecho → Salir de AirMe).
- **Ajustes**: nombre del receptor, iniciar con Windows, pantalla completa al conectar y PIN de 4 dígitos.
- Ajustes y registro del motor: `%APPDATA%\AirMe\` (`settings.json`, `engine.log`).

### Si el iPhone no ve "AirMe"

Pulsa **¿No aparece AirMe?** en la app. En resumen:

- iPhone y PC en la **misma** red WiFi (sin VPN, sin "aislamiento de clientes" de redes de invitados).
- La red del PC debe ser **Privada** y el Firewall debe permitir AirMe (la app lo comprueba).

## Estructura

```
app/AirMe/                    App de Windows (WPF, .NET 10): inicio, ajustes, bandeja
engine-mods/                  Mejoras de AirMe aplicadas sobre UxPlay al compilar
.github/workflows/build.yml   Compilación en la nube (MSYS2 UCRT64 + .NET)
scripts/package.sh            Empaqueta uxplay.exe + DLLs + plugins de GStreamer en engine/
scripts/smoke-test.ps1        Verifica el paquete fuera de MSYS2
```

Paquete resultante:

```
AirMe/
  AirMe.exe          la app (autocontenida, no necesita instalar .NET)
  engine/bin/        uxplay.exe y sus DLLs
  engine/lib/        plugins de GStreamer
  licenses/          licencia de UxPlay y enlace a su código fuente
```

La versión de UxPlay está fijada a un commit concreto en `build.yml` (`UXPLAY_REF`) para que
cada compilación sea reproducible.

## Hoja de ruta

- [x] Fase 1: prototipo con UxPlay empaquetado como "AirMe" y compilado en la nube.
- [x] Paso 1: ventana "AirMe" que se adapta sola a fotos y videos (`engine-mods/`).
- [x] Fase 2: app propia minimalista (inicio, ajustes, bandeja, PIN, comprobación de red y Firewall).
- [ ] Fase 3: instalador y versiones en GitHub Releases.

## Licencia

El motor (UxPlay) es GPL-3.0; el paquete distribuido incluye su licencia y el enlace al
código fuente exacto usado. Los cambios sobre el motor (`engine-mods/`) son GPL-3.0.
