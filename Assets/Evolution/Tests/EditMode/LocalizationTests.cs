using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Evolution.Localization;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class LocalizationTests
    {
        [Test] public void DefaultLanguageIsRussianAndEveryKeyHasLocalizedText()
        {
            Assert.AreSame(GameText.Russian, GameText.Current);
            foreach (TextKey key in Enum.GetValues(typeof(TextKey))) {
                string text = GameText.Get(key);
                Assert.IsNotEmpty(text, key.ToString());
                // Keep the EVOLUTION brand, standard temperature unit and format placeholders.
                string visible = Regex.Replace(text, @"\{\d+(?::[^}]+)?\}", "").Replace("EVOLUTION", "").Replace("°C", "");
                // Standard keycap labels are shared across keyboard layouts.
                visible = Regex.Replace(visible, @"\b(?:WASD|F|G|H|Q|R|T|Home|Esc)\b", "");
                Assert.IsFalse(Regex.IsMatch(visible, "[A-Za-z]"), $"Untranslated UI string: {key}");
            }
        }

        [Test] public void RussianNumberFormattingDoesNotDependOnSystemLanguage()
        {
            var previous = CultureInfo.CurrentCulture;
            try {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                Assert.AreEqual("Температура: 22,5 °C", GameText.Format(TextKey.Temperature, 22.5f));
                Assert.AreEqual("10×", GameText.Format(TextKey.TimeScale, 10));
                foreach (TextKey key in Enum.GetValues(typeof(TextKey)))
                    Assert.DoesNotThrow(() => GameText.Format(key, 1, 2, 3, 4, 5), key.ToString());
            } finally { CultureInfo.CurrentCulture = previous; }
        }
    }
}
