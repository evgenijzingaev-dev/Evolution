using System;
using Evolution.Core;

namespace Evolution.Volume
{
    // Presentation only: copies of cells and resources are used by FoodPotential.
    // No stepping, randomness, streaming or writes to the biological model.
    public struct PlayerReadout
    {
        public float Energy, Integrity, ThermalEfficiency, FlowRatio, Light, FoodRate, Need;
        public int Food, Threat;
        public bool Alive,ReadyToDivide;
        public bool EnoughFood => FoodRate >= Need;
        public string Temperature => ThermalEfficiency >= .85f ? "Комфортно" : ThermalEfficiency >= .65f ? "Прохладно / жарко" : "Некомфортно";
        public string Current => FlowRatio < .35f ? "Слабое" : FlowRatio < 1 ? "Умеренное" : "Сильное";
        public string Nutrition => FoodRate < Need ? "Мало" : FoodRate < Need * 2 ? "Достаточно" : "Много";
        public string Illumination => Light < .2f ? "Темно" : Light < .7f ? "Достаточно света" : "Ярко";
        public string Condition => !Alive ? "Жизнь завершилась" : Integrity < .4f ? "Повреждена" : Integrity < .8f ? "Есть повреждения" : Energy < .2f ? "Нужна энергия" : "Хорошее";
        public string Goal => !Alive ? "Выбери другую живую клетку" : Threat >= 0 ? "Отплыви от хищника" : ReadyToDivide ? "Разделись и наблюдай за потомком" : !EnoughFood ? "Найди подходящую пищу" : Energy < .8f ? "Подплыви к пище и накопи энергию" : "Исследуй воду и береги энергию";
        public static PlayerReadout Read(VolumeWorld world, int index)
        {
            var r = new PlayerReadout { Food = -1, Threat = -1 };
            if (index < 0 || index >= world.Count) return r;
            var a = world.Get(index); var c = a.Biology; var g = c.Genes;
            var sample = world.Sample(a.Position);
            r.Alive = a.Alive; r.ReadyToDivide=world.DivisionBlock(index)==null; r.Energy = Math.Max(0, Math.Min(1, c.Energy / Biology.Capacity(g)));
            r.Integrity = c.Integrity; r.Light = sample.Light;
            r.ThermalEfficiency = Biology.Efficiency(g, sample.Temperature);
            r.FlowRatio = world.Current(a.Position).Length / Math.Max(.01f, Ecology.Mobility(g));
            r.Need = Biology.Cost(g, sample.Temperature);
            float best = float.NegativeInfinity, sense = Ecology.SenseRadius(g);
            for (int k = 0; k < world.FoodCount; k++) {
                var f = world.GetFood(k); float distance = (f.Position - a.Position).Length;
                if (distance > sense + f.Radius) continue;
                float rate = world.FoodPotential(k, c); if (rate <= 0) continue;
                float score = rate / (1 + Math.Max(0, distance - f.Radius));
                if (score > best) { best = score; r.Food = k; r.FoodRate = rate; }
            }
            float nearest = sense;
            for (int i = 0; i < world.Count; i++) {
                var hunter = world.Get(i); float d = (hunter.Position - a.Position).Length;
                if (hunter.Alive && d < nearest && Ecology.CanHunt(hunter.Biology, c) &&
                    Ecology.Attack(hunter.Biology) / (g.Size * (1 + Ecology.Defense(c, 0))) > .28f) {
                    nearest = d; r.Threat = i;
                }
            }
            return r;
        }
    }
}
