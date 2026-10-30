const App = (() => {
    const state = {
        arrowTheme: localStorage.getItem("alpha-arrow-theme") === "1",
        brandClicks: 0,
        brandClickTimer: null,
        paintClicks: 0,
        paintClickTimer: null,
        kidsTheme: localStorage.getItem("alpha-67kids-theme") === "1",
        route: location.hash.slice(1) || "dashboard",
        role: localStorage.getItem("alpha-role") || "student",
        theme: localStorage.getItem("alpha-theme") || "system",
        sidebar: false,
        user: null,
        ideLanguage: localStorage.getItem("alpha-ide-language") || "python",

        currentRoomId: null,

        //Главная страница
        dashboard: null,
        dashboardLoading: false,
        dashboardError: null,
        dashboardLoaded: false,

        // ── Пользователи ──
        users: [],
        usersLoading: false,
        usersError: null,
        usersLoaded: false,

        // ── Предметы ──
        disciplines: [],
        disciplinesLoading: false,
        disciplinesError: null,
        disciplinesLoaded: false,
        //Оценки
        gradesItems: [],
        gradesSummary: null,
        gradesLoading: false,
        gradesError: null,
        gradesLoaded: false,

        // ── Классы (группы) ──
        groups: [],
        groupsLoading: false,
        groupsError: null,
        groupsLoaded: false,

        // ── Студенты ──
        students: [],
        studentsLoading: false,
        studentsError: null,
        studentsLoaded: false,

        // ── Преподаватели ──
        teachers: [],
        teachersLoading: false,
        teachersError: null,
        teachersLoaded: false,

        // ── Расписание ──
        schedule: [],
        scheduleLoading: false,
        scheduleError: null,
        scheduleLoaded: false,
        scheduleMonth: (() => {
            const now = new Date();
            return { year: now.getFullYear(), month: now.getMonth() };
        })(),
        lessonTimes: [],
        lessonTimesLoaded: false,

        // ── Посещаемость ──
        attendanceLessons: [],
        attendanceLoading: false,
        attendanceError: null,
        attendanceLoaded: false,

        // ── Домашки ──
        homeworks: [],
        homeworksLoading: false,
        homeworksError: null,
        homeworksLoaded: false,

        // ── Чаты ──
        chats: [],
        chatsLoading: false,
        chatsLoaded: false,
        currentChatId: null,
        chatMessages: [],
        chatMessagesLoading: false,

        // ── Создание ДЗ (форма) ──
        createHomeworkSaving: false,

        submissions: [],
        submissionsLoading: false,
        submissionsError: null,
        submissionsLoaded: false,
        submissionFilter: "pending",

        gradebookLessons: [],
        gradebookLoading: false,
        gradebookLoaded: false,
        gradebookDate: null,
    };

    const ROLES = {
        student: { id: 3, label: "Ученик" },
        teacher: { id: 2, label: "Преподаватель" },
        admin: { id: 1, label: "Администратор" },
    };

    const ROLE_BY_ID = Object.fromEntries(
        Object.entries(ROLES).map(([code, r]) => [r.id, code])
    );

    const CORE_NAV = [
        ["dashboard", "⌂", "Главная"],
        ["schedule", "▦", "Расписание"],
    ];

    const TAIL_NAV = [
        ["profile", "●", "Личный кабинет"],
    ];

    const nav = {
        student: [
            ...CORE_NAV,
            ["grades", "★", "Оценки"],
            ["homework", "✓", "Домашние задания"],
            ["video", "📹", "Видеоурок"],
            ["ide", "</>", "IDE"],
            ...TAIL_NAV,
        ],
        teacher: [
            ...CORE_NAV,
            ["create-homework", "＋", "Создание ДЗ"],
            ["check-homework", "✓", "Проверка ДЗ"],
            ["ide-submissions", "📥", "Присланные коды"],
            ["paint", "🎨", "Paint"],
            ["video", "📹", "Видеоурок"],
            ["grade-homework", "★", "Оценивание ДЗ"],
            ["gradebook", "▤", "Выставление оценок"],
            ["attendance", "◉", "Посещаемость"],
            ...TAIL_NAV,
        ],
        admin: [
            ...CORE_NAV,
            ["users", "♙", "Пользователи"],
            ["classes", "▥", "Классы"],
            ["subjects", "◈", "Предметы"],
            ["students", "♙", "Студенты"],
            ["teachers", "♤", "Преподаватели"],
            ...TAIL_NAV,
        ],
    };

    const FORBIDDEN = {
        student: new Set([
            "create-homework", "check-homework", "grade-homework",
            "gradebook", "attendance", "users", "classes", "subjects",
        ]),
        teacher: new Set(["users", "classes", "subjects"]),
        admin: new Set(),
    };

    const canAccess = (role, route) => !(FORBIDDEN[role]?.has(route));

    function applyTheme() {
        const dark =
            state.theme === "dark" ||
            (state.theme === "system" &&
                matchMedia("(prefers-color-scheme: dark)").matches);
        document.documentElement.dataset.theme = dark ? "dark" : "light";
        document.documentElement.classList.toggle("arrow-theme", !!state.arrowTheme);
        document.documentElement.classList.toggle("kids67-theme", !!state.kidsTheme);
    }
    matchMedia("(prefers-color-scheme: dark)").addEventListener?.("change", applyTheme);
    applyTheme();

    const THEME_ORDER = ["light", "dark", "system"];
    const THEME_LABEL = { light: "Светлая", dark: "Тёмная", system: "Система" };

    function setTheme(theme) {
        state.theme = theme;
        localStorage.setItem("alpha-theme", theme);
        applyTheme();
        render();
    }

    function nextTheme() {
        return THEME_ORDER[(THEME_ORDER.indexOf(state.theme) + 1) % THEME_ORDER.length];
    }

    const esc = (s) =>
        String(s).replace(/[&<>"']/g, (m) =>
        ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" }[m])
        );

    function pad2(n) { return String(n).padStart(2, "0"); }

    function toIsoDate(y, m, d) {
        return `${y}-${pad2(m + 1)}-${pad2(d)}`;
    }

    function formatDateRu(y, m, d) {
        return `${pad2(d)}.${pad2(m + 1)}.${y}`;
    }

    function monthTitle(y, m) {
        const months = [
            "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь",
            "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь",
        ];
        return `${months[m]} ${y}`;
    }

    function daysInMonth(y, m) {
        return new Date(y, m + 1, 0).getDate();
    }

    function firstWeekdayMondayBased(y, m) {
        const wd = new Date(y, m, 1).getDay();
        return (wd + 6) % 7;
    }

    const el = (tag, attrs = {}, children = []) => {
        const e = document.createElement(tag);
        Object.entries(attrs).forEach(([k, v]) =>
            k === "class"
                ? (e.className = v)
                : k === "text"
                    ? (e.textContent = v)
                    : e.setAttribute(k, v)
        );
        children.forEach((c) => e.append(c));
        return e;
    };

    function toast(msg) {
        const r = document.getElementById("toast-root");
        const t = el("div", { class: "toast", text: msg });
        r.append(t);
        setTimeout(() => t.remove(), 2800);
    }

    const MODAL_IDS = ["hw-modal", "user-modal", "hw-modal", "user-modal", "gradebook-modal", "attendance-modal",];
    const closeModal = (id) => document.getElementById(id)?.remove();

    function modal({ id, title, body, actions = "", closeAction }) {
        return `
        <div class="modal-backdrop" id="${id}">
          <div class="modal">
            <div class="modal-head">
              <h3 style="margin:0">${esc(title)}</h3>
              <button class="icon-btn" data-action="${closeAction}" title="Закрыть">✕</button>
            </div>
            ${body}
            ${actions ? `<div class="modal-actions">${actions}</div>` : ""}
          </div>
        </div>`;
    }

    async function afterAction(modalId, toastMsg, reloadFn) {
        toast(toastMsg);
        if (modalId) closeModal(modalId);
        if (reloadFn) await reloadFn();
        render();
    }

    async function runCode() {
        if (!window._pyodide) {
            window._pyodide = await loadPyodide();
        }
        const code = window._aceEditor.getValue();
        try {
            await window._pyodide.runPythonAsync(code);
        } catch (err) {
            toast("Ошибка выполнения: " + err.message);
        }
    }

    function setRoute(route) {
        state.route = route;
        location.hash = route;
        state.sidebar = false;
        render();
    }
    window.addEventListener("hashchange", () => {
        state.route = location.hash.slice(1) || "dashboard";
        render();
    });

    const data = {
        lessons: [
            ["08:30", "Математика", "Каб. 204", "Иванова Е.В.", "lecture"],
            ["10:15", "Программирование", "Лаб. 301", "Смирнов А.А.", "practice"],
            ["12:10", "Физика", "Каб. 118", "Петров И.И.", "lecture"],
            ["14:00", "Контрольная работа", "Каб. 204", "Иванова Е.В.", "test"],
        ],
        grades: [
            ["01.10.2026", "Математика", "5", "Контрольная", "Отличная работа"],
            ["29.09.2026", "Программирование", "5", "Практика", "Все тесты пройдены"],
            ["27.09.2026", "Физика", "4", "Устный ответ", "Хороший ответ"],
            ["25.09.2026", "Математика", "4", "Домашняя", "Есть небольшие ошибки"],
        ],
        students: [
            "Алексей Иванов",
            "Мария Петрова",
            "Дмитрий Соколов",
            "Анна Кузнецова",
            "Илья Морозов",
            "Елена Волкова",
        ],
    };

    function hwStatus(h) {
        if (h.grade) return { badge: "success", label: `Оценено: ${h.grade}` };
        if (h.dueDate) return { badge: "primary", label: "На проверке" };
        if (new Date(h.deadLine) < new Date()) return { badge: "danger", label: "Просрочено" };
        return { badge: "warning", label: "Не сдано" };
    }
    const statusBadge = (h) => hwStatus(h).badge;
    const statusLabel = (h) => hwStatus(h).label;

    function layout(title, body) {
        const items = nav[state.role] || nav.student;
        if (!items.some(x => x[0] === "communications")) items.splice(Math.max(0, items.length - 1), 0, ["communications", "◉", "Чаты и созвоны"]);
        const userLabel = state.user?.login || "Легионер";
        const initial = (userLabel[0] || "Л").toUpperCase();

        return `<div class="app-shell">
  <aside class="sidebar ${state.sidebar ? "open" : ""}">
    <div class="brand" data-action="brand-click" title="Нажмите 5 раз для секретной темы"><div class="logo">A</div><span>Project Alpha</span><svg class="green-arrow-mark" viewBox="0 0 64 64" aria-label="Зелёная стрела" role="img"><path d="M8 54 46 16M35 14l15-5-5 15M8 54l4-15 11 11" fill="none" stroke="currentColor" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/><path d="M30 30 43 43M36 24l13 13" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round"/></svg></div>
    <div class="nav-label">${ROLES[state.role]?.label || ""}</div>
    ${items
                .map(
                    ([r, ic, n]) =>
                        `<button class="nav-btn ${state.route === r ? "active" : ""}" data-route="${r}"><span>${ic}</span>${n}</button>`
                )
                .join("")}
    <div class="sidebar-bottom">
      <button class="nav-btn" data-action="theme">◐ Тема: ${THEME_LABEL[state.theme]}</button>
      <button class="nav-btn" data-action="logout">↪ Выйти</button>
    </div>
  </aside>
  <main class="main">
    <header class="topbar">
      <div class="topbar-left">
        <button class="icon-btn menu-toggle" data-action="menu">☰</button>
        <div class="page-title">${title}</div>
      </div>
      <div class="top-actions">
        <button class="icon-btn" data-action="theme" title="Переключить тему">◐</button>
        <button class="avatar" data-route="profile">${esc(initial)}</button>
      </div>
    </header>
    <section class="content">${body}</section>
  </main>
</div>`;
    }

    function lesson(a) {
        const typeMap = {
            test: { badge: "danger", label: "Контрольная" },
            practice: { badge: "success", label: "Практика" },
            lecture: { badge: "primary", label: "Лекция" },
        };
        const t = typeMap[a[4]] || typeMap.lecture;
        return `<div class="lesson ${a[4]}">
  <div class="lesson-time">${a[0]}</div>
  <div>
    <div class="lesson-title">${a[1]}</div>
    <div class="lesson-meta">${a[2]} · ${a[3]}</div>
  </div>
  <span class="badge ${t.badge}">${t.label}</span>
</div>`;
    }

    function dashboard() {
        if (state.dashboardLoading) {
            return layout("Главная",
                `<div class="card"><div class="loading-row"><div class="spinner"></div><span>Загрузка…</span></div></div>`);
        }
        if (state.dashboardError) {
            return layout("Главная",
                `<div class="card">
                   <div class="muted">Ошибка: ${esc(state.dashboardError)}</div>
                   <button class="btn primary" data-action="reload-dashboard" style="margin-top:12px">Повторить</button>
                 </div>`);
        }

        const d = state.dashboard || {};
        const isStudent = state.role === "student";
        const isTeacher = state.role === "teacher";
        const isAdmin = state.role === "admin";

        const avgGrade = d.averageGrade != null ? Number(d.averageGrade).toFixed(1) : "—";
        let activeHw = d.activeHomeworks ?? 0;
        if (state.role === "teacher") {
            activeHw = state.submissions.filter(s => !s.grade).length;
        }
        const attendance = d.attendancePercent != null ? d.attendancePercent + "%" : "—";
        const rank = d.classRank != null ? `${d.classRank} место` : "—";

        const lessons = d.upcomingLessons || [];
        const activity = d.recentActivity || [];

        return layout("Главная", `
      <div class="grid grid-${isStudent ? "4" : "2"}">
        ${isStudent
                ? `
              <div class="card stat">
                <div><div class="muted">Средний балл</div>
                     <div class="stat-value">${avgGrade}</div></div>
                <div class="stat-icon">★</div>
              </div>
              <div class="card stat">
                <div><div class="muted">Активные ДЗ</div>
                     <div class="stat-value">${activeHw}</div></div>
                <div class="stat-icon">✓</div>
              </div>
              <div class="card stat">
                <div><div class="muted">Посещаемость</div>
                     <div class="stat-value">${attendance}</div></div>
                <div class="stat-icon">◉</div>
              </div>
              <div class="card stat">
                <div><div class="muted">Рейтинг в классе</div>
                     <div class="stat-value">${rank}</div></div>
                <div class="stat-icon">♙</div>
              </div>`
                : ""
            }
        ${isTeacher
                ? `
              <div class="card stat">
                <div><div class="muted">Заданий на проверке</div>
                     <div class="stat-value">${activeHw}</div></div>
                <div class="stat-icon">✓</div>
              </div>
              <div class="card stat">
                <div><div class="muted">Пар на сегодня</div>
                     <div class="stat-value">${d.todayLessonCount ?? 0}</div></div>
                <div class="stat-icon">▦</div>
              </div>`
                : ""
            }
      </div>
      <div class="grid grid-2" style="margin-top:18px">
        <div class="card"><h3>Ближайшие занятия</h3>
          <div class="schedule">
            ${lessons.length
                ? lessons.map(l => `
                    <div class="lesson">
                      <div class="lesson-time">${esc(l.startTime)}</div>
                      <div>
                        <div class="lesson-title">${esc(l.disciplineTitle)}</div>
                        <div class="lesson-meta">${esc(l.room || "")}${l.teacherName ? " · " + esc(l.teacherName) : ""}</div>
                      </div>
                    </div>`).join("")
                : `<div class="muted" style="padding:16px 0">Занятий не найдено.</div>`}
          </div>
        </div>
        <div class="card"><h3>Последние действия</h3>
          ${activity.length
                ? activity.map(a => `
                  <div style="padding:13px 0;border-bottom:1px solid var(--border)">
                    <b>${esc(a.text)}</b>
                    <div class="muted" style="font-size:12px;margin-top:4px">
                      ${new Date(a.at).toLocaleString("ru-RU")}
                    </div>
                  </div>`).join("")
                : `<div class="muted" style="padding:16px 0">Нет недавних действий.</div>`}
        </div>
      </div>`);
    }

    // Добавьте эту функцию где-нибудь рядом с другими page-функциями
    async function ideSubmissionsPage() {
        let submissions = [];
        try {
            const res = await apiFetch("/api/v1/ide/submissions");
            submissions = res.items || []; 
        } catch (err) {
            return layout("Присланные коды", `<div class="card">Ошибка загрузки: ${esc(err.message)}</div>`);
        }

        return layout("Присланные коды", `
        <div class="card">
            <div class="table-wrap">
                <table class="table">
                    <thead>
                        <tr><th>Студент</th><th>Дата</th><th>Язык</th><th></th></tr>
                    </thead>
                    <tbody>
                        ${submissions.length ? submissions.map(s => `
                            <tr>
                                <td><b>${esc(s.studentLogin)}</b></td>
                                <td>${new Date(s.submittedAt).toLocaleString("ru-RU")}</td>
                                <td>${esc(s.language)}</td>
                                <td>
                                    <button class="btn primary" 
                                            data-action="view-ide-code" 
                                            data-code="${btoa(unescape(encodeURIComponent(s.code)))}" 
                                            data-student="${esc(s.studentLogin)}">
                                        Посмотреть код
                                    </button>
                                </td>
                            </tr>`).join("")
                : `<tr><td colspan="4" class="muted" style="text-align:center;padding:32px">Кодов пока нет.</td></tr>`}
                    </tbody>
                </table>
            </div>
        </div>
    `);
    }

    async function loadChats() {
        state.chatsLoading = true;
        try {
            const res = await api.chats.list();
            state.chats = res.items || res || [];
        } catch (err) {
            toast("Ошибка чатов: " + (err.message || err));
            state.chats = [];
        } finally {
            state.chatsLoaded = true;
            state.chatsLoading = false;
        }
    }

    async function loadSchedule() {
        state.scheduleLoading = true;
        state.scheduleError = null;
        try {
            const y = state.scheduleMonth.year;
            const m = state.scheduleMonth.month;
            const from = toIsoDate(y, m, 1);
            const to = toIsoDate(y, m, daysInMonth(y, m));

            const params = { from, to };

            const items = await api.schedule.list(params);
            state.schedule = items || [];

            if (!state.lessonTimesLoaded) {
                state.lessonTimes = await api.schedule.lessonTimes();
                state.lessonTimesLoaded = true;
            }
        } catch (err) {
            state.scheduleError = err.message || String(err);
            state.schedule = [];
        } finally {
            state.scheduleLoaded = true;
            state.scheduleLoading = false;
        }
    }

    async function loadSubmissions() {
        state.submissionsLoading = true;
        state.submissionsError = null;
        try {
            const res = await api.submissions({ page: 1, pageSize: 50 });
            state.submissions = res.items || [];
        } catch (err) {
            state.submissionsError = err.message || String(err);
            state.submissions = [];
        } finally {
            state.submissionsLoaded = true;
            state.submissionsLoading = false;
        }
    }

    async function loadChatMessages(chatId) {
        state.chatMessagesLoading = true;
        state.currentChatId = chatId;
        try {
            const res = await api.chats.messages(chatId);
            state.chatMessages = res.items || [];
            const chat = state.chats.find(c => Number(c.chatId) === Number(chatId));
            if (chat) chat.messageCount = state.chatMessages.length;
            if (window._hub) {
                window._hub.invoke("JoinChat", chatId).catch(err =>
                    console.warn("JoinChat failed:", err)
                );
            }
        } catch (err) {
            toast("Ошибка сообщений: " + (err.message || err));
            state.chatMessages = [];
        } finally {
            state.chatMessagesLoading = false;
        }
    }

    async function loadDashboard() {
        state.dashboardLoading = true;
        state.dashboardError = null;
        try {
            state.dashboard = await api.dashboard();
        } catch (err) {
            state.dashboardError = err.message || String(err);
            state.dashboard = null;
        } finally {
            state.dashboardLoaded = true;
            state.dashboardLoading = false;
        }
    }
    function communicationsPage() {
        if (!state.chatsLoaded) {
            loadChats().then(render);
            return layout("Чаты", `<div class="card">Загрузка…</div>`);
        }

        const currentChat = state.chats.find(c => c.chatId === state.currentChatId);

        return layout("Чаты и созвоны", `
          <div style="display:grid;grid-template-columns:300px 1fr;gap:12px;height:75vh">
            <div class="card" style="padding:12px;display:flex;flex-direction:column">
              <div class="toolbar" style="margin:0 0 12px 0">
                <input class="input" id="new-chat-title" placeholder="Название нового чата" />
                <button class="btn primary" data-action="create-chat">＋ Создать</button>
              </div>
              <div style="overflow:auto;flex:1">
                ${state.chats.length
                ? state.chats.map(c => `
                        <div class="chat-item ${c.chatId === state.currentChatId ? 'active' : ''}"
                             data-action="open-chat" data-id="${c.chatId}"
                             style="padding:10px;border-radius:8px;cursor:pointer;margin-bottom:4px;${c.chatId === state.currentChatId ? 'background:var(--surface-2)' : ''}">
                          <b>${esc(c.title)}</b>
                          <div class="muted" style="font-size:12px">${c.messageCount} сообщений</div>
                        </div>`).join("")
                : `<div class="muted">Чатов пока нет. Создайте первый.</div>`}
              </div>
            </div>
            <div class="card" style="padding:0;display:flex;flex-direction:column">
              ${currentChat ? `
                <div style="padding:12px;border-bottom:1px solid var(--border);display:flex;justify-content:space-between;align-items:center">
                  <b>${esc(currentChat.title)}</b>
                  <button class="btn" data-action="open-invite-modal" data-id="${currentChat.chatId}">👤 Пригласить</button>
                </div>
                <div style="flex:1;overflow:auto;padding:12px" id="chat-messages-container">
                  ${state.chatMessages.length
                    ? state.chatMessages.map(m => `
                          <div style="margin-bottom:12px">
                            <b>${esc(m.userName)}</b>
                            <span class="muted" style="font-size:11px;margin-left:6px">${new Date(m.createdAt).toLocaleTimeString("ru-RU", { hour: "2-digit", minute: "2-digit" })}</span>
                            <div>${esc(m.text)}</div>
                          </div>`).join("")
                    : `<div class="muted">Сообщений пока нет. Напишите первое.</div>`}
                </div>
                <div style="padding:12px;border-top:1px solid var(--border);display:flex;gap:8px">
                  <input class="input" id="chat-message-input" placeholder="Написать сообщение…" style="flex:1" />
                  <button class="btn primary" data-action="send-message">Отправить ↗</button>
                </div>
              ` : `<div class="muted" style="padding:32px;text-align:center">Выберите чат слева</div>`}
            </div>
          </div>`);
    }

    async function openInviteModal(chatId) {
        const usersRes = await api.users.lookup();
        const allUsers = usersRes.items || [];
        const membersRes = await api.chats.members(chatId);
        const existingIds = new Set(membersRes.userIds || []);
        const availableUsers = allUsers.filter(u =>
            u.userId !== state.user.userId && !existingIds.has(u.userId)
        );

        if (!availableUsers.length) {
            toast("Все доступные пользователи уже в чате");
            return;
        }

        const body = `
          <div class="field">
            <label>Выберите пользователя</label>
            <select class="select" id="invite-user-select">
              <option value="">— выберите —</option>
              ${availableUsers.map(u => `<option value="${u.userId}">${esc(u.login)}</option>`).join("")}
            </select>
          </div>`;

        const actions = `
          <button class="btn" data-action="close-invite-modal">Отмена</button>
          <button class="btn primary" data-action="invite-submit" data-chat-id="${chatId}">Пригласить</button>`;

        document.body.insertAdjacentHTML("beforeend", modal({
            id: "invite-modal",
            title: "Пригласить в чат",
            closeAction: "close-invite-modal",
            body,
            actions,
        }));

        bind();
    }

    function schedule() {
        if (state.scheduleLoading) {
            return layout(
                "Расписание",
                `<div class="card"><div class="loading-row"><div class="spinner"></div><span>Загрузка…</span></div></div>`
            );
        }
        if (state.scheduleError) {
            return layout(
                "Расписание",
                `<div class="card">
            <div class="muted">Ошибка: ${esc(state.scheduleError)}</div>
            <button class="btn primary" data-action="reload-schedule" style="margin-top:12px">Повторить</button>
        </div>`
            );
        }

        const y = state.scheduleMonth.year;
        const m = state.scheduleMonth.month;
        const daysCount = daysInMonth(y, m);
        const firstWd = firstWeekdayMondayBased(y, m);
        const byDate = {};
        state.schedule.forEach((s) => {
            (byDate[s.date] = byDate[s.date] || []).push(s);
        });
        const cells = [];
        for (let i = 0; i < firstWd; i++) {
            cells.push(`<div class="day muted"></div>`);
        }
        const todayIso = new Date().toLocaleDateString("sv-SE");
        for (let d = 1; d <= daysCount; d++) {
            const iso = toIsoDate(y, m, d);
            const lessons = byDate[iso] || [];
            const isToday = iso === todayIso;
            cells.push(`
        <div class="day ${isToday ? "today" : ""}">
            <div class="day-num">${d}</div>
            ${lessons
                    .slice(0, 3)
                    .map(
                        (l) =>
                            `<div class="day-event" data-action="open-lesson" data-id="${l.monthScheduleId}" title="${esc(l.disciplineTitle)} · ${esc(l.groupTitle)}">
                            ${l.pairNumber}. ${esc(l.disciplineTitle)}
                        </div>`
                    )
                    .join("")}
            ${lessons.length > 3 ? `<div class="muted" style="font-size:11px;margin-top:4px">+${lessons.length - 3} ещё</div>` : ""}
        </div>`);
        }

        const canEdit = state.role === "admin" || state.role === "teacher";

        return layout(
            "Расписание",
            `<div class="toolbar">
        <button class="btn" data-action="schedule-prev">◀</button>
        <b>${monthTitle(y, m)}</b>
        <button class="btn" data-action="schedule-next">▶</button>
        <button class="btn" data-action="schedule-today">Сегодня</button>
        <div class="spacer"></div>
        ${canEdit ? `<button class="btn primary" data-action="add-lesson">＋ Добавить пару</button>` : ""}
    </div>
    <div class="card">
        <div class="calendar-wrap">
            <div class="calendar">
                ${["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"].map((x) => `<div class="day-head">${x}</div>`).join("")}
                ${cells.join("")}
            </div>
        </div>
    </div>`
        );
    }

    function grades() {
        if (state.gradesLoading) {
            return layout("Оценки",
                `<div class="card"><div class="loading-row"><div class="spinner"></div><span>Загрузка…</span></div></div>`);
        }
        if (state.gradesError) {
            return layout("Оценки",
                `<div class="card">
               <div class="muted">Ошибка: ${esc(state.gradesError)}</div>
               <button class="btn primary" data-action="reload-grades" style="margin-top:12px">Повторить</button>
             </div>`);
        }

        const summary = state.gradesSummary || {};
        const avg = summary.averageGrade != null ? Number(summary.averageGrade).toFixed(1) : "—";
        const byDiscipline = summary.byDiscipline || [];
        const items = state.gradesItems || [];

        return layout("Оценки", `
      <div class="grid grid-4">
        <div class="card stat">
          <div><div class="muted">Средний балл</div>
               <div class="stat-value">${avg}</div></div>
          <div class="stat-icon">★</div>
        </div>
        ${byDiscipline.slice(0, 3).map(d => `
          <div class="card">
            <div class="muted">${esc(d.discipline)}</div>
            <div class="stat-value">${Number(d.average).toFixed(1)}</div>
            <div class="progress">
              <span style="width:${Math.min(100, (d.average / 5) * 100)}%"></span>
            </div>
          </div>`).join("")}
      </div>
      <div class="card" style="margin-top:18px">
        <div class="toolbar">
          <h3 style="margin:0">История оценок</h3>
          <div class="spacer"></div>
          <select class="select" style="width:190px">
            <option>Все типы работ</option>
          </select>
        </div>
        <div class="table-wrap">
          <table class="table">
            <thead><tr><th>Дата</th><th>Предмет</th><th>Оценка</th><th>Тип работы</th><th>Комментарий</th></tr></thead>
            <tbody>
              ${items.length
                ? items.map(g => `
                    <tr>
                      <td>${esc(g.date)}</td>
                      <td><b>${esc(g.discipline)}</b></td>
                      <td><span class="badge ${g.gradeValue >= 4 ? "success" : g.gradeValue === 3 ? "warning" : "danger"}">${g.gradeValue}</span></td>
                      <td>${esc(g.workType)}</td>
                      <td class="muted">${esc(g.comment || "")}</td>
                    </tr>`).join("")
                : `<tr><td colspan="5" class="muted" style="text-align:center;padding:32px">Оценок пока нет.</td></tr>`}
            </tbody>
          </table>
        </div>
      </div>`);
    }

    function homework() {
        if (state.homeworksLoading) {
            return layout(
                "Домашние задания",
                `<div class="card"><div class="loading-row"><div class="spinner"></div><span>Загрузка…</span></div></div>`
            );
        }
        if (state.homeworksError) {
            return layout(
                "Домашние задания",
                `<div class="card">
            <div class="muted">Ошибка: ${esc(state.homeworksError)}</div>
            <button class="btn primary" data-action="reload-homeworks" style="margin-top:12px">Повторить</button>
        </div>`
            );
        }
        const disciplines = [...new Set(state.homeworks.map((h) => h.discipline))];

        return layout(
            "Домашние задания",
            `<div class="toolbar">
    <select class="select" style="width:220px" id="hw-filter">
      <option value="">Все предметы</option>
      ${disciplines.map((d) => `<option>${esc(d)}</option>`).join("")}
    </select>
    <div class="spacer"></div>
    <button class="btn" data-action="reload-homeworks">Обновить</button>
  </div>
  <div class="grid grid-3">
    ${state.homeworks
                .map(
                    (h) => `<div class="card">
          <div class="muted">${esc(h.discipline)}</div>
          <h3 style="margin-top:6px">${esc(h.task)}</h3>
          <div class="muted">Дедлайн: ${esc(h.deadLine)}</div>
          ${h.taskFileUrl
                            ? `<div style="margin-top:10px">
                   <a href="${esc(h.taskFileUrl)}" target="_blank" rel="noopener"
                      style="display:inline-flex;align-items:center;gap:6px;color:#4a7dff;text-decoration:none;font-weight:500">
                     📎 Файл задания от преподавателя
                   </a>
                 </div>`
                            : ""
                        }
          <div style="margin:14px 0"><span class="badge ${statusBadge(h)}">${statusLabel(h)}</span></div>
          <button class="btn primary" data-action="open-homework" data-id="${h.homeworkId}">Открыть задание</button>
        </div>`
                )
                .join("")}
  </div>`
        );
    }

    function teacherTable(title, action = "") {
        const actionLabel = action === "publish" ? "Опубликовать оценки" : "Добавить";
        return layout(
            title,
            `<div class="toolbar">
    <select class="select" style="width:200px">
      <option>10-А класс</option><option>10-Б класс</option>
    </select>
    <select class="select" style="width:200px">
      <option>Программирование</option><option>Математика</option>
    </select>
    <div class="spacer"></div>
    ${action ? `<button class="btn primary" data-action="${action}">${actionLabel}</button>` : ""}
  </div>
  <div class="card">
    <div class="table-wrap">
      <table class="table">
        <thead>
          <tr><th>Ученик</th><th>Статус</th><th>Дата</th><th>Оценка</th><th>Комментарий</th><th></th></tr>
        </thead>
        <tbody>
          ${data.students
                .map(
                    (n, i) =>
                        `<tr>
                  <td><b>${n}</b></td>
                  <td><span class="badge ${i % 3 === 0 ? "warning" : "success"}">${i % 3 === 0 ? "На проверке" : "Проверено"}</span></td>
                  <td>0${i + 1}.10.2026</td>
                  <td><input class="input" style="width:80px" value="${i % 3 === 0 ? "" : 5}" placeholder="—"></td>
                  <td><input class="input" value="${i % 2 ? "Хорошая работа" : "Требуется доработка"}"></td>
                  <td><button class="btn" data-action="save">Сохранить</button></td>
                </tr>`
                )
                .join("")}
        </tbody>
      </table>
    </div>
  </div>`
        );
    }

    async function ensureDictionariesLoaded() {
        if (!state.groupsLoaded) await loadGroups();
        if (!state.disciplinesLoaded) await loadDisciplines();
    }

    function createHomework() {
        if (!state.groupsLoaded || !state.disciplinesLoaded) {
            ensureDictionariesLoaded().then(() => {
                if (state.route === "create-homework") render();
            });
            return layout(
                "Создание ДЗ",
                `<div class="card"><div class="loading-row"><div class="spinner"></div><span>Загрузка справочников…</span></div></div>`
            );
        }

        const today = new Date().toLocaleDateString("sv-SE");
        const d = new Date(Date.now() + 7 * 86400000);
        const weekAhead = d.toLocaleDateString("sv-SE");

        return layout(
            "Создание ДЗ",
            `<div class="grid grid-2">
        <div class="card">
            <h3>Новое домашнее задание</h3>

            <div class="field"><label>Группа</label>
                <select class="select" id="hw-group">
                    <option value="">— выберите группу —</option>
                    ${state.groups
                .map(
                    (g) =>
                        `<option value="${g.groupId}">${esc(g.title)} (${g.studentCount} студ.)</option>`
                )
                .join("")}
                </select>
            </div>

            <div class="field"><label>Предмет</label>
                <select class="select" id="hw-discipline">
                    <option value="">— выберите предмет —</option>
                    ${state.disciplines
                .map(
                    (d) =>
                        `<option value="${d.disciplineId}">${esc(d.title)}</option>`
                )
                .join("")}
                </select>
            </div>

            <div class="grid grid-2">
                <div class="field"><label>Дата выдачи</label>
                    <input class="input" type="date" id="hw-issue" value="${today}">
                </div>
                <div class="field"><label>Дедлайн</label>
                    <input class="input" type="date" id="hw-deadline" value="${weekAhead}">
                </div>
            </div>

            <div class="field">
                <label>Текст задания <span class="muted">(можно оставить пустым, если приложен файл)</span></label>
                <textarea class="textarea" id="hw-task" placeholder="Опишите задание…" style="min-height:140px"></textarea>
            </div>

            <div class="field">
                <label>Файл с заданием <span class="muted">(можно не прикладывать, если есть текст)</span></label>
                <input type="file" id="hw-file">
            </div>

            <div class="muted" style="font-size:12px;margin-top:-8px;margin-bottom:12px">
                Достаточно заполнить <b>любое из двух</b>: текст или файл.
            </div>

            <div style="margin-top:16px;display:flex;gap:8px">
                <button class="btn primary" data-action="publish-homework"
                        ${state.createHomeworkSaving ? "disabled" : ""}>
                    ${state.createHomeworkSaving ? "Публикуем…" : "Опубликовать"}
                </button>
            </div>
        </div>

        <div class="card">
            <h3>Как это работает</h3>
            <p class="muted" style="line-height:1.6">
                Задание создаётся <b>сразу для всех студентов</b> выбранной группы.
                Каждый студент получит отдельную запись в своём дневнике.
                Дедлайн — крайний срок, после которого задание становится «просроченным».
            </p>
            <p class="muted" style="line-height:1.6">
                Приложенный файл будет доступен студентам для скачивания.
                Отменить задание можно в разделе <b>«Домашние задания»</b>.
            </p>
        </div>
    </div>`
        );
    }

    async function publishHomework() {
        const groupId = parseInt(document.getElementById("hw-group")?.value || "0", 10);
        const disciplineId = parseInt(document.getElementById("hw-discipline")?.value || "0", 10);
        const task = document.getElementById("hw-task")?.value?.trim() || "";
        const dateOfIssue = document.getElementById("hw-issue")?.value;
        const deadLine = document.getElementById("hw-deadline")?.value;
        const file = document.getElementById("hw-file")?.files?.[0] || null;
        if (!groupId) { toast("Выберите группу"); return; }
        if (!disciplineId) { toast("Выберите предмет"); return; }
        if (!dateOfIssue || !deadLine) { toast("Заполните даты"); return; }
        if (new Date(deadLine) < new Date(dateOfIssue)) {
            toast("Дедлайн не может быть раньше даты выдачи");
            return;
        }
        if (!task && !file) {
            toast("Добавьте текст задания или приложите файл");
            return;
        }

        state.createHomeworkSaving = true;
        render();

        try {
            const res = await api.createHomeworkBatch(
                { groupId, disciplineId, task, dateOfIssue, deadLine },
                file
            );
            toast(`✓ Задание создано для ${res.created} студентов`);
            state.createHomeworkSaving = false;
            const taskEl = document.getElementById("hw-task");
            if (taskEl) taskEl.value = "";
            const fileEl = document.getElementById("hw-file");
            if (fileEl) fileEl.value = "";

            render();
        } catch (err) {
            state.createHomeworkSaving = false;
            toast("Ошибка: " + (err.message || err));
            render();
        }
    }

    async function loadAttendanceToday() {
        state.attendanceLoading = true;
        state.attendanceError = null;
        try {
            const today = new Date();
            const from = toIsoDate(today.getFullYear(), today.getMonth(), today.getDate());
            const to = from;

            const items = await api.schedule.list({ from, to });
            state.attendanceLessons = items || [];
        } catch (err) {
            state.attendanceError = err.message || String(err);
            state.attendanceLessons = [];
        } finally {
            state.attendanceLoaded = true;
            state.attendanceLoading = false;
        }
    }

    function attendance() {
        if (state.attendanceLoading) {
            return layout(
                "Посещаемость",
                `<div class="card"><div class="loading-row"><div class="spinner"></div><span>Загрузка…</span></div></div>`
            );
        }
        if (state.attendanceError) {
            return layout(
                "Посещаемость",
                `<div class="card">
            <div class="muted">Ошибка: ${esc(state.attendanceError)}</div>
            <button class="btn primary" data-action="reload-attendance" style="margin-top:12px">Повторить</button>
        </div>`
            );
        }

        return layout(
            "Посещаемость",
            `<div class="toolbar">
        <div class="spacer"></div>
        <button class="btn" data-action="reload-attendance">Обновить</button>
    </div>
    <div class="card">
        <h3>Занятия на сегодня</h3>
        ${state.attendanceLessons.length
                ? state.attendanceLessons
                    .map(
                        (l) => `<div class="lesson ${l.lessonType === 'Practice' ? 'practice' : l.lessonType === 'Test' ? 'test' : ''}" style="margin-bottom:8px">
                              <div class="lesson-time">${l.pairNumber}. ${l.startTime}–${l.endTime}</div>
                              <div>
                                  <div class="lesson-title">${esc(l.disciplineTitle)}</div>
                                  <div class="lesson-meta">${esc(l.groupTitle)}${l.room ? " · " + esc(l.room) : ""}</div>
                              </div>
                              <button class="btn primary" data-action="open-attendance" data-id="${l.monthScheduleId}">Отметить</button>
                          </div>`
                    )
                    .join("")
                : `<div class="muted" style="padding:32px;text-align:center">На сегодня занятий нет.</div>`
            }
    </div>`
        );
    }

    function ide() {
        const currentLang = "python";

        return layout(
            "Внутренняя IDE",
            `<div class="toolbar">
    <b>Редактор кода</b>
    <div class="spacer"></div>
    
    <!-- Выпадающий список преподавателей -->
    <select class="select" id="ide-teacher-select" style="width: 220px; margin-right: 8px;">
        <option value="">Загрузка...</option>
    </select>

    <button class="btn primary" data-action="ide-run" style="margin-left: 4px;">
        ▶ Запустить
    </button>
    
    <button class="btn success" data-action="ide-submit" style="margin-left: 8px;">
        📤 Отправить преподавателю
    </button>
</div>

<!-- Контейнер для разделенного экрана -->
<div style="display: flex; gap: 12px; height: 75vh; margin-top: 12px;">
    
    <!-- Левая колонка: Редактор -->
    <div class="card" style="flex: 2; padding:0; overflow:hidden; position: relative;">
        <div id="ace-editor-container" style="width:100%; height:100%;"></div>
    </div>

    <!-- Правая колонка: Консоль вывода -->
    <div class="card" style="flex: 1; padding:0; display: flex; flex-direction: column; background: #1e1e1e;">
        <div style="padding: 10px 12px; background: #2d2d2d; color: #ccc; font-size: 13px; font-weight: bold; border-bottom: 1px solid #333; display: flex; justify-content: space-between; align-items: center;">
            <span>Консоль вывода</span>
            <button class="btn" data-action="ide-clear-console" style="padding: 2px 8px; font-size: 11px; background: #444; color: #fff; border: none;">Очистить</button>
        </div>
        <div id="ide-output" style="padding: 12px; overflow-y: auto; flex: 1; font-family: 'Consolas', 'Courier New', monospace; font-size: 13px; white-space: pre-wrap; color: #4ade80;">
            <!-- Сюда будет выводиться результат -->
        </div>
    </div>
</div>
`
        );
    }

    function profile() {
        const u = state.user || {};
        const initial = (u.login?.[0] || "Л").toUpperCase();
        const fullName = [u.lastName, u.firstName, u.patronymic].filter(Boolean).join(" ") || u.login || "Легионер";

        const subtitle = state.role === "teacher"
            ? "Преподаватель"
            : state.role === "admin"
                ? "Администратор"
                : (u.groupTitle ? `${u.groupTitle}` : "Без группы");

        return layout("Личный кабинет", `
    <div class="card">
      <div class="profile">
        <div class="profile-avatar">${esc(initial)}</div>
        <div>
          <h2 style="margin:0">${esc(u.login || "Легионер")}</h2>
          <div class="muted">${ROLES[state.role]?.label || ""} · ${esc(subtitle)}</div>
        </div>
      </div>
    </div>
    <div class="grid grid-2" style="margin-top:18px">
      <div class="card">
        <h3>Профиль</h3>
        <div class="field"><label>ФИО</label>
          <input class="input" value="${esc(fullName)}" disabled>
        </div>
        <div class="field"><label>Контактный телефон</label>
          <input class="input" id="p-phone" value="${esc(u.phoneNumber || "")}" placeholder="+7 ...">
        </div>
        <div class="field"><label>Email</label>
          <input class="input" id="p-email" value="${esc(u.email || "")}">
        </div>
        ${u.groupTitle ? `
          <div class="field"><label>Группа</label>
            <input class="input" value="${esc(u.groupTitle)}" disabled>
          </div>` : ""}
        <button class="btn primary" data-action="save-profile">Сохранить изменения</button>
      </div>
      <div class="card">
        <h3>Настройки</h3>
        <div class="field"><label>Тема</label>
          <select class="select" id="theme-select">
            <option value="system" ${state.theme === "system" ? "selected" : ""}>Системная</option>
            <option value="light" ${state.theme === "light" ? "selected" : ""}>Светлая</option>
            <option value="dark" ${state.theme === "dark" ? "selected" : ""}>Тёмная</option>
          </select>
        </div>
        <div class="field"><label>Уведомления</label>
          <label><input type="checkbox" checked> Оценки и результаты</label>
          <label><input type="checkbox" checked> Новые домашние задания</label>
          <label><input type="checkbox"> Новости</label>
        </div>
      </div>
    </div>`);
    }

    function subjects() {
        if (state.disciplinesLoading) {
            return layout(
                "Предметы",
                `<div class="card"><div class="loading-row"><div class="spinner"></div><span>Загрузка…</span></div></div>`
            );
        }
        if (state.disciplinesError) {
            return layout(
                "Предметы",
                `<div class="card">
            <div class="muted">Ошибка: ${esc(state.disciplinesError)}</div>
            <button class="btn primary" data-action="reload-disciplines" style="margin-top:12px">Повторить</button>
        </div>`
            );
        }

        return layout(
            "Предметы",
            `<div class="toolbar">
        <div class="spacer"></div>
        <button class="btn" data-action="reload-disciplines">Обновить</button>
        <button class="btn primary" data-action="add-discipline">＋ Добавить</button>
    </div>
    <div class="card">
        <div class="table-wrap">
            <table class="table">
                <thead>
                    <tr><th>Название</th><th>Создан</th><th>Действия</th></tr>
                </thead>
                <tbody>
                    ${state.disciplines.length
                ? state.disciplines
                    .map(
                        (d) => `<tr>
                                          <td><b>${esc(d.title)}</b></td>
                                          <td class="muted">${new Date(d.createdAt).toLocaleDateString("ru-RU")}</td>
                                          <td>
                                              <button class="btn" data-action="edit-discipline" data-id="${d.disciplineId}">Редактировать</button>
                                              <button class="btn" data-action="delete-discipline" data-id="${d.disciplineId}">Удалить</button>
                                          </td>
                                      </tr>`
                    )
                    .join("")
                : `<tr><td colspan="3" class="muted" style="text-align:center;padding:32px">Пока нет предметов. Нажмите «Добавить».</td></tr>`
            }
                </tbody>
            </table>
        </div>
    </div>`
        );
    }

    function classes() {
        if (state.groupsLoading) {
            return layout(
                "Классы",
                `<div class="card"><div class="loading-row"><div class="spinner"></div><span>Загрузка…</span></div></div>`
            );
        }
        if (state.groupsError) {
            return layout(
                "Классы",
                `<div class="card">
            <div class="muted">Ошибка: ${esc(state.groupsError)}</div>
            <button class="btn primary" data-action="reload-groups" style="margin-top:12px">Повторить</button>
        </div>`
            );
        }

        return layout(
            "Классы",
            `<div class="toolbar">
        <div class="spacer"></div>
        <button class="btn" data-action="reload-groups">Обновить</button>
        <button class="btn primary" data-action="add-group">＋ Добавить</button>
    </div>
    <div class="card">
        <div class="table-wrap">
            <table class="table">
                <thead>
                    <tr>
                        <th>Название</th>
                        <th>Студентов</th>
                        <th>Создан</th>
                        <th>Действия</th>
                    </tr>
                </thead>
                <tbody>
                    ${state.groups.length
                ? state.groups
                    .map(
                        (g) => `<tr>
                                          <td><b>${esc(g.title)}</b></td>
                                          <td><span class="badge primary">${g.studentCount}</span></td>
                                          <td class="muted">${new Date(g.createdAt).toLocaleDateString("ru-RU")}</td>
                                          <td>
                                              <button class="btn" data-action="edit-group" data-id="${g.groupId}">Редактировать</button>
                                              <button class="btn" data-action="delete-group" data-id="${g.groupId}">Удалить</button>
                                          </td>
                                      </tr>`
                    )
                    .join("")
                : `<tr><td colspan="4" class="muted" style="text-align:center;padding:32px">Пока нет классов. Нажмите «Добавить».</td></tr>`
            }
                </tbody>
            </table>
        </div>
    </div>`
        );
    }

    function students() {
        if (state.studentsLoading) {
            return layout(
                "Студенты",
                `<div class="card"><div class="loading-row"><div class="spinner"></div><span>Загрузка…</span></div></div>`
            );
        }
        if (state.studentsError) {
            return layout(
                "Студенты",
                `<div class="card">
            <div class="muted">Ошибка: ${esc(state.studentsError)}</div>
            <button class="btn primary" data-action="reload-students" style="margin-top:12px">Повторить</button>
        </div>`
            );
        }

        return layout(
            "Студенты",
            `<div class="toolbar">
        <div class="spacer"></div>
        <button class="btn" data-action="reload-students">Обновить</button>
    </div>
    <div class="card">
        <div class="table-wrap">
            <table class="table">
                <thead>
                    <tr>
                        <th>ФИО</th>
                        <th>Логин</th>
                        <th>Группа</th>
                        <th>Email</th>
                        <th>Действия</th>
                    </tr>
                </thead>
                <tbody>
                    ${state.students.length
                ? state.students
                    .map(
                        (s) => `<tr>
                                          <td><b>${esc(s.lastName)} ${esc(s.firstName)}</b></td>
                                          <td>${esc(s.login)}</td>
                                          <td>${s.groupTitle
                                ? `<span class="badge primary">${esc(s.groupTitle)}</span>`
                                : `<span class="badge warning">Без группы</span>`
                            }</td>
                                          <td class="muted">${esc(s.email)}</td>
                                          <td>
                                              <button class="btn" data-action="edit-student" data-id="${s.studentId}">Редактировать</button>
                                          </td>
                                      </tr>`
                    )
                    .join("")
                : `<tr><td colspan="5" class="muted" style="text-align:center;padding:32px">Пока нет студентов.</td></tr>`
            }
                </tbody>
            </table>
        </div>
    </div>`
        );
    }

    function teachers() {
        if (state.teachersLoading) {
            return layout(
                "Преподаватели",
                `<div class="card"><div class="loading-row"><div class="spinner"></div><span>Загрузка…</span></div></div>`
            );
        }
        if (state.teachersError) {
            return layout(
                "Преподаватели",
                `<div class="card">
            <div class="muted">Ошибка: ${esc(state.teachersError)}</div>
            <button class="btn primary" data-action="reload-teachers" style="margin-top:12px">Повторить</button>
        </div>`
            );
        }

        return layout(
            "Преподаватели",
            `<div class="toolbar">
        <div class="spacer"></div>
        <button class="btn" data-action="reload-teachers">Обновить</button>
    </div>
    <div class="card">
        <div class="table-wrap">
            <table class="table">
                <thead>
                    <tr>
                        <th>ФИО</th>
                        <th>Логин</th>
                        <th>Предметы</th>
                        <th>Email</th>
                        <th>Действия</th>
                    </tr>
                </thead>
                <tbody>
                    ${state.teachers.length
                ? state.teachers
                    .map(
                        (t) => `<tr>
                                          <td><b>${esc(t.lastName)} ${esc(t.firstName)}</b></td>
                                          <td>${esc(t.login)}</td>
                                          <td>${t.disciplineTitles?.length
                                ? t.disciplineTitles
                                    .map((d) => `<span class="badge success" style="margin:2px">${esc(d)}</span>`)
                                    .join("")
                                : `<span class="badge warning">Не назначены</span>`
                            }</td>
                                          <td class="muted">${esc(t.email)}</td>
                                          <td>
                                              <button class="btn" data-action="edit-teacher" data-id="${t.teacherId}">Редактировать</button>
                                          </td>
                                      </tr>`
                    )
                    .join("")
                : `<tr><td colspan="5" class="muted" style="text-align:center;padding:32px">Пока нет преподавателей.</td></tr>`
            }
                </tbody>
            </table>
        </div>
    </div>`
        );
    }

    async function loadHomeworks() {
        state.homeworksLoading = true;
        state.homeworksError = null;
        try {
            const res = await api.homeworks({ page: 1, pageSize: 50 });
            state.homeworks = res.items || res || [];
        } catch (err) {
            state.homeworksError = err.message || String(err);
            state.homeworks = [];
        } finally {
            state.homeworksLoaded = true;
            state.homeworksLoading = false;
        }
    }

    async function loadUsers() {
        state.usersLoading = true;
        state.usersError = null;
        try {
            const res = await api.users.list({ page: 1, pageSize: 100 });
            console.log("Ответ от api.users.list:", res);

            if (res && Array.isArray(res.items)) {
                state.users = res.items;
            } else if (Array.isArray(res)) {
                state.users = res;
            } else {
                state.users = [];
            }
        } catch (err) {
            state.usersError = err.message || String(err);
            state.users = [];
        } finally {
            state.usersLoaded = true;
            state.usersLoading = false;
        }
    }

    async function loadDisciplines() {
        state.disciplinesLoading = true;
        state.disciplinesError = null;
        try {
            const res = await api.disciplines.list({ page: 1, pageSize: 100 });
            state.disciplines = res.items || res || [];
        } catch (err) {
            state.disciplinesError = err.message || String(err);
            state.disciplines = [];
        } finally {
            state.disciplinesLoaded = true;
            state.disciplinesLoading = false;
        }
    }

    async function loadGroups() {
        state.groupsLoading = true;
        state.groupsError = null;
        try {
            const res = await api.groups.list({ page: 1, pageSize: 100 });
            state.groups = res.items || res || [];
        } catch (err) {
            state.groupsError = err.message || String(err);
            state.groups = [];
        } finally {
            state.groupsLoaded = true;
            state.groupsLoading = false;
        }
    }

    async function loadGrades() {
        state.gradesLoading = true;
        state.gradesError = null;
        try {
            const studentId = state.user?.studentId;
            if (!studentId) {
                state.gradesError = "Не найден studentId";
                return;
            }
            const [itemsRes, summaryRes] = await Promise.all([
                api.students.grades(studentId),
                api.students.gradesSummary(studentId),
            ]);
            state.gradesItems = itemsRes.items || [];
            state.gradesSummary = summaryRes;
        } catch (err) {
            state.gradesError = err.message || String(err);
            state.gradesItems = [];
            state.gradesSummary = null;
        } finally {
            state.gradesLoaded = true;
            state.gradesLoading = false;
        }
    }

    async function loadStudents() {
        state.studentsLoading = true;
        state.studentsError = null;
        try {
            const res = await api.students.list({ page: 1, pageSize: 200 });
            state.students = res.items || res || [];
        } catch (err) {
            state.studentsError = err.message || String(err);
            state.students = [];
        } finally {
            state.studentsLoaded = true;
            state.studentsLoading = false;
        }
    }

    async function loadTeachers() {
        state.teachersLoading = true;
        state.teachersError = null;
        try {
            const res = await api.teachers.list({ page: 1, pageSize: 200 });
            state.teachers = res.items || res || [];
        } catch (err) {
            state.teachersError = err.message || String(err);
            state.teachers = [];
        } finally {
            state.teachersLoaded = true;
            state.teachersLoading = false;
        }
    }

    function openAddUserModal() {
        const body = `
    <div class="field"><label>Логин</label>
        <input class="input" id="u-login" placeholder="ivanov" autocomplete="off">
    </div>
    <div class="field"><label>Пароль</label>
        <input class="input" id="u-password" type="password" placeholder="минимум 6 символов" autocomplete="new-password">
    </div>
    <div class="field"><label>Email (опционально)</label>
        <input class="input" id="u-email" placeholder="ivanov@local" autocomplete="off">
    </div>
    <div class="field"><label>Роль</label>
        <select class="select" id="u-role">
            <option value="1">Администратор</option>
            <option value="2">Преподаватель</option>
            <option value="3" selected>Студент</option>
        </select>
    </div>

    <div id="u-profile-fields">
        <div class="field"><label>Имя</label>
            <input class="input" id="u-first" placeholder="Иван">
        </div>
        <div class="field"><label>Фамилия</label>
            <input class="input" id="u-last" placeholder="Иванов">
        </div>
        <div class="field"><label>Отчество (опционально)</label>
            <input class="input" id="u-pat" placeholder="Иванович">
        </div>
        <div class="field" id="u-birth-wrapper"><label>Дата рождения</label>
            <input class="input" id="u-birth" type="date">
        </div>
    </div>`;

        const actions = `
    <button class="btn" data-action="close-user-modal">Отмена</button>
    <button class="btn primary" data-action="save-user">Создать</button>`;

        document.body.insertAdjacentHTML("beforeend", modal({
            id: "user-modal",
            title: "Новый пользователь",
            closeAction: "close-user-modal",
            body,
            actions,
        }));

        bind();
        const roleSel = document.getElementById("u-role");
        const profile = document.getElementById("u-profile-fields");
        const birthWrap = document.getElementById("u-birth-wrapper");

        const updateVisibility = () => {
            const v = parseInt(roleSel.value, 10);
            if (v === 1) {
                profile.style.display = "none";
            } else {
                profile.style.display = "";
                birthWrap.style.display = v === 3 ? "" : "none";
            }
        };
        roleSel.onchange = updateVisibility;
        updateVisibility();
    }

    function openAddDisciplineModal() {
        const body = `
    <div class="field"><label>Название предмета</label>
        <input class="input" id="d-title" placeholder="Например: Программирование" autocomplete="off">
    </div>`;

        const actions = `
    <button class="btn" data-action="close-discipline-modal">Отмена</button>
    <button class="btn primary" data-action="save-discipline">Создать</button>`;

        document.body.insertAdjacentHTML("beforeend", modal({
            id: "discipline-modal",
            title: "Новый предмет",
            closeAction: "close-discipline-modal",
            body,
            actions,
        }));

        bind();
    }

    function openAddGroupModal() {
        const body = `
    <div class="field"><label>Название класса</label>
        <input class="input" id="g-title" placeholder="Например: 10-А" autocomplete="off">
    </div>`;

        const actions = `
    <button class="btn" data-action="close-group-modal">Отмена</button>
    <button class="btn primary" data-action="save-group">Создать</button>`;

        document.body.insertAdjacentHTML("beforeend", modal({
            id: "group-modal",
            title: "Новый класс",
            closeAction: "close-group-modal",
            body,
            actions,
        }));

        bind();
    }

    async function openEditStudentModal(student) {
        let groups = state.groups;
        if (!state.groupsLoaded) {
            await loadGroups();
            groups = state.groups;
        }

        const body = `
    <div class="field"><label>Логин</label>
        <input class="input" value="${esc(student.login)}" disabled>
    </div>
    <div class="field"><label>Имя</label>
        <input class="input" id="s-first" value="${esc(student.firstName)}">
    </div>
    <div class="field"><label>Фамилия</label>
        <input class="input" id="s-last" value="${esc(student.lastName)}">
    </div>
    <div class="field"><label>Отчество</label>
        <input class="input" id="s-pat" value="${esc(student.patronymic || "")}">
    </div>
    <div class="field"><label>Email</label>
        <input class="input" id="s-email" value="${esc(student.email)}">
    </div>
    <div class="field"><label>Группа</label>
        <select class="select" id="s-group">
            <option value="">— Без группы —</option>
            ${groups
                .map(
                    (g) =>
                        `<option value="${g.groupId}" ${g.groupId === student.groupId ? "selected" : ""}>${esc(g.title)}</option>`
                )
                .join("")}
        </select>
    </div>`;

        const actions = `
    <button class="btn" data-action="close-student-modal">Отмена</button>
    <button class="btn primary" data-action="save-student" data-id="${student.studentId}">Сохранить</button>`;

        document.body.insertAdjacentHTML("beforeend", modal({
            id: "student-modal",
            title: "Редактирование студента",
            closeAction: "close-student-modal",
            body,
            actions,
        }));

        bind();
    }

    async function openEditTeacherModal(teacher) {
        let disciplines = state.disciplines;
        if (!state.disciplinesLoaded) {
            await loadDisciplines();
            disciplines = state.disciplines;
        }

        const selected = new Set(teacher.disciplineIds || []);

        const body = `
    <div class="field"><label>Логин</label>
        <input class="input" value="${esc(teacher.login)}" disabled>
    </div>
    <div class="field"><label>Имя</label>
        <input class="input" id="t-first" value="${esc(teacher.firstName)}">
    </div>
    <div class="field"><label>Фамилия</label>
        <input class="input" id="t-last" value="${esc(teacher.lastName)}">
    </div>
    <div class="field"><label>Отчество</label>
        <input class="input" id="t-pat" value="${esc(teacher.patronymic || "")}">
    </div>
    <div class="field"><label>Email</label>
        <input class="input" id="t-email" value="${esc(teacher.email)}">
    </div>
    <div class="field">
        <label>Предметы</label>
        <div style="max-height:220px;overflow:auto;padding:8px;border:1px solid var(--border);border-radius:11px">
            ${disciplines.length
                ? disciplines
                    .map(
                        (d) => `<label style="display:flex;gap:8px;align-items:center;padding:6px 4px">
                                  <input type="checkbox" class="t-disc" value="${d.disciplineId}" ${selected.has(d.disciplineId) ? "checked" : ""}>
                                  ${esc(d.title)}
                              </label>`
                    )
                    .join("")
                : `<div class="muted">Нет предметов. Сначала создайте их в разделе «Предметы».</div>`
            }
        </div>
    </div>`;

        const actions = `
    <button class="btn" data-action="close-teacher-modal">Отмена</button>
    <button class="btn primary" data-action="save-teacher" data-id="${teacher.teacherId}">Сохранить</button>`;

        document.body.insertAdjacentHTML("beforeend", modal({
            id: "teacher-modal",
            title: "Редактирование преподавателя",
            closeAction: "close-teacher-modal",
            body,
            actions,
        }));

        bind();
    }

    function openEditGroupModal(group) {
        const body = `
    <div class="field"><label>Название класса</label>
        <input class="input" id="g-title" value="${esc(group.title)}">
    </div>`;

        const actions = `
    <button class="btn" data-action="close-group-modal">Отмена</button>
    <button class="btn primary" data-action="update-group" data-id="${group.groupId}">Сохранить</button>`;

        document.body.insertAdjacentHTML("beforeend", modal({
            id: "group-modal",
            title: "Редактирование класса",
            closeAction: "close-group-modal",
            body,
            actions,
        }));

        bind();
    }

    function openEditDisciplineModal(discipline) {
        const body = `
    <div class="field"><label>Название предмета</label>
        <input class="input" id="d-title" value="${esc(discipline.title)}">
    </div>`;

        const actions = `
    <button class="btn" data-action="close-discipline-modal">Отмена</button>
    <button class="btn primary" data-action="update-discipline" data-id="${discipline.disciplineId}">Сохранить</button>`;

        document.body.insertAdjacentHTML("beforeend", modal({
            id: "discipline-modal",
            title: "Редактирование предмета",
            closeAction: "close-discipline-modal",
            body,
            actions,
        }));

        bind();
    }

    function openEditUserModal(user) {
        const roleOptions = [
            { id: 1, name: "Администратор" },
            { id: 2, name: "Преподаватель" },
            { id: 3, name: "Студент" },
        ];

        const body = `
    <div class="field"><label>Логин</label>
        <input class="input" id="eu-login" value="${esc(user.login)}" disabled>
    </div>
    <div class="field"><label>Email</label>
        <input class="input" id="eu-email" value="${esc(user.email || "")}">
    </div>
    <div class="field"><label>Роль</label>
        <select class="select" id="eu-role">
            ${roleOptions
                .map(
                    (r) =>
                        `<option value="${r.id}" ${r.id === user.roleId ? "selected" : ""}>${r.name}</option>`
                )
                .join("")}
        </select>
    </div>
    <div class="field">
        <label style="display:flex;gap:8px;align-items:center">
            <input type="checkbox" id="eu-active" ${user.isActive ? "checked" : ""}>
            Активен (можно входить)
        </label>
    </div>
    <div class="field"><label>Новый пароль (оставьте пустым, чтобы не менять)</label>
        <input class="input" id="eu-password" type="password" placeholder="минимум 6 символов">
    </div>`;

        const actions = `
    <button class="btn" data-action="close-user-modal">Отмена</button>
    <button class="btn primary" data-action="update-user" data-id="${user.userId}">Сохранить</button>`;

        document.body.insertAdjacentHTML("beforeend", modal({
            id: "user-modal",
            title: "Редактирование: " + user.login,
            closeAction: "close-user-modal",
            body,
            actions,
        }));

        bind();
    }

    async function openLessonModal(lesson) {
        if (!state.lessonTimesLoaded) {
            state.lessonTimes = await api.schedule.lessonTimes();
            state.lessonTimesLoaded = true;
        }

        if (!state.disciplinesLoaded) await loadDisciplines();
        if (!state.groupsLoaded) await loadGroups();

        const isEdit = !!lesson;
        const d = lesson || {
            title: "",
            disciplineId: 0,
            teacherId: 0,
            groupId: 0,
            lessonTimeId: 0,
            date: toIsoDate(state.scheduleMonth.year, state.scheduleMonth.month, 1),
            room: "",
            lessonType: "Lecture",
        };

        const isTeacher = state.role === "teacher";
        let availableDisciplines = state.disciplines;
        let teacherFieldHtml = "";

        if (isTeacher) {
            try {
                const myProfile = await api.teachers.me();

                if (myProfile) {
                    d.teacherId = myProfile.teacherId;

                    if (myProfile.disciplineIds && myProfile.disciplineIds.length > 0) {
                        availableDisciplines = state.disciplines.filter(dis =>
                            myProfile.disciplineIds.includes(dis.disciplineId)
                        );
                    } else {
                        availableDisciplines = [];
                    }

                    teacherFieldHtml = `
                    <input class="input" value="${esc(myProfile.lastName)} ${esc(myProfile.firstName)} ${esc(myProfile.patronymic || '')}".trim() disabled style="background:#f5f5f5">
                    <input type="hidden" id="l-teacher" value="${myProfile.teacherId}">
                `;
                }
            } catch (err) {
                console.warn("Не удалось загрузить профиль преподавателя:", err);
                toast("Ошибка загрузки профиля преподавателя. Попробуйте перезайти.");

                if (!state.teachersLoaded) await loadTeachers();
                teacherFieldHtml = `
            <select class="select" id="l-teacher">
                <option value="">— выберите —</option>
                ${state.teachers.map(x =>
                    `<option value="${x.teacherId}">${esc(x.lastName)} ${esc(x.firstName)} ${esc(x.patronymic || '')}</option>`
                ).join("")}
            </select>
            `;
            }
        }
        else {
            if (!state.teachersLoaded) await loadTeachers();

            teacherFieldHtml = `
        <select class="select" id="l-teacher">
            <option value="">— выберите —</option>
            ${state.teachers.map(x =>
                `<option value="${x.teacherId}" ${x.teacherId === d.teacherId ? "selected" : ""}>${esc(x.lastName)} ${esc(x.firstName)} ${esc(x.patronymic || '')}</option>`
            ).join("")}
        </select>
    `;
        }

        const body = `
<div class="field"><label>Название</label>
    <input class="input" id="l-title" value="${esc(d.title || "")}" placeholder="Например: Лекция по БД">
</div>
<div class="field"><label>Предмет</label>
    <select class="select" id="l-discipline">
        <option value="">— выберите —</option>
        ${availableDisciplines
                .map(
                    (x) =>
                        `<option value="${x.disciplineId}" ${x.disciplineId === d.disciplineId ? "selected" : ""}>${esc(x.title)}</option>`
                )
                .join("")}
    </select>
    ${isTeacher && availableDisciplines.length === 0
                ? '<div class="muted" style="color:#e11;font-size:12px;margin-top:4px">У вас нет назначенных предметов. Обратитесь к администратору.</div>'
                : ''}
</div>

<div class="field"><label>Преподаватель</label>
    ${teacherFieldHtml}
</div>

<div class="field"><label>Группа</label>
    <select class="select" id="l-group">
        <option value="">— выберите —</option>
        ${state.groups
                .map(
                    (x) =>
                        `<option value="${x.groupId}" ${x.groupId === d.groupId ? "selected" : ""}>${esc(x.title)}</option>`
                )
                .join("")}
    </select>
</div>
<div class="field"><label>Пара (звонок)</label>
    <select class="select" id="l-lessontime">
        <option value="">— выберите —</option>
        ${state.lessonTimes
                .map(
                    (x) =>
                        `<option value="${x.lessonTimeId}" ${x.lessonTimeId === d.lessonTimeId ? "selected" : ""}>${x.pairNumber}. ${x.startTime}–${x.endTime}</option>`
                )
                .join("")}
    </select>
</div>
<div class="field"><label>Дата</label>
    <input class="input" type="date" id="l-date" value="${esc(d.date)}">
</div>
<div class="field"><label>Аудитория</label>
    <input class="input" id="l-room" value="${esc(d.room || "")}" placeholder="А-305">
</div>
<div class="field"><label>Тип занятия</label>
    <select class="select" id="l-type">
        <option value="Lecture"   ${d.lessonType === "Lecture" ? "selected" : ""}>Лекция</option>
        <option value="Practice"  ${d.lessonType === "Practice" ? "selected" : ""}>Практика</option>
        <option value="Laboratory"${d.lessonType === "Laboratory" ? "selected" : ""}>Лаборатория</option>
        <option value="Seminar"   ${d.lessonType === "Seminar" ? "selected" : ""}>Семинар</option>
    </select>
</div>`;

        const actions = `
<button class="btn" data-action="close-lesson-modal">Отмена</button>
<button class="btn primary" data-action="save-lesson" ${isEdit ? `data-id="${lesson.monthScheduleId}"` : ""}>${isEdit ? "Сохранить" : "Создать"}</button>`;

        document.body.insertAdjacentHTML("beforeend", modal({
            id: "lesson-modal",
            title: isEdit ? "Редактирование пары" : "Новая пара",
            closeAction: "close-lesson-modal",
            body,
            actions,
        }));

        bind();
    }

    async function openAttendanceSheet(monthScheduleId) {
        try {
            const sheet = await api.attendance.sheet(monthScheduleId);

            const rows = sheet.students
                .map((s) => {
                    const fullName = `${esc(s.lastName)} ${esc(s.firstName)}${s.patronymic ? " " + esc(s.patronymic) : ""}`;
                    const options = sheet.statuses
                        .map(
                            (st) =>
                                `<option value="${st.attendanceId}" ${s.attendanceId === st.attendanceId ? "selected" : ""}>${esc(st.title)}</option>`
                        )
                        .join("");

                    return `<tr data-student-id="${s.studentId}">
                <td><b>${fullName}</b></td>
                <td>
                    <select class="select att-status">
                        <option value="">— не отмечено —</option>
                        ${options}
                    </select>
                </td>
                <td>
                    <input class="input att-comment" value="${esc(s.comment || "")}" placeholder="Комментарий">
                </td>
            </tr>`;
                })
                .join("");

            const body = `
        <div class="toolbar" style="margin-bottom:12px">
            <b>${esc(sheet.disciplineTitle)}</b>
            <span class="muted">·</span>
            <span>${esc(sheet.groupTitle)}</span>
            <span class="muted">·</span>
            <span>${sheet.pairNumber}. ${sheet.startTime}–${sheet.endTime}</span>
            <span class="muted">·</span>
            <span>${esc(sheet.date)}</span>
        </div>
        ${sheet.students.length
                    ? `<div class="table-wrap">
                    <table class="table">
                        <thead>
                            <tr><th>Студент</th><th>Статус</th><th>Комментарий</th></tr>
                        </thead>
                        <tbody>${rows}</tbody>
                    </table>
                   </div>`
                    : `<div class="muted">В группе нет студентов.</div>`
                }`;

            const actions = `
        <button class="btn" data-action="close-attendance-modal">Отмена</button>
        <button class="btn primary" data-action="save-attendance" data-schedule-id="${sheet.scheduleId}">Сохранить</button>`;

            document.body.insertAdjacentHTML("beforeend", modal({
                id: "attendance-modal",
                title: "Посещаемость",
                closeAction: "close-attendance-modal",
                body,
                actions,
            }));

            bind();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function saveAttendance(scheduleId) {
        const rows = document.querySelectorAll("#attendance-modal tbody tr[data-student-id]");
        const marks = [];

        rows.forEach((tr) => {
            const studentId = parseInt(tr.dataset.studentId, 10);
            const statusId = parseInt(tr.querySelector(".att-status")?.value || "0", 10);
            const comment = tr.querySelector(".att-comment")?.value?.trim() || null;

            if (statusId > 0) {
                marks.push({ studentId, attendanceId: statusId, comment });
            }
        });

        if (!marks.length) {
            toast("Отметьте хотя бы одного студента");
            return;
        }

        try {
            await api.attendance.save(scheduleId, marks);
            toast("✓ Посещаемость сохранена");
            document.getElementById("attendance-modal")?.remove();
            state.attendanceLoaded = false;
            await loadAttendanceToday();
            render();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function saveLesson(id) {
        const title = document.getElementById("l-title")?.value?.trim();
        const disciplineId = parseInt(document.getElementById("l-discipline")?.value || "0", 10);
        const teacherId = parseInt(document.getElementById("l-teacher")?.value || "0", 10);
        const groupId = parseInt(document.getElementById("l-group")?.value || "0", 10);
        const lessonTimeId = parseInt(document.getElementById("l-lessontime")?.value || "0", 10);
        const date = document.getElementById("l-date")?.value;
        const room = document.getElementById("l-room")?.value?.trim() || null;
        const lessonType = document.getElementById("l-type")?.value || "Lecture";

        if (!title) { toast("Введите название"); return; }
        if (!disciplineId) { toast("Выберите предмет"); return; }
        if (!teacherId) { toast("Выберите преподавателя"); return; }
        if (!groupId) { toast("Выберите группу"); return; }
        if (!lessonTimeId) { toast("Выберите пару"); return; }
        if (!date) { toast("Укажите дату"); return; }

        const payload = { title, disciplineId, teacherId, groupId, lessonTimeId, date, room, lessonType };

        try {
            if (id) {
                await api.schedule.update(id, payload);
                toast("✓ Пара обновлена");
            } else {
                await api.schedule.create(payload);
                toast("✓ Пара создана");
            }
            document.getElementById("lesson-modal")?.remove();
            state.scheduleLoaded = false;
            await loadSchedule();
            render();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function deleteLesson(id) {
        if (!confirm("Отменить пару?")) return;
        try {
            await api.schedule.remove(id);
            toast("✓ Пара отменена");
            state.scheduleLoaded = false;
            await loadSchedule();
            render();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function openLesson(monthScheduleId) {
        const lesson = state.schedule.find((s) => s.monthScheduleId === monthScheduleId);
        if (!lesson) return;

        const canEdit = state.role === "admin" || state.role === "teacher";

        const todayIso = new Date().toLocaleDateString("sv-SE");
        const isToday = lesson.date === todayIso;
        const isCancelled = lesson.status === "Cancelled";

        const info = `
    <div class="field"><label>Дата</label><div>${esc(lesson.date)}</div></div>
    <div class="field"><label>Пара</label><div>${lesson.pairNumber}. ${esc(lesson.startTime)}–${esc(lesson.endTime)}</div></div>
    <div class="field"><label>Предмет</label><div>${esc(lesson.disciplineTitle)}</div></div>
    <div class="field"><label>Преподаватель</label><div>${esc(lesson.teacherName)}</div></div>
    <div class="field"><label>Группа</label><div>${esc(lesson.groupTitle)}</div></div>
    ${lesson.room ? `<div class="field"><label>Аудитория</label><div>${esc(lesson.room)}</div></div>` : ""}
    ${lesson.status ? `<div class="field"><label>Статус</label><div>${esc(lesson.status)}</div></div>` : ""}`;

        const actions = `
    <button class="btn" data-action="close-lesson-modal">Закрыть</button>
    ${isToday && !isCancelled
                ? `<button class="btn success" data-action="open-video" data-id="${lesson.monthScheduleId}">📹 Войти в видеоурок</button>`
                : ""
            }
    ${canEdit ? `<button class="btn" data-action="edit-lesson" data-id="${lesson.monthScheduleId}">Редактировать</button>` : ""}
    ${canEdit ? `<button class="btn danger" data-action="delete-lesson" data-id="${lesson.monthScheduleId}">Отменить</button>` : ""}`;

        document.body.insertAdjacentHTML("beforeend", modal({
            id: "lesson-modal",
            title: esc(lesson.title),
            closeAction: "close-lesson-modal",
            body: info,
            actions,
        }));

        bind();
    }

    async function saveUser() {
        const login = document.getElementById("u-login")?.value?.trim();
        const password = document.getElementById("u-password")?.value;
        const email = document.getElementById("u-email")?.value?.trim() || null;
        const roleId = parseInt(document.getElementById("u-role")?.value || "3", 10);
        const firstName = document.getElementById("u-first")?.value?.trim() || null;
        const lastName = document.getElementById("u-last")?.value?.trim() || null;
        const patronymic = document.getElementById("u-pat")?.value?.trim() || null;
        const birthRaw = document.getElementById("u-birth")?.value || null;
        const birthDate = birthRaw ? new Date(birthRaw).toISOString() : null;

        if (!login || !password) { toast("Заполните логин и пароль"); return; }
        if (password.length < 6) { toast("Пароль минимум 6 символов"); return; }
        if (roleId === 2 || roleId === 3) {
            if (!firstName || !lastName) { toast("Заполните имя и фамилию"); return; }
        }
        if (roleId === 3 && !birthDate) { toast("Укажите дату рождения студента"); return; }

        try {
            await api.users.create({
                login, password, email, roleId,
                firstName, lastName, patronymic, birthDate,
            });
            toast("✓ Пользователь создан");
            document.getElementById("user-modal")?.remove();
            state.usersLoaded = false;
            await loadUsers();
            render();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function saveStudent(id) {
        const firstName = document.getElementById("s-first")?.value?.trim();
        const lastName = document.getElementById("s-last")?.value?.trim();
        const patronymic = document.getElementById("s-pat")?.value?.trim() || null;
        const email = document.getElementById("s-email")?.value?.trim();
        const groupRaw = document.getElementById("s-group")?.value;
        const groupId = groupRaw ? parseInt(groupRaw, 10) : null;

        if (!firstName || !lastName || !email) {
            toast("Заполните имя, фамилию и email");
            return;
        }

        try {
            await api.students.update(id, { firstName, lastName, patronymic, email, groupId });
            toast("✓ Студент обновлён");
            document.getElementById("student-modal")?.remove();
            state.studentsLoaded = false;
            await loadStudents();
            render();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function saveTeacher(id) {
        const firstName = document.getElementById("t-first")?.value?.trim();
        const lastName = document.getElementById("t-last")?.value?.trim();
        const patronymic = document.getElementById("t-pat")?.value?.trim() || null;
        const email = document.getElementById("t-email")?.value?.trim();

        if (!firstName || !lastName || !email) {
            toast("Заполните имя, фамилию и email");
            return;
        }

        const disciplineIds = Array.from(
            document.querySelectorAll("#teacher-modal .t-disc:checked")
        ).map((el) => parseInt(el.value, 10));

        try {
            await api.teachers.update(id, { firstName, lastName, patronymic, email });
            await api.teachers.setDisciplines(id, disciplineIds);

            toast("✓ Преподаватель обновлён");
            document.getElementById("teacher-modal")?.remove();
            state.teachersLoaded = false;
            await loadTeachers();
            render();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function saveDiscipline() {
        const title = document.getElementById("d-title")?.value?.trim();
        if (!title) { toast("Введите название предмета"); return; }
        try {
            await api.disciplines.create({ title });
            toast("✓ Предмет создан");
            document.getElementById("discipline-modal")?.remove();
            state.disciplinesLoaded = false;
            await loadDisciplines();
            render();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function saveGroup() {
        const title = document.getElementById("g-title")?.value?.trim();
        if (!title) { toast("Введите название класса"); return; }
        try {
            await api.groups.create({ title });
            toast("✓ Класс создан");
            document.getElementById("group-modal")?.remove();
            state.groupsLoaded = false;
            await loadGroups();
            render();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function updateGroup(id) {
        const title = document.getElementById("g-title")?.value?.trim();
        if (!title) { toast("Введите название класса"); return; }
        try {
            await api.groups.update(id, { title });
            toast("✓ Класс обновлён");
            document.getElementById("group-modal")?.remove();
            state.groupsLoaded = false;
            await loadGroups();
            render();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function deleteGroup(id) {
        if (!confirm("Удалить класс?")) return;
        try {
            await api.groups.remove(id);
            toast("✓ Класс удалён");
            state.groupsLoaded = false;
            await loadGroups();
            render();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function updateDiscipline(id) {
        const title = document.getElementById("d-title")?.value?.trim();
        if (!title) { toast("Введите название предмета"); return; }
        try {
            await api.disciplines.update(id, { title });
            toast("✓ Предмет обновлён");
            document.getElementById("discipline-modal")?.remove();
            state.disciplinesLoaded = false;
            await loadDisciplines();
            render();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function deleteDiscipline(id) {
        if (!confirm("Удалить предмет?")) return;
        try {
            await api.disciplines.remove(id);
            toast("✓ Предмет удалён");
            state.disciplinesLoaded = false;
            await loadDisciplines();
            render();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function updateUser(id) {
        const email = document.getElementById("eu-email")?.value?.trim() || null;
        const roleId = parseInt(document.getElementById("eu-role")?.value || "3", 10);
        const isActive = document.getElementById("eu-active")?.checked ?? true;
        const newPassword = document.getElementById("eu-password")?.value || null;

        if (newPassword && newPassword.length < 6) {
            toast("Пароль минимум 6 символов");
            return;
        }

        try {
            await api.users.update(id, {
                email, roleId, isActive,
                newPassword: newPassword || null,
            });
            toast("✓ Пользователь обновлён");
            document.getElementById("user-modal")?.remove();
            state.usersLoaded = false;
            await loadUsers();
            render();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function deleteUser(id) {
        if (!confirm("Удалить пользователя?")) return;
        try {
            await api.users.remove(id);
            state.usersLoaded = false;
            await afterAction(null, "✓ Пользователь удалён", loadUsers);
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function openHomework(id) {
        try {
            const hw = await api.homework(id);
            const isStudent = state.role === "student";
            const isTeacher = state.role === "teacher";

            const canSubmit = isStudent && !hw.dueDate;
            const canGrade = isTeacher && hw.dueDate && !hw.grade;
            const s = hwStatus(hw);

            const body = `
        <p class="muted">Дедлайн: ${esc(hw.deadLine || "")}${hw.dueDate ? ` · Сдано: ${esc(hw.dueDate)}` : ""}</p>

        ${hw.taskFileUrl
                    ? `<div style="margin:12px 0;padding:12px;background:var(--surface-2);border-radius:8px">
                 <b>📎 Файл задания от преподавателя:</b>
                 <div style="margin-top:8px">
                   <a href="${esc(hw.taskFileUrl)}" target="_blank" rel="noopener">Скачать файл задания</a>
                 </div>
               </div>`
                    : ""
                }

        ${hw.homeworkText
                    ? `<div style="margin:12px 0;padding:12px;background:var(--surface-2);border-radius:8px">
                 <b>Ответ студента:</b><div style="margin-top:6px">${esc(hw.homeworkText)}</div>
               </div>`
                    : ""
                }

        ${hw.answerFileUrl
                    ? `<p><a href="${esc(hw.answerFileUrl)}" target="_blank" rel="noopener">📄 Скачать файл ответа студента</a></p>`
                    : ""
                }

        ${hw.archiveUrl
                    ? `<p><a href="${esc(hw.archiveUrl)}" target="_blank" rel="noopener">📦 Скачать архив ответа</a></p>`
                    : ""
                }

        <p><span class="badge ${s.badge}">${esc(s.label)}</span></p>

        ${hw.teacherComment
                    ? `<div style="margin:12px 0;padding:12px;background:var(--surface-2);border-radius:8px;border-left:3px solid #4a7dff">
                 <b>💬 Комментарий преподавателя:</b>
                 <div style="margin-top:6px">${esc(hw.teacherComment)}</div>
               </div>`
                    : ""
                }

        ${canSubmit
                    ? `<div class="field"><label>Комментарий</label>
                 <textarea class="textarea" id="hw-text" placeholder="Что вы сделали"></textarea>
               </div>
               <div class="field"><label>Файлы (можно несколько)</label>
                 <input type="file" id="hw-files" multiple>
               </div>`
                    : ""
                }

        ${canGrade
                    ? `<div class="field"><label>Оценка (1–5)</label>
                 <input class="input" id="hw-grade" type="number" min="1" max="5" style="width:100px">
               </div>
               <div class="field"><label>Комментарий</label>
                 <input class="input" id="hw-comment">
               </div>`
                    : ""
                }`;

            const actions = `
          <button class="btn" data-action="close-homework">Закрыть</button>
          ${canSubmit ? `<button class="btn primary" data-action="submit-homework" data-id="${id}">Сдать</button>` : ""}
          ${canGrade ? `<button class="btn success" data-action="grade-homework" data-id="${id}">Оценить</button>` : ""}`;

            document.body.insertAdjacentHTML("beforeend", modal({
                id: "hw-modal",
                title: hw.task || "Задание",
                closeAction: "close-homework",
                body,
                actions,
            }));

            bind();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function submitHomework(id) {
        const textEl = document.getElementById("hw-text");
        const filesEl = document.getElementById("hw-files");
        const text = textEl?.value || "";
        const files = filesEl?.files ? Array.from(filesEl.files) : [];

        if (!text && !files.length) {
            toast("Добавьте комментарий или файлы");
            return;
        }
        try {
            await api.submitHomework(id, text, files);
            state.homeworksLoaded = false;
            await afterAction("hw-modal", "✓ ДЗ сдано", loadHomeworks);
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function gradeHomework(id) {
        const gradeEl = document.getElementById("hw-grade");
        const commentEl = document.getElementById("hw-comment");
        const grade = parseInt(gradeEl?.value, 10);
        const comment = commentEl?.value || null;

        if (!(grade >= 1 && grade <= 5)) {
            toast("Оценка должна быть от 1 до 5");
            return;
        }
        try {
            await api.gradeHomework(id, grade, comment);
            state.homeworksLoaded = false;
            await afterAction("hw-modal", "✓ Оценка выставлена", loadHomeworks);
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    function onHomeworkSubmitted(payload) {
        if (state.role === "teacher" || state.role === "admin") {
            toast(`📥 Студент сдал ДЗ #${payload.homeworkId ?? ""}`);
            state.homeworksLoaded = false;
            loadHomeworks().then(render);
        }
    }

    function onHomeworkGraded(payload) {
        if (state.role === "student") {
            toast(`★ Оценка ${payload.grade ?? ""} за ДЗ #${payload.homeworkId ?? ""}`);
            state.homeworksLoaded = false;
            loadHomeworks().then(render);
        }
    }

    async function initSession() {
        state.user = await api.me();
        state.role = String(state.user.role || "Student").toLowerCase();
        localStorage.setItem("alpha-auth", "1");
        localStorage.setItem("alpha-role", state.role);

        state.usersLoaded = false;
        state.disciplinesLoaded = false;
        state.groupsLoaded = false;
        state.studentsLoaded = false;
        state.teachersLoaded = false;
        state.scheduleLoaded = false;
        state.attendanceLoaded = false;
        state.homeworksLoaded = false;
        state.lessonTimesLoaded = false;

        window._onChatMessage = (payload) => {
            if (!payload || payload.chatId == null) return;

            const chatId = Number(payload.chatId);
            const isCurrent = chatId === Number(state.currentChatId);
            if (isCurrent) {
                const already = state.chatMessages.some(
                    m => m.messageId && payload.messageId && m.messageId === payload.messageId
                );
                if (!already) {
                    state.chatMessages.push(payload);
                }
            }
            const chat = state.chats.find(c => Number(c.chatId) === chatId);
            if (chat) chat.messageCount = (chat.messageCount || 0) + 1;
            if (state.route === "communications") {
                render();
                requestAnimationFrame(() => {
                    const box = document.getElementById("chat-messages-container");
                    if (box) box.scrollTop = box.scrollHeight;
                });
                if (!isCurrent && chat) {
                    toast(`💬 Новое сообщение в «${chat.title}»`);
                }
            }

        };

        window._onNotification = (payload) => {
            toast(`🔔 ${payload.title}: ${payload.text}`);
            if (payload.url === "#ide") {
            }
        };

        await loadHomeworks();
        if (state.role === "teacher" || state.role === "admin") {
            await loadSubmissions();
        }
        try {
            await startSignalR(onHomeworkSubmitted, onHomeworkGraded);
        } catch (e) {
            console.warn("SignalR не подключился:", e);
        }
    }

    function gradebookPage() {
        if (state.gradebookLoading) {
            return layout("Журнал оценок",
                `<div class="card"><div class="loading-row"><div class="spinner"></div><span>Загрузка…</span></div></div>`);
        }

        const today = new Date().toLocaleDateString("sv-SE");

        return layout("Журнал оценок", `
      <div class="toolbar">
        <input class="input" type="date" id="gb-date" value="${esc(state.gradebookDate || today)}" style="width:170px">
        <button class="btn primary" data-action="load-gradebook">Показать пары</button>
        <div class="spacer"></div>
        <button class="btn" data-action="reload-gradebook">Обновить</button>
      </div>
      <div class="card">
        ${state.gradebookLessons.length
                ? state.gradebookLessons.map(l => `
                <div class="lesson" style="margin-bottom:8px">
                  <div class="lesson-time">${l.pairNumber}. ${l.startTime}–${l.endTime}</div>
                  <div>
                    <div class="lesson-title">${esc(l.disciplineTitle)}</div>
                    <div class="lesson-meta">${esc(l.groupTitle)}${l.room ? " · " + esc(l.room) : ""}</div>
                  </div>
                  <button class="btn primary"
                          data-action="open-gradebook"
                          data-id="${l.monthScheduleId}">Оценить</button>
                </div>`).join("")
                : `<div class="muted" style="padding:32px;text-align:center">
                 На эту дату занятий нет. Выберите другую дату.
               </div>`}
      </div>`);
    }

    async function openGradebook(monthScheduleId) {
        try {
            const sheet = await api.schedule.grades(monthScheduleId);

            const rows = sheet.students.map(s => `
            <tr data-student-id="${s.studentId}">
              <td><b>${esc(s.studentName)}</b></td>
              <td>
                <select class="select gb-grade" style="width:110px">
                  <option value="">—</option>
                  ${sheet.grades.map(g => `
                    <option value="${g.gradeId}" ${s.gradeId == g.gradeId ? "selected" : ""}>
                      ${g.gradeValue === 1 ? "1 (кол)" : g.gradeValue}
                    </option>`).join("")}
                </select>
              </td>
              <td>
                <input class="input gb-comment" value="${esc(s.comment || "")}" placeholder="Комментарий">
              </td>
            </tr>`).join("");

            const body = `
          <div class="toolbar" style="margin-bottom:12px">
            <b>${esc(sheet.disciplineTitle)}</b>
            <span class="muted">·</span>
            <span>${esc(sheet.groupTitle)}</span>
            <span class="muted">·</span>
            <span>${sheet.pairNumber}. ${esc(sheet.startTime)}–${esc(sheet.endTime)}</span>
            <span class="muted">·</span>
            <span>${esc(sheet.date)}</span>
          </div>
          ${sheet.students.length
                    ? `<div class="table-wrap">
                   <table class="table">
                     <thead><tr><th>Студент</th><th>Оценка</th><th>Комментарий</th></tr></thead>
                     <tbody>${rows}</tbody>
                   </table>
                 </div>`
                    : `<div class="muted">В группе нет студентов.</div>`}`;

            const actions = `
          <button class="btn" data-action="close-gradebook-modal">Отмена</button>
          <button class="btn primary" data-action="save-gradebook"
                  data-schedule-id="${sheet.scheduleId}"
                  data-month-schedule-id="${monthScheduleId}">Сохранить</button>`;

            document.body.insertAdjacentHTML("beforeend", modal({
                id: "gradebook-modal",
                title: "Оценки за пару",
                closeAction: "close-gradebook-modal",
                body,
                actions,
            }));
            bind();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function saveGradebook(monthScheduleId) {
        const rows = document.querySelectorAll("#gradebook-modal tbody tr[data-student-id]");
        const marks = [];

        rows.forEach(tr => {
            const studentId = parseInt(tr.dataset.studentId, 10);
            const gradeId = tr.querySelector(".gb-grade")?.value;
            const comment = tr.querySelector(".gb-comment")?.value?.trim() || null;

            if (gradeId) {
                marks.push({ studentId, gradeId: parseInt(gradeId, 10), comment });
            }
        });

        try {
            await api.schedule.saveGrades(monthScheduleId, marks);
            toast(marks.length ? "✓ Оценки сохранены" : "✓ Оценки очищены");
            document.getElementById("gradebook-modal")?.remove();
            render();
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
        }
    }

    async function loadGradebookLessons(date) {
        if (state.gradebookLoading) return;
        state.gradebookLoading = true;
        state.gradebookLoaded = false;
        state.gradebookDate = date;
        try {
            const items = await api.schedule.list({ from: date, to: date });
            state.gradebookLessons = items || [];
        } catch (err) {
            toast("Ошибка: " + (err.message || err));
            state.gradebookLessons = [];
        } finally {
            state.gradebookLoaded = true;
            state.gradebookLoading = false;
        }
    }
    function checkHomeworkPage() {
        if (state.submissionsLoading) {
            return layout("Проверка ДЗ",
                `<div class="card"><div class="loading-row"><div class="spinner"></div><span>Загрузка…</span></div></div>`);
        }
        if (state.submissionsError) {
            return layout("Проверка ДЗ",
                `<div class="card"><div class="muted">Ошибка: ${esc(state.submissionsError)}</div>
             <button class="btn primary" data-action="reload-submissions">Повторить</button></div>`);
        }

        const filteredSubmissions = state.submissions.filter(s => {
            if (state.submissionFilter === "pending") return !s.grade;
            if (state.submissionFilter === "graded") return !!s.grade;
            return true;
        });

        return layout("Проверка ДЗ", `
    <div class="toolbar">
      <select class="select" style="width:220px" id="sub-status">
        <option value="">Все</option>
        <option value="pending" ${state.submissionFilter === "pending" ? "selected" : ""}>На проверке</option>
        <option value="graded" ${state.submissionFilter === "graded" ? "selected" : ""}>Оценённые</option>
      </select>
      <div class="spacer"></div>
      <button class="btn" data-action="reload-submissions">Обновить</button>
    </div>
    <div class="card">
      <div class="table-wrap">
        <table class="table">
          <thead>
            <tr><th>Студент</th><th>Задание</th><th>Предмет</th><th>Сдано</th><th>Статус</th><th></th></tr>
          </thead>
          <tbody>
            ${filteredSubmissions.map(s => `
              <tr>
                <td><b>${esc(s.studentName)}</b></td>
                <td>${esc(s.task)}</td>
                <td>${esc(s.discipline)}</td>
                <td>${s.dueDate ? esc(s.dueDate) : "—"}</td>
                <td>${s.grade
                ? `<span class="badge success">Оценено: ${s.grade}</span>`
                : `<span class="badge warning">На проверке</span>`
            }</td>
                <td>
                  <button class="btn primary"
                          data-action="open-homework"
                          data-id="${s.homeworkId}">Проверить</button>
                </td>
              </tr>`).join("")}
            ${filteredSubmissions.length === 0 ? `<tr><td colspan="6" class="muted" style="text-align:center;padding:32px">Нет заданий с таким статусом.</td></tr>` : ""}
          </tbody>
        </table>
      </div>
    </div>`);
    }
    function paintPage() {
        return layout("🎨 Paint", `
      <div class="toolbar">
        <button class="btn" data-action="paint-color">🎨 Цвет кисти</button>
        <input type="color" id="paint-color-input" value="#000000" style="width:40px;height:36px;cursor:pointer">

        <button class="btn" data-action="paint-bg-color">🖼 Цвет фона</button>
        <input type="color" id="paint-bg-input" value="#ffffff" style="width:40px;height:36px;cursor:pointer">

        <button class="btn" data-action="paint-load-png">📂 Загрузить PNG</button>
        <input type="file" id="paint-file-input" accept="image/png,image/jpeg,image/webp,image/gif" style="display:none">

        <div class="spacer"></div>

        <label style="display:flex;align-items:center;gap:8px">
          Толщина:
          <input type="range" id="paint-size" min="1" max="30" value="3" style="width:120px">
          <span id="paint-size-label">3</span>
        </label>

        <div class="spacer"></div>

        <button class="btn primary" data-action="paint-brush" id="paint-brush-btn">✏️ Кисть</button>
        <button class="btn" data-action="paint-eraser">🧽 Ластик</button>
        <button class="btn" data-action="paint-text">🔤 Текст</button>
        <button class="btn" data-action="paint-clear">🗑 Очистить</button>
        <button class="btn success" data-action="paint-save">💾 Скачать PNG</button>
      </div>
      <div class="card" style="padding:0">
        <canvas id="paint-canvas" width="1200" height="600"
                style="display:block;width:100%;background:#ffffff;cursor:crosshair;border-radius:12px"></canvas>
      </div>
      <div class="muted" id="paint-status" style="padding:8px 4px">
        Готово. Выберите инструмент и рисуйте.
      </div>
    `);
    }

    function initAceEditor() {
        const container = document.getElementById('ace-editor-container');
        if (!container) return;

        if (window._aceEditor) {
            window._aceEditor.destroy();
            window._aceEditor = null;
        }
        container.innerHTML = '';

        const currentLang = state.ideLanguage || "python";
        const langMap = {
            python: 'python',
            javascript: 'javascript',
            java: 'java',
            cpp: 'c_cpp',
            csharp: 'csharp'
        };
        const mode = langMap[currentLang] || 'python';

        window._aceEditor = ace.edit("ace-editor-container");
        window._aceEditor.setTheme("ace/theme/monokai");
        window._aceEditor.session.setMode("ace/mode/" + mode);
        window._aceEditor.setValue("// Напишите ваш код на " + currentLang + " здесь\n", -1);
    }

    function initPaint() {
        const canvas = document.getElementById("paint-canvas");
        if (!canvas) return;
        const ctx = canvas.getContext("2d");
        if (canvas.id === "paint-canvas") {
            canvas.addEventListener("click", () => {
                state.paintClicks++; clearTimeout(state.paintClickTimer);
                state.paintClickTimer = setTimeout(() => { state.paintClicks = 0; }, 1800);
                if (state.paintClicks >= 5) {
                    state.paintClicks = 0; state.kidsTheme = !state.kidsTheme;
                    localStorage.setItem("alpha-67kids-theme", state.kidsTheme ? "1" : "0"); applyTheme();
                    toast(state.kidsTheme ? "Секретная тема 67kids активирована!" : "Тема 67kids выключена");
                }
            });
        }

        let drawing = false;
        let lastX = 0, lastY = 0;
        let mode = "brush";
        let brushColor = "#000000";
        let bgColor = "#ffffff";
        let brushSize = 3;

        ctx.fillStyle = bgColor;
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        const colorInput = document.getElementById("paint-color-input");
        const bgInput = document.getElementById("paint-bg-input");
        const sizeInput = document.getElementById("paint-size");
        const sizeLabel = document.getElementById("paint-size-label");
        const status = document.getElementById("paint-status");

        colorInput.oninput = () => { brushColor = colorInput.value; };
        bgInput.oninput = () => {
            bgColor = bgInput.value;
            canvas.style.background = bgColor;
        };
        sizeInput.oninput = () => {
            brushSize = parseInt(sizeInput.value, 10);
            sizeLabel.textContent = brushSize;
        };

        function getPos(e) {
            const r = canvas.getBoundingClientRect();
            const scaleX = canvas.width / r.width;
            const scaleY = canvas.height / r.height;
            return {
                x: (e.clientX - r.left) * scaleX,
                y: (e.clientY - r.top) * scaleY,
            };
        }

        canvas.onmousedown = (e) => {
            if (mode === "text") {
                const text = prompt("Введите текст:");
                if (text) {
                    const p = getPos(e);
                    ctx.fillStyle = brushColor;
                    ctx.font = `${brushSize * 6}px 'Segoe UI', sans-serif`;
                    ctx.textBaseline = "top";
                    ctx.fillText(text, p.x, p.y);
                }
                return;
            }
            drawing = true;
            const p = getPos(e);
            lastX = p.x; lastY = p.y;
        };

        canvas.onmousemove = (e) => {
            if (!drawing) return;
            const p = getPos(e);
            ctx.beginPath();
            ctx.strokeStyle = mode === "eraser" ? bgColor : brushColor;
            ctx.lineWidth = brushSize;
            ctx.lineCap = "round";
            ctx.lineJoin = "round";
            ctx.moveTo(lastX, lastY);
            ctx.lineTo(p.x, p.y);
            ctx.stroke();
            lastX = p.x; lastY = p.y;
        };

        canvas.onmouseup = () => { drawing = false; };
        canvas.onmouseleave = () => { drawing = false; };

        window._paintSetMode = (m) => {
            mode = m;
            const cursors = { brush: "crosshair", eraser: "cell", text: "text" };
            canvas.style.cursor = cursors[m] || "crosshair";
            status.textContent = { brush: "Кисть", eraser: "Ластик", text: "Текст" }[m] || m;
        };

        window._paintClear = () => {
            ctx.fillStyle = bgColor;
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            status.textContent = "Холст очищен";
        };

        window._paintSave = () => {
            const link = document.createElement("a");
            link.download = "paint-" + Date.now() + ".png";
            link.href = canvas.toDataURL("image/png");
            link.click();
        };

        const fileInput = document.getElementById("paint-file-input");

        if (fileInput) {
            fileInput.onchange = (e) => {
                const file = e.target.files?.[0];
                if (!file) return;
                if (!file.type.startsWith("image/")) {
                    status.textContent = "❌ Можно загружать только изображения";
                    toast("❌ Только изображения PNG/JPG/WebP/GIF");
                    return;
                }
                if (file.size > 10 * 1024 * 1024) {
                    status.textContent = "❌ Файл больше 10 МБ";
                    toast("❌ Файл больше 10 МБ");
                    return;
                }

                const reader = new FileReader();
                reader.onload = (ev) => {
                    const img = new Image();
                    img.onload = () => {
                        const maxWidth = 1200;
                        const scale = Math.min(1, maxWidth / img.width);
                        const newW = Math.round(img.width * scale);
                        const newH = Math.round(img.height * scale);
                        canvas.width = newW;
                        canvas.height = newH;
                        ctx.fillStyle = "#ffffff";
                        ctx.fillRect(0, 0, newW, newH);
                        ctx.drawImage(img, 0, 0, newW, newH);
                        status.textContent = `✓ Загружено: ${file.name} (${newW}×${newH})`;
                        toast(`✓ Загружено: ${file.name}`);
                    };
                    img.onerror = () => {
                        status.textContent = "❌ Не удалось загрузить изображение";
                        toast("❌ Не удалось загрузить изображение");
                    };
                    img.src = ev.target.result;
                };
                reader.readAsDataURL(file);
                e.target.value = "";
            };
        }
    }


    function render() {
        const app = document.getElementById("app");

        if (localStorage.getItem("alpha-auth") !== "1") {
            app.innerHTML = login();
            bind();
            return;
        }

        if (!state.user) {
            app.innerHTML = `<div class="auth"><div class="auth-card">Загрузка…</div></div>`;
            return;
        }

        if (state.route !== "video") {
            if (typeof destroyRecorder === "function") destroyRecorder();
        }

        if (state.route === "video") {
            setTimeout(() => {
                if (state.currentRoomId) initBoard(state.currentRoomId);
            }, 100);
        }

        if (!canAccess(state.role, state.route)) {
            state.route = "dashboard";
            location.hash = "dashboard";
            return;
        }

        if (
            state.route === "students" &&
            state.role === "admin" &&
            !state.studentsLoaded &&
            !state.studentsLoading
        ) {
            loadStudents().then(() => {
                if (state.route === "students") render();
            });
        }

        if (state.route === "paint") {
            setTimeout(initPaint, 0);
        }

        if (
            state.route === "teachers" &&
            state.role === "admin" &&
            !state.teachersLoaded &&
            !state.teachersLoading
        ) {
            loadTeachers().then(() => {
                if (state.route === "teachers") render();
            });
        }

        if (state.route === "grades" && !state.gradesLoaded && !state.gradesLoading) {
            loadGrades().then(() => {
                if (state.route === "grades") render();
            });
        }

        if (state.route === "gradebook" && state.role === "teacher" && !state.gradebookLoaded && !state.gradebookLoading) {
            const today = new Date().toLocaleDateString("sv-SE");
            loadGradebookLessons(today).then(() => {
                if (state.route === "gradebook") render();
            });
        }

        if (
            state.route === "classes" &&
            state.role === "admin" &&
            !state.groupsLoaded &&
            !state.groupsLoading
        ) {
            loadGroups().then(() => {
                if (state.route === "classes") render();
            });
        }

        if (
            state.route === "schedule" &&
            !state.scheduleLoaded &&
            !state.scheduleLoading
        ) {
            loadSchedule().then(() => {
                if (state.route === "schedule") render();
            });
        }

        if ((state.route === "check-homework" || state.route === "grade-homework")
            && state.role === "teacher"
            && !state.submissionsLoaded
            && !state.submissionsLoading) {
            loadSubmissions().then(() => render());
        }

        if (
            state.route === "attendance" &&
            !state.attendanceLoaded &&
            !state.attendanceLoading
        ) {
            loadAttendanceToday().then(() => {
                if (state.route === "attendance") render();
            });
        }

        if (
            state.route === "users" &&
            state.role === "admin" &&
            !state.usersLoaded &&
            !state.usersLoading
        ) {
            loadUsers().then(() => {
                if (state.route === "users") render();
            });
        }

        if (
            state.route === "subjects" &&
            state.role === "admin" &&
            !state.disciplinesLoaded &&
            !state.disciplinesLoading
        ) {
            loadDisciplines().then(() => {
                if (state.route === "subjects") render();
            });
        }

        if (state.route === "dashboard" && !state.dashboardLoaded && !state.dashboardLoading) {
            loadDashboard().then(() => {
                if (state.route === "dashboard") render();
            });
        }

        if ((state.route === "dashboard" || state.route === "check-homework" || state.route === "grade-homework")
            && (state.role === "teacher" || state.role === "admin")
            && !state.submissionsLoaded && !state.submissionsLoading) {
            loadSubmissions().then(() => render());
        }

        const pages = {
            dashboard,
            schedule,
            grades,
            students,
            teachers,
            homework,
            video: videoRoomPage,
            communications: communicationsPage,
            ide,
            profile,
            "create-homework": createHomework,
            paint: paintPage,
            "check-homework": checkHomeworkPage,
            "grade-homework": checkHomeworkPage,
            gradebook: gradebookPage,
            attendance,
            users: () => adminPage("users"),
            classes,
            subjects,
            "ide-submissions": ideSubmissionsPage,
        };

        const fn = pages[state.route] || dashboard;

        Promise.resolve(fn()).then((html) => {
            app.innerHTML = html;
            bind();
        });
    }

    const RELOAD = {
        "reload-homeworks": loadHomeworks,
        "reload-users": loadUsers,
        "reload-grades": loadGrades,
        "reload-dashboard": loadDashboard,
    };

    function videoRoomPage() {
        const roomId = state.currentRoomId || 0;
        if (!roomId) {
            return layout("Видеоурок", `
              <div class="card">
                <div class="muted">Комната не выбрана. Откройте расписание и нажмите «📹 Войти в видеоурок» на паре.</div>
              </div>`);
        }

        const roomName = "edu-lesson-" + roomId;
        const lesson = state.schedule.find(s => s.monthScheduleId === roomId);
        const lessonTitle = lesson
            ? `${lesson.disciplineTitle} · ${lesson.groupTitle}`
            : "Видеоурок";

        const displayName = encodeURIComponent(state.user?.login || "Гость");

        return layout("Видеоурок", `
          <div class="toolbar" style="flex-wrap:wrap;gap:6px">
            <b>${esc(lessonTitle)}</b>
            <span class="muted">· Комната: ${esc(roomName)}</span>
            <div class="spacer"></div>
            <button class="btn danger" data-action="rec-start" id="rec-start-btn">⏺ Начать запись</button>
            <button class="btn" data-action="rec-stop" id="rec-stop-btn" style="display:none">⏹ Остановить</button>
            <span class="muted" id="rec-indicator" style="display:none">
              <span style="color:#e11;font-weight:bold">● REC</span>
              <span id="rec-timer">00:00</span>
            </span>
          </div>

          <div class="card" style="padding:0;overflow:hidden">
            <iframe
              src="https://meet.jit.si/${esc(roomName)}#config.prejoinPageEnabled=false&config.disableDeepLinking=true&userInfo.displayName=%22${displayName}%22"
              style="width:100%;height:80vh;border:0"
              allow="camera; microphone; fullscreen; display-capture; autoplay; clipboard-write"
              allowfullscreen>
            </iframe>
          </div>
          <div class="toolbar" style="margin-top:12px">
            <button class="btn" data-action="copy-video-link" data-room="${esc(roomName)}">📋 Скопировать ссылку</button>
            <button class="btn" data-action="leave-video">← Выйти</button>
          </div>
        `);
    }

    let _recorder = null;
    let _recordChunks = [];
    let _recordTimerInterval = null;
    let _recordSeconds = 0;

    async function startRecording() {
        if (_recorder && _recorder.state === "recording") {
            toast("Запись уже идёт");
            return;
        }

        if (!navigator.mediaDevices || !navigator.mediaDevices.getDisplayMedia) {
            toast("❌ Ваш браузер не поддерживает запись экрана");
            return;
        }

        try {
            const stream = await navigator.mediaDevices.getDisplayMedia({
                video: { frameRate: 30 },
                audio: true,
            });

            _recordChunks = [];
            _recorder = new MediaRecorder(stream, {
                mimeType: pickRecorderMime(),
            });

            _recorder.ondataavailable = (e) => {
                if (e.data && e.data.size > 0) {
                    _recordChunks.push(e.data);
                }
            };

            _recorder.onstop = () => {
                const blob = new Blob(_recordChunks, { type: _recorder.mimeType });
                const url = URL.createObjectURL(blob);

                const now = new Date();
                const stamp = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}_${String(now.getHours()).padStart(2, "0")}-${String(now.getMinutes()).padStart(2, "0")}`;
                const filename = `video-lesson-${state.currentRoomId || "x"}-${stamp}.webm`;

                const a = document.createElement("a");
                a.href = url;
                a.download = filename;
                document.body.appendChild(a);
                a.click();
                a.remove();
                setTimeout(() => URL.revokeObjectURL(url), 5000);

                stopRecordTimer();
                const startBtn = document.getElementById("rec-start-btn");
                const stopBtn = document.getElementById("rec-stop-btn");
                const indicator = document.getElementById("rec-indicator");
                if (startBtn) startBtn.style.display = "";
                if (stopBtn) stopBtn.style.display = "none";
                if (indicator) indicator.style.display = "none";

                stream.getTracks().forEach(t => t.stop());

                toast("✓ Запись сохранена: " + filename);
                _recorder = null;
            };

            _recorder.start(1000);

            const startBtn = document.getElementById("rec-start-btn");
            const stopBtn = document.getElementById("rec-stop-btn");
            const indicator = document.getElementById("rec-indicator");
            if (startBtn) startBtn.style.display = "none";
            if (stopBtn) stopBtn.style.display = "";
            if (indicator) indicator.style.display = "";

            startRecordTimer();
            toast("⏺ Запись началась");
        } catch (err) {
            console.warn("Запись отменена или недоступна:", err);
            if (err.name !== "NotAllowedError" && err.name !== "AbortError") {
                toast("Ошибка записи: " + (err.message || err));
            }
        }
    }

    function stopRecording() {
        if (_recorder && _recorder.state === "recording") {
            _recorder.stop();
            toast("⏹ Запись остановлена, идёт сохранение файла…");
        }
    }

    function pickRecorderMime() {
        const candidates = [
            "video/webm;codecs=vp9,opus",
            "video/webm;codecs=vp8,opus",
            "video/webm;codecs=h264,opus",
            "video/webm",
        ];
        for (const m of candidates) {
            if (typeof MediaRecorder !== "undefined" && MediaRecorder.isTypeSupported(m)) {
                return m;
            }
        }
        return "";
    }

    function startRecordTimer() {
        _recordSeconds = 0;
        updateRecordTimer();
        _recordTimerInterval = setInterval(() => {
            _recordSeconds++;
            updateRecordTimer();
        }, 1000);
    }

    function updateRecordTimer() {
        const el = document.getElementById("rec-timer");
        if (!el) return;
        const mm = String(Math.floor(_recordSeconds / 60)).padStart(2, "0");
        const ss = String(_recordSeconds % 60).padStart(2, "0");
        el.textContent = `${mm}:${ss}`;
    }

    function stopRecordTimer() {
        if (_recordTimerInterval) {
            clearInterval(_recordTimerInterval);
            _recordTimerInterval = null;
        }
        _recordSeconds = 0;
    }

    function destroyRecorder() {
        if (_recorder && _recorder.state === "recording") {
            try { _recorder.stop(); } catch { }
        }
        stopRecordTimer();
        _recorder = null;
        _recordChunks = [];
    }

    function updateBoardAccessUI() {
        if (!_board) return;
        const panel = document.getElementById("board-access-panel");
        if (!panel) return;

        if (!_board.isTeacher) {
            const allowed = _board.allowedUserIds.includes(_board.currentUserId);
            panel.innerHTML = allowed
                ? `<span class="badge success">✅ Вам разрешено рисовать</span>`
                : `<span class="badge warning">🔒 Преподаватель пока не разрешил вам рисовать</span>`;
            return;
        }

        const others = _board.participantUserIds.filter(id => id !== _board.currentUserId);
        if (!others.length) {
            panel.innerHTML = `<span class="muted">В комнате пока никого нет</span>`;
            return;
        }

        const list = others.map(id => {
            const allowed = _board.allowedUserIds.includes(id);
            return `
              <label style="display:inline-flex;gap:6px;align-items:center;margin-right:12px">
                <input type="checkbox" data-board-user="${id}" ${allowed ? "checked" : ""}>
                Студент #${id}
              </label>`;
        }).join("");

        panel.innerHTML = `<b>Разрешить рисовать:</b> <div style="margin-top:8px">${list}</div>`;

        panel.querySelectorAll("[data-board-user]").forEach(chk => {
            chk.onchange = () => {
                const userId = parseInt(chk.dataset.boardUser, 10);
                if (typeof setDrawingPermission === "function") {
                    setDrawingPermission(_board.roomId, userId, chk.checked);
                }
            };
        });
    }

    function bind() {
        document.querySelectorAll("[data-route]").forEach((b) => {
            b.onclick = () => setRoute(b.dataset.route);
        });

        document.querySelectorAll("[data-role]").forEach((b) => {
            b.onclick = () => {
                state.role = b.dataset.role;
                localStorage.setItem("alpha-role", state.role);
                render();
            };
        });

        const msgs = document.getElementById("chat-messages-container");
        if (msgs) msgs.scrollTop = msgs.scrollHeight;

        document.querySelectorAll("[data-action]").forEach((b) => {
            b.onclick = async () => {
                const a = b.dataset.action;

                if (a === "brand-click") {
                    state.brandClicks++;
                    clearTimeout(state.brandClickTimer);
                    state.brandClickTimer = setTimeout(() => { state.brandClicks = 0; }, 1800);
                    if (state.brandClicks >= 5) {
                        state.brandClicks = 0;
                        state.arrowTheme = !state.arrowTheme;
                        localStorage.setItem("alpha-arrow-theme", state.arrowTheme ? "1" : "0");
                        applyTheme();
                        toast(state.arrowTheme ? "Секретный режим Зелёной Стрелы активирован!" : "Обычный дизайн восстановлен");
                        render();
                    }
                    return;
                }
                if (a === "start-call") { window.open("https://meet.jit.si/ProjectAlpha-" + Math.random().toString(36).slice(2, 9), "_blank", "noopener,noreferrer"); return; }
                if (a === "theme") { setTheme(nextTheme()); return; }
                if (a === "menu") { state.sidebar = !state.sidebar; render(); return; }

                if (a === "logout") {
                    try { await api.logout(); } catch { }
                    try { await stopSignalR(); } catch { }
                    localStorage.removeItem("alpha-auth");
                    localStorage.removeItem("alpha-role");
                    state.user = null;
                    state.role = "student";
                    state.homeworks = [];
                    state.users = [];
                    render();
                    return;
                }
                if (a === "close-ide-code-modal") {
                    document.getElementById("ide-code-modal")?.remove();
                    return;
                }
                if (a === "create-chat") {
                    const title = document.getElementById("new-chat-title")?.value?.trim();
                    if (!title) { toast("Введите название"); return; }
                    try {
                        await api.chats.create(title);
                        state.chatsLoaded = false;
                        await loadChats();
                        render();
                    } catch (err) { toast("Ошибка: " + (err.message || err)); }
                    return;
                }
                if (a === "open-chat" && b.dataset.id) {
                    await loadChatMessages(parseInt(b.dataset.id, 10));
                    render();
                    return;
                }
                if (a === "send-message") {
                    const text = document.getElementById("chat-message-input")?.value?.trim();
                    if (!text) return;
                    try {
                        await api.chats.send(state.currentChatId, text);
                        document.getElementById("chat-message-input").value = "";
                        await loadChatMessages(state.currentChatId);
                        render();
                        requestAnimationFrame(() => {
                            const box = document.getElementById("chat-messages-container");
                            if (box) box.scrollTop = box.scrollHeight;
                        });
                    } catch (err) { toast("Ошибка: " + (err.message || err)); }
                    return;
                }

                if (a === "view-ide-code" && b.dataset.code) {
                    const code = decodeURIComponent(escape(atob(b.dataset.code)));
                    const student = b.dataset.student;

                    document.body.insertAdjacentHTML("beforeend", modal({
                        id: "ide-code-modal",
                        title: `Код от ${student}`,
                        closeAction: "close-ide-code-modal",
                        body: `<pre style="background:#1e1e1e;color:#d4d4d4;padding:16px;border-radius:8px;overflow:auto;max-height:60vh;white-space:pre-wrap;font-family:monospace">${esc(code)}</pre>`,
                        actions: `<button class="btn" data-action="close-ide-code-modal">Закрыть</button>`
                    }));
                    return;
                }
                if (a === "close-ide-code-modal") {
                    document.getElementById("ide-code-modal")?.remove();
                    return;
                }

                if (a === "login") {
                    const loginEl = document.getElementById("login");
                    const passEl = document.getElementById("password");
                    if (!loginEl?.value || !passEl?.value) {
                        toast("Заполните логин и пароль");
                        return;
                    }
                    try {
                        await api.login(loginEl.value, passEl.value);
                        await initSession();
                        state.route = "dashboard";
                        location.hash = "dashboard";
                        render();
                    } catch (err) {
                        toast("Ошибка входа: " + (err.message || err));
                    }
                    return;
                }

                if (a === "open-invite-modal" && b.dataset.id) {
                    await openInviteModal(parseInt(b.dataset.id, 10));
                    return;
                }
                if (a === "invite-submit" && b.dataset.chatId) {
                    const userId = parseInt(document.getElementById("invite-user-select")?.value || "0", 10);
                    if (!userId) { toast("Выберите пользователя"); return; }
                    try {
                        await api.chats.invite(parseInt(b.dataset.chatId, 10), userId);
                        closeModal("invite-modal");
                        toast("✓ Приглашение отправлено");
                    } catch (err) {
                        toast("Ошибка: " + (err.message || err));
                    }
                    return;
                }
                if (a === "close-invite-modal") {
                    closeModal("invite-modal");
                    return;
                }

                if (a === "open-video" && b.dataset.id) {
                    closeModal("lesson-modal");
                    const roomId = parseInt(b.dataset.id, 10);
                    const roomName = "edu-lesson-" + roomId;
                    const displayName = encodeURIComponent(state.user?.login || "Гость");
                    const url = `https://meet.jit.si/${roomName}#config.prejoinPageEnabled=false&config.disableDeepLinking=true&userInfo.displayName=%22${displayName}%22`;
                    window.open(url, "_blank", "noopener");
                    return;
                }

                if (a === "ide-run") {
                    if (!window._aceEditor) {
                        toast("Редактор еще загружается.");
                        return;
                    }

                    const code = window._aceEditor.getValue();
                    const outputEl = document.getElementById("ide-output");
                    if (!outputEl) return;

                    outputEl.textContent = "⏳ Выполняется...";
                    outputEl.style.color = "#facc15";

                    if (!window._pyodide) {
                        window._pyodide = await loadPyodide({
                            stdout: (text) => {
                                outputEl.textContent += text + "\n";
                            },
                            stderr: (text) => {
                                outputEl.textContent += text + "\n";
                                outputEl.style.color = "#ef4444";
                            }
                        });
                    }

                    try {
                        outputEl.textContent = "";
                        outputEl.style.color = "#4ade80";
                        await window._pyodide.runPythonAsync(code);
                        if (!outputEl.textContent.trim()) {
                            outputEl.textContent = "✅ Код выполнен без ошибок (нет вывода).";
                        }
                    } catch (err) {
                        outputEl.textContent = "❌ Ошибка:\n" + err.message;
                        outputEl.style.color = "#ef4444";
                    }
                    return;
                }

                if (a === "ide-submit") {
                    if (!window._aceEditor) {
                        toast("Редактор еще загружается. Подождите секунду.");
                        return;
                    }

                    const teacherSelect = document.getElementById("ide-teacher-select");
                    const teacherId = teacherSelect?.value ? parseInt(teacherSelect.value, 10) : null;

                    if (!teacherId) {
                        toast("Выберите преподавателя перед отправкой");
                        return;
                    }

                    try {
                        b.disabled = true;
                        b.textContent = "⏳ Отправка...";
                        const codeToSend = window._aceEditor.getValue();

                        if (!codeToSend.trim()) {
                            toast("Редактор пуст. Напишите код перед отправкой.");
                            b.disabled = false;
                            b.textContent = "📤 Отправить преподавателю";
                            return;
                        }

                        await api.ide.submit(codeToSend, "python", teacherId);
                        toast("✓ Код успешно отправлен преподавателю!");
                    } catch (err) {
                        toast("Ошибка: " + (err.message || err));
                    } finally {
                        b.disabled = false;
                        b.textContent = "📤 Отправить преподавателю";
                    }
                    return;
                }

                if (a === "leave-video") {
                    state.currentRoomId = null;
                    setRoute("schedule");
                    return;
                }

                if (a === "rec-start") { await startRecording(); return; }
                if (a === "rec-stop") { stopRecording(); return; }

                if (a === "paint-load-png") {
                    document.getElementById("paint-file-input")?.click();
                    return;
                }

                if (a === "copy-video-link" && b.dataset.room) {
                    const url = `https://meet.jit.si/${b.dataset.room}`;
                    navigator.clipboard.writeText(url)
                        .then(() => toast("✓ Ссылка скопирована"))
                        .catch(() => toast("Ссылка: " + url));
                    return;
                }

                if (a === "paint-color") { document.getElementById("paint-color-input").click(); return; }
                if (a === "paint-bg-color") { document.getElementById("paint-bg-input").click(); return; }
                if (a === "paint-brush") { window._paintSetMode?.("brush"); return; }
                if (a === "paint-eraser") { window._paintSetMode?.("eraser"); return; }
                if (a === "paint-text") { window._paintSetMode?.("text"); return; }
                if (a === "paint-clear") { window._paintClear?.(); return; }
                if (a === "paint-save") { window._paintSave?.(); return; }

                if (a === "save-profile") {
                    const phone = document.getElementById("p-phone")?.value || null;
                    const email = document.getElementById("p-email")?.value || null;
                    try {
                        await api.updateMe({ phoneNumber: phone, email });
                        toast("✓ Профиль сохранён");
                        state.user = await api.me();
                        render();
                    } catch (err) {
                        toast("Ошибка: " + (err.message || err));
                    }
                    return;
                }

                if (RELOAD[a]) { await RELOAD[a](); render(); return; }

                if (a === "load-gradebook") {
                    const date = document.getElementById("gb-date")?.value;
                    if (date) {
                        state.gradebookLoaded = false;
                        await loadGradebookLessons(date);
                        render();
                    }
                    return;
                }

                if (a === "reload-gradebook") {
                    state.gradebookLoaded = false;
                    await loadGradebookLessons(state.gradebookDate || new Date().toLocaleDateString("sv-SE"));
                    render();
                    return;
                }

                if (a === "open-gradebook" && b.dataset.id) { await openGradebook(parseInt(b.dataset.id, 10)); return; }
                if (a === "close-gradebook-modal") { document.getElementById("gradebook-modal")?.remove(); return; }
                if (a === "save-gradebook" && b.dataset.monthScheduleId) { await saveGradebook(parseInt(b.dataset.monthScheduleId, 10)); return; }

                if (a === "reload-submissions") { state.submissionsLoaded = false; await loadSubmissions(); render(); return; }

                if (a === "add" && state.route === "users") { openAddUserModal(); return; }
                if (a === "save-user") { await saveUser(); return; }
                if (a === "update-user" && b.dataset.id) { await updateUser(b.dataset.id); return; }
                if (a === "close-user-modal") { closeModal("user-modal"); return; }

                if (a === "reload-disciplines") { state.disciplinesLoaded = false; await loadDisciplines(); render(); return; }
                if (a === "add-discipline") { openAddDisciplineModal(); return; }
                if (a === "edit-discipline" && b.dataset.id) {
                    const d = state.disciplines.find((x) => x.disciplineId === parseInt(b.dataset.id, 10));
                    if (d) openEditDisciplineModal(d);
                    return;
                }
                if (a === "delete-discipline" && b.dataset.id) { await deleteDiscipline(b.dataset.id); return; }
                if (a === "save-discipline") { await saveDiscipline(); return; }
                if (a === "update-discipline" && b.dataset.id) { await updateDiscipline(b.dataset.id); return; }
                if (a === "close-discipline-modal") { document.getElementById("discipline-modal")?.remove(); return; }

                if (a === "reload-groups") { state.groupsLoaded = false; await loadGroups(); render(); return; }
                if (a === "add-group") { openAddGroupModal(); return; }
                if (a === "edit-group" && b.dataset.id) {
                    const g = state.groups.find((x) => x.groupId === parseInt(b.dataset.id, 10));
                    if (g) openEditGroupModal(g);
                    return;
                }
                if (a === "delete-group" && b.dataset.id) { await deleteGroup(b.dataset.id); return; }
                if (a === "save-group") { await saveGroup(); return; }
                if (a === "update-group" && b.dataset.id) { await updateGroup(b.dataset.id); return; }
                if (a === "close-group-modal") { document.getElementById("group-modal")?.remove(); return; }

                if (a === "publish-homework") { await publishHomework(); return; }

                if (a === "reload-students") { state.studentsLoaded = false; await loadStudents(); render(); return; }
                if (a === "edit-student" && b.dataset.id) {
                    const s = state.students.find((x) => x.studentId === parseInt(b.dataset.id, 10));
                    if (s) await openEditStudentModal(s);
                    return;
                }
                if (a === "save-student" && b.dataset.id) { await saveStudent(b.dataset.id); return; }
                if (a === "close-student-modal") { document.getElementById("student-modal")?.remove(); return; }

                if (a === "reload-teachers") { state.teachersLoaded = false; await loadTeachers(); render(); return; }
                if (a === "edit-teacher" && b.dataset.id) {
                    const t = state.teachers.find((x) => x.teacherId === parseInt(b.dataset.id, 10));
                    if (t) await openEditTeacherModal(t);
                    return;
                }
                if (a === "save-teacher" && b.dataset.id) { await saveTeacher(b.dataset.id); return; }
                if (a === "close-teacher-modal") { document.getElementById("teacher-modal")?.remove(); return; }

                if (a === "edit" && state.route === "users" && b.dataset.id) {
                    const user = state.users.find((u) => u.userId === parseInt(b.dataset.id, 10));
                    if (user) openEditUserModal(user);
                    return;
                }
                if (a === "delete" && state.route === "users" && b.dataset.id) { await deleteUser(b.dataset.id); return; }

                if (a === "reload-attendance") { state.attendanceLoaded = false; await loadAttendanceToday(); render(); return; }
                if (a === "open-attendance" && b.dataset.id) { await openAttendanceSheet(parseInt(b.dataset.id, 10)); return; }
                if (a === "close-attendance-modal") { document.getElementById("attendance-modal")?.remove(); return; }
                if (a === "save-attendance" && b.dataset.scheduleId) { await saveAttendance(parseInt(b.dataset.scheduleId, 10)); return; }

                if (a === "reload-schedule") { state.scheduleLoaded = false; await loadSchedule(); render(); return; }
                if (a === "schedule-prev") {
                    const m = state.scheduleMonth.month - 1;
                    if (m < 0) state.scheduleMonth = { year: state.scheduleMonth.year - 1, month: 11 };
                    else state.scheduleMonth.month = m;
                    state.scheduleLoaded = false;
                    render();
                    return;
                }
                if (a === "schedule-next") {
                    const m = state.scheduleMonth.month + 1;
                    if (m > 11) state.scheduleMonth = { year: state.scheduleMonth.year + 1, month: 0 };
                    else state.scheduleMonth.month = m;
                    state.scheduleLoaded = false;
                    render();
                    return;
                }
                if (a === "schedule-today") {
                    const now = new Date();
                    state.scheduleMonth = { year: now.getFullYear(), month: now.getMonth() };
                    state.scheduleLoaded = false;
                    render();
                    return;
                }
                if (a === "add-lesson") { await openLessonModal(null); return; }
                if (a === "save-lesson") { await saveLesson(b.dataset.id ? parseInt(b.dataset.id, 10) : null); return; }
                if (a === "close-lesson-modal") { document.getElementById("lesson-modal")?.remove(); return; }
                if (a === "open-lesson" && b.dataset.id) { await openLesson(parseInt(b.dataset.id, 10)); return; }
                if (a === "edit-lesson" && b.dataset.id) {
                    document.getElementById("lesson-modal")?.remove();
                    const lesson = state.schedule.find((s) => s.monthScheduleId === parseInt(b.dataset.id, 10));
                    if (lesson) await openLessonModal(lesson);
                    return;
                }
                if (a === "delete-lesson" && b.dataset.id) { await deleteLesson(parseInt(b.dataset.id, 10)); return; }

                if (a === "open-homework") { await openHomework(b.dataset.id); return; }
                if (a === "close-homework") { closeModal("hw-modal"); return; }
                if (a === "submit-homework") { await submitHomework(b.dataset.id); return; }
                if (a === "grade-homework" && b.dataset.id) { await gradeHomework(b.dataset.id); return; }

                if (["save", "publish", "submit", "run", "today", "edit"].includes(a)) {
                    toast(
                        a === "run" ? "✓ Выполнение завершено" :
                            a === "publish" ? "✓ Опубликовано" :
                                "✓ Изменения сохранены"
                    );
                }
            };
        });

        const ideLangSelect = document.getElementById("ide-language-select");
        if (ideLangSelect) {
            ideLangSelect.onchange = () => {
                state.ideLanguage = ideLangSelect.value;
                localStorage.setItem("alpha-ide-language", state.ideLanguage);
                render();
            };
        }

        const show = document.getElementById("showpass");
        if (show) {
            show.onchange = () => {
                const p = document.getElementById("password");
                if (p) p.type = show.checked ? "text" : "password";
            };
        }

        const ts = document.getElementById("theme-select");
        if (ts) ts.onchange = () => setTheme(ts.value);

        const subStatus = document.getElementById("sub-status");
        if (subStatus) {
            subStatus.value = state.submissionFilter || "";
            subStatus.onchange = () => {
                state.submissionFilter = subStatus.value;
                render();
            };
        }

        if (state.route === "ide") {
            setTimeout(() => initAceEditor(), 50);

            const select = document.getElementById("ide-teacher-select");
            if (select) {
                api.teachers.list({ page: 1, pageSize: 100 })
                    .then(res => {
                        const teachers = res.items || [];
                        if (teachers.length === 0) {
                            select.innerHTML = `<option value="">Нет доступных преподавателей</option>`;
                            return;
                        }
                        select.innerHTML = `<option value="">— Выберите преподавателя —</option>` +
                            teachers.map(t => `<option value="${t.teacherId}">${esc(t.lastName)} ${esc(t.firstName)} ${esc(t.patronymic || '')}</option>`).join("");
                    })
                    .catch(err => {
                        select.innerHTML = `<option value="">Ошибка загрузки</option>`;
                        toast("Не удалось загрузить преподавателей: " + err.message);
                    });
            }
        }
    }



    function adminPage(type) {
        if (type !== "users") {
            const names = ["10-А", "10-Б", "11-А", "11-Б"];
            const title = type === "classes" ? "Классы" : "Предметы";
            return layout(
                title,
                `<div class="toolbar">
      <div class="spacer"></div>
      <button class="btn primary" data-action="add">＋ Добавить</button>
    </div>
    <div class="card">
      <div class="table-wrap">
        <table class="table">
          <thead><tr><th>Название</th><th>Статус</th><th>Изменено</th><th>Действия</th></tr></thead>
          <tbody>
            ${names
                    .map(
                        (n) =>
                            `<tr>
                    <td><b>${n}</b></td>
                    <td><span class="badge success">Активно</span></td>
                    <td>Сегодня</td>
                    <td>
                      <button class="btn" data-action="edit">Редактировать</button>
                      <button class="btn" data-action="delete">Удалить</button>
                    </td>
                  </tr>`
                    )
                    .join("")}
          </tbody>
        </table>
      </div>
    </div>`
            );
        }

        if (state.usersLoading) {
            return layout(
                "Пользователи",
                `<div class="card"><div class="loading-row"><div class="spinner"></div><span>Загрузка…</span></div></div>`
            );
        }
        if (state.usersError) {
            return layout(
                "Пользователи",
                `<div class="card">
      <div class="muted">Ошибка: ${esc(state.usersError)}</div>
      <button class="btn primary" data-action="reload-users" style="margin-top:12px">Повторить</button>
    </div>`
            );
        }

        return layout(
            "Пользователи",
            `<div class="toolbar">
    <div class="spacer"></div>
    <button class="btn" data-action="reload-users">Обновить</button>
    <button class="btn primary" data-action="add">＋ Добавить</button>
  </div>
  <div class="card">
    <div class="table-wrap">
      <table class="table">
        <thead>
          <tr><th>Логин</th><th>Email</th><th>Роль</th><th>Статус</th><th>Действия</th></tr>
        </thead>
        <tbody>
          ${state.users
                .map(
                    (u) => `<tr>
                <td><b>${esc(u.login)}</b></td>
                <td>${esc(u.email || "")}</td>
                <td>${ROLES[ROLE_BY_ID[u.roleId]]?.label || u.roleCode || "—"}</td>
                <td><span class="badge ${u.isActive ? "success" : "danger"}">${u.isActive ? "Активно" : "Отключён"}</span></td>
                <td>
                  <button class="btn" data-action="edit" data-id="${u.userId}">Редактировать</button>
                  <button class="btn" data-action="delete" data-id="${u.userId}">Удалить</button>
                </td>
              </tr>`
                )
                .join("")}
        </tbody>
      </table>
    </div>
  </div>`
        );
    }

    function login() {
        return `<div class="auth">
  <div class="auth-card">
    <div class="auth-brand"><div class="logo">A</div></div>
    <h1>Project Alpha</h1>
    <div class="auth-sub">Электронный дневник</div>

    <div class="field"><label>Логин</label>
      <input class="input" id="login" placeholder="Введите логин" autocomplete="username">
    </div>
    <div class="field"><label>Пароль</label>
      <input class="input" id="password" type="password" placeholder="Введите пароль" autocomplete="current-password">
    </div>

    <label style="display:flex;gap:8px;align-items:center;margin-bottom:16px">
      <input type="checkbox" id="showpass"> Показать пароль
    </label>

    <button class="btn primary" style="width:100%" data-action="login">Войти</button>
  </div>
</div>`;
    }

    function initBoard(roomId) {
        console.log(">>> [1] initBoard вызван, roomId =", roomId);
        const canvas = document.getElementById("board-canvas");
        console.log(">>> [2] canvas найден:", !!canvas);
        if (!canvas) return;
        const ctx = canvas.getContext("2d");
        console.log(">>> [3] ctx получен:", !!ctx);

        _board = {
            canvas, ctx,
            drawing: false,
            lastX: 0, lastY: 0,
            color: "#000000",
            size: 3,
            eraser: false,
            roomId,
            allowedUserIds: [],
            isTeacher: state.role === "teacher" || state.role === "admin",
            participantUserIds: [],
            teacherUserId: 0,
            currentUserId: state.user?.userId || 0,
        };
        console.log(">>> [4] _board создан:", !!_board);

        ctx.fillStyle = "#ffffff";
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        const colorInput = document.getElementById("board-color");
        const sizeInput = document.getElementById("board-size");
        if (colorInput) colorInput.oninput = () => { _board.color = colorInput.value; };
        if (sizeInput) sizeInput.oninput = () => { _board.size = parseInt(sizeInput.value, 10); };

        function getPos(e) {
            const r = canvas.getBoundingClientRect();
            const sx = canvas.width / r.width;
            const sy = canvas.height / r.height;
            return { x: (e.clientX - r.left) * sx, y: (e.clientY - r.top) * sy };
        }

        function canDraw() {
            return _board.allowedUserIds.includes(_board.currentUserId);
        }

        canvas.onmousedown = (e) => {
            if (!canDraw()) {
                toast("🔒 Преподаватель не разрешил вам рисовать");
                return;
            }
            _board.drawing = true;
            const p = getPos(e);
            _board.lastX = p.x; _board.lastY = p.y;
        };

        canvas.onmousemove = (e) => {
            if (!_board.drawing) return;
            const p = getPos(e);
            const stroke = {
                fromX: _board.lastX, fromY: _board.lastY,
                toX: p.x, toY: p.y,
                color: _board.color, width: _board.size, isEraser: _board.eraser,
            };
            drawStrokeLocal(stroke);
            if (typeof sendBoardStroke === "function") sendBoardStroke(roomId, stroke);
            _board.lastX = p.x; _board.lastY = p.y;
        };

        canvas.onmouseup = () => { _board.drawing = false; };
        canvas.onmouseleave = () => { _board.drawing = false; };

        if (typeof joinBoard === "function") {
            console.log(">>> [5] вызываю joinBoard с ролью:", _board.isTeacher ? "Teacher" : "Student");
            joinBoard(roomId, _board.currentUserId, _board.isTeacher ? "Teacher" : "Student");
        } else {
            console.warn(">>> [5] joinBoard НЕ НАЙДЕН!");
        }

        window._boardOnStroke = (stroke) => drawStrokeLocal(stroke);
        window._boardOnClear = () => {
            if (!_board) return;
            _board.ctx.fillStyle = "#ffffff";
            _board.ctx.fillRect(0, 0, _board.canvas.width, _board.canvas.height);
        };
        window._boardOnPermissions = (payload) => {
            console.log(">>> [6] _boardOnPermissions пришли права:", payload);
            if (!_board) return;
            _board.allowedUserIds = payload.allowedUserIds || [];
            _board.participantUserIds = payload.participantUserIds || [];
            _board.teacherUserId = payload.teacherUserId || 0;

            const allowed = _board.allowedUserIds.includes(_board.currentUserId);
            _board.canvas.style.cursor = allowed ? "crosshair" : "not-allowed";
            updateBoardAccessUI();
        };
        console.log(">>> [7] initBoard завершён успешно");
    }

    document.addEventListener("click", (e) => {
        if (MODAL_IDS.includes(e.target.id)) e.target.remove();
    });

    async function bootstrap() {
        if (localStorage.getItem("alpha-auth") === "1") {
            try {
                await initSession();
            } catch (e) {
                console.warn("Не удалось восстановить сессию:", e);
                localStorage.removeItem("alpha-auth");
                localStorage.removeItem("alpha-role");
                state.user = null;
                state.role = "student";
            }
        }
        render();
    }

    bootstrap();

    window.__app = { state, render };

    return { render };
})();