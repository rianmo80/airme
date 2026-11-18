# AirMe

Duplica la pantalla de tu iPhone en tu PC con Windows, por WiFi y sin instalar nada en el iPhone.

En el iPhone: **Centro de Control → Duplicar pantalla → AirMe**.

## Cómo funciona

AirMe hace que tu PC aparezca en la red como un receptor AirPlay (igual que un Apple TV).
El motor de recepción es [UxPlay](https://github.com/FDH2/UxPlay) (GPL-3.0), compilado
en la nube con GitHub Actions. En tu PC no hace falta instalar compiladores, MSYS2 ni GStreamer:
todo viene empaquetado en la carpeta `AirMe`.

## Uso (prototipo)

1. Ve a la pestaña **Actions** del repositorio, abre la última compilación exitosa y
   descarga el artefacto `AirMe-windows-x64`.
2. Descomprímelo donde quieras.
3. Doble clic en `AirMe.bat`.
4. Si Windows pregunta por el Firewall, permite el acceso en **redes privadas**.
5. En el iPhone (misma red WiFi): Centro de Control → Duplicar pantalla → **AirMe**.

Atajos: `Alt+Enter` alterna pantalla completa.

### Si el iPhone no ve "AirMe"

- El PC debe estar en una red **privada**, no pública (`Get-NetConnectionProfile` en PowerShell).
- iPhone y PC en la **misma** red WiFi (sin VPN, sin "aislamiento de clientes" de redes de invitados).
- Revisa que el Firewall permita `bin\uxplay.exe`.

## Estructura

```
.github/workflows/build.yml   Compilación en la nube (Windows + MSYS2 UCRT64)
scripts/package.sh            Empaqueta uxplay.exe + DLLs + plugins de GStreamer
scripts/smoke-test.ps1        Verifica que el paquete arranca fuera de MSYS2
packaging/AirMe.bat           Lanzador del prototipo
```

La versión de UxPlay está fijada a un commit concreto en `build.yml` (`UXPLAY_REF`) para que
cada compilación sea reproducible.

## Hoja de ruta

- [x] Fase 1 — Prototipo: UxPlay empaquetado como "AirMe", compilado en la nube.
- [ ] Fase 2 — Interfaz propia (.NET): ventana AirMe, pantalla completa, PIN, icono en bandeja.
- [ ] Fase 3 — Grabación y capturas de pantalla, instalador.

## Licencia

El motor (UxPlay) es GPL-3.0; el paquete distribuido incluye su licencia y el enlace al
código fuente exacto usado.
