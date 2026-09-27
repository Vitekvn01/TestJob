# TestJob — REST API для анализа HTML-страниц

Тестовое задание: REST API на .NET 10, который принимает JSON с HTML-страницей,
разбирает её через AngleSharp, извлекает атрибуты элементов и email-адреса,
расшифровывает AES-256-ECB строку и сохраняет найденные элементы в PostgreSQL
через Dapper.

## Стек

- .NET 10 / ASP.NET Core Web API
- AngleSharp — парсинг HTML
- FluentValidation — валидация входных данных
- Dapper + Npgsql — доступ к PostgreSQL
- PostgreSQL 18 — база данных
- pgAdmin 4 — веб-UI для БД
- Docker + docker compose — запуск всего стека

## Запуск

Требуется Docker Desktop.

```bash
docker compose up -d
```

Дождитесь, пока все три контейнера поднимутся:

```bash
docker ps
```

Ожидаемый вывод — три `Up`:

- `testjob-postgres`
- `testjob-api`
- `testjob-pgadmin`

## Адреса

| Сервис | URL |
|---|---|
| Swagger UI | http://localhost:8090/api/swagger |
| pgAdmin | http://localhost:8080 |
| PostgreSQL | localhost:5433 |

## Как тестировать

1. Открой http://localhost:8090/api/swagger
2. Разверни `POST /api/HtmlAnalysis`
3. Нажми **Try it out**
4. Вставь содержимое одного из файлов `json_payload_1.txt` или `json_payload_2.txt`
5. Нажми **Execute**

Пример запроса:

```json
{
  "selector": "a[href]",
  "attribute": "href",
  "url_b64": "aHR0cHM6Ly90ZXN0LmNvbS9wYWdlMQ==",
  "encrypted_text_bytes_b64": "hXeVCcIEyC/5ovf4eyJCozhRbTUV5jjBzOUPBM6dgZnoGyY8CNFBYxffu9fHJp5bSPKzdsFbMZ9gNZfhCG17Sg==",
  "key_bytes_b64": "SGVsbG8gVGVzdEpvYiAyNTYgYml0IHNlY3JldCBrZXk=",
  "page_b64": "..."
}
```

Ожидаемый ответ — JSON со следующими полями:

- `is_error` — 0 или 1
- `error_code` — код ошибки (если есть)
- `error_message` — текст ошибки
- `elements_count` — количество найденных элементов
- `emails_count` — количество email-адресов
- `url` — декодированный URL
- `decrypted_plain_text` — результат AES-расшифровки
- `elements_attr_list` — значения атрибутов найденных элементов
- `emails_list` — список email-адресов

## pgAdmin

Открой http://localhost:8080 в браузере.

pgAdmin откроется **без авторизации**. Сервер `TestJob` и база `testjob` уже добавлены
автоматически через файл `servers.json`.

PostgreSQL запущен в режиме `trust` — пароль не требуется.

**При первом клике на сервер `TestJob` появится окно ввода пароля.
Вводить ничего не нужно — можно просто нажать `OK` или ввести любой символ
(например `x`) и нажать `OK`.** PostgreSQL работает в режиме `trust` и не
проверяет пароль. После первого подключения окно больше не появится.

> Режим `trust` используется только для локальной разработки. В проде так делать нельзя.

## Результаты работы

- `json_result_1.txt` — ответ API для `json_payload_1.txt`
- `json_result_2.txt` — ответ API для `json_payload_2.txt`

## Архитектура

```
Controllers/    — HtmlAnalysisController (тонкий, передаёт данные в сервис)
Services/       — HtmlAnalysisService (вся бизнес-логика)
Models/         — HtmlAnalysisRequest, HtmlAnalysisResponse
Validators/     — HtmlAnalysisRequestValidator (FluentValidation)
Exceptions/     — BusinessException (свой класс с кодом ошибки)
Options/        — DatabaseOptions (настройки подключения к БД)
```

## Об асинхронности

### Где используется async

Асинхронность применяется для **I/O-операций** — в первую очередь для записи
в PostgreSQL через Dapper (`ExecuteAsync`). Это ключевой сценарий:

- Пока поток ждёт ответа от базы данных, он **не блокируется**.
- Thread Pool может **переиспользовать этот поток** для обработки других запросов.
- Под нагрузкой это даёт **кратный рост пропускной способности** — не 16
  одновременных запросов на 16 потоках, а сотни, потому что большинство
  запросов в момент ожидания БД не занимают поток.

