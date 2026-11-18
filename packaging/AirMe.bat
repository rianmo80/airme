@echo off
rem AirMe - lanzador del prototipo (motor UxPlay)
setlocal
set "HERE=%~dp0"

rem Usar solo los plugins de GStreamer incluidos en esta carpeta
set "GST_PLUGIN_PATH=%HERE%lib\gstreamer-1.0"
set "GST_PLUGIN_SYSTEM_PATH=%HERE%lib\gstreamer-1.0"
set "GST_PLUGIN_SCANNER=%HERE%bin\gst-plugin-scanner.exe"
if not exist "%LOCALAPPDATA%\AirMe" mkdir "%LOCALAPPDATA%\AirMe"
set "GST_REGISTRY=%LOCALAPPDATA%\AirMe\gst-registry.bin"

rem No leer ~/.uxplayrc de otras instalaciones
set "UXPLAYRC=%HERE%airme.rc"

rem -n AirMe -nh : nombre que aparece en el iPhone, sin "@equipo"
rem -vs d3d11videosink : video por Direct3D 11 (Alt+Enter = pantalla completa)
"%HERE%bin\uxplay.exe" -n AirMe -nh -vs d3d11videosink %*
