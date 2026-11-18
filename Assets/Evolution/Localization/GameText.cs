using System.Collections.Generic;
using System.Globalization;

namespace Evolution.Localization
{
    public enum TextKey
    {
        Title, Clock, Population, GenerationEnergy, Speed, ThermalOptimum, Diet, Remains,
        DietColors, TemperatureColors, Resume, Pause, TimeScale, Temperature, Regeneration,
        Restart, ZoomIn, ZoomOut, DietLegend, TemperatureLegend, FoodLegend, Extinction, Colonies, Transport, ColonyTraits, FeedingCells,
        MovementCells, DefenseCells, ReproductiveCells, RoleColors, RoleLegend, ColonyHelp, SelectionHelp, ColonyBlueprint,
        Observe, PopulationShort, TimeShort, Environment, CloseEnvironment, Overview, Focus, Follow, StopFollow, NothingSelected, PickHelp, OrganismTitle, CellIdentity, AgeGeneration, EnergyReserve, Vitality, VitalityHelp, ReproductionReady, ReproductionYoung, ReproductionEnergy, PopulationFull, Genes, HideGenes, GeneMotion, GeneFeeding, GeneTemperature, GeneAdhesion, GeneStructure, GeneRoles, ColonyEnergy, RolesSummary, Generalists, MutationSummary, NoMutations, FounderGenome, ControlHelp, ShortcutHelp, PendingEnvironment, MutationSpeed, MutationSize, MutationUptake, MutationTemperature, MutationDiet, MutationAdhesion, MutationColonySize, MutationBranching, MutationDifferentiation, MutationTransport, MutationPattern, RoleDetail, GeneralistRole, FeedingRole, MovementRole, DefenseRole, ReproductionRole, LegendCompact, RestartConfirm, RestartCancel, RestartWarning, SelectedValue
    }

    // UI uses keys. Future languages supply a separate catalog with the same keys.
    public sealed class TextCatalog
    {
        readonly CultureInfo culture;
        readonly IReadOnlyDictionary<TextKey, string> messages;
        public TextCatalog(CultureInfo culture, IReadOnlyDictionary<TextKey, string> messages)
        { this.culture = culture; this.messages = messages; }
        public string Get(TextKey key) => messages[key];
        public string Format(TextKey key, params object[] args) => string.Format(culture, Get(key), args);
    }

