# Unity Editor CI и Unity Personal

CI из PR #5 подготовлен поверх PR #4: `1a08e10de0d8f25262911364145f78f2c02a26ab`.
Интеграционная ветка `codex/integrate-evolution-main` переносит всю цепочку
PR #3 → #4 → #5 в отдельный PR в `main`, сохраняя исходные коммиты.
История, решения и проверки — в [INTEGRATION.md](INTEGRATION.md).
Исходные PR и ветки сохраняются; автоматическое слияние не выполняется.

## Что запускается

`.github/workflows/core-tests.yml` сохранён без изменений: .NET 8 запускает
50 существующих NUnit-тестов настоящих Core/Localization и проверку каталога.
Эти проверки работают без Unity и без секретов, включая PR из fork.

`.github/workflows/unity.yml` автоматически работает при `pull_request`
и push в `main`, `migration/godot-reference` и ветки PR #3/#4. После попадания
workflow в default branch доступен также `workflow_dispatch` с выбором ветки.
Для текущего отдельного PR достаточно его открытия и последующих push.

Последовательность на GitHub-hosted Ubuntu 24.04:

1. Проверить наличие трёх Secrets, не выводя значения. При отсутствии — ошибка
   с именами недостающих Secrets, без ложного успешного статуса Unity.
2. Прочитать точную версию из `ProjectSettings/ProjectVersion.txt`:
   **Unity 6.6.9f1 / `6000.6.9f1`**. Проверить registry и закрепить digest образов
   для данного запуска. Нет автоматического перехода на другую Unity.
3. GameCI Builder запускает Editor, проверяет загрузку сборок `Evolution.Core`,
   `Evolution.Localization`, `Evolution.Runtime`, `Evolution.Editor` и выполняет
   штатный `PrototypeSetup.CreatePrototype`, необходимый для URP в batch mode.
   Ошибка компиляции не позволит выполнить этот метод и завершит шаг ошибкой.
4. GameCI Test Runner запускает все Edit Mode тесты, затем Play Mode тесты.
   Play Mode выполняется в Editor с виртуальным дисплеем, без принудительного
   `-nographics`: smoke-тесты загружают настоящую сцену, проверяют инициализацию
   Runner/камеры/ресурса шейдера, продвижение симуляции через Update и остановку
   обновлений при отключении компонента. Неожиданные Unity error/exception логи
   также считаются ошибками Test Runner. Play Mode запускается и при провале
   Edit Mode, чтобы получить независимый результат; сборка требует успеха обоих.
5. Дополнительный барьер требует два корректных NUnit XML-отчёта и хотя бы один
   выполненный тест в каждом. Failed, skipped, inconclusive, пустой или
   отсутствующий отчёт не принимаются за успех.
6. При наличии точного Linux IL2CPP образа GameCI собирает включённую сцену
   через `BuildPipeline.BuildPlayer`, проверяя `BuildResult.Succeeded`.
   При отсутствии образа сборка явно помечается **NOT RUN** в summary.
   Ошибки сети/registry, компиляции, модулей и самой сборки не подавляются.
7. Артефакты на 7 дней: `unity-test-results` (только XML) и
   `evolution-linux-il2cpp` (проверочный player). Отсутствие player при успешном
   шаге сборки считается ошибкой. Это проверка сборки, не запуск готового player.

GameCI закреплён по SHA: Test Runner **v4.3.2** и Builder **v4.8.2**.
Эти стабильные версии включают исправление для Unity 6.6: Docker `/dev/shm`
увеличен до `1025m`; без него редактор может падать ещё при активации.
Test Runner использует виртуальный дисплей образа. Code Coverage package не
добавляется; метрики покрытия не являются частью этой настройки.

### Дисковое пространство GitHub-hosted runner

Перед первым запуском GameCI и загрузкой Docker-образа выполняется
`Tools/ci/prepare_unity_runner.sh`. Он выводит `df -h` до и после очистки
для корня, checkout и файловой системы Docker, записывает свободное место
и объём освобождённого пространства в summary. Если осталось менее 30 GiB,
job завершается понятной ошибкой до загрузки Unity.

