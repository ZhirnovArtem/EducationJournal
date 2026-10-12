(() => {
    const App = (() => {
        const state = {
            route: location.hash.slice(1) || "dashboard",
            role: localStorage.getItem("alpha-role") || "student",
            theme: localStorage.getItem("alpha-theme") || "system",
            sidebar: false,
            user: null,
            disciplines: [],
            disciplinesLoading: false,
            disciplinesLoaded: false,
            disciplinesError: null,
            disciplinesLoaded: false,   
            homeworks: [],
            loading: false,
            error: null,
            users: [],
            usersLoading: false,
            usersError: null,
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
            student: [...CORE_NAV,
            ["grades", "★", "Оценки"],
            ["homework", "✓", "Домашние задания"],
            ["ide", "</>", "IDE"],
            ...TAIL_NAV,
            ],
            teacher: [...CORE_NAV,
            ["create-homework", "＋", "Создание ДЗ"],
            ["check-homework", "✓", "Проверка ДЗ"],
            ["grade-homework", "★", "Оценивание ДЗ"],
            ["gradebook", "▤", "Выставление оценок"],
            ["attendance", "◉", "Посещаемость"],
            ...TAIL_NAV,
            ],
            admin: [...CORE_NAV,
            ["users", "♙", "Пользователи"],
            ["classes", "▥", "Классы"],
            ["subjects", "◈", "Предметы"],
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

        const MODAL_IDS = ["hw-modal", "user-modal"];
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
            const userLabel = state.user?.login || "Легионер";
            const initial = (userLabel[0] || "Л").toUpperCase();

            return `<div class="app-shell">
      <aside class="sidebar ${state.sidebar ? "open" : ""}">
        <div class="brand"><div class="logo">A</div>Project Alpha</div>
        <div class="nav-label">${ROLES[state.role]?.label || ""}</div>
        ${items
                    .map(
                        ([r, ic, n]) =>
                            `<button class="nav-btn ${state.route === r ? "active" : ""
                            }" data-route="${r}"><span>${ic}</span>${n}</button>`
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
            const teacher = state.role === "teacher";
            const admin = state.role === "admin";
            return layout(
                "Главная",
                `
      <div class="grid grid-4">
        ${[
                    [teacher ? "Средний балл классов" : "Средний балл", teacher ? "4.6" : "4.7", "★"],
                    [teacher ? "Заданий на проверке" : "Активные ДЗ", teacher ? "12" : String(state.homeworks.length || 3), "✓"],
                    ["Посещаемость", "94%", "◉"],
                    [admin ? "Пользователей" : "Рейтинг в классе", admin ? String(state.users.length || 248) : "5 место", "♙"],
                ]
                    .map(
                        (x) =>
                            `<div class="card stat"><div><div class="muted">${x[0]}</div><div class="stat-value">${x[1]}</div></div><div class="stat-icon">${x[2]}</div></div>`
                    )
                    .join("")}
      </div>
      <div class="grid grid-2" style="margin-top:18px">
        <div class="card"><h3>Ближайшие занятия</h3>
          <div class="schedule">${data.lessons.slice(0, 3).map(lesson).join("")}</div>
        </div>
        <div class="card"><h3>Последние действия</h3>
          ${["Добавлено домашнее задание по математике", "Получена оценка за практику", "Обновлено расписание"]
                    .map(
                        (x, i) =>
                            `<div style="padding:13px 0;border-bottom:1px solid var(--border)"><b>${x}</b><div class="muted" style="font-size:12px;margin-top:4px">${i + 1} ч. назад</div></div>`
                    )
                    .join("")}
        </div>
      </div>`
            );
        }

        function schedule() {
            const cells = Array.from({ length: 35 }, (_, i) => {
                const n = i - 2;
                return `<div class="day ${n === 4 ? "today" : ""}">
        <div class="day-num">${n > 0 && n <= 31 ? n : ""}</div>
        ${n > 0 && n <= 31 && n % 4 === 0 ? '<div class="day-event">2 урока</div>' : ""}
      </div>`;
            }).join("");
            return layout(
                "Расписание",
                `<div class="toolbar">
        <div class="tabs">
          <button class="tab active">День</button>
          <button class="tab">Неделя</button>
          <button class="tab">Месяц</button>
        </div>
        <div class="spacer"></div>
        <button class="btn">◀</button><b>4 октября 2026</b><button class="btn">▶</button>
        <button class="btn primary" data-action="today">Сегодня</button>
      </div>
      <div class="grid grid-2">
        <div class="card"><h3>Сегодня · воскресенье</h3>
          <div class="schedule">${data.lessons.map(lesson).join("")}</div>
        </div>
        <div class="card"><h3>Месяц</h3>
          <div class="calendar-wrap">
            <div class="calendar">
              ${["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"].map((x) => `<div class="day-head">${x}</div>`).join("")}
              ${cells}
            </div>
          </div>
        </div>
      </div>`
            );
        }

        function grades() {
            return layout(
                "Оценки",
                `<div class="grid grid-4">
        <div class="card stat"><div><div class="muted">Средний балл</div><div class="stat-value">4.7</div></div><div class="stat-icon">★</div></div>
        ${["Математика", "Программирование", "Физика"]
                    .map(
                        (s, i) =>
                            `<div class="card"><div class="muted">${s}</div><div class="stat-value">${i === 1 ? "5.0" : i === 2 ? "4.3" : "4.8"
                            }</div><div class="progress"><span style="width:${i === 1 ? 100 : i === 2 ? 86 : 96}%"></span></div></div>`
                    )
                    .join("")}
      </div>
      <div class="card" style="margin-top:18px">
        <div class="toolbar">
          <h3 style="margin:0">История оценок</h3>
          <div class="spacer"></div>
          <select class="select" style="width:190px">
            <option>Все типы работ</option>
            <option>Контрольная</option>
            <option>Домашняя</option>
            <option>Устный ответ</option>
          </select>
        </div>
        <div class="table-wrap">
          <table class="table">
            <thead><tr><th>Дата</th><th>Предмет</th><th>Оценка</th><th>Тип работы</th><th>Комментарий</th></tr></thead>
            <tbody>
              ${data.grades
                    .map(
                        (g) =>
                            `<tr><td>${g[0]}</td><td><b>${g[1]}</b></td><td><span class="badge success">${g[2]}</span></td><td>${g[3]}</td><td class="muted">${g[4]}</td></tr>`
                    )
                    .join("")}
            </tbody>
          </table>
        </div>
      </div>`
            );
        }

        function homework() {
            if (state.loading) {
                return layout(
                    "Домашние задания",
                    `<div class="card"><div class="loading-row"><div class="spinner"></div><span>Загрузка…</span></div></div>`
                );
            }
            if (state.error) {
                return layout(
                    "Домашние задания",
                    `<div class="card">
          <div class="muted">Ошибка: ${esc(state.error)}</div>
          <button class="btn primary" data-action="reload-homeworks" style="margin-top:12px">Повторить</button>
        </div>`
                );
            }
            if (!state.homeworks.length) {
                return layout(
                    "Домашние задания",
                    `<div class="card empty">
          <div>Пока нет домашних заданий.</div>
          <button class="btn" data-action="reload-homeworks">Обновить</button>
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

        function createHomework() {
            return layout(
                "Создание ДЗ",
                `<div class="grid grid-2">
        <div class="card">
          <h3>Новое домашнее задание</h3>
          <div class="field"><label>Класс / группа</label>
            <select class="select"><option>10-А</option><option>10-Б</option></select>
          </div>
          <div class="field"><label>Предмет</label>
            <select class="select"><option>Программирование</option><option>Математика</option></select>
          </div>
          <div class="grid grid-2">
            <div class="field"><label>Дата выдачи</label><input class="input" type="date" value="2026-10-04"></div>
            <div class="field"><label>Дедлайн</label><input class="input" type="date" value="2026-10-06"></div>
          </div>
          <div class="field"><label>Название</label>
            <input class="input" placeholder="Например: Работа с массивами">
          </div>
          <div class="field"><label>Описание задания</label>
            <textarea class="textarea" placeholder="Введите условие задания..."></textarea>
          </div>
          <div class="dropzone">📎 Перетащите файлы сюда или <button class="btn">выберите файл</button></div>
          <div style="margin-top:16px;display:flex;gap:8px">
            <button class="btn" data-action="save">Сохранить как черновик</button>
            <button class="btn primary" data-action="publish">Опубликовать</button>
          </div>
        </div>
        <div class="card"><h3>Последние ДЗ</h3>
          ${["Работа с массивами", "Квадратные уравнения", "Лабораторная №3"]
                    .map(
                        (x, i) =>
                            `<div style="padding:14px 0;border-bottom:1px solid var(--border)"><b>${x}</b><div class="muted">Опубликовано ${i + 1} дн. назад</div></div>`
                    )
                    .join("")}
        </div>
      </div>`
            );
        }

        function attendance() {
            return layout(
                "Посещаемость",
                `<div class="toolbar">
        <input class="input" type="date" value="2026-10-04" style="width:180px">
        <select class="select" style="width:200px"><option>10-А · Программирование</option></select>
        <div class="spacer"></div>
        <button class="btn success" data-action="save">Сохранить посещаемость</button>
      </div>
      <div class="card">
        <div class="table-wrap">
          <table class="table">
            <thead><tr><th>Ученик</th><th>Статус</th><th>Причина</th><th>Комментарий</th></tr></thead>
            <tbody>
              ${data.students.slice(0, 5)
                    .map(
                        (n) =>
                            `<tr>
                      <td><b>${n}</b></td>
                      <td><select class="select">
                        <option>✅ Присутствовал</option>
                        <option>❌ Отсутствовал</option>
                        <option>⏰ Опоздал</option>
                      </select></td>
                      <td><select class="select">
                        <option>—</option>
                        <option>Уважительная</option>
                        <option>Неуважительная</option>
                      </select></td>
                      <td><input class="input" placeholder="Комментарий"></td>
                    </tr>`
                    )
                    .join("")}
            </tbody>
          </table>
        </div>
      </div>`
            );
        }

        function ide() {
            return layout(
                "Внутренняя IDE",
                `<div class="toolbar">
        <select class="select" style="width:180px">
          <option>JavaScript</option><option>Python</option>
        </select>
        <div class="spacer"></div>
        <button class="btn" data-action="save">Сохранить</button>
        <button class="btn primary" data-action="run">▶ Запустить</button>
        <button class="btn success" data-action="submit">Отправить преподавателю</button>
      </div>
      <div class="grid grid-2">
        <div class="card">
          <h3>Задача №14 · Массивы</h3>
          <p class="muted">Напишите функцию, которая возвращает сумму всех чётных элементов массива.</p>
          <div class="code">function sumEven(numbers) {
  // Ваш код здесь

}

console.log(sumEven([1,2,3,4,5,6]));</div>
        </div>
        <div class="card">
          <h3>Консоль</h3>
          <div class="console" id="console">Готово к запуску...</div>
          <h3 style="margin-top:20px">Результат проверки</h3>
          <span class="badge success">✓ 8 / 8 тестов пройдено</span>
        </div>
      </div>`
            );
        }

        function profile() {
            const teacher = state.role === "teacher";
            const u = state.user || {};
            const initial = (u.login?.[0] || "Л").toUpperCase();
            return layout(
                "Личный кабинет",
                `<div class="card">
        <div class="profile">
          <div class="profile-avatar">${esc(initial)}</div>
          <div>
            <h2 style="margin:0">${esc(u.login || "Легионер")}</h2>
            <div class="muted">${ROLES[state.role]?.label || ""} · ${teacher ? "Преподаватель информатики" : "10-А класс"}</div>
          </div>
        </div>
      </div>
      <div class="grid grid-2" style="margin-top:18px">
        <div class="card">
          <h3>Профиль</h3>
          ${["ФИО", "Контактный телефон", "Email"]
                    .map(
                        (x, i) =>
                            `<div class="field"><label>${x}</label>
                  <input class="input" value="${i === 0
                                ? esc(u.login || "")
                                : i === 1
                                    ? "+49 000 000 00"
                                    : esc(u.email || "alpha@example.com")
                            }" ${i === 0 ? "disabled" : ""}>
                </div>`
                    )
                    .join("")}
          <button class="btn primary" data-action="save">Сохранить изменения</button>
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
      </div>`
            );
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
                    <td><span class="badge ${u.isActive ? "success" : "danger"}">${u.isActive ? "Активно" : "Отключён"
                            }</span></td>
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

        <div class="field"><label>Роль</label>
          <div class="role-grid">
            ${Object.entries(ROLES)
                    .map(
                        ([k, r]) =>
                            `<button class="role ${state.role === k ? "selected" : ""}" data-role="${k}">${r.label}</button>`
                    )
                    .join("")}
          </div>
        </div>

        <button class="btn primary" style="width:100%" data-action="login">Войти</button>

        <div style="text-align:center;margin-top:15px">
          <button class="btn" style="border:0">Зарегистрироваться</button>
          <button class="btn" style="border:0">Забыли пароль?</button>
        </div>
      </div>
    </div>`;
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

        async function loadHomeworks() {
            state.loading = true;
            state.error = null;
            try {
                const res = await api.homeworks({ page: 1, pageSize: 50 });
                state.homeworks = res.items || res || [];
            } catch (err) {
                state.error = err.message || String(err);
                state.homeworks = [];
            } finally {
                state.loading = false;
            }
        }

        async function loadUsers() {
            state.usersLoading = true;
            state.usersError = null;
            try {
                const res = await api.users.list({ page: 1, pageSize: 100 });
                state.users = res.items || res || [];
            } catch (err) {
                state.usersError = err.message || String(err);
                state.users = [];
            } finally {
                state.usersLoading = false;
            }
        }

        async function loadDisciplines() {
            state.disciplinesLoading = true;
            state.disciplinesError = null;
            try {
                const res = await api.disciplines.list({ page: 1, pageSize: 100 });
                state.disciplines = res.items || res || [];
                state.disciplinesLoaded = true;
            } catch (err) {
                state.disciplinesError = err.message || String(err);
                state.disciplines = [];
            } finally {
                state.disciplinesLoading = false;
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

            const modal = `
    <div class="modal-backdrop" id="user-modal">
      <div class="modal">
        <div class="modal-head">
          <h3 style="margin:0">Редактирование: ${esc(user.login)}</h3>
          <button class="icon-btn" data-action="close-user-modal">✕</button>
        </div>

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
        </div>

        <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
          <button class="btn" data-action="close-user-modal">Отмена</button>
          <button class="btn primary" data-action="update-user" data-id="${user.userId}">Сохранить</button>
        </div>
      </div>
    </div>`;
            document.body.insertAdjacentHTML("beforeend", modal);
            bind();
        }   

        async function saveUser() {
            const login = document.getElementById("u-login")?.value?.trim();
            const password = document.getElementById("u-password")?.value;
            const email = document.getElementById("u-email")?.value?.trim() || null;
            const roleId = parseInt(document.getElementById("u-role")?.value || "3", 10);

            if (!login || !password) {
                toast("Заполните логин и пароль");
                return;
            }
            if (password.length < 6) {
                toast("Пароль минимум 6 символов");
                return;
            }

            try {
                await api.users.create({ login, password, email, roleId });
                await afterAction("user-modal", "✓ Пользователь создан", loadUsers);
            } catch (err) {
                toast("Ошибка: " + (err.message || err));
            }
            
        }

        async function saveDiscipline() {
            const title = document.getElementById("d-title")?.value?.trim();
            if (!title) {
                toast("Введите название предмета");
                return;
            }
            try {
                await api.disciplines.create({ title });
                toast("✓ Предмет создан");
                document.getElementById("discipline-modal")?.remove();
                await loadDisciplines();
                render();
            } catch (err) {
                toast("Ошибка: " + (err.message || err));
            }
        }

        async function updateDiscipline(id) {
            const title = document.getElementById("d-title")?.value?.trim();
            if (!title) {
                toast("Введите название предмета");
                return;
            }
            try {
                await api.disciplines.update(id, { title });
                toast("✓ Предмет обновлён");
                document.getElementById("discipline-modal")?.remove();
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
                    email,
                    roleId,
                    isActive,
                    newPassword: newPassword || null,
                });
                toast("✓ Пользователь обновлён");
                document.getElementById("user-modal")?.remove();
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

            ${hw.homeworkText
                        ? `<div style="margin:12px 0;padding:12px;background:var(--surface-2);border-radius:8px">
                     <b>Ответ студента:</b><div style="margin-top:6px">${esc(hw.homeworkText)}</div>
                   </div>`
                        : ""}

            ${hw.archiveUrl
                        ? `<p><a href="${esc(hw.archiveUrl)}" target="_blank" rel="noopener">📦 Скачать архив ответа</a></p>`
                        : ""}

            <p><span class="badge ${s.badge}">${esc(s.label)}</span></p>

            ${canSubmit
                        ? `<div class="field"><label>Комментарий</label>
                     <textarea class="textarea" id="hw-text" placeholder="Что вы сделали"></textarea>
                   </div>
                   <div class="field"><label>Файлы (можно несколько)</label>
                     <input type="file" id="hw-files" multiple>
                   </div>`
                        : ""}

            ${canGrade
                        ? `<div class="field"><label>Оценка (1–5)</label>
                     <input class="input" id="hw-grade" type="number" min="1" max="5" style="width:100px">
                   </div>
                   <div class="field"><label>Комментарий</label>
                     <input class="input" id="hw-comment">
                   </div>`
                        : ""}`;

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
                await afterAction("hw-modal", "✓ Оценка выставлена", loadHomeworks);
            } catch (err) {
                toast("Ошибка: " + (err.message || err));
            }
        }

        function onHomeworkSubmitted(payload) {
            if (state.role === "teacher" || state.role === "admin") {
                toast(`📥 Студент сдал ДЗ #${payload.homeworkId ?? ""}`);
                loadHomeworks().then(render);
            }
        }

        function onHomeworkGraded(payload) {
            if (state.role === "student") {
                toast(`★ Оценка ${payload.grade ?? ""} за ДЗ #${payload.homeworkId ?? ""}`);
                loadHomeworks().then(render);
            }
        }

        async function initSession() {
            state.user = await api.me();
            state.role = String(state.user.role || "Student").toLowerCase();
            localStorage.setItem("alpha-auth", "1");
            localStorage.setItem("alpha-role", state.role);

            await loadHomeworks();

            try {
                await startSignalR(onHomeworkSubmitted, onHomeworkGraded);
            } catch (e) {
                console.warn("SignalR не подключился:", e);
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

            const pages = {
                dashboard,
                schedule,
                grades,
                homework,
                ide,
                profile,
                "create-homework": createHomework,
                "check-homework": () => teacherTable("Проверка ДЗ"),
                "grade-homework": () => teacherTable("Оценивание ДЗ", "publish"),
                gradebook: () => teacherTable("Выставление оценок", "add"),
                attendance,
                users: () => adminPage("users"),
                classes: () => adminPage("classes"),
                subjects: subjects,
            };

            if (!canAccess(state.role, state.route)) {
                state.route = "dashboard";
                location.hash = "dashboard";
            }

            const fn = pages[state.route] || dashboard;
            app.innerHTML = fn();
            bind();

            if (
                state.route === "users" &&
                state.role === "admin" &&
                !state.usersLoading &&
                state.users.length === 0 &&
                !state.usersError
            ) {
                loadUsers().then(() => {
                    if (state.route === "users") render();
                });
            }
            bind();

            if (
                state.route === "subjects" &&
                state.role === "admin" &&
                !state.disciplinesLoaded &&
                !state.disciplinesLoading &&
                state.disciplines.length === 0 &&
                !state.disciplinesError
            ) {
                loadDisciplines().then(() => {
                    if (state.route === "subjects") render();
                });
            }
        }

        const RELOAD = {
            "reload-homeworks": loadHomeworks,
            "reload-users": loadUsers,
        };

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

            document.querySelectorAll("[data-action]").forEach((b) => {
                b.onclick = async () => {
                    const a = b.dataset.action;

                    if (a === "theme") {
                        setTheme(nextTheme());
                        return;
                    }

                    if (a === "menu") {
                        state.sidebar = !state.sidebar;
                        render();
                        return;
                    }

                    if (a === "logout") {
                        try { await api.logout(); } catch { }
                        try { await stopSignalR(); } catch { }
                        localStorage.removeItem("alpha-auth");
                        state.user = null;
                        state.homeworks = [];
                        state.users = [];
                        render();
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

                    if (RELOAD[a]) {
                        await RELOAD[a]();
                        render();
                        return;
                    }

                    if (a === "add" && state.route === "users") {
                        openAddUserModal();
                        return;
                    }

                    if (a === "save-user") {
                        await saveUser(); 
                        return;
                    }

                    if (a === "update-user" && b.dataset.id) {
                        await updateUser(b.dataset.id);
                        return;
                    }

                    if (a === "close-user-modal") {
                        closeModal("user-modal");
                        return;
                    }

                    if (a === "reload-disciplines") {
                        await loadDisciplines();
                        render();
                        return;
                    }

                    if (a === "add-discipline") {
                        openAddDisciplineModal();
                        return;
                    }

                    if (a === "edit-discipline" && b.dataset.id) {
                        const id = parseInt(b.dataset.id, 10);
                        const d = state.disciplines.find((x) => x.disciplineId === id);
                        if (d) openEditDisciplineModal(d);
                        return;
                    }

                    if (a === "delete-discipline" && b.dataset.id) {
                        await deleteDiscipline(b.dataset.id);
                        return;
                    }

                    if (a === "save-discipline") {
                        await saveDiscipline();
                        return;
                    }

                    if (a === "update-discipline" && b.dataset.id) {
                        await updateDiscipline(b.dataset.id);
                        return;
                    }

                    if (a === "close-discipline-modal") {
                        document.getElementById("discipline-modal")?.remove();
                        return;
                    }

                    if (a === "edit" && state.route === "users" && b.dataset.id) {
                        const userId = parseInt(b.dataset.id, 10);
                        const user = state.users.find((u) => u.userId === userId);
                        if (user) openEditUserModal(user);
                        return;
                    }

                    if (a === "delete" && state.route === "users" && b.dataset.id) {
                        await deleteUser(b.dataset.id);
                        return;
                    }

                    if (a === "open-homework") {
                        await openHomework(b.dataset.id);
                        return;
                    }

                    if (a === "close-homework") {
                        closeModal("hw-modal");
                        return;
                    }

                    if (a === "submit-homework") {
                        await submitHomework(b.dataset.id);
                        return;
                    }

                    if (a === "grade-homework" && b.dataset.id) {
                        await gradeHomework(b.dataset.id);
                        return;
                    }

                    if (["save", "publish", "submit", "run", "today", "edit"].includes(a)) {
                        toast(
                            a === "run" ? "✓ Выполнение завершено" :
                                a === "publish" ? "✓ Опубликовано" :
                                    "✓ Изменения сохранены"
                        );
                    }
                };
            });

            const show = document.getElementById("showpass");
            if (show) {
                show.onchange = () => {
                    const p = document.getElementById("password");
                    if (p) p.type = show.checked ? "text" : "password";
                };
            }

            const ts = document.getElementById("theme-select");
            if (ts) {
                ts.onchange = () => setTheme(ts.value);
            }
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
                    state.user = null;
                }
            }
            render();
        }

        bootstrap();

        return { render };
    })();
})();