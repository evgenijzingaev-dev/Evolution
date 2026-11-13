# Visual Preview: настоящие кадры Unity / URP

Workflow [Visual Preview](https://github.com/evgenijzingaev-dev/Evolution/actions/workflows/visual-preview.yml)
строит отдельный Linux Mono-плеер сцены `WaterPrototype3D` редактором **6000.6.9f1**.
Затем запускает его с настоящим OpenGL-контекстом, обычным игровым циклом и URP.
Режим `-batchmode` допустим для сборки, но **не используется для игрового рендера**;
`-nographics` при захвате запрещён. Preview-плеер собирается с явным OpenGL Core
и выполнением в фоне: отсутствие фокуса окна Xvfb не должно останавливать игровой цикл.
Существующий IL2CPP workflow не изменён.

## Запуск и результаты

- В Actions → Visual Preview → Run workflow выбрать нужную ветку. GitHub показывает
  `workflow_dispatch` после появления workflow в default branch; до этого можно
  запустить его событием PR.
- Для автоматических запусков поставить собственному PR метку `visual-preview`.
  Workflow учитывает только перечисленные визуальные пути: шейдеры, материалы,
  геометрию, камеру, сцену, настройки рендера и сам сценарий проверки. При изменении
  только биологии или документации тяжёлого запуска нет. Отдельный дешёвый job сравнивает
  предыдущий и новый head именно последнего push: накопленный визуальный diff PR
  не вызывает повторную сборку при последующих невизуальных исправлениях. После снятия метки
  автоматические запуски прекращаются. Draft PR поддерживаются. Fork PR не получают
  секреты и не запускаются на runner проекта.
- В summary запуска и отдельном комментарии PR появится ссылка на artifact.
  Комментарий указывает точный head SHA; снимки относятся к нему, а не к merge commit.
  Ручной запуск публикует ссылку в summary (без привязки к произвольному PR).
- Artifact хранится 14 дней. Содержит пять основных PNG **1920×1080** и `manifest.json`:
  общий океан; клетка крупно; группа на разных глубинах; течения; температура и пища.
  Дополнительный `06-water-only.png` показывает ту же игровую воду со скрытыми организмами.
  Первые три кадра снимаются с RenderTexture игровой URP-камеры без интерфейса.
  Последние два — из полного игрового framebuffer с пространственными слоями и
  существующей русской IMGUI-панелью: видны температура °C, вектор течения и ресурсы.
  Постобработка и данные реальной симуляции сохраняются; изображение не компонуется
  из отдельных слоёв вне Unity.

Сценарий включается только аргументом `-evolution-visual-preview <каталог>`.
Он использует seed 42, временный архив регионов в памяти и после первых 20 тактов
приостанавливает реальную симуляцию. Питание, генетика и физика не подменяются;
камеры выбирают существующие организмы и настоящие ресурсы. При обычном запуске
игры сценарий ничего не делает. Аудиомодуль сохранён; добавлены встроенные модули
`imageconversion` и `screencapture` для PNG из RenderTexture и игрового framebuffer. Подтверждаются
реальные события `endCameraRendering`; ручной `Camera.Render` и альтернативный рендерер
не используются. Сборка сохраняет instancing-варианты процедурных материалов,
которые Unity не может обнаружить по ссылкам сцены до запуска.

## Лицензия: настройка владельца

Workflow использует ту же конфигурацию, что [Unity Editor CI](UNITY_CI.md):
GitHub Actions secrets `UNITY_LICENSE` (содержимое совместимого `.ulf`, не base64
всего файла), `UNITY_EMAIL`, `UNITY_PASSWORD`. Владелец должен предоставить действующую
лицензию, разрешённую для CI, и проверить её активацию для Unity 6000.6.9f1.
Наличие имён секретов ещё не подтверждает активацию. Не создавать и не коммитить
лицензионные файлы или Editor в проект; секреты вводятся через настройки GitHub.
Запуски сериализованы с Unity Editor CI, чтобы не активировать одну лицензию одновременно.
Если текущий тип лицензии не поддерживает этот способ активации, владельцу потребуется
совместимая CI-лицензия/лицензионный сервер и соответствующая настройка GameCI;
workflow не обходит лицензирование.

## Обычный runner и резервный runner с GPU

По умолчанию: `ubuntu-24.04`, Xvfb и Mesa/llvmpipe, принудительный OpenGL Core.
Это реальная программная растеризация URP, а не имитация скриншота. До загрузки
Unity выполняется `glxinfo -B`: требуется контекст OpenGL ≥4.5. Окончательное
подтверждение — **только успешный запуск Unity и пять проверенных кадров**.
Успешный GLX probe или наличие Docker-образа сами по себе не доказывают поддержку URP.

Если программный рендер не проходит, владелец может переключить тот же workflow
на выделенный Linux x64 runner с графическим ускорением. В Settings → Secrets and
variables → Actions → Variables установить:

```text
VISUAL_PREVIEW_RUNNER = ["self-hosted","linux","x64","evolution-visual-gpu"]
VISUAL_PREVIEW_BACKEND = hardware
```

Подготовка такого runner:

1. Установить актуальный драйвер NVIDIA/AMD/Intel и работающий Xorg-дисплей
   (например, `DISPLAY=:0`). Сервис Actions должен иметь доступ к этому дисплею;
   задать `DISPLAY` и при необходимости `XAUTHORITY` в окружении сервиса.
2. Установить `mesa-utils` (`glxinfo`), Python 3, `timeout`, Docker и системные
   библиотеки Unity Linux player (`libGL`, X11/Xcursor/Xrandr/Xinerama/Xi, ALSA).
   Runner должен иметь возможность запускать GameCI Docker-контейнер для сборки.
   Сам плеер рендерится **нативно на хосте**, GPU passthrough в Docker не нужен.
3. Зарегистрировать runner с меткой `evolution-visual-gpu`, выделить примерно
   50 ГиБ свободного диска и ≥16 ГиБ RAM. Автоматическая очистка SDK/диска выполняется
   только на disposable GitHub-hosted runner, не на этой машине.
4. От имени пользователя сервиса выполнить `glxinfo -B` и
   `EVOLUTION_VISUAL_BACKEND=hardware bash Tools/ci/visual_preview.sh --probe`.
   Проверка отклоняет llvmpipe/softpipe при выборе hardware. Затем запустить workflow.
   Открывать Xorg всем пользователям через `xhost +` не требуется.

Этот режим не переключается на программный рендер автоматически. Ошибка контекста,
шейдера, таймаут или пустой кадр делают workflow красным; заглушки не создаются.
Сведения об API, GPU/драйвере, Unity, камере, seed, тиках, глубинах, температуре,
скорости течения и реальных ресурсах записываются в manifest успешного запуска.

## Что проверяется автоматически

- Чистый checkout без `Library`, компиляция и сборка только нужной сцены точным Editor.
- Активный `UniversalRenderPipelineAsset`, не-Null графическое устройство,
  shader level ≥45, наличие и поддержка четырёх шейдеров микромира.
- Пять различных PNG нужного размера; непустой диапазон/дисперсия яркости,
  отсутствие характерных magenta пикселей ошибки шейдера.
- В ракурсе глубины есть ≥3 живых организма с разницей camera-space глубины ≥3;
  слои течения и температуры включены, рядом есть реальные пищевые ресурсы.
- Любая ошибка/exception/assert Unity прекращает захват. На всё выполнение плеера
  отведено шесть минут. Валидированный набор загружается только после полной проверки.
- Если отклонён настоящий кадр камеры, отдельный artifact `NOT-VALIDATED` сохраняет
  диагностический PNG и JSON с `passed=false`. Отчёт явно отличает его от успешного
  набора; пустые картинки программно не создаются. Глобальная дисперсия допускает
  разреженную сцену с небольшими яркими организмами, при этом постоянный цвет,
  отсутствие реальных кадров камеры и недостаточная глубина отклоняются.
- В artifacts попадают только перечисленные PNG и JSON: не Editor/player logs,
  не домашний каталог runner, не лицензии и не исполняемые файлы.

Это визуальное свидетельство для человека, **не автоматическое одобрение красоты**
и не pixel-perfect regression baseline. В Unity Editor ещё нужно оценить движение,
параллакс, управление камерой, прозрачность с разных сторон и удобство поиска пищи.
Проверка не заменяет старые EditMode/PlayMode smoke-тесты `CellPrototype`.

## Подтверждённый запуск

13 ноября 2026 года [Visual Preview #15](https://github.com/evgenijzingaev-dev/Evolution/actions/runs/43352991831)
успешно проверил коммит `df1d660f09b4106add8c1062521869804b3bbd0c` после чистого checkout.
[Пять настоящих PNG и manifest](https://github.com/evgenijzingaev-dev/Evolution/actions/runs/43352991831/artifacts/14005631005)
доступны в Artifacts до 27 ноября 2026 года.

- Unity **6000.6.9f1**, URP, Ubuntu 24.04, OpenGL Core 4.5,
  Mesa 25.2.8 / llvmpipe (LLVM 20.1.2): программный рендер подтверждён настоящими кадрами.
- Все пять PNG имеют 1920×1080; на каждый ракурс зарегистрированы четыре события
  рендера игровой камеры. В ракурсе глубины видны четыре живых организма
  с camera-space расстояниями 14,26–28,26.
- Кадры течений и температуры/пищи содержат существующий русский HUD:
  видны 23,8 °C, вектор/скорость потока и реальные количества ресурсов.
- Существующие секреты обеспечили активацию и возврат лицензии. Дополнительный GPU
  для проверенного варианта не требуется; конфигурация физического GPU пока не тестировалась.
- `Unable to load player prefs` появлялось при первом запуске чистого профиля и
  не препятствовало успешному рендеру. Пустой файл настроек для сокрытия сообщения не создавался.

Кадры показывают текущую графику прототипа. Они не означают художественную приёмку
или подтверждённую удобную читаемость слоёв; это по-прежнему нужно оценивать отдельно.

Локальный повтор после сборки плеера в пути workflow:

```bash
EVOLUTION_PREVIEW_SHA=local bash Tools/ci/visual_preview.sh
```

Без установленного Unity нельзя подтвердить компиляцию Unity API, URP и качество
снимков одними статическими проверками. Первый успешный Actions run и его manifest
нужно использовать как подтверждение выбранного runner; при отсутствии такого
запуска поддержка рендера остаётся непроверенной.

## Public JPEG previews

`Publish Visual Preview` runs after a successful trusted `Visual Preview/render` job.
The Unity build, PNG upload allowlist and original artifact retention are unchanged.
The publisher downloads that exact unexpired artifact, verifies its native capture
manifest against the render SHA, and converts all **12** PNGs to progressive JPEG
(1440×810, with a 1280×720 fallback). It does not generate or repair images.
The original manifest currently describes 11 frames; the water-only frame is also
required, decoded and converted from the native artifact.

Public files live only on the `visual-previews` branch:

- `pr-<number>/*.jpg`: the 12 frames, at most 350 KiB each;
- `pr-<number>/publication.json`: safe provenance, dimensions and SHA-256 hashes;
- `pr-<number>/index.md`: direct anonymous HTTPS links and original artifact link;
- root `index.md`: navigation to retained PRs.

The publication job verifies **every** raw URL anonymously: HTTP 200, JPEG content
 type, SHA-256, decoded dimensions. It retries CDN propagation for up to five
minutes. Only then does the separate report job update its own marked PR comment
with the 12 direct URLs. A failed conversion/push/URL check fails the job and does
not announce a successful publication. Original PNG artifacts remain available.

### Security and owner configuration

No new repository secrets or Unity license configuration are needed. Only source
runs of `.github/workflows/visual-preview.yml` from this repository's `main` or
`codex/*` branches are accepted; fork PRs are excluded. There is no
`pull_request_target`, inherited Unity secrets or uploaded logs/builds. The
publisher uses its job's ephemeral `GITHUB_TOKEN` (`actions:read`, `contents:write`).
The comment job has only `pull-requests:write` and never checks out PR code.
Repository collaborators who can push these trusted branches can modify workflow
code; these are the explicit publication trust boundary. Narrow the branch
allowlist if collaboration permissions change.

Allow GitHub Actions to write repository contents/comments. If a branch ruleset
covers `visual-previews`, permit the Actions bot to create and force-update this
publication branch. Protect game branches normally. No force pushes target game
branches. A protected publication ref will fail visibly rather than fall back to
publishing elsewhere.

### Storage and concurrent updates

At most **8 PR folders / 32 MiB** are retained; each folder contains only its most
recent successful run. Other retained PR folders remain byte-identical. Old runs
cannot overwrite newer ones. Each update writes a parentless snapshot and uses
an explicit `--force-with-lease` on `refs/heads/visual-previews`; concurrent changes
are re-read before retrying. This keeps obsolete binaries out of reachable branch
history. GitHub controls garbage collection of unreachable Git objects, so disk
reclamation is not immediate. Evicted PR links eventually return 404; their PNG
artifacts follow the unchanged Actions retention policy. Public branch URLs point
to the latest successful run, not immutable archives; use `publication.json` hashes
and the original artifact when exact provenance matters.

### Backfill / manual use

The initial publication PR's trusted branch `codex/public-visual-previews` has a
small PR-triggered backfill using `Tools/ci/public_preview_request.json`: PR #19,
run `43409807998`. This exercises real publication without rerendering Unity.
Other branches cannot use this bootstrap trigger. Once the workflow is on the
default branch, run **Publish Visual Preview → Run workflow** with the successful
source run ID and destination PR number (0 auto-detects a unique associated PR).
For normal future renders the reusable workflow is called automatically; no
manual backfill is needed. Manual runs must select a trusted main/codex branch
that contains these scripts. The source artifact must still be unexpired.

Local checks (Pillow 13.0.0):

```sh
python3 -B Tools/ci/test_public_previews.py
python3 -B Tools/ci/test_visual_preview.py
```

The publication tests use synthetic pixels only inside temporary unit-test
fixtures. They never publish them. Production requires a successful Unity render,
validated upload, native manifest and all twelve original game frames.
