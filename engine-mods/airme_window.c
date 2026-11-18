/*
 * AirMe — ajustes de la ventana de video (Windows).
 *
 * Problema que resuelve: d3d11videosink solo dimensiona su ventana la primera vez
 * que la muestra. Al duplicar la pantalla el iPhone envía video vertical, pero al
 * abrir Fotos o un video envía un cuadro horizontal 16:9; la ventana se quedaba
 * vertical y la imagen se veía diminuta.
 *
 * Solución: una sonda en el pad del sink de video detecta cada cambio de tamaño
 * (evento CAPS) y, desde el hilo principal de GLib, se ajusta la ventana de
 * GStreamer a la nueva proporción dentro del 90 % del área útil del monitor.
 * Además se le pone el título "AirMe".
 *
 * Este archivo se compila dentro de UxPlay, por lo que se distribuye bajo GPL-3.0.
 */
#include "airme_window.h"

#ifdef _WIN32

#include <windows.h>
#include <wchar.h>
#include <gst/video/videooverlay.h>

#define AIRME_MAX_ATTEMPTS 50   /* 50 x 100 ms = 5 s esperando a que aparezca la ventana */
#define AIRME_SCREEN_FILL  90   /* % del área útil del monitor que puede ocupar la ventana */

static wchar_t airme_title[256] = L"AirMe";
static volatile gint airme_video_w = 0;
static volatile gint airme_video_h = 0;
static volatile gint airme_pending = 0;   /* 1 si ya hay un ajuste programado */
static int airme_attempts = 0;
static HWND airme_hwnd = NULL;            /* última ventana ajustada (solo hilo principal) */

void airme_window_init(const char *title_utf8) {
    if (title_utf8 && *title_utf8) {
        if (!MultiByteToWideChar(CP_UTF8, 0, title_utf8, -1, airme_title,
                                 (int) (sizeof(airme_title) / sizeof(airme_title[0])))) {
            wcscpy(airme_title, L"AirMe");
        }
    }
}

/* ---- búsqueda de la ventana creada por d3d11videosink / d3d12videosink ---- */

typedef struct {
    DWORD pid;
    HWND found;
} airme_find_t;

static BOOL CALLBACK airme_enum_cb(HWND hwnd, LPARAM lparam) {
    airme_find_t *ctx = (airme_find_t *) lparam;
    DWORD pid = 0;
    wchar_t cls[64];

    GetWindowThreadProcessId(hwnd, &pid);
    if (pid != ctx->pid || !IsWindowVisible(hwnd)) {
        return TRUE;
    }
    /* GetClassNameW no envía mensajes a la ventana (no puede bloquearse) */
    if (GetClassNameW(hwnd, cls, 64) && wcsncmp(cls, L"GSTD3D1", 7) == 0) {
        ctx->found = hwnd;
        return FALSE;
    }
    return TRUE;
}

static HWND airme_find_window(void) {
    airme_find_t ctx = { GetCurrentProcessId(), NULL };
    EnumWindows(airme_enum_cb, (LPARAM) &ctx);
    return ctx.found;
}

/* ---- ajuste de tamaño ---- */

static void airme_fit(HWND hwnd, int vw, int vh, gboolean center_on_monitor) {
    LONG style = GetWindowLongW(hwnd, GWL_STYLE);
    LONG exstyle = GetWindowLongW(hwnd, GWL_EXSTYLE);

    /* No tocar si está minimizada, maximizada o en pantalla completa (sin barra de título) */
    if (IsIconic(hwnd) || IsZoomed(hwnd) || !(style & WS_CAPTION)) {
        return;
    }

    HMONITOR mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
    MONITORINFO mi;
    mi.cbSize = sizeof(mi);
    if (!GetMonitorInfoW(mon, &mi)) {
        return;
    }
    RECT work = mi.rcWork;
    int work_w = work.right - work.left;
    int work_h = work.bottom - work.top;

    RECT frame = { 0, 0, 0, 0 };
    AdjustWindowRectEx(&frame, (DWORD) style, FALSE, (DWORD) exstyle);
    int frame_w = frame.right - frame.left;
    int frame_h = frame.bottom - frame.top;

    int max_cw = work_w * AIRME_SCREEN_FILL / 100 - frame_w;
    int max_ch = work_h * AIRME_SCREEN_FILL / 100 - frame_h;
    if (max_cw <= 0 || max_ch <= 0) {
        return;
    }

    double scale_w = (double) max_cw / vw;
    double scale_h = (double) max_ch / vh;
    double scale = scale_w < scale_h ? scale_w : scale_h;
    int win_w = (int) (vw * scale + 0.5) + frame_w;
    int win_h = (int) (vh * scale + 0.5) + frame_h;

    int cx, cy;
    if (center_on_monitor) {
        cx = work.left + work_w / 2;
        cy = work.top + work_h / 2;
    } else {
        RECT wr;
        GetWindowRect(hwnd, &wr);
        cx = (wr.left + wr.right) / 2;
        cy = (wr.top + wr.bottom) / 2;
    }
    int x = cx - win_w / 2;
    int y = cy - win_h / 2;
    if (x + win_w > work.right) x = work.right - win_w;
    if (y + win_h > work.bottom) y = work.bottom - win_h;
    if (x < work.left) x = work.left;
    if (y < work.top) y = work.top;

    /* ASYNC: la ventana pertenece al hilo de GStreamer; no bloquear el hilo principal */
    SetWindowPos(hwnd, NULL, x, y, win_w, win_h,
                 SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);
}

