const API_BASE = "";

async function apiFetch(path, options = {}) {
    const isFormData = options.body instanceof FormData;

    const headers = {
        ...(isFormData ? {} : { "Content-Type": "application/json" }),
        ...(options.headers || {}),
    };

    let res;
    try {
        res = await fetch(API_BASE + path, {
            credentials: "include",
            headers,
            ...options,
        });
    } catch {
        throw new Error("Сервер недоступен");
    }

    if (res.status === 401) {
        localStorage.removeItem("alpha-auth");
        if (window.App) {
            try { window.App.render(); } catch { }
        } else {
            location.reload();
        }
        throw new Error("Сессия истекла");
    }

    if (res.status === 204) return null;

    if (!res.ok) {
        let detail = `HTTP ${res.status}`;
        try {
            const data = await res.json();
            detail = data.detail || data.title || data.error || detail;
        } catch {
            try {
                const text = await res.text();
                if (text) detail = text;
            } catch { }
        }
        throw new Error(detail);
    }

    const ct = res.headers.get("content-type") || "";
    if (!ct.includes("application/json")) return res;
    return res.json();
}

const api = {
    login: (login, password) =>
        apiFetch("/api/v1/auth/login", {
            method: "POST",
            body: JSON.stringify({ login, password }),
        }),

    logout: () =>
        apiFetch("/api/v1/auth/logout", { method: "POST" }),

    me: () =>
        apiFetch("/api/v1/auth/me"),

    homeworks: (params = {}) => {
        const clean = Object.fromEntries(
            Object.entries(params).filter(
                ([, v]) => v !== undefined && v !== null && v !== ""
            )
        );
        const qs = new URLSearchParams(clean).toString();
        return apiFetch(`/api/v1/homeworks${qs ? "?" + qs : ""}`);
    },

    homework: (id) =>
        apiFetch(`/api/v1/homeworks/${id}`),

    submitHomework: async (id, text, files) => {
        const form = new FormData();
        if (text) form.append("text", text);
        (files || []).forEach((f) => form.append("files", f));

        const res = await fetch(`${API_BASE}/api/v1/homeworks/${id}/submit`, {
            method: "POST",
            credentials: "include",
            body: form,
        });

        if (res.status === 401) {
            localStorage.removeItem("alpha-auth");
            throw new Error("Сессия истекла");
        }
        if (!res.ok) {
            let detail = `HTTP ${res.status}`;
            try {
                const data = await res.json();
                detail = data.detail || data.title || data.error || detail;
            } catch { }
            throw new Error(detail);
        }
        return res.status === 204 ? null : res.json();
    },

    gradeHomework: (id, grade, comment) =>
        apiFetch(`/api/v1/homeworks/${id}/grade`, {
            method: "POST",
            body: JSON.stringify({ grade, comment: comment || null }),
        }),

    deleteHomework: (id) =>
        apiFetch(`/api/v1/homeworks/${id}`, { method: "DELETE" }),

    archiveUrl: (homeworkId) =>
        apiFetch(`/api/v1/homeworks/${homeworkId}/archive`),

    archiveEntries: (homeworkId) =>
        apiFetch(`/api/v1/homeworks/${homeworkId}/archive/entries`),

    archiveEntry: (homeworkId, path) =>
        fetch(
            `${API_BASE}/api/v1/homeworks/${homeworkId}/archive/entries/${encodeURI(path)}`,
            { credentials: "include" }
        ),

    file: (id) =>
        apiFetch(`/api/v1/files/${id}`),

    fileUrl: (id) =>
        apiFetch(`/api/v1/files/${id}/url`),

    deleteFile: (id) =>
        apiFetch(`/api/v1/files/${id}`, { method: "DELETE" }),

    users: {
        list: (params = {}) => {
            const clean = Object.fromEntries(
                Object.entries(params).filter(
                    ([, v]) => v !== undefined && v !== null && v !== ""
                )
            );
            const qs = new URLSearchParams(clean).toString();
            return apiFetch(`/api/v1/users${qs ? "?" + qs : ""}`);
        },
        create: (data) =>
            apiFetch("/api/v1/users", {
                method: "POST",
                body: JSON.stringify(data),
            }),
        update: (id, data) =>
            apiFetch(`/api/v1/users/${id}`, {
                method: "PUT",
                body: JSON.stringify(data),
            }),
        remove: (id) =>
            apiFetch(`/api/v1/users/${id}`, { method: "DELETE" }),
    },

    disciplines: {
        list: (params = {}) => {
            const clean = Object.fromEntries(
                Object.entries(params).filter(
                    ([, v]) => v !== undefined && v !== null && v !== ""
                )
            );
            const qs = new URLSearchParams(clean).toString();
            return apiFetch(`/api/v1/disciplines${qs ? "?" + qs : ""}`);
        },
        create: (data) =>
            apiFetch("/api/v1/disciplines", {
                method: "POST",
                body: JSON.stringify(data),
            }),
        update: (id, data) =>
            apiFetch(`/api/v1/disciplines/${id}`, {
                method: "PUT",
                body: JSON.stringify(data),
            }),
        remove: (id) =>
            apiFetch(`/api/v1/disciplines/${id}`, { method: "DELETE" }),
    },

    healthLive: () => apiFetch("/health/live"),
    healthReady: () => apiFetch("/health/ready"),


};

let _hub = null;

async function startSignalR(onHomeworkSubmitted, onHomeworkGraded) {
    if (_hub) return _hub;
    if (typeof signalR === "undefined") {
        console.warn("SignalR не загружен");
        return null;
    }

    _hub = new signalR.HubConnectionBuilder()
        .withUrl(`${API_BASE}/hubs/notifications`, { withCredentials: true })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    if (onHomeworkSubmitted) {
        _hub.on("HomeworkSubmitted", (payload) => {
            try { onHomeworkSubmitted(payload); } catch (e) { console.error(e); }
        });
    }

    if (onHomeworkGraded) {
        _hub.on("HomeworkGraded", (payload) => {
            try { onHomeworkGraded(payload); } catch (e) { console.error(e); }
        });
    }

    try {
        await _hub.start();
        console.info("SignalR подключён");
        return _hub;
    } catch (err) {
        console.warn("SignalR error:", err?.message || err);
        _hub = null;
        return null;
    }
}

async function stopSignalR() {
    if (!_hub) return;
    try { await _hub.stop(); } catch { }
    _hub = null;
}

async function downloadResponseAsFile(response, fileName) {
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName || "download";
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

function openPresignedUrl(url) {
    if (!url) return;
    window.open(url, "_blank", "noopener");
}