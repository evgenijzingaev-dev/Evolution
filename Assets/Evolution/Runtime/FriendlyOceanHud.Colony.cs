using Evolution.Volume;
using UnityEngine;

namespace Evolution.Runtime
{
    public sealed partial class FriendlyOceanHud
    {
        void DrawColonyEntry()
        {
            if (water.ColonyAttempt.Active || water.Overview.Active || CapturesCommands) return;
            bool ready = ColonyChallenge.CanBegin(water.World, water.SelectedIndex);
            string tip = ready ? "От клетки к первой колонии" : "Выбери живую одиночную клетку";
            if (Button(new Rect(160, 22, 178, 42), "Первая колония · F7", tip, false, ready))
                water.StartColonyChallenge();
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
                    if (Button(new Rect(r.x + 16, r.y + 189, r.width - 32, 35), "Повторить · F7", tip, false, ready))
                        water.StartColonyChallenge();
                } else {
                    GUI.Label(new Rect(r.x + 16, r.y + 105, r.width - 32, 50),
                        attempt.Guidance ?? "Удерживай потомка у пищи", title);
                    if (!CapturesCommands && !water.Overview.Typing
                        && attempt.OffspringToFollow(water.World, water.EvolutionPlay.Identity) >= 0)
                        GUI.Label(new Rect(r.x + 16, r.y + 187, r.width - 32, 28), "Y — играть за потомка", micro);
                }
                if (attempt.Path == BirthConnection.Retain) {
                    string exchange = attempt.EnergyReceived > 0
                        ? "По связи получено " + attempt.EnergyReceived.ToString("0.##") + " энергии"
                        : "Для зачёта нужен обмен энергией";
                    GUI.Label(new Rect(r.x + 16, r.y + 154, r.width - 32, 29), exchange, micro);
                }
            }
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
                : water.Paused ? "Продолжи симуляцию · P" : water.PlayerDivisionBlock(world.Controlled) ?? "Разделись · R";
            GUI.Label(new Rect(r.x + 16, r.y + 159, r.width - 32, 59), status, micro);
        }
    }
}
