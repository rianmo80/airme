# Verifica que el paquete AirMe arranca en Windows "limpio" (sin MSYS2 en el PATH)
# y que GStreamer encuentra los elementos que usa UxPlay.
param([string]$Dist = "dist/AirMe")
$ErrorActionPreference = "Stop"

$Dist = (Resolve-Path $Dist).Path
$env:PATH = "$env:SystemRoot\System32;$env:SystemRoot"
$env:GST_PLUGIN_PATH = "$Dist\lib\gstreamer-1.0"
$env:GST_PLUGIN_SYSTEM_PATH = "$Dist\lib\gstreamer-1.0"
$env:GST_PLUGIN_SCANNER = "$Dist\bin\gst-plugin-scanner.exe"
$env:GST_REGISTRY = "$env:RUNNER_TEMP\airme-registry.bin"

$version = & "$Dist\bin\uxplay.exe" -v 2>&1 | Out-String
Write-Host $version
if ($LASTEXITCODE -ne 0 -or $version -notmatch "UxPlay version") {
    throw "uxplay.exe no arrancó (código $LASTEXITCODE). ¿Falta alguna DLL?"
}

$required = "appsrc", "h264parse", "h265parse", "avdec_h264", "avdec_aac",
            "avdec_alac", "videoconvert", "d3d11videosink", "wasapisink", "audioconvert"
$missing = @()
foreach ($el in $required) {
    & "$Dist\bin\gst-inspect-1.0.exe" --exists $el
    if ($LASTEXITCODE -ne 0) { $missing += $el }
}
if ($missing) { throw "Faltan elementos de GStreamer: $($missing -join ', ')" }
Write-Host "OK: uxplay.exe arranca y los $($required.Count) elementos de GStreamer están presentes."