Для пиков памяти Editor/IL2CPP на runner с 8 GiB RAM добавляется 8 GiB swap
в новый `/mnt/evolution-unity-ci.swap`; существующий swap сохраняется.
После очистки требуется не менее 38 GiB: 8 для swap и 30 для Unity.
Существующий файл по этому пути не перезаписывается. `free -h` и дополнительный
`df -h` фиксируют ресурсы после добавления swap. Сборка ограничена двумя CPU
и `-job-worker-count 2`, чтобы уменьшить пики памяти. При завершении job
выводятся disk/RAM и только сообщения kernel OOM, без окружения и Secrets.

Очистка разрешена только при `GITHUB_ACTIONS=true`, `RUNNER_ENVIRONMENT=github-hosted`
и `RUNNER_OS=Linux`. Удаляется фиксированный список предустановленных SDK:
Android, .NET, Swift, GHC/ghcup, CodeQL и PowerShell; очищаются APT cache,
неиспользуемые Docker images и build cache. Checkout, runner tools, Docker,
Git, системный Python, Node для Actions, volumes и активные containers сохраняются.
Нельзя запускать этот скрипт на собственной машине; защитная проверка отклонит запуск.
.NET-тесты остаются в прежнем отдельном job на другой VM, поэтому удаление
host .NET SDK в Unity job на них не влияет. SDK для Unity находятся в образе.

Когда доступен `linux-il2cpp`, все четыре вызова GameCI используют один и тот же
digest этого образа: компиляция, Edit Mode, Play Mode и сборка. Отдельный `base`
не загружается и не занимает дополнительное место. Если Linux IL2CPP образа нет,
тесты используют `base`, а сборка по-прежнему явно получает NOT RUN.
Версия Unity, Secrets и тесты при этой оптимизации не меняются.

Проверено 2026-11-12: Docker Registry вернул HTTP 200 и Linux amd64 manifests
для `unityci/editor:ubuntu-6000.6.9f1-base-3` и
`unityci/editor:ubuntu-6000.6.9f1-linux-il2cpp-3`. Наличие образа не подтверждает
импорт URP 17.3.0/Test Framework 1.6.0: их совместимость проверит настоящий Editor.

## Действия владельца аккаунта: активация Personal

Пароль, email и содержимое лицензии вводятся **только в GitHub Secrets**.
Не отправляйте их в чат, issue, PR, коммит, команды с буквальными значениями
или скриншоты. Платная подписка и `UNITY_SERIAL` для этого сценария не требуются.

