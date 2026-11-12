# EVOLUTION — клеточный прототип

Unity **6.3 LTS (6000.3.0f1)**, C#, URP 17.3. Трёхмерная визуализация микроскопической чашки: клетки пока движутся по плоскости XZ с периодическими границами.

## Запуск

1. Откройте корень репозитория через Unity Hub в Unity 6.3 LTS. Дождитесь импорта пакетов и компиляции.
2. При первом импорте редактор создаст URP renderer и pipeline в `Assets/Evolution/Settings` и откроет сцену. Если нужно, вызовите **Evolution → Create or Open Cell Prototype**.
3. Откройте `Assets/Evolution/Scenes/CellPrototype.unity` и нажмите Play.
4. В Game доступны Pause/Resume, 1×/2×/5×/10×, температура, регенерация пищи, перезапуск с тем же seed и приближение камеры. Слева отображаются численность, рождения, смерти, поколение и средние признаки. Цвет клеток показывает температурный оптимум; зелёный субстрат — пищу.
5. Seed и начальные параметры задаются на компоненте Evolution Simulation до запуска. Для старта от одной клетки задайте Initial Population = 1. По умолчанию 160 основателей позволяют быстрее наблюдать отбор. Изменения Inspector во время Play применяются только после Restart.

При росте температуры холодолюбивые клетки теряют больше энергии, хуже усваивают пищу и реже делятся. Быстрое движение и высокий захват пищи имеют энергетическую цену. В условиях голода популяция может полностью вымереть — автоматического заселения нет.

## Тесты

В Unity: **Window → General → Test Runner → EditMode → Run All**.

Без Unity, при установленном .NET 8 SDK:

```sh
dotnet test Tools/CoreTests/CoreTests.csproj
```

Этот проект компилирует непосредственно исходники Core и те же NUnit-тесты. Он проверяет биологию, но не Unity API, импорт, шейдеры или сборку игры.

Пакетный запуск Unity (путь к executable зависит от ОС):

```sh
Unity -batchmode -projectPath "$PWD" -executeMethod Evolution.Editor.PrototypeSetup.CreatePrototype -quit -logFile setup.log
Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testResults TestResults/editmode.xml -logFile tests.log
```

## Документация

- [Архитектура и ограничения](docs/ARCHITECTURE.md)
- [Дорожная карта](docs/ROADMAP.md)
- [Проверки текущей версии](docs/VALIDATION.md)
- [Полный список исходных файлов](docs/FILES.md)
