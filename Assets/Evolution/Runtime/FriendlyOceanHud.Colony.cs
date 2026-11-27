using Evolution.Volume;
using UnityEngine;

namespace Evolution.Runtime
{
    public sealed partial class FriendlyOceanHud
    {
        void DrawColonyEntry()
        {
            if (water.IsFreshColonyOcean) {
                Label(160, 34, 128, "Временный океан", micro);
                if (Button(new Rect(292, 22, 78, 42), "Назад", "Вернуться в сохранённый океан на этом seed",
                    false, water.CanReloadColonyOcean && !CapturesCommands)) water.ReturnToSavedOcean();
                return;
            }
            if (water.ColonyAttempt.Active || water.Overview.Active || CapturesCommands) return;
            bool ready = ColonyChallenge.CanBegin(water.World, water.SelectedIndex);
            string tip = ready ? "От клетки к первой колонии" : "Выбери живую одиночную клетку";
            if (Button(new Rect(160, 22, 112, 42), "Колония · F7", tip, false, ready))
                water.StartColonyChallenge();
            DrawFreshColonyButton(new Rect(278, 22, 92, 42), "Практика");
        }

        bool DrawColonyChallenge()
        {
            var attempt = water.ColonyAttempt;
            if (!attempt.Active || water.Overview.Active) return false;
            var r = Left;
            Box(r);
            GUI.Label(new Rect(r.x + 16, r.y + 12, r.width - 32, 54), "От клетки к первой колонии", title);
            if (attempt.State == ColonyChallengeState.AwaitingDivision) {
                DrawColonyPath(r, attempt);
            } else {
                string path = attempt.Path == BirthConnection.Retain ? "Связанная пара"
                    : attempt.Path == BirthConnection.Separate ? "Отдельный потомок" : "До первого деления";
                int seconds = LineageChallenge.DisplayNicheSeconds(attempt.ResidenceSeconds);
                GUI.Label(new Rect(r.x + 16, r.y + 68, r.width - 32, 30), path + " · " + seconds + " / 15 сек", text);
                if (attempt.Finished) {
                    string outcome = attempt.State == ColonyChallengeState.Won ? "Победа · 15 секунд у пищи"
                        : attempt.Failure == ColonyChallengeFailure.BondBroken ? "Связь пары разорвана"
                        : attempt.Failure == ColonyChallengeFailure.OffspringDied ? "Потомок погиб" : "Родитель погиб";
                    GUI.Label(new Rect(r.x + 16, r.y + 104, r.width - 32, 46), outcome, title);
                    bool ready = ColonyChallenge.CanBegin(water.World, water.SelectedIndex);
                    string tip = ready ? "Новая попытка в текущем океане" : "Выбери живую одиночную клетку";
                    if (Button(new Rect(r.x + 16, r.y + 184, r.width - 32, 31), "В текущем океане · F7", tip, false, ready))
                        water.StartColonyChallenge();
                    DrawFreshColonyButton(new Rect(r.x + 16, r.y + 219, r.width - 32, 34), "Начать в свежем океане");
                } else {
                    bool canFollow = !CapturesCommands && !water.Overview.Typing
                        && attempt.OffspringToFollow(water.World, water.EvolutionPlay.Identity) >= 0;
                    string guidance = attempt.Guidance ?? "Удерживай потомка у пищи";
                    GUI.Label(new Rect(r.x + 16, r.y + 105, r.width - 32, 50),
                        canFollow ? "Y — играть за потомка" : guidance, title);
                    if (canFollow)
                        GUI.Label(new Rect(r.x + 16, r.y + 187, r.width - 32, 28), guidance, micro);
                }
                if (attempt.Path == BirthConnection.Retain) {
                    string exchange = attempt.EnergyReceived > 0
                        ? "По связи получено " + attempt.EnergyReceived.ToString("0.##") + " энергии"
                        : "Для зачёта нужен обмен энергией";
                    GUI.Label(new Rect(r.x + 16, r.y + 154, r.width - 32, 29), exchange, micro);
                }
            }
            if (!attempt.Finished)
                GUI.Label(new Rect(r.x + 16, r.y + 230, r.width - 32, 24), "F6 — выйти из испытания", micro);
            return true;
        }

        void DrawColonyPath(Rect r, ColonyChallenge attempt)
        {
            var world = water.World;
            bool controlled = attempt.CanChoosePath(world);
            var choice = controlled ? world.Get(world.Controlled).PlannedConnection : BirthConnection.Natural;
            string linkBlock = controlled ? world.ConnectionBlock(world.Controlled) : null;
            GUI.Label(new Rect(r.x + 16, r.y + 67, r.width - 32, 34), "Продержись у пищи 15 секунд", text);
            if (Button(new Rect(r.x + 16, r.y + 108, 113, 39), "Отделить", "Потомок плывёт самостоятельно",
                choice == BirthConnection.Separate, controlled)) world.PlanConnection(BirthConnection.Separate);
            if (Button(new Rect(r.x + 136, r.y + 108, 120, 39), "Связать · +0,30",
                linkBlock ?? "Пара движется вместе и обменивается энергией", choice == BirthConnection.Retain, controlled && linkBlock == null))
                world.PlanConnection(BirthConnection.Retain);
            string status = !controlled ? "Возьми управление исходной клеткой"
                : water.Paused ? choice == BirthConnection.Natural ? "Выбери способ, затем P" : "Продолжи симуляцию · P"
                : water.PlayerDivisionBlock(world.Controlled) ?? "Разделись · R";
            GUI.Label(new Rect(r.x + 16, r.y + 154, r.width - 32, 40), status, micro);
            if (!water.IsFreshColonyOcean || world.Tick > 0)
                DrawFreshColonyButton(new Rect(r.x + 16, r.y + 194, r.width - 32, 32), "Начать в свежем океане");
        }

        void DrawFreshColonyButton(Rect area, string label)
        {
            if (Button(area, label, "Испытание с начала на том же seed. Океан и результаты этой попытки остаются в памяти.",
                false, water.CanReloadColonyOcean && !CapturesCommands)) water.StartFreshColonyOcean();
        }
    }
}
