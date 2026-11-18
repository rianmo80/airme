# AirMe

Duplica la pantalla de tu iPhone en tu PC con Windows, por WiFi y sin instalar nada en el iPhone.

En el iPhone: **Centro de Control → Duplicar pantalla → AirMe**.

## Cómo funciona

AirMe hace que tu PC aparezca en la red como un receptor AirPlay (igual que un Apple TV).
La app `AirMe.exe` (.NET/WPF) arranca en segundo plano el motor de recepción,
[UxPlay](https://github.com/FDH2/UxPlay) (GPL-3.0), compilado en la nube con GitHub Actions.
En tu PC no hace falta instalar nada: todo viene en la carpeta `AirMe`.

## Instalar

1. Descarga `AirMe-Setup-X.Y.Z.exe` de la última versión en
   [Releases](https://github.com/rianmo80/airme/releases).
2. Ábrelo e instala. El instalador deja configurado el Firewall de Windows (redes privadas).
   Como el instalador no está firmado, Windows puede mostrar "Windows protegió su PC":
   pulsa **Más información → Ejecutar de todas formas**.
3. En el iPhone (misma red WiFi): Centro de Control → Duplicar pantalla → **AirMe**.

También hay una versión **portable** (`AirMe-X.Y.Z-portable.zip`): descomprímela y abre `AirMe.exe`.
En ese caso, si Windows pregunta por el Firewall, permite el acceso en **redes privadas**
(o pulsa **Permitir** en la ventana de AirMe).

Para desinstalar: Configuración → Aplicaciones → AirMe → Desinstalar.

## Uso

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
installer/AirMe.iss           Instalador (Inno Setup): Archivos de programa + regla del Firewall
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

### Publicar una versión

1. Sube `<Version>` en `app/AirMe/AirMe.csproj` (por ejemplo `0.3.1`) y haz commit.
2. Crea y sube la etiqueta: `git tag v0.3.1 && git push origin v0.3.1`.
3. GitHub Actions compila y crea la Release con el instalador y el zip portable.
   Si la etiqueta no coincide con la versión del `.csproj`, la compilación falla.

## Hoja de ruta

- [x] Fase 1: prototipo con UxPlay empaquetado como "AirMe" y compilado en la nube.
- [x] Paso 1: ventana "AirMe" que se adapta sola a fotos y videos (`engine-mods/`).
- [x] Fase 2: app propia minimalista (inicio, ajustes, bandeja, PIN, comprobación de red y Firewall).
- [x] Fase 3: instalador (Inno Setup) y versiones en GitHub Releases.

## Licencia

El motor (UxPlay) es GPL-3.0; el paquete distribuido incluye su licencia y el enlace al
código fuente exacto usado. Los cambios sobre el motor (`engine-mods/`) son GPL-3.0.