static gboolean airme_apply(gpointer user_data) {
    (void) user_data;
    HWND hwnd = airme_find_window();
    if (!hwnd) {
        if (++airme_attempts < AIRME_MAX_ATTEMPTS) {
            return G_SOURCE_CONTINUE;   /* la ventana aún no se muestra; reintentar */
        }
        g_atomic_int_set(&airme_pending, 0);
        return G_SOURCE_REMOVE;
    }

    gboolean is_new_window = (hwnd != airme_hwnd);
    airme_hwnd = hwnd;

    /* Título "AirMe" (con tiempo límite para no quedar bloqueados) */
    DWORD_PTR result = 0;
    SendMessageTimeoutW(hwnd, WM_SETTEXT, 0, (LPARAM) airme_title,
                        SMTO_ABORTIFHUNG | SMTO_NORMAL, 200, &result);

    g_atomic_int_set(&airme_pending, 0);
    int vw = g_atomic_int_get(&airme_video_w);
    int vh = g_atomic_int_get(&airme_video_h);
    if (vw > 0 && vh > 0) {
        airme_fit(hwnd, vw, vh, is_new_window);
    }
    return G_SOURCE_REMOVE;
}

static void airme_on_video_size(int w, int h) {
    if (w == g_atomic_int_get(&airme_video_w) && h == g_atomic_int_get(&airme_video_h)) {
        return;
    }
    g_atomic_int_set(&airme_video_w, w);
    g_atomic_int_set(&airme_video_h, h);
    GST_INFO("AirMe: tamaño de video %dx%d", w, h);
    /* Programar un único ajuste en el hilo principal (default main context de uxplay) */
    if (g_atomic_int_compare_and_exchange(&airme_pending, 0, 1)) {
        airme_attempts = 0;
        g_timeout_add(100, airme_apply, NULL);
    }
}

/* ---- sonda de CAPS en el sink de video ---- */

static GstPadProbeReturn airme_caps_probe(GstPad *pad, GstPadProbeInfo *info, gpointer user_data) {
    (void) pad;
    (void) user_data;
    GstEvent *event = GST_PAD_PROBE_INFO_EVENT(info);
    if (event && GST_EVENT_TYPE(event) == GST_EVENT_CAPS) {
        GstCaps *caps = NULL;
        gst_event_parse_caps(event, &caps);
        if (caps && gst_caps_get_size(caps) > 0) {
            const GstStructure *s = gst_caps_get_structure(caps, 0);
            int w = 0, h = 0;
            if (gst_structure_get_int(s, "width", &w) && gst_structure_get_int(s, "height", &h) &&
                w > 0 && h > 0) {
                airme_on_video_size(w, h);
            }
        }
    }
    return GST_PAD_PROBE_OK;
}

static void airme_attach(GstElement *element) {
    if (!GST_IS_VIDEO_OVERLAY(element) || g_object_get_data(G_OBJECT(element), "airme-probe")) {
        return;
    }
    GstPad *pad = gst_element_get_static_pad(element, "sink");
    if (!pad) {
        return;   /* p. ej. playbin/playsink: no tienen pad "sink" estático */
    }
    gst_pad_add_probe(pad, GST_PAD_PROBE_TYPE_EVENT_DOWNSTREAM, airme_caps_probe, NULL, NULL);
    g_object_set_data(G_OBJECT(element), "airme-probe", GINT_TO_POINTER(1));
    gst_object_unref(pad);
}

static void airme_deep_element_added(GstBin *bin, GstBin *sub_bin, GstElement *element, gpointer user_data) {
    (void) bin;
    (void) sub_bin;
    (void) user_data;
    airme_attach(element);
}

void airme_window_watch_pipeline(GstElement *pipeline) {
    if (!pipeline || !GST_IS_BIN(pipeline)) {
        return;
    }
    GstIterator *it = gst_bin_iterate_recurse(GST_BIN(pipeline));
    GValue item = G_VALUE_INIT;
    gboolean done = FALSE;
    while (!done) {
        switch (gst_iterator_next(it, &item)) {
        case GST_ITERATOR_OK:
            airme_attach(GST_ELEMENT(g_value_get_object(&item)));
            g_value_reset(&item);
            break;
        case GST_ITERATOR_RESYNC:
            gst_iterator_resync(it);
            break;
        default:
            done = TRUE;
            break;
        }
    }
    g_value_unset(&item);
    gst_iterator_free(it);
    /* sinks que se crean después (playbin, autovideosink) */
    g_signal_connect(pipeline, "deep-element-added", G_CALLBACK(airme_deep_element_added), NULL);
}

#else /* !_WIN32: sin cambios en otras plataformas */

void airme_window_init(const char *title_utf8) { (void) title_utf8; }
void airme_window_watch_pipeline(GstElement *pipeline) { (void) pipeline; }

#endif
