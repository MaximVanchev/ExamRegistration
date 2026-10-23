# ExamRegistration — записване за изпити в ТУ

Курсов проект по ПРС (тема 3.1): микроуслуги на .NET 8, които комуникират чрез **Azure Service Bus** и се тестват **офлайн** с **Azure Service Bus Emulator**.

Документацията на курсовия проект (стъпки A–I) е в [`docs/Documentation.pdf`](docs/Documentation.pdf) (изходен текст: [`docs/documentation.html`](docs/documentation.html)).

## Бърз старт

Нужни са: .NET SDK 8, Docker Desktop.

```bash
./run-all.sh
```

Скриптът вдига емулатора (`emulator/docker-compose.yml`), изчаква го и стартира трите услуги. Api слуша на `http://localhost:5080`.

## Тестове

```bash
dotnet test tests/ExamRegistration.Api.Tests
```

```bash
dotnet test tests/ExamRegistration.IntegrationTests
```

Интеграционните тестове изискват работещ емулатор (`cd emulator && docker compose up -d`).

## Документация

```bash
./scripts/build-docs.sh
```

Отпечатва `docs/documentation.html` в `docs/Documentation.pdf` чрез Google Chrome (headless).
