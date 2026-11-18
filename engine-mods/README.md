# Mejoras de AirMe sobre UxPlay

La compilación (`.github/workflows/build.yml`) copia estos archivos a `engine/renderers/`
y aplica `airme.patch` sobre el commit fijo de UxPlay (`UXPLAY_REF`).

| Archivo | Qué hace |
|---|---|
| `airme_window.c/.h` | La ventana de video se adapta sola a la forma del video (vertical al duplicar la pantalla, horizontal con fotos y videos) y se titula "AirMe". Solo Windows; en otras plataformas no hace nada. |
| `airme.patch` | Conecta lo anterior al renderizador de video de UxPlay y permite salir con Alt+Enter cuando se arranca en pantalla completa (`fs`). |

Al cambiar `UXPLAY_REF`, regenera el parche si deja de aplicarse.

Todo el código de esta carpeta se compila dentro de UxPlay y se distribuye bajo GPL-3.0.
