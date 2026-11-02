# Власна карта source-to-sink

Поле: Incident.Description. Стан/commit: заповніть. Вхід/Guid запису: заповніть.



Поле: Incident.Description

Стан/commit: vulnerable — 1525756

Вхід: Alice

Guid перевіреного seed-інциденту: 20000000-0000-0000-0000-000000000001



|Крок|Файл та функція/DTO|Конкретне поле/значення|Доказ і довіра|
|-|-|-|-|
|Source|browser-lab.html, форма #edit|description — текст користувача|Дані вводить користувач; недовірене джерело.|
|Request|browser-lab.js, submit; CreateIncidentRequest|POST /api/incidents, поле description|JSON-поле description контролює клієнт; дані недовірені.|
|Validation/mapping|Lab02Endpoints.cs, MapPost|request.Description, перевірка до 4000 символів, Trim()|Сервер перевіряє довжину й обов'язковість, але не робить текст безпечним HTML.|
|Storage|Incident, PostgreSQL|Description = description!, SaveChangesAsync()|Значення зберігається у PostgreSQL; запис у БД не робить його довіреним.|
|Response|IncidentQueries.GetDetailsAsync, IncidentDetailsResponse|JSON-поле description|GET повертає JSON-поле description; значення підтверджено в Network.|
|Client transform|browser-lab.js, load()|response.json() → current.description|response.json() перетворює JSON на JavaScript-об'єкт, але не очищає HTML.|
|Sink/context|browser-lab.js, load()|#description.innerHTML — HTML-контекст|innerHTML інтерпретує недовірений текст як HTML; небезпечний sink.|

Інвентаризація: файл/функція → категорія даних → контекст → обґрунтоване рішення. Для «Добре / Відмінно» додайте повний другий потік IncidentComment.Text за тією самою таблицею. Для «Відмінно» додайте результат поглибленої перевірки comments/повторного render. Рівні накопичувальні; основний Description потрібний для всіх.





Інвентаризація DOM-операцій



У файлі browser-lab.js виявлено два небезпечні використання innerHTML:



\- #description.innerHTML - відображає збережений опис інциденту як HTML. Потребує заміни на textContent.

\- paragraph.innerHTML - відображає збережений текст коментаря як HTML. Також потребує заміни на textContent.



Назва інциденту та HTTP-статус безпечно відображаються через textContent. Пошук outerHTML, insertAdjacentHTML, eval() і new Function() збігів не виявив.



Висновок: два небезпечні DOM sinks буде виправлено на Етапі 2.

