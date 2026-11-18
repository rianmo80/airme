/*
 * AirMe — ajustes de la ventana de video (Windows).
 * Se compila dentro de UxPlay (GPL-3.0); ver engine-mods/README.md.
 */
#ifndef AIRME_WINDOW_H
#define AIRME_WINDOW_H

#include <gst/gst.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Guarda el título de la ventana (UTF-8), p. ej. el nombre del servidor "AirMe". */
void airme_window_init(const char *title_utf8);

/* Vigila los sinks de video del pipeline: cuando cambia el tamaño del video
 * (pantalla vertical <-> fotos/videos horizontales), adapta la forma de la ventana. */
void airme_window_watch_pipeline(GstElement *pipeline);

#ifdef __cplusplus
}
#endif

#endif /* AIRME_WINDOW_H */