Сам `ProcessAsync` помечен `async`, потому что содержит `await` на вызове
`SaveElementsAsync` (запись в БД).

### Где async НЕ используется — и почему

Операции, которые упираются в **CPU**, а не в ожидание внешнего ресурса,
выполняются синхронно. Async для них **не даёт выигрыша**: процессор всё равно
занят, поток не освобождается. Более того, оборачивание CPU-bound кода в
`Task.Run` — это **антипаттерн**, который приводит к лишним переключениям
контекста и расходу потоков Thread Pool без пользы.

Конкретно:

- **`Convert.FromBase64String`** — чистая математика на CPU. Async-варианта не существует, потому что это не I/O.
- **AES-расшифровка** (`TransformFinalBlock`) — CPU-bound операция. Считать её в отдельном потоке — бессмысленно, шифрование выполняется мгновенно и полностью грузит ядро.
- **`HtmlParser.ParseDocument` (AngleSharp)** — парсинг HTML упирается в CPU, а не в I/O. У AngleSharp нет async-API — библиотека синхронная. Оборачивать её в `Task.Run` — только вредить.
- **`Regex.Matches`** — применение регулярного выражения к строке это CPU-работа. `MatchesAsync` не существует.
- **`JsonSerializer`** — сериализация/десериализация тоже CPU-bound. У ASP.NET Core есть асинхронные варианты, но они нужны, только когда тело запроса приходит по сети по частям. У нас тело полностью приходит в контроллер — сериализация мгновенная.

### Итоговое правило

> **Async — для I/O. Sync — для CPU.**

Это стандартное правило .NET. Его придерживаются и сами разработчики ASP.NET Core:
например, `File.ReadAllTextAsync` существует (I/O), а `int.Parse` асинхронного
варианта не имеет (CPU).

### Что было бы, если бы мы использовали `Task.Run` везде

- Поток Thread Pool переключается на другой поток, выполняет работу, переключается обратно.
- Итог: та же работа + накладные расходы на переключение.
- Под нагрузкой Thread Pool забивается фоновыми задачами вместо того, чтобы обслуживать новые HTTP-запросы.
- Результат: **производительность падает**, а не растёт.

Поэтому мы используем async там, где он реально помогает, и sync там, где
async — это только вред.

## Структура compose

```yaml
services:
  postgres:  # PostgreSQL 18
  api:       # .NET 10 Web API (собирается при старте)
  pgadmin:   # pgAdmin 4
```

`api` собирается при старте контейнера (`dotnet build` + `dotnet run`).
Исходный код монтируется как том (`./:/src`), что позволяет менять код
без пересборки образа.

## API-контракт

### POST /api/HtmlAnalysis

**Request body** (application/json):

| Поле | Тип | Описание |
|---|---|---|
| `selector` | string | CSS-селектор |
| `attribute` | string | Имя HTML-атрибута |
| `url_b64` | string | URL, закодированный в Base64 |
| `encrypted_text_bytes_b64` | string | Шифротекст AES-256-ECB (Base64) |
| `key_bytes_b64` | string | Ключ AES-256 (Base64) |
| `page_b64` | string | HTML-страница (Base64) |

**Response** — JSON с полями, перечисленными выше.

**Коды ошибок:**

| Код | Значение |
|---|---|
| `EMPTY_SELECTOR` | пустой селектор |
| `EMPTY_ATTRIBUTE` | пустой атрибут |
| `MISSING_URL` / `INVALID_BASE64_URL` | url_b64 отсутствует или не base64 |
| `MISSING_PAGE` / `INVALID_BASE64_PAGE` | page_b64 отсутствует или не base64 |
| `MISSING_KEY` / `INVALID_BASE64_KEY` | key_bytes_b64 отсутствует или не base64 |
| `MISSING_ENCRYPTED` / `INVALID_BASE64_ENCRYPTED` | encrypted_text_bytes_b64 отсутствует или не base64 |
| `URL_DECODE_FAILED` | url_b64 не декодируется в UTF-8 |
| `KEY_DECODE_FAILED` | key_bytes_b64 невалиден |
| `ENCRYPTED_DECODE_FAILED` | encrypted_text_bytes_b64 невалиден |
| `DECRYPTION_FAILED` | AES не смог расшифровать |
| `PAGE_DECODE_FAILED` | page_b64 невалиден |
| `INVALID_SELECTOR` | AngleSharp не разобрал CSS-селектор |
| `INTERNAL_ERROR` | неизвестная ошибка |
