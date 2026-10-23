#!/usr/bin/env bash
# Стартира емулатора и трите микроуслуги. Ctrl+C спира услугите.
set -euo pipefail
cd "$(dirname "$0")"

(cd emulator && docker compose up -d)
echo "Чакам емулатора..."
until docker logs servicebus-emulator 2>&1 | grep -q "Successfully Up"; do sleep 2; done

dotnet build ExamRegistration.sln -v q

trap 'kill 0' EXIT
dotnet run --no-build --project src/ExamRegistration.Processor &
dotnet run --no-build --project src/ExamRegistration.Notifier &
dotnet run --no-build --project src/ExamRegistration.Api --launch-profile http &
wait
