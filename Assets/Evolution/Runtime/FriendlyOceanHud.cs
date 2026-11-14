using System;
using System.Collections.Generic;
using System.Text;
using Evolution.Core;
using Evolution.Volume;
using UnityEngine;

namespace Evolution.Runtime
{
    public enum HudDetail { Normal, Observer, Scientific }
    public enum HudPage { Cell, Overview, Development }

    // A real Unity IMGUI overlay. Textures below are only reusable rounded chrome;
    // all labels, bars, map points and interactions are driven by the live world.
    public sealed class FriendlyOceanHud : MonoBehaviour
    {
        WaterPrototype water;
        VolumeWorld readWorld;int readIndex=-2;
        public HudDetail Detail;
        public bool HelpVisible, AttackRequested;
        public HudPage Page => development ? HudPage.Development : water.Overview.Active ? HudPage.Overview : HudPage.Cell;
        bool development, settings, journal;
        float requestedScale=1, nextRead, transition, mapRange=45, shownEnergy;
        string identity;
        double assimilated;
        long eventTick;
        PlayerReadout readout;
        readonly List<Notice> notices=new List<Notice>(3);
        readonly Dictionary<string,float> cooldown=new Dictionary<string,float>();
        struct Notice { public string Text, Sub; public float Until; public bool Danger; }
        GUISkin skin; GUIStyle text, title, micro, centered, compactAction, button, panel, activeButton, muted, valueText;
        Texture2D chrome, chromeActive, white, dot;
        readonly List<Rect> occupied=new List<Rect>(10);
        Vector2 scienceScroll;
        readonly HashSet<int> lines=new HashSet<int>();
        int living; SurveyRegion busiest, foodRegion, dangerRegion;
        public bool LayoutFits => Top.xMax<=W&&MapBox.y>=Left.yMax+12&&Actions.x>MapBox.xMax&&Actions.xMax<Right.x&&Right.yMax<Footer.y&&DevelopmentBox.yMax<Actions.y;
        float Scale => Mathf.Min(Mathf.Min(Screen.width/1440f,Screen.height/810f)*requestedScale,
            Mathf.Min(Screen.width/1280f,Screen.height/760f));
        float W => Screen.width/Scale;
        float H => Screen.height/Scale;
        Rect Top => new Rect(18,14,W-36,58);
        Rect Left => new Rect(18,90,272,Page==HudPage.Overview?264:170);
        Rect Right => new Rect(W-310,Page==HudPage.Overview?242:252,292,Detail==HudDetail.Scientific?Mathf.Min(420,H-360):300);
        Rect MapBox => new Rect(18,H-310,272,238);
        Rect Actions => new Rect(W/2-258,H-154,516,80);
        Rect Footer => new Rect(18,H-58,W-36,42);
        Rect SettingsBox => new Rect(W-326,80,308,158);
        Rect HelpBox => new Rect(W-326,80,308,158);
        Rect DevelopmentBox => new Rect(W/2-270,102,540,Mathf.Min(420,H-286));
        Rect ObserverBox => new Rect(306,90,W-632,116);
        Rect ToastBox => new Rect(W-310,88,292,150);
        public PlayerReadout Readout => readout;
        public int NotificationCount => notices.Count;
        public void RefreshNow(){nextRead=0;Update();}
        public float EffectiveScale => Scale;
        public void Initialize(WaterPrototype prototype)
        {
            water=prototype;requestedScale=Mathf.Clamp(PlayerPrefs.GetFloat("OceanHud.Scale",1),.8f,1.25f);
        }
        public bool CloseOverlay()
        {
            if(settings||HelpVisible||journal){settings=HelpVisible=journal=false;return true;}
            if(development){development=false;transition=0;return true;}
            return false;
        }
        public void CycleDetail(){Detail=(HudDetail)(((int)Detail+1)%3);transition=0;}
        public void SetPage(HudPage page)
        {
            development=page==HudPage.Development;transition=0;AttackRequested=false;
            if(page==HudPage.Cell)water.PlaySelected();
            else if(page==HudPage.Overview){water.Overview.Enter();water.Overview.SetMode(false);}
            else water.Observe();
        }
        // Input hit areas must match visible overlays: otherwise invisible panels block
        // picking organisms and issuing movement commands in the 3D ocean.
        bool ShowObserverControls => !development &&
            (Detail==HudDetail.Observer||(Page==HudPage.Overview&&water.Overview.Analytical));
        // Pure geometry is shared by the runtime and deterministic Edit Mode tests.
        // The journal covers its whole panel; normal notices only cover drawn cards.
        public static bool NoticeHit(Vector2 p,Rect area,int count,bool journal,bool coveredBySettings)
        {
            if(coveredBySettings)return false;
            if(journal)return area.Contains(p);
            for(int i=0;i<Mathf.Min(count,2);i++)
                if(new Rect(area.x,area.y+i*78,area.width,72).Contains(p))return true;
            return false;
        }
        bool OverVisibleNotice(Vector2 p) =>
            NoticeHit(p,ToastBox,notices.Count,journal,settings||HelpVisible);
        // The observer overlay has gaps between its actual buttons. Keep those gaps
        // clickable so a player can still select organisms behind the overlay.
        bool OverObserverButton(Vector2 p)
        {
            if(!ShowObserverControls)return false;
            var r=ObserverBox;
            float half=(r.width-8)/2;
            return new Rect(r.x,r.y,half,34).Contains(p)||
                (busiest!=null&&new Rect(r.x+half+8,r.y,half,34).Contains(p))||
                (foodRegion!=null&&new Rect(r.x,r.y+40,half,32).Contains(p))||
                (dangerRegion!=null&&new Rect(r.x+half+8,r.y+40,half,32).Contains(p))||
                (water.Overview.Survey.Events.Count>0&&new Rect(r.x,r.y+80,r.width,32).Contains(p));
        }
        public bool OverUi(Vector2 pixels)
        {
            var p=new Vector2(pixels.x/Scale,(Screen.height-pixels.y)/Scale);
            return Top.Contains(p)||Left.Contains(p)||Right.Contains(p)||MapBox.Contains(p)||Actions.Contains(p)||Footer.Contains(p)||
                ((settings||HelpVisible)&&SettingsBox.Contains(p))||(development&&DevelopmentBox.Contains(p))||
                OverVisibleNotice(p)||OverObserverButton(p);
        }
        void Update()
        {
            if(water==null||water.World==null)return;
            transition=Mathf.MoveTowards(transition,1,Time.unscaledDeltaTime*4);
            shownEnergy=Mathf.MoveTowards(shownEnergy,readout.Energy,Time.unscaledDeltaTime*.8f);
            if(readWorld==water.World&&readIndex==water.SelectedIndex&&Time.unscaledTime<nextRead)return;
            nextRead=Time.unscaledTime+.5f;readWorld=water.World;readIndex=water.SelectedIndex;
            readout=PlayerReadout.Read(water.World,water.SelectedIndex);
            water.Overview.Refresh();living=0;lines.Clear();busiest=foodRegion=dangerRegion=null;
            foreach(var a in water.Overview.Survey.Agents)if(InLayer(a.Position)){living++;lines.Add(a.Lineage);}
            foreach(var r in water.Overview.Survey.Regions)if(r.Loaded&&InLayer(r.Center)){
                if(busiest==null||r.Living>busiest.Living)busiest=r;
                if(foodRegion==null||r.Substrate*2+r.Organic>foodRegion.Substrate*2+foodRegion.Organic)foodRegion=r;
                if(r.Attacks>0&&(dangerRegion==null||r.Attacks>dangerRegion.Attacks))dangerRegion=r;
            }
            if(water.SelectedIndex>=0){var a=water.World.Get(water.SelectedIndex);
                if(identity!=a.Identity){identity=a.Identity;assimilated=a.Biology.AssimilatedEnergy;AttackRequested=false;}
                else if(a.Biology.AssimilatedEnergy>assimilated+.01){Notify("food","Организм получил энергию","Пища усваивается автоматически рядом с источником",false);assimilated=a.Biology.AssimilatedEnergy;}
            }
            if(readout.Threat>=0)Notify("danger","Рядом опасность!","Хищник в пределах чувствительности клетки",true);
            if(readout.FoodRate>=readout.Need*2)Notify("abundance","Обнаружено много пищи","Золотая отметка показывает подходящий источник",false);
            long previousEventTick=eventTick;
            foreach(var e in water.Overview.Survey.Events)if(e.Tick>previousEventTick){
                if(e.Kind==SurveyEventKind.Division)Notify("birth","Родился потомок","Линия сохранена; геном унаследован с возможной мутацией",false);
                else if(e.Kind==SurveyEventKind.KnownLineageLost)Notify("line","Линия исчезла из известной области","Подтверждено наблюдавшейся гибелью",true);
                else if(e.Kind==SurveyEventKind.PopulationChange&&e.Count<0)Notify("decline","В регионе стало меньше жизни","Изменение наблюдаемой численности",false);
                eventTick=Math.Max(eventTick,e.Tick);
            }
            notices.RemoveAll(n=>n.Until<Time.unscaledTime);
        }
        bool InLayer(V3 p)=>water.Overview.FullDepth||ChunkKey.At(p).Y==water.Overview.SliceIndex;
        void Notify(string key,string message,string sub,bool danger)
        {
            if(cooldown.TryGetValue(key,out float until)&&Time.unscaledTime<until)return;
            cooldown[key]=Time.unscaledTime+18;
            if(notices.Count==3)notices.RemoveAt(notices.Count-1);
            var notice=new Notice {Text=message,Sub=sub,Danger=danger,Until=Time.unscaledTime+7};
            if(danger)notices.Insert(0,notice);else notices.Add(notice);
        }
        static Texture2D Rounded(Color fill,Color edge)
        {
            const int size=48;var t=new Texture2D(size,size,TextureFormat.RGBA32,false);t.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++){
                float dx=Mathf.Max(12-x,x-35),dy=Mathf.Max(12-y,y-35);
                float distance=new Vector2(Mathf.Max(0,dx),Mathf.Max(0,dy)).magnitude;
                Color c=distance>11?Color.clear:distance>9.5f?edge:fill;
                t.SetPixel(x,y,c);
            }
            t.Apply();return t;
        }
        void Prepare()
        {
            if(skin)return;
            skin=Instantiate(GUI.skin);skin.font=Resources.Load<Font>("WaterUI/OpenSans-Regular");
            chrome=Rounded(new Color(.025f,.095f,.155f,.91f),new Color(.35f,.70f,.84f,.58f));
            chromeActive=Rounded(new Color(.08f,.31f,.47f,.96f),new Color(.46f,.85f,1,.95f));
            white=new Texture2D(1,1);white.SetPixel(0,0,Color.white);white.Apply();
            dot=new Texture2D(32,32,TextureFormat.RGBA32,false);
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)dot.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(16-new Vector2(x-15.5f,y-15.5f).magnitude)));
            dot.Apply();
            text=new GUIStyle(skin.label){font=skin.font,fontSize=16,wordWrap=true,richText=false};text.normal.textColor=new Color(.88f,.96f,1);
            title=new GUIStyle(text){fontSize=19,fontStyle=FontStyle.Bold};
            valueText=new GUIStyle(text){fontSize=14};
            micro=new GUIStyle(text){fontSize=13};micro.normal.textColor=new Color(.64f,.80f,.88f);
            centered=new GUIStyle(text){alignment=TextAnchor.MiddleCenter};
            compactAction=new GUIStyle(centered){fontSize=13,wordWrap=false};
            muted=new GUIStyle(micro){alignment=TextAnchor.MiddleCenter};
            button=new GUIStyle(skin.button){font=skin.font,fontSize=15,wordWrap=true,border=new RectOffset(13,13,13,13),padding=new RectOffset(7,7,5,5)};
            button.normal.background=button.hover.background=button.active.background=button.focused.background=chrome;
            button.normal.textColor=button.hover.textColor=button.active.textColor=button.focused.textColor=Color.white;
            activeButton=new GUIStyle(button);activeButton.normal.background=activeButton.hover.background=activeButton.active.background=chromeActive;
            panel=new GUIStyle(skin.box){border=new RectOffset(13,13,13,13)};panel.normal.background=chrome;
            skin.label=text;skin.button=button;
        }
        void Box(Rect r,string name=null)
        {
            GUI.Box(r,GUIContent.none,panel);occupied.Add(r);
            if(name!=null){GUI.Label(new Rect(r.x+16,r.y+12,r.width-32,28),name,title);Line(new Vector2(r.x+16,r.y+48),new Vector2(r.xMax-16,r.y+48),new Color(.4f,.7f,.8f,.3f));}
        }
        bool Button(Rect r,string value,string tip="",bool selected=false,bool enabled=true)
        {
            bool old=GUI.enabled;GUI.enabled=enabled;
            bool pressed=GUI.Button(r,new GUIContent(value,tip),selected?activeButton:button);GUI.enabled=old;
            // Disabled controls still explain why they cannot run.
            if(!enabled&&r.Contains(Event.current.mousePosition))hoverTip=tip;
            return pressed;
        }
        bool Action(Rect r,int icon,string name,string tip,bool selected=false,bool enabled=true)
        {
            bool clicked=Button(r,"",tip,selected,enabled);
            var old=GUI.color;GUI.color=enabled?Color.white:new Color(1,1,1,.35f);
            var c=new Vector2(r.center.x,r.y+26);Color ink=new Color(.65f,.9f,1);
            if(icon==0){
                for(int wave=0;wave<3;wave++)for(int i=0;i<12;i++){
                    float x=i*2.5f-15,y=wave*7-7;
                    Line(c+new Vector2(x,y+Mathf.Sin(i*.5f)*2.5f),c+new Vector2(x+2.5f,y+Mathf.Sin((i+1)*.5f)*2.5f),ink,1.5f);
                }
            }else if(icon==1){Dot(c+new Vector2(-8,-5),4,ink);Dot(c+new Vector2(7,-9),3,ink);Dot(c+new Vector2(4,7),5,ink);}
            else if(icon==4){
                for(int i=0;i<16;i++){
                    float y=i*1.8f-14,y2=y+1.8f,x=Mathf.Sin(i*.4f)*9,x2=Mathf.Sin((i+1)*.4f)*9;
                    Line(c+new Vector2(x,y),c+new Vector2(x2,y2),ink,1.5f);Line(c+new Vector2(-x,y),c+new Vector2(-x2,y2),ink,1.5f);
                    if(i%4==0)Line(c+new Vector2(x,y),c+new Vector2(-x,y),ink);
                }
            }else{
                for(int ring=0;ring<(icon==3?2:1);ring++)for(int i=0;i<24;i++){
                    var center=c+new Vector2(icon==3?(ring==0?-8:8):0,0);float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;
                    Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*10,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*10,ink,1.5f);
                }
                if(icon==2){Line(c+Vector2.left*16,c+Vector2.right*16,ink);Line(c+Vector2.up*16,c+Vector2.down*16,ink);}
            }
            GUI.Label(new Rect(r.x+3,r.y+47,r.width-6,25),name,name.Length>10?compactAction:centered);GUI.color=old;return clicked;
        }
        string hoverTip;
        void Label(float x,float y,float width,string value,GUIStyle style=null)=>GUI.Label(new Rect(x,y,width,28),value,style??text);
        void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,white);GUI.color=old;}
        void Dot(Vector2 p,float radius,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(p.x-radius,p.y-radius,radius*2,radius*2),dot);GUI.color=old;}
        void Line(Vector2 a,Vector2 b,Color color,float thickness=1)
        {
            var matrix=GUI.matrix;var d=b-a;
            GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(a.x,a.y,0),Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg),Vector3.one);
            Fill(new Rect(0,0,d.magnitude,thickness),color);GUI.matrix=matrix;
        }
        public void Draw()
        {
            if(readWorld!=water.World||readIndex!=water.SelectedIndex)RefreshNow();
            Prepare();occupied.Clear();hoverTip=null;
            var matrix=GUI.matrix;var oldSkin=GUI.skin;var oldColor=GUI.color;
            GUI.matrix=Matrix4x4.Scale(Vector3.one*Scale);GUI.skin=skin;
            GUI.color=new Color(1,1,1,.75f+.25f*transition);
            DrawTop();DrawTasks();DrawCell();DrawMap();DrawActions();DrawFooter();
            if(development)DrawDevelopment();
            if(settings)DrawSettings();else if(HelpVisible)DrawHelp();else DrawNotices();
            if(ShowObserverControls)DrawObserverLinks();
            if(water.FindFood&&!development)DrawWorldLabels();
            string tip=hoverTip??GUI.tooltip;
            if(!string.IsNullOrEmpty(tip)){
                float x=Mathf.Clamp(Event.current.mousePosition.x-150,18,W-338),y=Mathf.Clamp(Event.current.mousePosition.y+22,80,H-140);
                var r=new Rect(x,y,320,70);GUI.Box(r,GUIContent.none,panel);GUI.Label(new Rect(x+12,y+9,296,54),tip,micro);
            }
            GUI.color=oldColor;GUI.matrix=matrix;GUI.skin=oldSkin;
        }
        void DrawTop()
        {
            Box(Top);Label(34,29,215,"EVOLUTION",title);
            float start=W/2-264;
            if(Button(new Rect(start,22,176,42),"Игра за клетку","Управление выбранной живой клеткой",Page==HudPage.Cell))SetPage(HudPage.Cell);
            if(Button(new Rect(start+180,22,176,42),"Общий обзор","Наблюдение и аналитика активной области",Page==HudPage.Overview))SetPage(HudPage.Overview);
            if(Button(new Rect(start+360,22,168,42),"Развитие","Реальные наследуемые черты организма",development))SetPage(HudPage.Development);
            if(Button(new Rect(W-280,22,178,42),Detail==HudDetail.Normal?"Обычный · F2":Detail==HudDetail.Observer?"Наблюдатель · F2":"Научный · F2","Меняет только представление данных"))CycleDetail();
            if(Button(new Rect(W-94,22,32,42),"?","Подсказки · F1",HelpVisible)){HelpVisible=!HelpVisible;settings=false;}
            if(Button(new Rect(W-58,22,32,42),"≡","Размер интерфейса и звук",settings)){settings=!settings;HelpVisible=false;}
        }
        void DrawTasks()
        {
            var r=Left;Box(r,Page==HudPage.Overview?"Жизнь вокруг":"Твоя следующая цель");
            if(Page!=HudPage.Overview){
                GUI.Label(new Rect(r.x+16,r.y+60,r.width-32,54),readout.Goal,title);
                GUI.Label(new Rect(r.x+16,r.y+119,r.width-32,42),water.World.Controlled>=0?"WASD — плыть · Q / E — глубина\nИли щёлкни по свободной воде":"Выбери клетку и нажми «Движение»",micro);
                return;
            }
            Label(r.x+16,r.y+58,r.width-32,"Организмов: "+living,title);
            Label(r.x+16,r.y+89,r.width-32,"Генетических линий: "+lines.Count);
            Label(r.x+16,r.y+119,r.width-32,"В загруженной области / слое",micro);
            if(Button(new Rect(r.x+12,r.y+155,r.width-24,36),water.Overview.Analytical?"Аналитический обзор · Tab":"Кинематографический · Tab","Переключает реальные маркеры и слои",water.Overview.Analytical))water.Overview.SetMode(!water.Overview.Analytical);
            if(Button(new Rect(r.x+12,r.y+199,120,36),"Автокадр","Кадрировать местную группу"))water.Overview.Home();
            if(Button(new Rect(r.x+140,r.y+199,120,36),water.Overview.AutoOrbit?"Облёт: да":"Облёт: нет","Плавное вращение камеры"))water.Overview.AutoOrbit=!water.Overview.AutoOrbit;
        }
        void Row(Rect r,int index,string name,string value,string tip,Color? colour=null)
        {
            float y=r.y+60+index*32;GUI.Label(new Rect(r.x+16,y,126,28),new GUIContent(name,tip),text);
            var old=GUI.color;if(colour.HasValue)GUI.color=colour.Value;
            GUI.Label(new Rect(r.x+143,y,r.width-156,28),new GUIContent(value,tip),value.Length>15?valueText:text);GUI.color=old;
        }
        void DrawCell()
        {
            var r=Right;Box(r,Detail==HudDetail.Scientific?"Научные данные":"Твоя клетка");
            if(water.SelectedIndex<0){Label(r.x+16,r.y+60,260,"Выбери живой организм");return;}
            var a=water.World.Get(water.SelectedIndex);var c=a.Biology;
            if(Detail==HudDetail.Scientific){DrawScience(r,a);return;}
            Row(r,0,"Энергия",Mathf.RoundToInt(readout.Energy*100)+"%","Запас энергии / вместимость клетки. Движение расходует энергию.");
            Fill(new Rect(r.x+16,r.y+91,r.width-32,5),new Color(.12f,.25f,.3f));
            Fill(new Rect(r.x+16,r.y+91,(r.width-32)*shownEnergy,5),readout.Energy<.2f?new Color(1,.55f,.35f):new Color(.55f,.9f,.4f));
            Row(r,1,"Состояние",readout.Condition,"Состояние оболочки и запас энергии. Это реальные Integrity и Energy.");
            Row(r,2,"Питание",readout.Nutrition,"Возможное усвоение ближайшей доступной пищи за секунду относительно базового расхода клетки.");
            Row(r,3,"Температура",readout.Temperature,"Комфорт определяется эффективностью обмена с учётом любимой температуры и устойчивости клетки.");
            Row(r,4,"Течение",readout.Current,"Скорость воды относительно собственной подвижности организма.");
            Row(r,5,"Свет",readout.Illumination,"Доступность света в этой точке воды; особенно важна для фотосинтеза.");
            int offspring=water.Ocean.Find(water.World.LastOffspringIdentity);
            if(offspring>=0&&water.World.Get(offspring).Alive&&water.World.Get(offspring).Identity.StartsWith(a.Identity+"/b",StringComparison.Ordinal)) {
                if(Button(new Rect(r.x+12,r.y+252,r.width-24,36),"Наблюдать потомка")){water.Select(offspring);return;}
            } else GUI.Label(new Rect(r.x+16,r.y+255,r.width-32,38),readout.Threat>=0?"Опасность: рядом хищник":water.World.DivisionBlock(water.SelectedIndex)??"Можно разделиться · R",micro);
        }
        void DrawScience(Rect r,VolumeAgent a)
        {
            var c=a.Biology;var g=c.Genes;var sample=water.World.Sample(a.Position);var v=water.World.Current(a.Position);
            var b=new StringBuilder();
            b.AppendLine($"ID {c.Id} · lineage {c.LineageId} · {a.Identity}");
            b.AppendLine($"XYZ {a.Position.X:F2} / {a.Position.Y:F2} / {a.Position.Z:F2}");
            b.AppendLine($"Velocity {a.Velocity.X:F3} / {a.Velocity.Y:F3} / {a.Velocity.Z:F3}");
            b.AppendLine($"Energy {c.Energy:F3} / {Biology.Capacity(g):F3} · Integrity {c.Integrity:F3}");
            b.AppendLine($"Age {c.Age:F2} · Generation {c.Generation} · Growth {c.Growth:F3}");
            b.AppendLine($"Temperature {sample.Temperature:F3} °C · Light {sample.Light:F3}");
            b.AppendLine($"Current {v.X:F3} / {v.Y:F3} / {v.Z:F3}");
            b.AppendLine($"Budget dt {a.RenderDuration:F3}: intake {c.Budget.Intake:F4} · net {c.Budget.Net:F4}");
            b.AppendLine($"Substrate {c.Budget.Substrate:F4} · Organic {c.Budget.Organic:F4} · Light {c.Budget.Light:F4} · Prey {c.Budget.Prey:F4}");
            b.AppendLine($"Division {c.Budget.Division:F4} · Growth {c.Budget.Growth:F4} · Movement {c.Budget.Movement:F4} · Attack {c.Budget.Attack:F4} · Loss {c.Budget.PredationLoss:F4}");
            b.AppendLine("Геном (только чтение):");
            foreach(var f in typeof(Genome).GetFields())b.AppendLine(f.Name+": "+((float)f.GetValue(g)).ToString("F3"));
            if(readout.Food>=0){var f=water.World.GetFood(readout.Food);b.AppendLine($"Food {f.Identity}: S {f.Substrate:F3} · O {f.Organic:F3} · concentration {f.Concentration:F4}");}
            b.AppendLine($"Tick {water.World.Tick} · loaded chunks {water.Ocean.LoadedCount} · particles {water.Water.Count}");
            scienceScroll=GUI.BeginScrollView(new Rect(r.x+12,r.y+58,r.width-24,r.height-72),scienceScroll,new Rect(0,0,r.width-48,1100));
            GUI.Label(new Rect(0,0,r.width-52,1100),b.ToString(),micro);GUI.EndScrollView();
        }
        void DrawActions()
        {
            var r=Actions;float cell=100;
            if(Action(new Rect(r.x,r.y,cell,76),0,"Движение","Взять управление. WASD или щелчок по воде; Q/E — вверх и вниз",water.World.Controlled>=0,readout.Alive)){development=false;water.PlaySelected();}
            if(Action(new Rect(r.x+104,r.y,cell,76),1,"Питание","Подсветить подходящую пищу. Усвоение автоматическое внутри облака",water.FindFood)){water.FindFood=!water.FindFood;}
            bool canAttack=readout.Alive&&water.World.Controlled>=0&&water.World.Get(water.SelectedIndex).Biology.Genes.Predation>.2f&&water.World.Get(water.SelectedIndex).Biology.Energy>.5f;
            string why=!readout.Alive?"Выбери живую клетку":water.World.Controlled<0?"Сначала возьми управление":water.World.Get(water.SelectedIndex).Biology.Genes.Predation<=.2f?"Эта способность отсутствует":"Недостаточно энергии";
            if(Action(new Rect(r.x+208,r.y,cell,76),2,AttackRequested?"Атака: вкл":"Атака",canAttack?"Разрешить укусы в пределах досягаемости. Также можно удерживать Space":why,AttackRequested,canAttack))AttackRequested=!AttackRequested;
            string blocked=water.World.DivisionBlock(water.SelectedIndex);
            if(water.Paused)blocked="Продолжи симуляцию";
            else if(water.World.Controlled!=water.SelectedIndex)blocked="Сначала возьми управление";
            float divisionCost=0;
            string divisionTip=blocked??"Рождение самостоятельного потомка";
            if(water.SelectedIndex>=0) {
                var c=water.World.Get(water.SelectedIndex).Biology;
                float construction=Biology.StructureEnergy(c.Genes)+Biology.BirthOverhead(c.Genes);
                float provision=Mathf.Max(c.Energy,water.World.DivisionThreshold(water.SelectedIndex));
                float cost=construction+Mathf.Min((provision-construction)*.5f,Biology.Capacity(c.Genes)*.5f);divisionCost=cost;
                divisionTip+="\nРасход ≈ "+cost.ToString("F1")+": структура и запас.\nЦена зависит от генома потомка.";
            }
            if(Action(new Rect(r.x+312,r.y,cell,76),3,"Разделиться",divisionTip,false,blocked==null))water.World.RequestDivision();
            if(water.SelectedIndex>=0)GUI.Label(new Rect(r.x+70,r.y-42,r.width-140,38),
                "Нужно "+water.World.DivisionThreshold(water.SelectedIndex).ToString("F1")+" · Расход ≈ "+divisionCost.ToString("F1")+" энергии\n"+(blocked??"Готово · R"),muted);
            if(Action(new Rect(r.x+416,r.y,cell,76),4,"Развитие","Просмотреть наследуемые черты",development))SetPage(HudPage.Development);
        }
        void DrawFooter()
        {
            var r=Footer;Box(r);
            string layer=water.Overview.FullDepth?"Вся толща":"Слой "+water.Overview.SliceIndex*20+"…"+(water.Overview.SliceIndex+1)*20;
            Label(r.x+16,r.y+9,r.width-180,$"В области: {living}  ·  Линий: {lines.Count}  |  Пища: {readout.Nutrition.ToLowerInvariant()}  ·  Вода: {readout.Temperature.ToLowerInvariant()}  |  {layer}",micro);
            if(Button(new Rect(r.xMax-150,r.y+5,138,32),water.Paused?"Продолжить · P":"Пауза · P"))water.Paused=!water.Paused;
        }
        Vector2 MapPoint(V3 p,Rect map,V3 center)=>new Vector2(map.center.x+(p.X-center.X)/mapRange*map.width*.5f,map.center.y-(p.Z-center.Z)/mapRange*map.height*.5f);
        void DrawMap()
        {
            var r=MapBox;Box(r);Label(r.x+12,r.y+8,210,"Карта экосистемы",title);
            var map=new Rect(r.x+12,r.y+42,r.width-24,126);var center=VolumePainter.V(water.Rig.Focus);
            Fill(map,new Color(.015f,.07f,.12f,.65f));
            for(int n=1;n<4;n++){Line(new Vector2(map.x+n*map.width/4,map.y),new Vector2(map.x+n*map.width/4,map.yMax),new Color(.25f,.55f,.7f,.18f));}
            for(int k=0;k<water.World.FoodCount;k++){
                var f=water.World.GetFood(k);if(!InLayer(f.Position)||f.Substrate+f.Organic<=.01f)continue;
                var p=MapPoint(f.Position,map,center);if(map.Contains(p))Dot(p,2,new Color(.95f,.8f,.3f,.8f));
            }
            var aSel=water.SelectedIndex>=0?water.World.Get(water.SelectedIndex):default;
            foreach(var a in water.Overview.Survey.Agents){if(!InLayer(a.Position))continue;var p=MapPoint(a.Position,map,center);if(!map.Contains(p))continue;
                bool selected=a.Identity==aSel.Identity;bool threat=water.SelectedIndex>=0&&readout.Threat>=0&&a.Identity==water.World.Get(readout.Threat).Identity;
                Dot(p,selected?5:2.2f,selected?Color.white:threat||a.Threat?new Color(1,.4f,.4f):Color.Lerp(new Color(.15f,.6f,.85f),new Color(.55f,.95f,.7f),Mathf.InverseLerp(center.Y-40,center.Y+40,a.Position.Y)));
            }
            Vector2 eye=MapPoint(VolumePainter.V(water.Lens.transform.position),map,center);var forward=water.Lens.transform.forward;Vector2 dir=new Vector2(forward.x,-forward.z).normalized;
            if(map.Contains(eye)){Line(eye,eye+dir*14,Color.white,2);Line(eye+dir*14,eye+dir*6+new Vector2(-dir.y,dir.x)*5,Color.white);}
            if(Event.current.type==EventType.ScrollWheel&&map.Contains(Event.current.mousePosition)){mapRange=Mathf.Clamp(mapRange*Mathf.Exp(Event.current.delta.y*.08f),10,180);Event.current.Use();}
            if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&map.Contains(Event.current.mousePosition)){
                var p=Event.current.mousePosition;V3 position=new V3(center.X+(p.x-map.center.x)*2*mapRange/map.width,water.Overview.FullDepth?center.Y:water.Overview.SliceIndex*20+10,center.Z-(p.y-map.center.y)*2*mapRange/map.height);
                int chosen=-1;float nearest=mapRange*.06f;for(int i=0;i<water.World.Count;i++){var a=water.World.Get(i);if(!a.Alive||!InLayer(a.Position))continue;float distance=new Vector2(a.Position.X-position.X,a.Position.Z-position.Z).magnitude;if(distance<nearest){nearest=distance;chosen=i;}}
                development=false;water.Overview.Enter();water.Overview.AutoFrame=false;
                if(chosen>=0)water.Overview.ChooseOrganism(chosen,true);else {water.Overview.ChooseRegion(ChunkKey.At(position),false);water.Rig.FocusOn(VolumePainter.V(position),water.Rig.Distance);}
                Event.current.Use();
            }
            string depth=water.Overview.FullDepth?"Вся толща · цвет = высота":"Срез Y "+water.Overview.SliceIndex*20+"…"+(water.Overview.SliceIndex+1)*20;
            Label(r.x+12,r.y+170,r.width-24,depth,micro);
            if(Button(new Rect(r.x+10,r.y+198,128,30),water.Overview.FullDepth?"Выбрать слой":"Вся толща","Срез по высоте 20 условных единиц")){water.Overview.SetSlice(!water.Overview.FullDepth,ChunkKey.At(center).Y);}
            if(Button(new Rect(r.x+142,r.y+198,35,30),"−","Ниже на 20"))water.Overview.SetSlice(false,water.Overview.SliceIndex-1);
            if(Button(new Rect(r.x+181,r.y+198,35,30),"+","Выше на 20"))water.Overview.SetSlice(false,water.Overview.SliceIndex+1);
            if(Button(new Rect(r.x+220,r.y+198,40,30),"×2","Масштаб карты; колесо над картой"))mapRange=mapRange>100?25:mapRange*2;
        }
        void DrawDevelopment()
        {
            var r=DevelopmentBox;Box(r,"Развитие твоего организма");
            if(water.SelectedIndex<0)return;var c=water.World.Get(water.SelectedIndex).Biology;var g=c.Genes;
            GUI.Label(new Rect(r.x+20,r.y+62,r.width-40,60),"В 3D доступны существующие наследуемые черты.\nПотомки наследуют геном; возможны небольшие мутации.",text);
            Row(r,2,"Линия",c.LineageId.ToString(),"Генетическая линия выбранного организма");
            Row(r,3,"Питание",g.Photosynthesis>.5f?"Фотосинтез":g.Predation>.5f?"Хищничество":"Смешанное","Преобладающая стратегия существующего генома");
            Row(r,4,"Оболочка",g.Membrane>.7f?"Плотная":"Гибкая","Мембрана пассивно участвует в защите. Отдельная активная защита в 3D отсутствует.");
            Row(r,5,"Подвижность",g.Speed>1.18f?"Быстрая":"Умеренная","Наследуемая скорость организма");
            Row(r,6,"Поколение",c.Generation.ToString(),"Реальное поколение модели. Прогресс размножения в 3D не имитируется.");
            if(Button(new Rect(r.x+20,r.yMax-64,240,42),"Наблюдать генетическую линию")){development=false;water.Overview.Enter();water.Overview.ChooseOrganism(water.SelectedIndex,false);water.Overview.SelectLineage();water.Overview.SetMode(true);water.Overview.FollowSelection();}
            if(Button(new Rect(r.x+280,r.yMax-64,240,42),"Вернуться к клетке"))SetPage(HudPage.Cell);
        }
        void DrawSettings()
        {
            var r=SettingsBox;Box(r,"Настройки");Label(r.x+16,r.y+54,260,"Размер интерфейса",micro);
            float value=GUI.HorizontalSlider(new Rect(r.x+16,r.y+87,270,18),requestedScale,.8f,1.25f);
            if(Mathf.Abs(value-requestedScale)>.001f){requestedScale=value;PlayerPrefs.SetFloat("OceanHud.Scale",value);}
            Label(r.x+16,r.y+110,80,"Звук",micro);water.SoundVolume=GUI.HorizontalSlider(new Rect(r.x+90,r.y+120,196,18),water.SoundVolume,0,.6f);
        }
        void DrawHelp()
        {
            var r=HelpBox;Box(r,"Управление");GUI.Label(new Rect(r.x+16,r.y+55,r.width-32,96),"WASD — движение · Q/E — глубина\nShift — ускорение · Space — укус · R — деление\nПКМ — вращение · колесо — масштаб\nTab — обзор / следующая клетка\nH — скрыть HUD · Esc — назад",micro);
        }
        void DrawNotices()
        {
            if(journal){DrawJournal();return;}
            var r=ToastBox;int n=0;
            foreach(var notice in notices){if(n==2)break;var b=new Rect(r.x,r.y+n*78,r.width,72);Box(b);
                Fill(new Rect(b.x+3,b.y+14,3,40),notice.Danger?new Color(1,.42f,.4f):new Color(.5f,.9f,.65f));
                GUI.Label(new Rect(b.x+14,b.y+6,b.width-24,26),notice.Text,text);GUI.Label(new Rect(b.x+14,b.y+33,b.width-24,36),notice.Sub,micro);n++;}
        }
        void DrawJournal()
        {
            var r=ToastBox;Box(r,"Недавние события");
            var events=water.Overview.Survey.Events;
            for(int n=0;n<3&&n<events.Count;n++){
                int i=events.Count-1-n;var e=events[i];
                if(Button(new Rect(r.x+10,r.y+51+n*32,r.width-20,30),EventName(e.Kind)+" · "+e.Count,"Перейти к месту фактического события")){
                    development=false;if(!water.Overview.Active)water.Overview.Enter();water.Overview.VisitEvent(i);
                }
            }
        }
        void DrawWorldLabels()
        {
            if(readout.Food>=0){var food=water.World.GetFood(readout.Food);WorldLabel(food.Position,"Пища · подплыви ближе",new Color(.95f,.83f,.4f));}
            if(readout.Threat>=0)WorldLabel(water.World.Get(readout.Threat).Position,"Опасность · хищник",new Color(1,.55f,.5f));
        }
        void WorldLabel(V3 point,string value,Color colour)
        {
            var p=water.Lens.WorldToScreenPoint(VolumePainter.V(point));if(p.z<=0)return;
            var r=new Rect(Mathf.Clamp(p.x/Scale-104,305,W-530),Mathf.Clamp((Screen.height-p.y)/Scale-65,90,H-220),208,40);
            foreach(var box in occupied)if(r.Overlaps(box))return;
            var old=GUI.color;GUI.color=colour;GUI.Box(r,GUIContent.none,panel);GUI.Label(r,value,centered);GUI.color=old;
            Line(new Vector2(r.center.x,r.yMax),new Vector2(Mathf.Clamp(p.x/Scale,18,W-18),Mathf.Clamp((Screen.height-p.y)/Scale,80,H-72)),colour);
        }
        static readonly string[] Layers={"Генетические линии","Формы жизни","Способы питания","Угрозы","Плотность жизни","Запасы пищи","Температура","Течения","Свет"};
        void DrawObserverLinks()
        {
            var r=ObserverBox;occupied.Add(r);float half=(r.width-8)/2;
            if(Button(new Rect(r.x,r.y,half,34),"Слой: "+Layers[(int)water.Overview.Layer],"Переключить аналитические данные")){water.Overview.Layer=(SurveyLayer)(((int)water.Overview.Layer+1)%9);water.Overview.SetMode(true);}
            if(busiest!=null&&Button(new Rect(r.x+half+8,r.y,half,34),"Больше жизни: "+busiest.Living))water.Overview.ChooseRegion(busiest.Key,true);
            if(foodRegion!=null&&Button(new Rect(r.x,r.y+40,half,32),"Найти пищу"))water.Overview.ChooseRegion(foodRegion.Key,true);
            if(dangerRegion!=null&&Button(new Rect(r.x+half+8,r.y+40,half,32),"Недавние атаки"))water.Overview.ChooseRegion(dangerRegion.Key,true);
            int count=water.Overview.Survey.Events.Count;
            if(count>0){var e=water.Overview.Survey.Events[count-1];if(Button(new Rect(r.x,r.y+80,r.width,32),"События: "+count+" · "+EventName(e.Kind),"Показать журнал последних наблюдений"))journal=!journal;}
        }
        static string EventName(SurveyEventKind kind)=>kind==SurveyEventKind.Division?"рождение потомка":kind==SurveyEventKind.Predation?"атака":kind==SurveyEventKind.Death?"гибель клетки":kind==SurveyEventKind.Migration?"перемещение группы":kind==SurveyEventKind.KnownLineageLost?"исчезновение линии":"изменение численности";
        void OnDestroy(){Destroy(skin);Destroy(chrome);Destroy(chromeActive);Destroy(white);Destroy(dot);}
    }
}
