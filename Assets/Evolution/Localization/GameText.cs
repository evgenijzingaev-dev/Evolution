using System.Collections.Generic;
using System.Globalization;

namespace Evolution.Localization
{
    public enum TextKey
    {
        Title, Clock, Population, GenerationEnergy, Speed, ThermalOptimum, Diet, Remains,
        DietColors, TemperatureColors, Resume, Pause, TimeScale, Temperature, Regeneration,
        Restart, ZoomIn, ZoomOut, DietLegend, TemperatureLegend, FoodLegend, Extinction
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
                [TextKey.Extinction] = "Популяция вымерла. Начните новый опыт и измените условия."
            });
        public static TextCatalog Current { get; set; } = Russian;
        public static string Get(TextKey key) => Current.Get(key);
        public static string Format(TextKey key, params object[] args) => Current.Format(key, args);
    }
}