1. На своей машине установите [Unity Hub](https://unity.com/download) и целевой
   редактор Unity 6.6.9f1. Войдите в Unity ID, который имеет право использовать
   Unity Personal, и завершите необходимые подтверждения аккаунта/2FA в Hub.
2. В Hub откройте **Preferences/Settings → Licenses → Add → Get a free personal
   license**. Примите условия Personal и откройте Editor, чтобы проверить
   локальную активацию. Даже если лицензия уже видна, проверьте наличие файла.
3. Найдите созданный Hub файл `Unity_lic.ulf`:

   | ОС | Путь |
   | --- | --- |
   | Windows | `C:\ProgramData\Unity\Unity_lic.ulf` |
   | macOS | `/Library/Application Support/Unity/Unity_lic.ulf` |
   | Linux | `~/.local/share/unity3d/Unity/Unity_lic.ulf` |

   Это приватный файл. По инструкции GameCI можно получить его на Windows,
   macOS или Linux и использовать в Linux CI. Не добавляйте файл в репозиторий.
4. Откройте **Evolution → Settings → Secrets and variables → Actions →
   Repository secrets → New repository secret** и создайте:

   | Secret | Значение |
   | --- | --- |
   | `UNITY_LICENSE` | Полное содержимое `Unity_lic.ulf`, исходный XML, без Base64 |
   | `UNITY_EMAIL` | Email Unity ID, которым активирована эта Personal-лицензия |
   | `UNITY_PASSWORD` | Пароль того же Unity ID |

   Нужны именно Secrets, а не Variables. Если используется вход только через
   внешнего провайдера, владелец должен самостоятельно настроить допустимый
   способ входа Unity ID; workflow не выполняет интерактивный вход или 2FA.
5. Убедитесь, что GitHub Actions разрешены в репозитории и разрешено выполнение
   `game-ci/unity-builder`, `game-ci/unity-test-runner` и официальных `actions/*`.
   Workflow требует только `contents: read`; права на запись checks не нужны.
6. После сохранения Secrets откройте неудачный запуск **Unity Editor CI**
   в Actions → **Re-run all jobs**. Если первого запуска ещё нет, создайте новый
   push в CI PR. Когда workflow появится в default branch, можно выбрать
   **Actions → Unity Editor CI → Run workflow → нужная ветка**.
7. Дождитесь реального завершения всех шагов. Проверьте успешные Compile,
   Edit Mode, Play Mode, проверку двух XML и Linux build. Скачайте артефакты.
   Наличие Secrets или успешная активация сами по себе не означают прохождение
   Unity-тестов. Лицензионная ошибка требует исправления активации и нового прогона.

Актуальная [инструкция GameCI для Personal](https://game.ci/docs/github/activation/#personal-license)
описывает этот путь через Hub и три Secrets. Возможности Personal/Hub могут
меняться: если Hub не создаёт `.ulf`, GameCI не может извлечь Personal-данные
или Unity отклоняет CI-активацию, не заменяйте файл выдуманным serial и не
используйте старую инструкцию ручной `.alf`-активации как гарантированное решение.
Владелец должен проверить актуальные инструкции GameCI/Unity и доступность
активации для своего аккаунта. Пока это не решено, Unity CI остаётся
неподтверждённым. Не нужно отключать 2FA или передавать учётные данные другим.

## Защита Secrets и ограничения

- Secrets передаются только шагам проверки наличия и GameCI через `env`.
  Значения не записываются в исходники, параметры `with`, пользовательские
  сообщения или summary. GameCI регистрирует маски для производного serial;
  GitHub маскирует заданные Secrets в консольном выводе.
- Не включайте `set -x`, `printenv`, `ACTIONS_STEP_DEBUG` и вывод `.ulf`.
  Сырые Editor/activation логи, runner home, Library и лицензии не загружаются
  как артефакты и не кэшируются. `.ulf`, `.alf`, `.lic`, `.env*` игнорируются Git.
- Секретные шаги работают только для PR из этого репозитория; fork и Dependabot
  пропускаются. Для них отсутствие Unity-результата означает **не проверено**,
  даже если .NET workflow зелёный. Нет `pull_request_target` и обхода изоляции.
  Перед запуском недоверенного кода в собственной ветке владелец должен его
  проверить: любой код в таком CI имеет доступ к переданным ему Secrets.
- Один Unity job, все вызовы Editor последовательны; общая concurrency group
  запрещает параллельное использование аккаунта этим workflow. Активный запуск
  не отменяется новым push. Это не снимает ограничения лицензии Unity.
- Первый импорт и IL2CPP могут занять значительное время; лимит job — 90 минут.
  Это GitHub-hosted CI с доступом к Unity Package Manager и сервису лицензий;
  локальная облачная среда Codex не получает ваши GitHub Secrets.
- Smoke-тесты не доказывают корректность изображения, кириллицы и производительности
  на GPU или запуск готового player. Ручные проверки из `VALIDATION.md` остаются.

## Что действительно проверено в исходном PR #5

На коммите `86650332017285f542fe9e11318359d00dd748e0` реально завершились:

- [Unity Editor CI](https://github.com/evgenijzingaev-dev/Evolution/actions/runs/43207955811):
  компиляция Core/Localization/Runtime/Editor, 50 Edit Mode, 2 Play Mode,
  Linux IL2CPP build; опубликованы XML и player.
- [.NET CI](https://github.com/evgenijzingaev-dev/Evolution/actions/runs/43207956062):
  50 passed, 0 failed, 0 skipped и локализация.

Это результаты исходного PR #5. Новый интеграционный PR запускает собственные
проверки; их итог фиксируется в его описании. Unity Editor/лицензия локально
не предоставлены, GitHub Secrets не переносятся в облачную среду.

Исходники `.github/workflows/core-tests.yml`, `Tools/CoreTests` и прежних
Edit Mode тестов сохранены. Добавлены только CI entry points и Play Mode тесты;
генерация URP и изменение backend для проверочной IL2CPP-сборки происходят
в одноразовом checkout GitHub runner, без коммитов игровых настроек.