    public static class GameText
    {
        public static TextCatalog Russian { get; } = new TextCatalog(CultureInfo.GetCultureInfo("ru-RU"),
            new Dictionary<TextKey, string> {
                [TextKey.Observe] = "Наблюдение за микромиром",
                [TextKey.PopulationShort] = "{0} клеток   ·   {1} колоний",
                [TextKey.TimeShort] = "Время опыта: {0:F1} с",
                [TextKey.Environment] = "Условия среды",
                [TextKey.CloseEnvironment] = "Скрыть настройки",
                [TextKey.Overview] = "Общий обзор",
                [TextKey.Focus] = "Рассмотреть",
                [TextKey.Follow] = "Следить",
                [TextKey.StopFollow] = "Остановить слежение",
                [TextKey.NothingSelected] = "Выберите организм",
                [TextKey.PickHelp] = "Нажмите на клетку, чтобы рассмотреть её строение, геном и состояние колонии.",
                [TextKey.OrganismTitle] = "Организм № {0}   ·   {1} клеток",
                [TextKey.CellIdentity] = "Выбранная клетка № {0}   ·   родитель № {1}",
                [TextKey.AgeGeneration] = "Возраст: {0:F1} с   ·   поколение {1}",
                [TextKey.EnergyReserve] = "Энергия: {0:F1} / {1:F1}",
                [TextKey.Vitality] = "Жизнеспособность по энергии: {0:P0}",
                [TextKey.VitalityHelp] = "Отдельного показателя здоровья пока нет. Жизнеспособность отражает долю энергетического резерва.",
                [TextKey.ReproductionReady] = "Готова к делению",
                [TextKey.ReproductionYoung] = "Созревание: ещё {0:F1} с",
                [TextKey.ReproductionEnergy] = "Накопление энергии для деления: {0:P0}",
                [TextKey.PopulationFull] = "Свободных мест для потомства нет",
                [TextKey.Genes] = "Генетические признаки",
                [TextKey.HideGenes] = "Скрыть генетические признаки",
                [TextKey.GeneMotion] = "Скорость: {0:F2}   ·   размер: {1:F2}",
                [TextKey.GeneFeeding] = "Поглощение: {0:F2}   ·   органика: {1:P0}",
                [TextKey.GeneTemperature] = "Температурный оптимум: {0:F1} °C",
                [TextKey.GeneAdhesion] = "Адгезия: {0:P0}   ·   транспорт: {1:P0}",
                [TextKey.GeneStructure] = "Предел колонии: {0}   ·   ветвление: {1:P0}",
                [TextKey.GeneRoles] = "Потенциал тканей: {0:P0}   ·   программа ролей: {1:P0}",
                [TextKey.ColonyEnergy] = "Энергия организма: {0:F1} / {1:F1}",
                [TextKey.RolesSummary] = "Питание {0}   ·   движение {1}\nЗащита {2}   ·   размножение {3}",
                [TextKey.Generalists] = "Универсальные клетки: {0}",
                [TextKey.MutationSummary] = "Изменённые при рождении признаки: {0}",
                [TextKey.NoMutations] = "нет",
                [TextKey.FounderGenome] = "Основатель: исходный геном",
                [TextKey.ControlHelp] = "Колесо — масштаб   ·   WASD / стрелки — движение   ·   средняя кнопка — перетаскивание   ·   правая — вращение",
                [TextKey.ShortcutHelp] = "F — рассмотреть   ·   G — следить   ·   Home — обзор   ·   пробел — пауза   ·   Esc — снять выбор",
                [TextKey.PendingEnvironment] = "Условия применятся на следующем такте",
                [TextKey.MutationSpeed] = "скорость",
                [TextKey.MutationSize] = "размер",
                [TextKey.MutationUptake] = "поглощение",
                [TextKey.MutationTemperature] = "температура",
                [TextKey.MutationDiet] = "питание",
                [TextKey.MutationAdhesion] = "адгезия",
                [TextKey.MutationColonySize] = "предел колонии",
                [TextKey.MutationBranching] = "ветвление",
                [TextKey.MutationDifferentiation] = "развитие тканей",
                [TextKey.MutationTransport] = "транспорт",
                [TextKey.MutationPattern] = "программа ролей",
                [TextKey.RoleDetail] = "{0}   ·   развитие {1:P0}",
                [TextKey.GeneralistRole] = "Универсальная",
                [TextKey.FeedingRole] = "Питающая",
                [TextKey.MovementRole] = "Двигательная",
                [TextKey.DefenseRole] = "Защитная",
                [TextKey.ReproductionRole] = "Репродуктивная",
                [TextKey.LegendCompact] = "Мятный — питание · голубой — движение\nЗолотой — защита · розовый — размножение",
                [TextKey.RestartConfirm] = "Подтвердить новый опыт",
                [TextKey.RestartCancel] = "Сохранить текущий опыт",
                [TextKey.RestartWarning] = "Текущая симуляция будет сброшена.",
                [TextKey.SelectedValue] = "{0}",
                [TextKey.Title] = "EVOLUTION — лаборатория жизни",
                [TextKey.Clock] = "Зерно генерации: {0} | Такт: {1} | {2:F1} с",
                [TextKey.Population] = "Клетки: {0}/{1} | Рождения: {2} | Смерти: {3}",
                [TextKey.GenerationEnergy] = "Поколение: {0} | Средняя энергия: {1:F1}",
                [TextKey.Speed] = "Средняя скорость: {0:F2}",
                [TextKey.ThermalOptimum] = "Температурный оптимум: {0:F1} °C",
                [TextKey.Diet] = "Специализация на органике: {0:P0}",
                [TextKey.Remains] = "Энергия органических остатков: {0:F1}",
                [TextKey.DietColors] = "Цвет клеток: питание",
                [TextKey.TemperatureColors] = "Цвет клеток: температура",
                [TextKey.Resume] = "Продолжить",
                [TextKey.Pause] = "Пауза",
                [TextKey.TimeScale] = "{0}×",
                [TextKey.Temperature] = "Температура: {0:F1} °C",
                [TextKey.Regeneration] = "Восстановление субстрата: {0:F2}/с",
                [TextKey.Restart] = "Начать заново с тем же зерном",
                [TextKey.ZoomIn] = "Приблизить",
                [TextKey.ZoomOut] = "Отдалить",
                [TextKey.DietLegend] = "Зелёные клетки лучше усваивают субстрат; оранжевые — органику.",
                [TextKey.TemperatureLegend] = "Синие клетки приспособлены к холоду; красные — к теплу.",
                [TextKey.FoodLegend] = "Зелёный грунт — питательный субстрат. Коричневые участки — конечные органические остатки.",
                [TextKey.Colonies] = "Колонии: {0} | Клеток в колониях: {1} | Крупнейшая: {2}",
                [TextKey.Transport] = "Передача энергии внутри организмов: {0:F2}/с",
                [TextKey.ColonyBlueprint] = "Наследуемый предел колонии: {0:F1} | Ветвление: {1:P0} | Потенциал тканей: {2:P0}",
                [TextKey.ColonyTraits] = "Адгезия: {0:P0} | Развитие тканей: {1:P0} | Транспорт: {2:P0}",
                [TextKey.FeedingCells] = "Питающие клетки: {0}",
                [TextKey.MovementCells] = "Двигательные клетки: {0}",
                [TextKey.DefenseCells] = "Защитные клетки: {0}",
                [TextKey.ReproductiveCells] = "Репродуктивные клетки: {0}",
                [TextKey.RoleColors] = "Цвет клеток: функции",
                [TextKey.RoleLegend] = "Зелёный — питание; голубой — движение; золотой — защита; розовый — размножение. Функции развиваются постепенно; счётчики учитывают развитие от 25%.",
                [TextKey.ColonyHelp] = "Общая оболочка и перемычки объединяют клетки в организм. Адгезия удерживает соседей и потомство; геном задаёт ветвление, предел размера и распределение функций. Энергия идёт по связям от сытых клеток к голодным с потерями.",
                [TextKey.SelectionHelp] = "Колонии платят за связи и ткани, движутся медленнее. Питающие клетки лучше собирают пищу, двигательные ускоряют организм, защитные смягчают температурный стресс, репродуктивные раньше созревают. При голоде связи и ткани могут стать обузой. Одиночная жизнь остаётся жизнеспособной стратегией.",
                [TextKey.Extinction] = "Популяция вымерла. Начните новый опыт и измените условия."
            });
        public static TextCatalog Current { get; set; } = Russian;
        public static string Get(TextKey key) => Current.Get(key);
        public static string Format(TextKey key, params object[] args) => Current.Format(key, args);
    }
}
