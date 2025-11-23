# Запуск в Docker

1. Подготовьте каталоги (в корне репозитория):
   ```bash
   mkdir -p backend/logs backend/data
   ```

2. Создайте файл `backend/.env` на основе `backend/.env.example` и заполните значения:
   - `BOT_TOKEN=...` — токен Telegram-бота (обязательно);
   - `OWNER_IDS=...` (опционально, список id через запятую);
   остальные переменные можно оставить по умолчанию или изменить при необходимости.

3. Сборка и запуск контейнеров:
   ```bash
   docker compose -f backend/compose.yml up -d --build
   ```

4. Проверка состояния:
   ```bash
   docker compose -f backend/compose.yml ps
   docker compose -f backend/compose.yml logs -f advent-bot
   docker compose -f backend/compose.yml logs -f advent-worker
   ```

5. Файлы на хосте:
   - база SQLite: `backend/data/advent.db`;
   - логи: `backend/logs/*.log`.

Остановка сервисов:
```bash
docker compose -f backend/compose.yml down
```
