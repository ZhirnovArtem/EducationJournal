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
    // ── Auth ──
    login: (login, password) =>
        apiFetch("/api/v1/auth/login", {
            method: "POST",
            body: JSON.stringify({ login, password }),
        }),

    logout: () =>
        apiFetch("/api/v1/auth/logout", { method: "POST" }),

    me: () =>
        apiFetch("/api/v1/auth/me"),

    updateMe: (data) =>
        apiFetch("/api/v1/auth/me", { method: "PUT", body: JSON.stringify(data) }),

    // ── Домашние задания ──
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

    submissions: (params = {}) => {
        const clean = Object.fromEntries(
            Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== "")
        );
        const qs = new URLSearchParams(clean).toString();
        return apiFetch(`/api/v1/homeworks/submissions${qs ? "?" + qs : ""}`);
    },

    createHomeworkBatch: async (data, file) => {
        const form = new FormData();
        form.append("groupId", data.groupId);
        form.append("disciplineId", data.disciplineId);
        form.append("task", data.task || "");
        form.append("dateOfIssue", data.dateOfIssue);
        form.append("deadLine", data.deadLine);
        if (file) form.append("file", file);

        const res = await fetch(`${API_BASE}/api/v1/homeworks/batch`, {
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
        return res.json();
    },

    // ── Файлы ──
    file: (id) => apiFetch(`/api/v1/files/${id}`),
    fileUrl: (id) => apiFetch(`/api/v1/files/${id}/url`),
    deleteFile: (id) => apiFetch(`/api/v1/files/${id}`, { method: "DELETE" }),

    // ── Пользователи ──
    users: {
        list: (params = {}) => {
            const clean = Object.fromEntries(
                Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== "")
            );
            const qs = new URLSearchParams(clean).toString();
            return apiFetch(`/api/v1/users${qs ? "?" + qs : ""}`);
        },
        lookup: () => apiFetch("/api/v1/users/lookup"),
        create: (data) =>
            apiFetch("/api/v1/users", { method: "POST", body: JSON.stringify(data) }),
        update: (id, data) =>
            apiFetch(`/api/v1/users/${id}`, { method: "PUT", body: JSON.stringify(data) }),
        remove: (id) =>
            apiFetch(`/api/v1/users/${id}`, { method: "DELETE" }),
    },

    // ── Предметы ──
    disciplines: {
        list: (params = {}) => {
            const clean = Object.fromEntries(
                Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== "")
            );
            const qs = new URLSearchParams(clean).toString();
            return apiFetch(`/api/v1/disciplines${qs ? "?" + qs : ""}`);
        },
        create: (data) =>
            apiFetch("/api/v1/disciplines", { method: "POST", body: JSON.stringify(data) }),
        update: (id, data) =>
            apiFetch(`/api/v1/disciplines/${id}`, { method: "PUT", body: JSON.stringify(data) }),
        remove: (id) =>
            apiFetch(`/api/v1/disciplines/${id}`, { method: "DELETE" }),
    },

    // ── Группы ──
    groups: {
        list: (params = {}) => {
            const clean = Object.fromEntries(
                Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== "")
            );
            const qs = new URLSearchParams(clean).toString();
            return apiFetch(`/api/v1/groups${qs ? "?" + qs : ""}`);
        },
        create: (data) =>
            apiFetch("/api/v1/groups", { method: "POST", body: JSON.stringify(data) }),
        update: (id, data) =>
            apiFetch(`/api/v1/groups/${id}`, { method: "PUT", body: JSON.stringify(data) }),
        remove: (id) =>
            apiFetch(`/api/v1/groups/${id}`, { method: "DELETE" }),
    },

    // ── Студенты ──
    students: {
        list: (params = {}) => {
            const clean = Object.fromEntries(
                Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== "")
            );
            const qs = new URLSearchParams(clean).toString();
            return apiFetch(`/api/v1/students${qs ? "?" + qs : ""}`);
        },
        get: (id) => apiFetch(`/api/v1/students/${id}`),
        update: (id, data) =>
            apiFetch(`/api/v1/students/${id}`, { method: "PUT", body: JSON.stringify(data) }),
        setGroup: (id, groupId) =>
            apiFetch(`/api/v1/students/${id}/group`, {
                method: "PUT",
                body: JSON.stringify({ groupId }),
            }),
        remove: (id) => apiFetch(`/api/v1/students/${id}`, { method: "DELETE" }),
        grades: (studentId) =>
            apiFetch(`/api/v1/students/${studentId}/grades`),
        gradesSummary: (studentId) =>
            apiFetch(`/api/v1/students/${studentId}/grades/summary`),
    },

    // ── Преподаватели ──
    teachers: {
        list: (params = {}) => {
            const clean = Object.fromEntries(
                Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== "")
            );
            const qs = new URLSearchParams(clean).toString();
            return apiFetch(`/api/v1/teachers${qs ? "?" + qs : ""}`);
        },
        me: () => apiFetch("/api/v1/teachers/me"),
        get: (id) => apiFetch(`/api/v1/teachers/${id}`),
        update: (id, data) =>
            apiFetch(`/api/v1/teachers/${id}`, { method: "PUT", body: JSON.stringify(data) }),
        setDisciplines: (id, disciplineIds) =>
            apiFetch(`/api/v1/teachers/${id}/disciplines`, {
                method: "PUT",
                body: JSON.stringify({ disciplineIds }),
            }),
        remove: (id) => apiFetch(`/api/v1/teachers/${id}`, { method: "DELETE" }),
    },

    // ── Расписание ──
    schedule: {
        list: (params = {}) => {
            const clean = Object.fromEntries(
                Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== "")
            );
            const qs = new URLSearchParams(clean).toString();
            return apiFetch(`/api/v1/schedule${qs ? "?" + qs : ""}`);
        },
        get: (id) => apiFetch(`/api/v1/schedule/${id}`),
        lessonTimes: () => apiFetch("/api/v1/schedule/lesson-times"),
        create: (data) =>
            apiFetch("/api/v1/schedule", { method: "POST", body: JSON.stringify(data) }),
        update: (id, data) =>
            apiFetch(`/api/v1/schedule/${id}`, { method: "PUT", body: JSON.stringify(data) }),
        remove: (id) => apiFetch(`/api/v1/schedule/${id}`, { method: "DELETE" }),

        // Журнал оценок (gradebook)
        grades: (monthScheduleId) =>
            apiFetch(`/api/v1/schedule/${monthScheduleId}/grades`),
        saveGrades: (monthScheduleId, marks) =>
            apiFetch(`/api/v1/schedule/${monthScheduleId}/grades`, {
                method: "POST",
                body: JSON.stringify({ marks }),
            }),
    },
    chats: {
        list: () => apiFetch("/api/v1/chats"),
        create: (title) => apiFetch("/api/v1/chats", { method: "POST", body: JSON.stringify({ title }) }),
        messages: (chatId) => apiFetch(`/api/v1/chats/${chatId}/messages`),
        send: (chatId, text) => apiFetch(`/api/v1/chats/${chatId}/messages`, { method: "POST", body: JSON.stringify({ text }) }),

        // Эти два обязательно должны быть:
        members: (chatId) => apiFetch(`/api/v1/chats/${chatId}/members`),
        invite: (chatId, userId) => apiFetch(`/api/v1/chats/${chatId}/members`, {
            method: "POST", body: JSON.stringify({ userId })
        }),f
    },
    ide: {
        submit: (code, language = "python", teacherId = null) =>
            apiFetch("/api/v1/ide/submit", {
                method: "POST",
                body: JSON.stringify({ code, language, teacherId }),
            }),
        submissions: () => apiFetch("/api/v1/ide/submissions"),
        teachers: () => apiFetch("/api/v1/ide/teachers"), // <-- Новый метод
    },
    // ── Посещаемость ──
    attendance: {
        sheet: (monthScheduleId) => apiFetch(`/api/v1/attendance/${monthScheduleId}`),
        save: (scheduleId, marks) =>
            apiFetch(`/api/v1/attendance/${scheduleId}`, {
                method: "POST",
                body: JSON.stringify({ marks }),
            }),
    },

    // ── Дашборд ──
    dashboard: () => apiFetch("/api/v1/dashboard"),

    // ── Health ──
    healthLive: () => apiFetch("/health/live"),
    healthReady: () => apiFetch("/health/ready"),
};

