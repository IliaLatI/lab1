# Звіт до лабораторної роботи № 1

## 1. Ідентифікація стану

- Варіант: 2-A «Трекер інцидентів».
- Гілка: `lab/1-system`.
- Фінальний тег: `v0.1.0`.
- Commit hash: `1c155b9`.

## 2. Змінений маршрут

Маршрут обробки запиту зведеної статистики за рівнем важкості:

1. **Клієнтська дія та запит (`Client/index.html`, `Client/app.js`):** Натискання кнопки «Показати підсумок» викликає обробник події `click`, який виконує функцію `loadSeveritySummary()` та надсилає асинхронний запит `GET /api/incidents/severity-summary` через `apiFetch()`.
2. **Серверний ендпоінт (`Presentation/Endpoints/IncidentEndpoints.cs`):** Маршрутизатор ASP.NET Core спрямовує запит до ендпоінту `MapGet("/severity-summary", GetSeveritySummaryAsync)`.
3. **Обробка логіки та SQL-запит (`Application/Incidents/IncidentQueries.cs`):** Метод `IncidentQueries.GetSeveritySummaryAsync` виконує асинхронний read-only запит за допомогою EF Core (`dbContext.Incidents.AsNoTracking()`). На рівні бази даних PostgreSQL виконується групування за полем `severity` та підрахунок кількості записів `Count()`.
4. **Контракт відповіді (`Presentation/Contracts/IncidentResponses.cs`):** Результат проєктується у масив DTO `IncidentSeveritySummaryResponse` (поля `Severity` та `Count`), серіалізується у JSON та повертається клієнту з кодом `200 OK`.
5. **Відображення в DOM (`Client/app.js`):** Отриманий JSON-масив обробляється функцією `loadSeveritySummary()`, яка безпечно виводить значення у DOM-вузли через властивість `textContent`.

## 3. Виконані зміни

- **`Presentation/Contracts/IncidentResponses.cs`**: Додано окремий типізований DTO `IncidentSeveritySummaryResponse(string Severity, int Count)` для виключення витоку чутливих даних сутності.
- **`Application/Incidents/IncidentQueries.cs`**: Реалізовано асинхронний метод `GetSeveritySummaryAsync`, що використовує `.AsNoTracking()`, `GroupBy()`, `Count()`, доповнює відсутні рівні важкості значеннями `0` (політика повного переліку груп) та впорядковує результат.
- **`Presentation/Endpoints/IncidentEndpoints.cs`**: Замінено базову заготовку із кодом `501 Not Implemented` на повноцінний ендпоінт `GET /api/incidents/severity-summary` з описом метаданих `.Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>()`.
- **`Client/index.html` & `Client/app.js`**: Додано інтерфейсний блок «Підсумок за severity» та асинхронну функцію `loadSeveritySummary()`. Реалізовано обробку стану завантаження, порожнього стану, помилок мережі у блоці `catch` та безпечне виведення через `textContent`.
- **`tests/http/incidents.http`**: Оновлено коментар та очікуваний статус код для запиту підсумку з `501` на `200 OK`.

## 4. Перевірка

| ID | Передумови | Дія | Очікувано | Фактично | Доказ |
|---|---|---|---|---|---|
| T-01 | PostgreSQL healthy, API запущено | `GET /health` | 200 OK, стан готовності API та БД | 200 OK (`"status": "Healthy"`) | `.http` / `curl` |
| T-02 | Відновлений seed | `GET /api/incidents?status=Triaged` | 200 OK, список відповідає фільтру | 200 OK, повернуто 1 інцидент | DevTools Network |
| T-03 | Відновлений seed | `GET /api/incidents?status=Resolved` | 200 OK з порожнім масивом `[]` | 200 OK з порожнім масивом `[]` | `.http` response |
| T-04 | Відновлений seed | `GET /api/incidents/99999999-9999-9999-9999-999999999999` | 404 Problem Details | 404 Not Found (Problem Details) | `.http` response |
| T-05 | Відновлений seed | `GET /api/incidents?status=Unknown` | 400 Validation Problem Details | 400 Bad Request | `.http` response |
| T-06 | Реалізовано Етап 3, відновлений seed | `GET /api/incidents/severity-summary` | 200 OK, масив з 4 груп (Critical: 0, High: 1, Medium: 1, Low: 1) | 200 OK, відповідає контракту DTO | `.http` response / Scalar |
| T-07 | Запущено API та клієнт | Натиснути кнопку «Показати підсумок» | UI безпечно показує результат, обробляє стани завантаження/помилок | Динамічно виведено зведену статистику через `textContent` | Скриншот веб-інтерфейсу |
| T-08 | Після змін даних | Виконати `--reset-database`, повторити T-02 та T-06 | Стенд повертається до початкового seed-стану | Стенд успішно скинуто до початкового стану | Термінал (`dotnet run -- --reset-database`) |

## 5. Security-сценарій

У цій лабораторній роботі навмисні уразливості відсутні. Безпека реалізованого модуля забезпечується на етапі проектування:

- **Захист від XSS (DOM Sinks):** Відображення текстових даних у `app.js` здійснюється виключно через безпечну властивість `textContent` та розбудову елементів через `document.createElement`, що виключає можливість інтерпретації користувацького вводу як HTML/JS[cite: 4].
- **Мінімізація витоку даних (Information Disclosure):** Використання окремого DTO `IncidentSeveritySummaryResponse` гарантує, що клієнту передаються лише агреговані дані без розкриття полів сутності `Incident` (наприклад, `ownerUserId` чи внутрішніх коментарів)[cite: 4].
- **Безпечна обробка помилок:** При виникненні мережевих збоїв клієнтський скрипт відображає зрозуміле фіксоване повідомлення без розкриття деталей серверних винятків або структури БД[cite: 4].
- **Контроль Git-депозитарію:** Перед фіксацією змін перевірено `git diff --staged` та підтверджено відсутність секретів, токенів, конфігураційних `.env` файлів та локальних дампів БД[cite: 4].

## 6. Висновок

У ході виконання лабораторної роботи № 1 було успішно розгорнуто локальний навчальний стенд вебсистеми «Трекер інцидентів» у контейнерах Docker та середовищі .NET 10[cite: 4]. Досліджено наскрізний шлях проходження HTTP-запиту від дій користувача в інтерфейсі до виконання SQL-запитів у PostgreSQL та відображення даних у DOM[cite: 4].

Успішно реалізовано наскрізне розширення системи — модуль зведеної статистики інцидентів за рівнем важкості (`GET /api/incidents/severity-summary`)[cite: 4]. Створено типізований DTO, асинхронний read-only query з групуванням та агрегацією, оновлено ендпоінт API та розширено користувацький інтерфейс[cite: 4]. Функціональність перевірено ручними `.http`-сценаріями, інструментами DevTools Network та автоматизованими інтеграційними тестами xUnit (4/4 пройшли успішно)[cite: 4]. Зміни зафіксовано в системі контролю версій Git, злито в гілку `main` із використанням прапорця `--no-ff` та позначено підсумковим тегом `v0.1.0`[cite: 4].