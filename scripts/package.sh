#!/usr/bin/env bash
# Empaqueta el motor (uxplay.exe + DLLs + plugins de GStreamer) en dist/AirMe/engine.
# Debe ejecutarse dentro de MSYS2 UCRT64 después de compilar engine/build/uxplay.exe.
# La app AirMe.exe (.NET) se publica después en dist/AirMe (ver build.yml).
set -euo pipefail

PREFIX="${MINGW_PREFIX:-/ucrt64}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/dist/AirMe"
ENG="$OUT/engine"
EXE="$ROOT/engine/build/uxplay.exe"

[ -f "$EXE" ] || { echo "No existe $EXE (compila primero)"; exit 1; }

rm -rf "$OUT"
mkdir -p "$ENG/bin" "$ENG/lib/gstreamer-1.0" "$OUT/licenses"

cp "$EXE" "$ENG/bin/"
cp "$PREFIX"/lib/gstreamer-1.0/*.dll "$ENG/lib/gstreamer-1.0/"
# El escáner de plugins va en bin/ para que encuentre las mismas DLLs que uxplay.exe
cp "$PREFIX/libexec/gstreamer-1.0/gst-plugin-scanner.exe" "$ENG/bin/"
# Herramienta de diagnóstico (ver qué elementos de GStreamer hay disponibles)
cp "$PREFIX/bin/gst-inspect-1.0.exe" "$ENG/bin/"

# Copiar todas las DLLs de MSYS2 de las que dependen el exe, las herramientas y los plugins
mapfile -t deps < <(
  for f in "$ENG"/bin/*.exe "$ENG"/lib/gstreamer-1.0/*.dll; do
    ldd "$f" 2>/dev/null | awk -v p="$PREFIX/bin/" 'index($3, p) == 1 { print $3 }'
  done | sort -u
)
for d in "${deps[@]}"; do cp -n "$d" "$ENG/bin/"; done
echo "DLLs copiadas: ${#deps[@]}"

# Licencia y origen del motor (requisito GPL-3.0)
cp "$ROOT/engine/LICENSE" "$OUT/licenses/UxPlay-LICENSE.txt"
REF="$(git -C "$ROOT/engine" rev-parse HEAD)"
cat > "$OUT/licenses/README.txt" <<EOF
AirMe incluye UxPlay (GPL-3.0): https://github.com/FDH2/UxPlay
Código fuente exacto usado: https://github.com/FDH2/UxPlay/tree/$REF
Cambios de AirMe sobre UxPlay: carpeta engine-mods/ del repositorio de AirMe.
GStreamer y demás bibliotecas provienen de los paquetes MSYS2 UCRT64 (https://packages.msys2.org).
EOF

du -sh "$OUT"