// ══════════════════════════════════════════════
// SignalR (уведомления + совместная доска)
// ══════════════════════════════════════════════

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
    window._hub = _hub;

    // ── Обработчики уведомлений ──
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

    // Внутри startSignalR, где вы подписываетесь на события
    _hub.on("ReceiveNotification", (payload) => {
        console.log("[SignalR] ReceiveNotification:", payload);
        try { window._onNotification?.(payload); } catch (e) { console.error(e); }
    });

    // ── Обработчики доски ──
    _hub.on("StrokeReceived", (stroke) => {
        try { window._boardOnStroke?.(stroke); } catch (e) { console.error(e); }
    });
    _hub.on("BoardCleared", () => {
        try { window._boardOnClear?.(); } catch (e) { console.error(e); }
    });
    _hub.on("CursorMoved", (connId, x, y) => {
        try { window._boardOnCursor?.(connId, x, y); } catch (e) { console.error(e); }
    });
    _hub.on("BoardPermissionsUpdated", (payload) => {
        try { window._boardOnPermissions?.(payload); } catch (e) { console.error(e); }
    });
    _hub.on("MessageReceived", (payload) => {
        console.log("[SignalR] MessageReceived:", payload);
        try { window._onChatMessage?.(payload); } catch (e) { console.error(e); }
    });

    _hub.on("ChatMessageReceived", (payload) => {
        console.log("[SignalR] ChatMessageReceived:", payload);
        try { window._onChatMessage?.(payload); } catch (e) { console.error(e); }
    });
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

// ── Утилиты для работы с доской ──
function joinBoard(roomId, userId, role) {
    if (_hub) _hub.invoke("JoinBoard", roomId, userId, role).catch(() => { });
}
function leaveBoard(roomId) {
    if (_hub) _hub.invoke("LeaveBoard", roomId).catch(() => { });
}
function sendBoardStroke(roomId, stroke) {
    if (_hub) _hub.invoke("DrawStroke", roomId, stroke).catch(() => { });
}
function sendBoardClear(roomId) {
    if (_hub) _hub.invoke("ClearBoard", roomId).catch(() => { });
}
function sendBoardCursor(roomId, x, y) {
    if (_hub) _hub.invoke("MoveCursor", roomId, x, y).catch(() => { });
}
function setDrawingPermission(roomId, targetUserId, allowed) {
    if (_hub) _hub.invoke("SetDrawingPermission", roomId, targetUserId, allowed).catch(() => { });
}
function setFreeDrawMode(roomId, free) {
    if (_hub) _hub.invoke("SetFreeDrawMode", roomId, free).catch(() => { });
}

// ── Утилиты ──
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