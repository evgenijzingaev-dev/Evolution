using Evolution.Core;
using Evolution.Volume;
using UnityEngine;

namespace Evolution.Runtime
{
    public sealed partial class FriendlyOceanHud
    {
        Vector2 evolutionScroll;
        string lastOwnBirth,lastOwnDeath;
        void DrawFamilyRecovery(Rect r)
        {
            VolumeAgent? fallen=water.SelectedIndex>=0?water.World.Get(water.SelectedIndex):water.EvolutionPlay.Fallen;
            string message=fallen.HasValue?"Поколение "+fallen.Value.Biology.Generation+"\n"+CellEvolution.DeathReason(fallen.Value):"Клетка не выбрана или вне загруженной области. Выгрузка не означает гибель.";
            GUI.Label(new Rect(r.x+16,r.y+60,r.width-32,154),message,text);
            var children=CellEvolution.Offspring(water.World,water.EvolutionPlay.Identity);
            if(Button(new Rect(r.x+12,r.y+228,r.width-24,42),"Продолжить за потомка · Y",children.Count==0?"Живых прямых потомков в загруженной области нет":"Перейти к последнему живому потомку",false,children.Count>0))water.FollowOffspring();
            if(Button(new Rect(r.x+12,r.y+278,r.width-24,42),"Выбрать потомка / сравнить"))SetPage(HudPage.Development);
        }
        void DrawDevelopment()
        {
            var r=DevelopmentBox;Box(r,"Наследование и выбор потомка");
            var children=CellEvolution.Offspring(water.World,water.EvolutionPlay.Identity);
            bool hasCell=water.SelectedIndex>=0;
            var agent=hasCell?water.World.Get(water.SelectedIndex):water.EvolutionPlay.Fallen??default;
            var c=agent.Biology;
            float height=children.Count*96+(hasCell||water.EvolutionPlay.Fallen.HasValue?810+CellEvolution.TraitNames.Length*25:100);
            evolutionScroll=GUI.BeginScrollView(new Rect(r.x+12,r.y+58,r.width-24,r.height-122),evolutionScroll,new Rect(0,0,r.width-48,height));
            float width=r.width-68,y=0;
            GUI.Label(new Rect(6,y,width,34),"Твои прямые потомки в загруженной области: "+children.Count,title);y+=40;
            if(children.Count==0){GUI.Label(new Rect(6,y,width,48),"Накопи энергию и разделись. Потомок будет самостоятельной клеткой.\nY сразу передаёт управление последнему живому потомку.",micro);y+=54;}
            foreach(int index in children) {
                var child=water.World.Get(index);
                if(Button(new Rect(6,y,width,32),"Играть за потомка · поколение "+child.Biology.Generation+" · энергия "+child.Biology.Energy.ToString("F1"))) {
                    GUI.EndScrollView();if(water.FollowOffspring(index)){development=false;transition=0;}return;
                }
                GUI.Label(new Rect(8,y+35,width-8,56),CellEvolution.Changes(child.Biology),micro);y+=96;
            }
            if(hasCell||water.EvolutionPlay.Fallen.HasValue) {
                GUI.Label(new Rect(6,y,width,32),"Поколение "+c.Generation+" · линия "+c.LineageId,title);y+=36;
                GUI.Label(new Rect(6,y,width,54),c.ParentId==0?"Основатель: родительский геном неизвестен.":"Родитель → эта клетка. Стрелка означает изменение, а не обязательное улучшение.\nВыбранное наследование: "+CellEvolution.Name(agent.InheritedDevelopment)+"; оплачено "+agent.DevelopmentPaid.ToString("F1"),micro);y+=62;
                for(int i=0;i<CellEvolution.TraitNames.Length;i++) {
                    float current=CellEvolution.Trait(c.Genes,i),parent=CellEvolution.Trait(c.ParentGenes,i);
                    string value=c.ParentId==0?current.ToString("F3"):parent.ToString("F3")+" → "+current.ToString("F3")+(parent==current?" · без изменений":current>parent?" ↑":" ↓");
                    GUI.Label(new Rect(8,y,200,24),CellEvolution.TraitNames[i],micro);GUI.Label(new Rect(218,y,width-218,24),value,micro);y+=25;
                }
                GUI.Label(new Rect(6,y+8,width,30),"Направление следующего деления",title);y+=46;
                bool controlled=agent.Alive&&water.World.Controlled==water.SelectedIndex&&!water.Paused;
                string why=water.Paused?"Продолжи симуляцию":!agent.Alive?"Клетка погибла":"Сначала возьми управление выбранной клеткой";
                GUI.Label(new Rect(6,y,width,58),"Меняется наследуемый ген следующего потомка; случайные мутации остаются.\nЦена — 2 × размер родителя, только при успешном делении. Выбор можно отменить.",micro);y+=64;
                for(int n=1;n<=3;n++) {
                    var direction=(DevelopmentDirection)n;bool available=CellEvolution.Available(c,direction);
                    var planned=CellEvolution.Template(c.Genes,direction);
                    string label=CellEvolution.Name(direction)+" · +"+CellEvolution.Cost(c,direction).ToString("F1")+" энергии";
                    if(Button(new Rect(6,y,width,30),label,!controlled?why:!available?"Ген достиг предела":CellEvolution.Tradeoff(direction),agent.PlannedDevelopment==direction,controlled&&available))water.World.PlanDevelopment(direction);
                    GUI.Label(new Rect(8,y+34,width-8,52),CellEvolution.Tradeoff(direction)+"\nПлан без случайных мутаций: "+(direction==DevelopmentDirection.Photosynthesis?planned.Photosynthesis:direction==DevelopmentDirection.Organic?planned.OrganicSpecialization:planned.Speed).ToString("F2"),micro);y+=100;
                }
                if(Button(new Rect(6,y,width,32),"Естественное наследование · без доплаты",controlled?"Отменить план; обычная стоимость деления сохраняется":why,agent.PlannedDevelopment==DevelopmentDirection.Natural,controlled))water.World.PlanDevelopment(DevelopmentDirection.Natural);
                y+=40;GUI.Label(new Rect(6,y,width,50),hasCell?water.World.DivisionBlock(water.SelectedIndex)??"Можно делиться: закрой панель и нажми R.":"Продолжение доступно за живого потомка.",micro);
            }
            GUI.EndScrollView();
            if(Button(new Rect(r.x+16,r.yMax-54,292,38),"Вернуться к клетке")){development=false;transition=0;}
            if(Button(new Rect(r.x+324,r.yMax-54,300,38),"Взять управление клеткой","Явно начать игру за выбранную живую клетку",false,hasCell&&agent.Alive)){water.PlaySelected();development=false;}
        }
    }
}
