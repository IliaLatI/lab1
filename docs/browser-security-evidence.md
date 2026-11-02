# Докази безпеки браузера (DEL-01)

Рівень: добрий («Добре»). Рівні накопичувальні; повні критерії й маршрут — на початку практичної частини методички. Заповнюйте лише розділи свого рівня й усі попередні. Не записуйте паролі, cookie/token values чи export browser storage.

## Стенд і Git-стан (Для всіх)

ОС, browser/version, API/probe origins: … Release/base\_commit/entry\_parent з receipt: … (entry\_parent — commit, від якого встановлено scaffold; збігається з base\_commit, якщо після v0.4.0 не було commit політики SDK). Vulnerable/fixed hashes, v0.5.0, main і спосіб передавання: …

Механізм сеансу (Application → Storage → Cookies → http://localhost:5080): назва cookie, Domain, Path, HttpOnly, SameSite, Secure — без Value: …





ОС: Windows 11.

Release: lab-05-start-v1.

Base commit: 27edc58ae02a28596159041ff5ef3dee511968bf.

Entry parent: 27edc58ae02a28596159041ff5ef3dee511968bf.

Vulnerable commit: 1525756.

Fixed commit: поки не створений.

API origin: http://localhost:5080.

Probes: http://localhost:4173 і http://localhost:4174.



Механізм сеансу: cookie-based authentication (ASP.NET Core Identity).

Name: .AspNetCore.Identity.Application.

Domain: localhost.

Path: /.

HttpOnly: true.

Secure: false (локальний HTTP Development).

SameSite: Lax.

Expires: Session.

## Гіпотеза й REG-XSS-01 (Для всіх)

Карта Description: … Гіпотеза source → storage → response → sink/context: … Передумови, локальний користувач, Guid запису, marker із resources/lab05-marker.txt, команди reset/start і create/read: …



Значення Incident.Description надходить із форми браузерного клієнта, зберігається у PostgreSQL, повертається через JSON-поле description відповіді GET /api/incidents/{id} і передається до #description.innerHTML. Оскільки innerHTML інтерпретує рядок як HTML, браузер може сприймати недовірені дані як розмітку, а не звичайний текст.





|Спостереження|До fix|Після fix|Посилання на доказ|
|-|-|-|-|
|Значення в БД/API|Marker збережено в JSON|Marker збережено без зміни тексту|Network → POST/GET до fix та GET після fix|
|DOM основного Description|Створювався <img>|Текстовий вузол|Elements до/після fix|
|Дія marker|executed|null|Console: executed / null|
|Апостроф/лапки, 5 < 7, Unicode, HTML-like рядок|HTML-теги інтерпретувалися|Усі символи відображаються як текст|Network Response та Elements до/після|
|Довжина за власними правилами ЛР 2|3900|3900|Console: 3900 до/після|
|Вікно Morgan: GET і перевірка marker; контроль Bob|Morgan: 200, executed; Bob: 403, null|Morgan: 200, null; Bob: 403, null|Скріншоти сеансів Morgan і Bob до/після fix|

Finding: актив; передумови; першопричина; safe DOM diff; retest; залишковий ризик: … Повторювана REG-XSS-01, негативний контроль і очікування: …



Актив: браузерний сеанс користувача.

Передумови: можливість створити інцидент із контрольованим Description.

Першопричина: недовірений текст передається до innerHTML.

Вплив: локальний marker виконав контрольну DOM-дію в сеансах Alice та Morgan.

Виправлення: обидва innerHTML замінено спільним helper renderSafeText(), що використовує текстові вузли DOM.

Retest: Description та IncidentComment.Text відображають marker як текст; Console повертає null. Morgan: 200/null; Bob: 403/null. Спеціальні символи й 3900 символів збережено.

Залишковий ризик: інші неперевірені DOM-контексти та сторонні JavaScript-компоненти.



## CORS і REG-CORS-01 (Для всіх)

Прогноз (Етап 3, крок 1), заповніть до зміни policy:

|Сценарій|Чи доходить request до API?|Чи може JavaScript прочитати response?|Чому|
|-|-|-|-|
|same-origin 5080 → API|Так|Так|CORS не потрібен|
|simple GET 4173 → API|Так|Ні|Немає дозволу CORS|
|simple GET 4174 → API|Так|Ні|Немає дозволу CORS|
|curl/.http → API|Так|не застосовується|Не браузерний клієнт|
|preflighted 4173 → API («Добре / Відмінно»)|OPTIONS — так; основний запит — ні без успішного preflight|Ні|Браузер перевіряє CORS перед основним запитом|





Команди запуску probes: … Policy — лише Development, один exact origin 4173; 4174 — контроль. Для «Задовільно» без AllowCredentials().





Команди запуску probes:

py -3 -m http.server 4173 --bind localhost --directory demonstrations/cors-probe

py -3 -m http.server 4174 --bind localhost --directory demonstrations/cors-probe



Кінцевий стан: політика Lab05BrowserProbe діє лише в Development. Дозволено тільки http://localhost:4173 із підтримкою credentials. Origin 4174 залишається недозволеним.



REG-CORS-01: запити з 4173 успішно проходять CORS-перевірку, а відповіді для 4174 браузер блокує. Preflight для 4173 повертає OPTIONS 204 із дозволеними заголовками, для 4174 — OPTIONS 204 без CORS-дозволів.



Без cookie захищений endpoint повертає 401. Прямий HTTP-клієнт curl отримує відповідь /health незалежно від CORS, оскільки браузерна політика SOP на нього не поширюється.





|Клієнт / простий запит|До policy|Fixed state|Status, ACAO, Console / доказ|
|-|-|-|-|
|Same-origin 5080|Дозволено|Дозволено, CORS не потрібен|відповідь API 200|
|Allowed 4173|CORS error|GET 200, ACAO: 4173|Console 200, Network із Access-Control-Allow-Origin: http://localhost:4173|
|Disallowed 4174|CORS error|GET доходить до API, але браузер блокує відповідь|CORS error, Network без Access-Control-Allow-Origin|
|Direct HTTP / curl|200 OK|200 OK, CORS не обмежує curl|PowerShell, два запити з результатом 200|

REG-CORS-01: browser evidence і дві actual HTTP-відповіді для Origin 4173/4174: … Перевірка порядку через читання 401 без cross-origin cookie: … Пояснення різниці browser/direct HTTP: …

##

## Додатково для Добре та Відмінно

Другий flow IncidentComment.Text: … Public seed comment Guid/текст, похідні counts за потреби, DOM до/після: … Єдиний safe helper і виклики для обох полів: …

Preflight з 4173 і 4174, OPTIONS → GET / blocked GET, exact origin/header/method: … Cookie-дослід 4173/4174/без AllowCredentials()/401 без входу: … Кінцевий AllowCredentials() — лише Development policy для 4173. Review XSS/CORS/CSRF/authorization — посилання на DEL-03: …

|Директива CSP|Фактичні ресурси застосунку|Обране джерело / причина|
|-|-|-|
|default-src|Резервне правило для ресурсів|'self' — власний origin|
|script-src|app.js, browser-lab.js, auth-lab.js|'self' — власні JS-файли|
|style-src|styles.css|'self' — власний CSS|
|connect-src|API incidents, session-check|'self' — API на тому самому origin|
|img-src|SVG data:image/svg+xml, власні зображення|'self' data: — підтримка фактичного SVG|
|frame-ancestors|Вбудовування сторінок не потрібне|'none' — заборона вбудовування|



Місце зміни middleware: … Enforcement: document 200 і фактичний CSP header; три сторінки flow; кероване порушення new Image() до 4173 — результат: … Для «Добре» докази report-only не обов’язкові.





Місце зміни middleware: Program.cs, наявний блок app.Use після UseStatusCodePages().



Enforcement: основний документ отримано зі статусом 200 та заголовком Content-Security-Policy. Перевірено завантаження основної сторінки й auth-lab.html. Кероване завантаження зображення з http://localhost:4173/x.png заблоковано директивою img-src 'self' data:.



Виняток: сторінки /scalar звільнені від навчальної CSP лише в Development через особливості стороннього UI-пакета.

|Header / control|Фактичне значення|Ризик і межа захисту / policy rationale|
|-|-|-|
|X-Content-Type-Options|nosniff|Забороняє браузеру самостійно визначати MIME-тип ресурсу. Не замінює правильний Content-Type та валідацію.|
|Referrer-Policy|no-referrer|Запобігає передаванню адреси попередньої сторінки через Referer. Підходить для SecureLab, оскільки досліджений клієнт не потребує таких даних для навігації.|
|frame-ancestors|'none'|Забороняє вбудовування сторінок у фрейми та зменшує ризик clickjacking. Не замінює authorization або виправлення XSS.|

## Додатково для Відмінно

DOM-тест: файл/команда, browser, vulnerable commit і FAIL, fixed commit і PASS: … Comments, повторний render без дублювання та позитивні рядки: … CSP report-only → enforcement: фактичні headers, flow і кероване порушення в обох режимах: … safe-url.js / атрибут: … Пояснення text/attribute/URL/JavaScript та ризик third-party scripts — посилання на DEL-03: …

Додаткових DEL, навчальних демо або нового журналу не подавайте. Перед поданням повторіть чекліст для свого накопичувального рівня.

