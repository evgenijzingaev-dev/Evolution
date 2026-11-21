(() => {
'use strict';
const STORE='dom13-nightshift-v4';
const LEGACY_STORES=['dom13-nightshift-v2','dom13-nightshift-v3'];
const $=id=>document.getElementById(id);
const clamp=(n,min=0,max=100)=>Math.max(min,Math.min(max,n));
const residents=[
 {id:'301',name:'Кира',role:'Студентка',emoji:'👩🏻‍🎓',habit:'Снимает всё странное на телефон. Вечно расследует чужие тайны.',home:h=>h>=19,trait:'любопытная'},
 {id:'302',name:'Лев',role:'Стример',emoji:'🧑🏽‍💻',habit:'Ведёт ночные эфиры. Никогда не выключает микрофон.',home:h=>h>=19,trait:'наблюдательный'},
 {id:'201',name:'Майя',role:'Музыкант',emoji:'👩🏾‍🎤',habit:'Репетирует вечером. Замечает звуки, которые не слышат другие.',home:h=>h>=20,trait:'творческая'},
 {id:'202',name:'Виктор',role:'Мастер',emoji:'👨🏻‍🔧',habit:'Знает каждый щиток и подсобку. Не любит, когда ему мешают.',home:h=>h>=21,trait:'замкнутый'},
 {id:'101',name:'Тамара',role:'Старшая дома',emoji:'👵🏻',habit:'Ведёт домовой журнал и помнит всех жильцов по именам.',home:h=>true,trait:'заботливая'},
 {id:'102',name:'Олег',role:'Курьер',emoji:'🧔🏽',habit:'Возвращается поздно. По долгу службы знает подъезды и двери.',home:h=>h>=22,trait:'уставший'}
];
const cases=[
 {icon:'⚡',title:'22:13',hook:'Каждую ночь ровно в 22:13 в доме на секунду гаснет свет, а из подвала доносится металлический щелчок. Это чья-то диверсия или забытый механизм?',correct:'relay',reveal:'Никто не ломал щит вручную. В подвале срабатывало старое аварийное реле «Линия 13», пытаясь запитать скрытую систему дома. Именно этот щелчок и вызывал ночной провал напряжения.',
  guesses:[['miner','🖥 Тайный майнер в подвале','Кто-то ворует электричество ради скрытого оборудования'],['relay','🧲 Аварийное реле «Линия 13»','Старая система сама запускает забытый контур'],['search','🔦 Кто-то ищет клад','Отключение света нужно, чтобы незаметно шарить по подвалу']],
  clues:[
   ['log13','101','knock','Пометка «22:13»','Тамара показывает старый журнал отключений: время каждого сбоя совпадает до минуты — 22:13.'],
   ['sparkmark','102','observe','Следы копоти','У подвальной двери Олег замечает не лужи, а свежую копоть у кабель-канала.'],
   ['clickloop','201','listen','Один и тот же щелчок','Майя уверяет: перед каждым отключением звучит одинаковый металлический «клац», будто срабатывает реле.'],
   ['line13','202','knock','Схема с подписью «Линия 13»','Виктор находит старую схему: часть щита ведёт на отдельный резервный контур.'],
   ['flash13','301','observe','Видео вспышки','Кира сняла короткую вспышку не в квартирах, а в решётке вентиляции у подвала.'],
   ['hum13','302','listen','Гул за стеной','На записи стрима Льва после щелчка слышен секундный гул, словно запускается старый трансформатор.']
  ],deduction:'Совпадающее время, щелчок и резервная схема указывают не на случайность, а на автоматический запуск старого контура.'},
 {icon:'📦',title:'Посылка для смотрителя',hook:'Из общей доставки исчезла одна коробка без адресата. На ней только надпись: «Передать смотрителю №13». Почему кто-то прячет её от жильцов?',correct:'caretaker',reveal:'Среди обычных доставок пришла старая коробка, адресованная смотрителю дома №13. Внутри лежали ключ, кассета и обрывок схемы. Её спрятали не ради кражи, а чтобы сначала понять, с чем связана находка.',
  guesses:[['theft','🛵 Курьер подворовывает','Олег забирает ценные коробки себе'],['caretaker','🗝 Кто-то ищет посылку смотрителя','Неизвестным нужен именно этот странный ящик'],['prank','🎥 Это розыгрыш для контента','Кто-то устроил шоу ради просмотров и слухов']],
  clues:[
   ['notebox','101','knock','Записка в журнале','Тамара вписала: «Одна коробка пришла без квартиры, только со словом “смотритель”».'],
   ['roughtwine','102','observe','Старый шпагат','Олег показывает обрывок бечёвки: странная коробка была перевязана не как современная доставка.'],
   ['dragmarks','201','listen','Шорох под лестницей','Майя ночью слышала, как нечто небольшое волокли к служебной двери, а не к выходу.'],
   ['brasskey','202','knock','Латунный ключ','Виктор видел в коробке тяжёлый ключ с клеймом «0», прежде чем её унесли из коридора.'],
   ['framegrab','301','observe','Кадр с человеком в капюшоне','На стоп-кадре видно: неизвестный берёт только одну коробку, остальные его не интересуют.'],
   ['cassetterattle','302','listen','Пустой гул ленты','Лев на слух узнаёт звук аудиокассеты внутри коробки, когда её несут по лестнице.']
  ],deduction:'Не пропадают все посылки — исчезает только один особый ящик с ключом и кассетой, связанный со смотрителем.'},
 {icon:'📻',title:'Голос, который знает квартиры',hook:'После полуночи чердак оживает: металлический голос перечисляет номера квартир и просит «приготовиться к спуску». Кто вообще читает этот список?',correct:'announcer',reveal:'На чердаке ожил старый модуль оповещения аварийного убежища. После перезапуска резервного контура он начал крутить тестовую запись для жильцов дома.',
  guesses:[['spies','🕵️ За домом следят','Кто-то использует скрытый передатчик для прослушки'],['announcer','📢 Это старый модуль оповещения','Дом сам проговаривает архивное сообщение'],['ghost','👁 Говорит нечто из прошлого','Сигнал невозможно объяснить обычной техникой']],
  clues:[
   ['archivevoice','101','knock','Опись оборудования','В папке Тамары есть старая опись: «чердачный блок голосового оповещения».'],
   ['speakergrill','102','observe','Ржавая решётка динамика','Олег замечает под стропилами старую акустическую решётку, закрашенную в цвет балки.'],
   ['loopphrase','201','listen','Зацикленная фраза','Майя слышит одно и то же объявление без живых интонаций — как на записи.'],
   ['fusevoice','202','knock','Подключение к резерву','Виктор подтверждает: тот же контур «Линия 13» уходит наверх, к чердаку.'],
   ['waveform','301','observe','Осциллограмма на телефоне','Кира показывает график: сигнал повторяется идеально ровно, без человеческих пауз.'],
   ['flatlist','302','listen','Список заканчивается на 302','Лев отмечает странность: голос перечисляет только реальные квартиры дома и замолкает.']
  ],deduction:'Повторяемость, список квартир и старое оборудование указывают на автоматическое объявление, а не на живого шпиона.'},
 {icon:'✉️',title:'Письма тринадцатой ночи',hook:'Под дверями появляются копии старых писем с фразой: «Если снова услышите дом — не открывайте №0 в одиночку». Кто рассылает эти предупреждения?',correct:'warning',reveal:'Это не шантаж. Копии писем бывшего смотрителя кто-то разнёс по дому после того, как нашёл кассету и понял: старые предупреждения снова актуальны.',
  guesses:[['threat','🕵️ Это давление на жильцов','Кто-то пытается запугать весь подъезд'],['warning','📚 Это настоящее предупреждение','Кто-то хочет, чтобы жильцы узнали правду раньше паники'],['content','🎭 Чья-то театральная игра','Послания подбрасывают ради эффекта и драмы']],
  clues:[
   ['stamp1998','101','knock','Штемпель 1998 года','Тамара узнаёт старую дату и вспоминает первого смотрителя, внезапно исчезнувшего много лет назад.'],
   ['copytoner','102','observe','Следы ксерокса','Олег замечает: это не оригиналы, а свежие копии с потёртых листов.'],
   ['whisperwarn','201','listen','Фраза с кассеты','Майя находит на плёнке те же слова: «Не открывайте нулевую дверь в одиночку».'],
   ['samehand','202','knock','Почерк на схеме и письме','Виктор сравнивает заметки и понимает: подписи на схеме убежища и в письмах сделаны одной рукой.'],
   ['foldershot','301','observe','Кадр с архивной папкой','На видео Киры видно, как кто-то несёт старую папку в сторону копировального центра.'],
   ['namecut','302','listen','Вырезанное имя','Лев разбирает на кассете кусок фразы: «...смотритель Аркадий...», прежде чем запись обрывается.']
  ],deduction:'Письма совпадают с записью на кассете и старой схемой — это не пугалка, а предупреждение от прошлого дома.'},
 {icon:'🚪',title:'Комната №0',hook:'На подвальной схеме есть закрашенная дверь без квартиры. Иногда за стеной слышны шаги и работающий мотор. Что прячется за комнатой №0?',correct:'shelter',reveal:'За дверью №0 скрыто законсервированное аварийное убежище первого смотрителя. Реле «Линия 13», особая посылка, голос с чердака и письма были частью одной забытой системы защиты дома, которую случайно пробудили.',
  guesses:[['gang','🔒 Тайный склад чужаков','Помещение используют посторонние для своих дел'],['shelter','🏠 Аварийное убежище дома','За стеной законсервированная система безопасности'],['portal','🌀 Нечто за пределом стен','Дверь ведёт к необъяснимому месту']],
  clues:[
   ['evacplan','101','knock','План эвакуации','Тамара показывает пожелтевший лист: «Помещение №0 — спуск по команде смотрителя».'],
   ['paintscar','102','observe','Закрашенный знак выхода','Олег находит под краской старую стрелку аварийного выхода возле стены подвала.'],
   ['motorpulse','201','listen','Пульс мотора','Майя слышит знакомый ритм — это тот же короткий гул, что шёл после отключения в первом деле.'],
   ['backupbus','202','knock','Резервная шина','Виктор узнаёт проводку автономного питания, уходящую прямо в замурованный отсек.'],
   ['wallshift','301','observe','Смещение кладки','Кира замечает, что ряд кирпичей вокруг «глухой» стены новее остального подвала.'],
   ['keyzero','302','listen','Щелчок латунного ключа','Лев на записи узнаёт звук того самого ключа из посылки: дверь №0 действительно открывается механически.']
  ],deduction:'Все нити сходятся сюда: резервное питание, оповещение, ключ и предупреждения — это единая забытая система дома.'}
];

function makeState(){return {version:4,chapter:0,day:1,hour:19,focus:4,tension:18,kindness:0,trust:{},clues:[],actions:0,helped:{},log:[{icon:'📻',text:'Началась новая ночная смена в доме №13.'}],history:[],score:0,closed:false,ending:null,pendingEvent:null,eventsSeen:{},sound:false,rainAudio:false,seenTutorial:false,chapterStart:18,achievements:[],lifeEvents:0,choices:[],hints:0,storiesSeen:{}}}
let state;
try{
 let old=JSON.parse(localStorage.getItem(STORE));
 if(!old){
  for(const legacy of LEGACY_STORES){
   const raw=localStorage.getItem(legacy);
   if(raw){old=JSON.parse(raw);break}
  }
 }
 const compatible=old&&Number.isInteger(old.chapter)&&old.chapter>=0&&old.chapter<cases.length&&Array.isArray(old.clues)&&Array.isArray(old.history)&&old.version===4;
 state=compatible?{...makeState(),...old,version:4}:makeState();
 if(!Array.isArray(state.achievements))state.achievements=[];
 state.rainAudio=false;
 if(!state.storiesSeen||typeof state.storiesSeen!=='object')state.storiesSeen={};
 if(!Array.isArray(state.choices))state.choices=[];
 if(!Array.isArray(state.log))state.log=[];
 if(!state.eventsSeen||typeof state.eventsSeen!=='object')state.eventsSeen={};
 if(!state.helped||typeof state.helped!=='object')state.helped={};
 if(!state.trust||typeof state.trust!=='object')state.trust={};
}catch{state=makeState()}
let toastTimer, audio, rainLoop, rainGain;
const modal=$('modal');
const current=()=>cases[state.chapter];
const getClues=()=>current().clues.map(c=>({id:c[0],resident:c[1],action:c[2],title:c[3],body:c[4]}));
const home=p=>p.home(state.hour);
const addLog=(text,icon='📻')=>{state.log.unshift({text,icon});state.log=state.log.slice(0,30)};
const specialEvents=[
 ['⚡ Щелчок в 22:13','Свет погас ровно на секунду. В подвале оживает забытое устройство; Виктор зовёт на помощь, Кира уже снимает видео. Кому ты доверишься?',
  ['🤝 Помочь Виктору','Разгрузить щит и снизить тревогу','protect'],['📱 Снять происходящее','Получить общественное внимание, но напугать соседей','expose']],
 ['📦 Тележка у лифта','Кто-то несёт коробки к подсобке. Жильцы уже называют это кражей.',
  ['🧑‍🤝‍🧑 Остановить ссору','Выслушать всех и снизить напряжение','protect'],['📸 Обнародовать запись','Подогреть обсуждение и найти свидетелей','expose']],
 ['📻 Странный прямой эфир','Лев собирается транслировать загадочный голос всему городу.',
  ['🎙 Попросить отложить эфир','Защитить спокойствие соседей','protect'],['📢 Разослать запись','Вызвать общественный резонанс','expose']],
 ['✉️ Конверт с печатью','Ты находишь ещё одно письмо и понимаешь, что его содержимое лично касается Тамары.',
  ['💛 Поговорить с Тамарой','Уважить личную историю соседки','protect'],['📰 Вывесить письмо','Дать всем узнать тайну немедленно','expose']],
 ['🚪 Замок двери №0','Вокруг старой двери собираются жильцы. Что делать в первую очередь?',
  ['🧯 Проверить безопасность','Убедиться, что внутри никого нет','protect'],['🔨 Вскрыть замок сейчас','Рискнуть ради быстрой разгадки','expose']]
];
const dailyScenes=[
 ['301','📱','Кира снимает сторис из окна: снова что-то происходит во дворе.'],
 ['302','🎧','Лев включает ночной эфир. В чате спорят о странностях дома.'],
 ['201','🎶','Из квартиры Майи слышен короткий музыкальный мотив.'],
 ['202','🧰','Виктор несёт ящик с инструментами по лестнице.'],
 ['101','🫖','Тамара разносит соседям горячий чай.'],
 ['102','📦','Олег возвращается с последним заказом.']
];
const stories={
 '301':'Кира признаётся: она боится не странных звуков, а того, что ей никто не поверит.',
 '302':'Лев ставит эфир на паузу и говорит, что чужая тайна не должна становиться развлечением.',
 '201':'Майя рассказывает, что узнаёт соседей по звукам шагов на лестнице.',
 '202':'Виктор впервые смеётся: «Гайку затянуть проще, чем попросить помощи».',
 '101':'Тамара вспоминает первого смотрителя: он всегда оставлял свет в подъезде для тех, кто поздно возвращался.',
 '102':'Олег рассказывает, как соседи однажды спасли его из застрявшего лифта.'
};
const achievementsCatalog=[
 ['first','🔎 Первый след','Найти первую улику'],
 ['neighbor','💛 Добрый сосед','Помочь жильцам три раза'],
 ['patient','📚 Архивариус','Найти все шесть улик в одном деле'],
 ['peace','🕊 Миротворец','Решить дело без сильной паники'],
 ['detective','🏅 Детектив дома №13','Верно раскрыть все пять дел']
];
function unlock(key){if(state.achievements.includes(key))return;state.achievements.push(key);const a=achievementsCatalog.find(i=>i[0]===key);if(a)addLog(`Достижение: ${a[1]} — ${a[2]}`,a[1].split(' ')[0])}
function evaluateAchievements(){if(state.clues.length)unlock('first');if(state.kindness>=3)unlock('neighbor');if(state.clues.length===6)unlock('patient');if(state.history.some(x=>x.win&&x.tension<=30))unlock('peace');if(state.history.length===cases.length&&state.history.every(x=>x.win))unlock('detective')}
function lifeScene(){if(state.closed)return;const i=(state.chapter*7+state.day*3+state.hour)%dailyScenes.length,scene=dailyScenes[i];if(state.hour===20||state.hour===23){const resident=residents.find(p=>p.id===scene[0]);if(resident&&home(resident))addLog(scene[2],scene[1]);else addLog(`На лестничной клетке слышны шаги, но квартира ${scene[0]} ещё пуста.`,'◦');state.lifeEvents++}}

const save=()=>{try{localStorage.setItem(STORE,JSON.stringify(state))}catch{}};
function tone(freq=500,duration=.06){if(!state.sound)return;try{audio??=new (window.AudioContext||window.webkitAudioContext)();const osc=audio.createOscillator(),gain=audio.createGain();osc.type='sine';osc.frequency.value=freq;gain.gain.setValueAtTime(.05,audio.currentTime);gain.gain.exponentialRampToValueAtTime(.001,audio.currentTime+duration);osc.connect(gain).connect(audio.destination);osc.start();osc.stop(audio.currentTime+duration)}catch{}}
function toggleRainSound(){
 state.rainAudio=!state.rainAudio;
 try{
  audio??=new (window.AudioContext||window.webkitAudioContext)();
  if(audio.state==='suspended')audio.resume();
  if(state.rainAudio){
   if(!rainLoop){
    const seconds=2,samples=Math.round(audio.sampleRate*seconds),buffer=audio.createBuffer(1,samples,audio.sampleRate);
    const values=buffer.getChannelData(0);for(let i=0;i<samples;i++)values[i]=(Math.random()*2-1)*.5;
    rainLoop=audio.createBufferSource();rainLoop.buffer=buffer;rainLoop.loop=true;
    const lowpass=audio.createBiquadFilter();lowpass.type='lowpass';lowpass.frequency.value=2100;
    const highpass=audio.createBiquadFilter();highpass.type='highpass';highpass.frequency.value=350;
    rainGain=audio.createGain();rainGain.gain.value=.018;
    rainLoop.connect(lowpass).connect(highpass).connect(rainGain).connect(audio.destination);rainLoop.start();
   }else rainGain.gain.setTargetAtTime(.018,audio.currentTime,.18);
  }else if(rainGain){rainGain.gain.setTargetAtTime(0,audio.currentTime,.09)}
 }catch{state.rainAudio=false}
 save();showJournal();notify(state.rainAudio?'🌧 Атмосферный дождь включён':'🔇 Дождь выключен')
}
function notify(msg){const el=$('toast');el.textContent=msg;el.classList.add('show');clearTimeout(toastTimer);toastTimer=setTimeout(()=>el.classList.remove('show'),2300);tone(570)}
function closeModal(){if(modal.open)modal.close()}
function openModal(html){$('sheet').innerHTML='<div class="sheet-handle"></div>'+html;if(!modal.open)modal.showModal();$('sheet').querySelectorAll('[data-close]').forEach(b=>b.addEventListener('click',()=>state.pendingEvent?notify('Сначала выбери действие'):closeModal()))}
function heading(icon,title,sub){return `<div class="sheet-title-row"><div class="sheet-icon">${icon}</div><div><div class="sheet-title">${title}</div><p class="sheet-sub">${sub}</p></div><button class="sheet-close" data-close aria-label="Закрыть">✕</button></div>`}
function actionButton(name,desc,action,cost='−1 👁',disabled=false,css=''){return `<button class="action ${css}" data-action="${action}" ${disabled?'disabled':''}><span><strong>${name}</strong><small>${desc}</small></span><span class="action-cost">${cost}</span></button>`}
// Small original vector portraits instead of platform-dependent emoji faces.
function residentArt(p){
 // Art-directed chiaroscuro busts. The glass hides some detail on purpose.
 const palette={
  '301':{skin:'#b4a091',shadow:'#594c4a',hair:'#151b20',coat:'#353f45',collar:'#a28a75'},
  '302':{skin:'#9a8271',shadow:'#504641',hair:'#171b20',coat:'#262f38',collar:'#667880'},
  '201':{skin:'#806b60',shadow:'#403434',hair:'#10161c',coat:'#433a3f',collar:'#8b7373'},
  '202':{skin:'#ae8f7b',shadow:'#594742',hair:'#282c2d',coat:'#343f41',collar:'#6b6860'},
  '101':{skin:'#c0a795',shadow:'#73605a',hair:'#737171',coat:'#4c5050',collar:'#b5a08c'},
  '102':{skin:'#917866',shadow:'#503f3d',hair:'#20272a',coat:'#46494a',collar:'#788789'}
 };
 const c=palette[p.id];
 const hair={
  '301':'<path d="M23 39Q14 25 22 14Q27 4 42 8Q63 5 65 30L60 54Q55 45 57 27Q44 18 32 27L24 48Z" fill="H"/><path d="M28 20Q40 8 57 19" fill="none" stroke="#65717a" stroke-width="1.4" opacity=".26"/>',
  '302':'<path d="M22 31Q21 11 41 11Q56 11 59 29L54 25Q41 19 27 28Z" fill="H"/><path d="M27 51Q38 63 53 51Q51 65 39 65Q27 64 27 51Z" fill="H"/>',
  '201':'<path d="M19 38Q9 19 22 13Q34 2 51 12Q62 16 60 39L57 67L50 51V29Q37 20 28 29L24 62Z" fill="H"/><path d="M20 28Q11 48 19 65" fill="none" stroke="#4c555b" stroke-width="2" opacity=".3"/>',
  '202':'<path d="M21 31Q20 16 35 13Q51 9 60 25L54 27Q41 22 26 31Z" fill="H"/><path d="M26 51Q40 68 54 49Q51 65 41 67Q28 65 26 51Z" fill="H"/>',
  '101':'<path d="M22 33Q17 24 25 16Q31 4 47 12Q60 13 60 34L53 29Q39 20 27 30Z" fill="H"/><circle cx="50" cy="12" r="11" fill="H"/><path d="M19 41Q17 54 24 60M58 40Q62 53 57 61" fill="none" stroke="H" stroke-width="7"/>',
  '102':'<path d="M18 29Q21 16 37 12Q51 7 61 25L55 29Q36 23 24 33Z" fill="H"/><path d="M24 17h40l-4 10H18Z" fill="H"/><path d="M26 49Q38 61 53 48Q54 60 40 65Q27 62 26 49Z" fill="H"/>'
 }[p.id].replaceAll('H',c.hair);
 const accessory={
  '301':'<path d="M30 78L40 68L51 79" stroke="#827d77" stroke-width="1.4" fill="none"/>',
  '302':'<rect x="17" y="37" width="7" height="20" rx="3" fill="#26333b"/><rect x="56" y="37" width="7" height="20" rx="3" fill="#26333b"/><path d="M21 38Q18 10 40 12Q61 11 60 38" fill="none" stroke="#66737b" stroke-width="3" opacity=".7"/>',
  '201':'<path d="M25 75L39 84L52 75" stroke="#8e696b" stroke-width="2" fill="none"/><circle cx="21" cy="51" r="2" fill="#ac8f76"/>',
  '202':'<path d="M23 73L39 81L56 73M39 69V91" stroke="#8c8175" stroke-width="2" fill="none"/>',
  '101':'<path d="M20 42h15m9 0h15" stroke="#b8b5a1" stroke-width="1.3"/><ellipse cx="30" cy="43" rx="9" ry="6" fill="none" stroke="#9e9c91" stroke-width="1.5"/><ellipse cx="50" cy="43" rx="9" ry="6" fill="none" stroke="#9e9c91" stroke-width="1.5"/>',
  '102':'<path d="M22 68L38 83L56 68" fill="none" stroke="#5b6360" stroke-width="4"/><path d="M42 81h5" stroke="#b6a58c" stroke-width="1.3"/>'
 }[p.id];
 return `<svg class="resident-svg" viewBox="0 0 80 96" role="img" aria-label="Силуэт ${p.name}">
 <path d="M2 96Q3 71 22 69L31 62H49L61 69Q77 73 78 96Z" fill="${c.coat}"/>
 <path d="M18 78L30 63L41 76L51 63L64 78L54 96H26Z" fill="${c.collar}" opacity=".55"/>
 <path d="M31 57L29 69Q39 79 50 68L48 56Z" fill="${c.shadow}"/>
 <ellipse cx="40" cy="39" rx="20" ry="25" fill="${c.skin}"/>
 <ellipse cx="59" cy="40" rx="3.4" ry="5" fill="${c.shadow}"/><ellipse cx="21" cy="41" rx="3.4" ry="5" fill="${c.skin}"/>
 <path d="M39 15Q48 17 58 26L57 50Q53 61 40 64Z" fill="${c.shadow}" opacity=".76"/>
 ${hair}
 <path d="M28 38Q32 36 36 38M45 38Q48 36 53 39" stroke="#3d3231" fill="none" stroke-width="1.7" opacity=".85"/>
 <path d="M29 43h6m11 0h6" stroke="#1d2527" stroke-width="1.5" stroke-linecap="round"/>
 <path d="M40 44L37 53L42 54" stroke="#705e58" stroke-width="1" fill="none" opacity=".7"/>
 <path d="M34 58Q39 59 44 57" stroke="#674e49" stroke-width="1.4" fill="none" opacity=".8"/>
 <path d="M26 51Q24 44 27 32" fill="none" stroke="#f7d6b3" stroke-width="1.2" opacity=".18"/>
 ${accessory}
 <path d="M0 96H80" stroke="#0b1318" stroke-width="2"/>
 </svg>`;
}
function caseArt(index){
 const lines=[
  '<path d="M14 9L7 23h11l-3 14 16-20H20l3-8Z"/>',
  '<path d="M8 16l12-6 12 6v17l-12 6-12-6Z"/><path d="M8 16l12 7 12-7M20 23v16"/>',
  '<path d="M21 11v28M13 16q-7 9 0 18M29 16q7 9 0 18M8 10q-12 15 0 30M34 10q12 15 0 30"/>',
  '<rect x="8" y="12" width="27" height="24" rx="1"/><path d="M10 15l11 11 12-11M14 30h15"/>',
  '<path d="M11 38V10h21v28Z"/><circle cx="26" cy="25" r="1.5"/><path d="M8 39h28"/>'
 ][index];
 return `<svg viewBox="0 0 44 48" width="26" height="28" aria-hidden="true" fill="none" stroke="#d4b18b" stroke-width="1.5" stroke-linejoin="round">${lines}</svg>`;
}

function render(){evaluateAchievements();const c=current();$('time').innerHTML=`День ${Math.min(3,state.day)} <small>${String(state.hour).padStart(2,'0')}:00</small>`;
 $('focus').innerHTML=`${state.focus} <small>/ 4</small>`;$('cluecount').innerHTML=`${state.clues.length} <small>/ 6</small>`;
 $('progress-n').textContent=`${Math.min(state.clues.length,3)} из 3`;$('clueprogress').style.width=`${clamp(state.clues.length/3*100)}%`;
 $('tensionlabel').textContent=`${state.tension}%`;$('tensionbar').style.width=`${state.tension}%`;
 $('journal-n').textContent=state.clues.length;$('wait').disabled=state.closed;
 $('theories').classList.toggle('ready',state.clues.length>=3&&!state.closed);
 $('mission').textContent=state.closed?'Дело закрыто. Посмотри результаты и перейди к следующему.':c.hook;
 $('case-name').textContent=`Дело №0${state.chapter+1}: ${c.title}`;$('case-icon').innerHTML=caseArt(state.chapter);
 $('campaign').textContent=`АРХИВ / ${String(state.chapter+1).padStart(2,'0')}—0${cases.length} · ${state.score} ОЧКОВ`;
 document.body.dataset.chapter=String(state.chapter);
 document.body.dataset.night=(state.day>=2||state.tension>=64)?'storm':'quiet';
 $('stage-time').textContent=`${String(state.hour).padStart(2,'0')}:00 · ${state.day>=2?'сильный дождь':'мелкий дождь'}`;
 $('chapter-dots').innerHTML=cases.map((v,i)=>`<span class="chapter-dot ${i<state.chapter?'done':i===state.chapter?'current':''}" title="Дело ${i+1}: ${v.title}" aria-label="${i<state.chapter?'Пройдено':i===state.chapter?'Сейчас':'Ожидает'}: ${v.title}"></span>`).join('');
 $('lore').textContent=state.chapter<2?'Кажется, дом реагирует на одно и то же время — 22:13.':'Разрозненные странности складываются в дело о бывшем смотрителе и комнате №0.';
 const house=$('house');house.replaceChildren();
 for(let floor=3;floor>=1;floor--){const block=document.createElement('div');block.className='floor';const n=document.createElement('div');n.className='floor-number';n.textContent=`${floor} ЭТАЖ`;block.append(n);const rooms=document.createElement('div');rooms.className='rooms';
  for(const p of residents.filter(r=>r.id.startsWith(String(floor)))){const here=home(p),found=getClues().some(k=>k.resident===p.id&&state.clues.includes(k.id));const tr=state.trust[p.id]||0;const btn=document.createElement('button');btn.type='button';btn.dataset.unit=p.id;btn.className=`room ${here?'home':'out'} ${here&&state.hour===22?'window-flash':''}`;
   btn.setAttribute('aria-label',`${p.name}, квартира ${p.id}, ${here?'дома':'нет дома'}`);
   btn.innerHTML=`<span class="room-badge ${found?'discovered':''}">${found?'★ УЛИКА':p.id}</span><span class="window-view"><span class="curtain left"></span><span class="curtain right"></span><span class="window-cross"></span><span class="window-lights">•••</span><span class="room-furniture"><span class="frame"></span><span class="shelf"></span><span class="plant"></span><span class="lamp"></span></span>${here?`<span class="avatar">${residentArt(p)}</span>`:'<span class="room-empty" aria-hidden="true"></span>'}</span><span class="room-info"><b>${p.name}</b><em>${here?(state.hour===23?'● не спит':'● дома'):'○ нет дома'}</em></span>`;
   btn.addEventListener('click',()=>state.closed?showEnding():showPerson(p.id));rooms.append(btn)}block.append(rooms);house.append(block)}
 const recent=state.log[0]||{icon:'📻',text:'Тишина.'};$('feed-icon').textContent='◈';$('feed-text').textContent=recent.text;
 $('foot').textContent=`КАМПАНИЯ · ${state.history.length} ДЕЛ · ${state.achievements.length} ДОСТИЖЕНИЙ · СОХРАНЕНО`;save()}
function showPerson(id){if(state.pendingEvent){showEvent();return}const p=residents.find(r=>r.id===id);if(!p)return;const here=home(p),can=state.focus>0&&!state.closed;const helped=!!state.helped[`${state.chapter}-${state.day}-${id}`],tr=state.trust[id]||0;
 const observeTitle=here?'👀 Наблюдать':'🚪 Осмотреть дверь';
 const observeDesc=here?'Присмотреться к окну и коридору':'Изучить дверь, коврик и следы рядом';
 const listenDesc=here?'Разобрать звуки из квартиры':'Послушать, что слышно за дверью и в подъезде';
 const talkDesc=here?'Задать вопрос жильцу':'В квартире пусто — поговорить не с кем';
 const helpDesc=helped?'Сегодня уже помогал':here?'Повысить доверие и снизить напряжение':'Помочь получится только когда жилец вернётся';
 openModal(`${heading(residentArt(p),p.name,`Кв. ${p.id} · ${p.role}`)}<p class="sheet-copy">${p.habit}</p><div class="pill">${here?'● ПРИСУТСТВУЕТ':'○ КВАРТИРА ПУСТА'}</div><div class="pill">${p.trait}</div><div class="pill">💛 Доверие: ${tr>0?'+':''}${tr}</div><div class="actions" style="margin-top:12px">${actionButton(observeTitle,observeDesc,'observe','−1 👁',!can)}${actionButton('👂 Прислушаться',listenDesc,'listen','−1 👁',!can)}${actionButton('🚪 Поговорить',talkDesc,'knock','−1 👁',!can||!here)}${actionButton('🤝 Помочь',helpDesc,'help','−1 👁',!can||!here||helped)}</div><p class="sheet-copy muted">${!here?'Если жильца нет дома, можно искать следы и слушать подъезд, но не разговаривать с пустой квартирой.':'Помощь укрепляет доверие на всю кампанию.'}</p>`);
 $('sheet').querySelectorAll('[data-action]').forEach(b=>b.addEventListener('click',()=>act(id,b.dataset.action)))}
function tick(){state.hour++;
 if(state.hour===21&&state.day===1&&!state.eventsSeen[`${state.chapter}-special`]){state.pendingEvent='special';state.eventsSeen[`${state.chapter}-special`]=true}
 if(state.hour===22&&state.day===1&&!state.eventsSeen[`${state.chapter}-power`]){state.pendingEvent='power';state.eventsSeen[`${state.chapter}-power`]=true}
 if(state.hour>=24){state.hour=19;state.day++;state.focus=clamp(state.focus+2,0,4);
  if(state.day===2){state.tension=clamp(state.tension+5);addLog('🌧 Начался дождь. Соседи тревожатся из-за нового происшествия.','🌧');if(!state.eventsSeen[`${state.chapter}-meeting`]){state.pendingEvent='meeting';state.eventsSeen[`${state.chapter}-meeting`]=true}}
  if(state.day===3){state.tension=clamp(state.tension+8);addLog('🚨 Последняя ночь! Скоро придётся объявить свою версию.','🚨')}
  if(state.day>3){finish('timeout');return}}
 lifeScene();if(state.tension>=100)finish('panic')}
function act(id,type){if(state.pendingEvent){showEvent();return}if(state.closed||state.focus<1)return;const p=residents.find(r=>r.id===id);if(!p)return;const here=home(p);if((type==='knock'||type==='help')&&!here)return;const key=`${state.chapter}-${state.day}-${id}`;if(type==='help'&&state.helped[key])return;
 state.focus--;state.actions++;let icon={observe:'👀',listen:'👂',knock:'🚪',help:'🤝'}[type];
 const genericHome={
  observe:[`${p.name} задержался у окна и явно чего-то ждёт во дворе.`,`Ты замечаешь в комнате ${p.name} странную мелочь, которая может пригодиться в деле.`],
  listen:[`Из квартиры доносится приглушённый разговор и шорох шагов.`,`За стеной слышно, как ${p.name} реагирует на происходящее в доме.`],
  knock:[`${p.name} внимательно выслушивает тебя и нехотя делится тем, что заметил.`,`Разговор проходит напряжённо, но ты всё же вытягиваешь важные детали.`],
  help:[`Ты помог жильцу ${p.name} с мелкой проблемой. Доверие заметно выросло.`]
 };
 const genericAway={
  observe:[`Квартира ${id} пуста: у двери только потёртый коврик и свежие следы в коридоре.`,`В окне темно. Ты осматриваешь дверь и замечаешь, что жильца давно нет дома.`],
  listen:[`За дверью тихо, но в подъезде слышен далёкий шорох и эхо лифта.`,`Из квартиры не доносится ни звука — зато коридор иногда выдаёт важные мелочи.`]
 };
 let result=(here?genericHome[type]:genericAway[type])[ (state.actions+Number(id)) % (here?genericHome[type]:genericAway[type]).length ];
 if(type==='help'){state.helped[key]=true;state.kindness++;state.trust[id]=clamp((state.trust[id]||0)+2,-2,4);state.tension=clamp(state.tension-11);
 if(!state.storiesSeen[id]&&state.trust[id]>=2){state.storiesSeen[id]=true;addLog(`${p.name} доверяет тебе. ${stories[id]}`,'💬')}
 }
 if(type==='knock'){state.trust[id]=clamp((state.trust[id]||0)+1,-2,4);state.tension=clamp(state.tension-3)}
 if(type==='listen')state.tension=clamp(state.tension+3);
 if(type==='observe')state.tension=clamp(state.tension+1);
 const match=here?getClues().find(k=>k.resident===id&&k.action===type&&!state.clues.includes(k.id)):null;
 if(match){state.clues.push(match.id);result=`Улика «${match.title}»: ${match.body}`;icon='🔎'}
 addLog(result,icon);tick();closeModal();render();if(state.closed){showEnding();return}if(state.pendingEvent){showEvent();return}notify(match?`🔎 Улика: ${match.title}`:type==='help'?'💛 Доверие выросло':`+1 час · ${p.name}`)}
function waitHour(){if(state.pendingEvent){showEvent();return}if(state.closed){showEnding();return}state.focus=clamp(state.focus+2,0,4);addLog('Ты отдыхаешь, сортируешь записи и восстанавливаешь внимание.','☕');tick();render();if(state.closed){showEnding();return}if(state.pendingEvent){showEvent();return}notify('☕ +2 внимания · +1 час')}
function showEvent(){const e=state.pendingEvent;if(!e||state.closed)return;
 if(e==='special'){
  const data=specialEvents[state.chapter];
  openModal(`${heading(data[0].split(' ')[0],data[0].slice(3),'Событие текущего расследования')}<p class="sheet-copy">${data[1]}</p><div class="actions">${actionButton(data[2][0],data[2][1],'protect','💛')}${actionButton(data[3][0],data[3][1],'expose','⚠️')}</div>`);
 } else if(e==='power')openModal(`${heading('⚡','Сбой 22:13','Жильцы замерли на лестнице')}<p class="sheet-copy">Сначала исчезает жужжание холодильников. Затем на долю секунды темнеют все окна, а за стеной щёлкает металл. Что предпримешь, пока свет не вернулся?</p><div class="actions">${actionButton('🧯 Успокоить соседей','Снизить панику, потратить часть сил','calm','−1 👁')}${actionButton('🔍 Осмотреть лестницу','Получить подсказку, но усилить тревогу','inspect','+5 ⚠️')}</div>`);
 else openModal(`${heading('🗣','Собрание у лифта','Соседи требуют ответа')}<p class="sheet-copy">Жильцы спорят, кто виноват. Ты можешь наладить разговор или отстраниться и заняться своим делом.</p><div class="actions">${actionButton('🤝 Организовать разговор','Снять напряжение и заслужить доверие','talk','−1 👁')}${actionButton('📋 Проверить записи','Не терять время, получить напоминание','notes','+3 ⚠️')}</div>`);
 $('sheet').querySelectorAll('[data-action]').forEach(b=>b.addEventListener('click',()=>chooseEvent(b.dataset.action)))}
function chooseEvent(ch){if(!state.pendingEvent)return;const e=state.pendingEvent;state.pendingEvent=null;
 if(e==='special'){
  state.choices.push({chapter:state.chapter,action:ch});
  if(ch==='protect'){state.tension=clamp(state.tension-10);state.kindness++;const p=residents[state.chapter%residents.length];state.trust[p.id]=clamp((state.trust[p.id]||0)+1,-2,4);addLog('Ты помог соседям договориться. В доме стало немного спокойнее.','💛')}
  else{state.tension=clamp(state.tension+12);addLog('История разлетелась по домовому чату, но теперь все нервничают.','📢')}
 }
 if(ch==='calm'){state.focus=clamp(state.focus-1,0,4);state.tension=clamp(state.tension-14);addLog('Ты вывел соседей из темноты и успокоил их.','🤝')}
 if(ch==='inspect'){state.tension=clamp(state.tension+5);addLog(`Подсказка: присмотрись к уликам в квартирах ${current().clues[0][1]} и ${current().clues[3][1]}.`,'🔍')}
 if(ch==='talk'){state.focus=clamp(state.focus-1,0,4);state.tension=clamp(state.tension-12);state.kindness++;addLog('Соседи наконец выслушали друг друга.','💛')}
 if(ch==='notes'){state.tension=clamp(state.tension+3);addLog(`Подсказка: поговори с ${residents.find(r=>r.id===current().clues[3][1]).name} и проверь записи соседей.`,'📋')}
 closeModal();render();notify('Решение повлияло на дом')}
function askHint(){if(state.pendingEvent){showEvent();return}if(state.closed||state.focus<=0){notify('Нужно внимание: отдохни один час');return}
 if(state.hints>=2){notify('В этом деле уже получены две подсказки');return}
 const pending=getClues().filter(k=>!state.clues.includes(k.id));
 if(!pending.length){notify('Все улики уже найдены');return}
 const candidate=pending.find(k=>home(residents.find(r=>r.id===k.resident))&&k.action==='knock')||pending.find(k=>home(residents.find(r=>r.id===k.resident)))||pending[0];
 const p=residents.find(r=>r.id===candidate.resident);
 state.hints++;state.focus--;state.tension=clamp(state.tension+4);state.actions++;
 addLog(`Запись в досье: ${p.name} (кв. ${p.id}) может помочь. ${!home(p)?'Пока жильца нет, дождись возвращения. ':''}Попробуй: ${candidate.action==='knock'?'поговорить':candidate.action==='observe'?'наблюдать': 'прислушаться к звукам'}.`,'◈');
 tick();closeModal();render();
 if(state.closed){showEnding();return}if(state.pendingEvent){showEvent();return}notify('💡 Новая подсказка в журнале')
}
function showJournal(){if(state.pendingEvent){showEvent();return}const found=getClues().filter(k=>state.clues.includes(k.id));const body=found.length?found.map((c,i)=>`<div class="clue"><b>${i+1}. ${c.title}</b><p>${c.body}</p></div>`).join(''):'<div class="empty">Пока нет улик. Исследуй окна, слушай звуки и разговаривай с жильцами.</div>';
 const past=state.history.map((h,i)=>`<div class="clue"><b>${h.win?'✅':'❌'} Дело ${i+1}: ${cases[i].title}</b><p>${h.win?'Разгадано':'Не разгадано'} · ${h.earned} очков · улик ${h.clues}/6</p></div>`).join('');
 const events=state.log.slice(0,8).map(l=>`<p class="sheet-copy" style="margin:6px 0">${l.icon} ${l.text}</p>`).join('');
 openModal(`${heading('📓','Журнал детектива',`Дело ${state.chapter+1}/${cases.length} · ${state.score} очков`)}<p class="sheet-copy">Собери минимум 3 улики, затем выбери версию. Доверие жильцов сохраняется между делами.</p>${body}${past?'<h3 class="part-title">ПРОШЛЫЕ ДЕЛА</h3>'+past:''}<h3 class="part-title">ПОСЛЕДНИЕ СОБЫТИЯ</h3>${events}<h3 class="part-title">ДОСТИЖЕНИЯ · ${state.achievements.length}/${achievementsCatalog.length}</h3><div class="achievement-grid">${achievementsCatalog.map(a=>`<div class="achievement ${state.achievements.includes(a[0])?'unlocked':''}"><b>${state.achievements.includes(a[0])?a[1]:'🔒 '+a[1].slice(2)}</b><small>${a[2]}</small></div>`).join('')}</div><div class="actions" style="margin-top:17px">${actionButton('💡 Попросить подсказку',`Осталось ${Math.max(0,2-state.hints)} за дело · +4% напряжения`,'hint','−1 👁',state.hints>=2||state.closed)}${actionButton(state.sound?'🔊 Звук включён':'🔇 Включить звук','Короткие сигналы при действиях','sound','⚙️')}${actionButton(state.rainAudio?'🌧 Выключить дождь':'🌧 Включить атмосферу','Тихий фоновый шум дождя · работает только после нажатия','rain','🎧')}${actionButton('↻ Начать заново','Сбросить всю кампанию и улики','reset','СБРОС',false, 'danger')}</div>`);
 $('sheet').querySelector('[data-action="hint"]').addEventListener('click',askHint);
 $('sheet').querySelector('[data-action="rain"]').addEventListener('click',toggleRainSound);
 $('sheet').querySelector('[data-action="sound"]').addEventListener('click',()=>{state.sound=!state.sound;render();showJournal();notify(state.sound?'🔊 Звук включён':'🔇 Звук выключен')});
 $('sheet').querySelector('[data-action="reset"]').addEventListener('click',()=>{if(confirm('Начать всю кампанию заново? Прогресс пяти дел будет потерян.')){state=makeState();state.seenTutorial=true;closeModal();render();notify('Новая кампания началась')}})}
function cleanTheoryLabel(label){return label.includes(' ')?label.slice(label.indexOf(' ')+1):label}
const keyEvidence=[['line13','clickloop','log13'],['brasskey','notebox','framegrab'],['archivevoice','fusevoice','loopphrase'],['whisperwarn','samehand','stamp1998'],['evacplan','backupbus','keyzero']];
function evidenceCards(clues,interactive=false){return `<div class="evidence-board">${clues.map((k,i)=>`<${interactive?'button':'div'} class="evidence-card" ${interactive?`data-evidence="${k.id}" type="button"`:''}><small>УЛИКА / ${String(i+1).padStart(2,'0')}</small><b>${k.title}</b></${interactive?'button':'div'}>`).join('')}</div>`}
function showTheories(){if(state.pendingEvent){showEvent();return}if(state.closed){showEnding();return}
 const c=current(),found=getClues().filter(k=>state.clues.includes(k.id)),enough=found.length>=3;
 openModal(`${heading('◈','Доска улик',`ДЕЛО ${String(state.chapter+1).padStart(2,'0')} / ${c.title}`)}<span class="theory-tag">СВИДЕТЕЛЬСТВА ${found.length} / 6</span><p class="sheet-copy">${enough?'Свидетельств уже достаточно. Составь версию — затем выбери улику, которая подтверждает её.':'Нужны как минимум три независимых свидетельства, прежде чем делать окончательное заключение.'}</p>${found.length?evidenceCards(found):'<div class="empty">Архив пуст. Начни с наблюдения за окнами и разговора с жильцами.</div>'}<div class="clue"><b>Рабочая гипотеза</b><p>${found.length>=4?c.deduction:'Пока каждый факт существует отдельно. Сверь время, место и показания — соседи могут ошибаться.'}</p></div><div class="actions">${c.guesses.map((g,i)=>actionButton(`${String(i+1).padStart(2,'0')} / ${cleanTheoryLabel(g[1])}`,g[2],g[0],'ВЫБРАТЬ',!enough)).join('')}</div><p class="choice-footnote">После выбора версии понадобится подтверждающее свидетельство. Ошибка не остановит историю, но повлияет на оценку расследования.</p>`);
 $('sheet').querySelectorAll('[data-action]').forEach(b=>b.addEventListener('click',()=>showEvidenceChoice(b.dataset.action)))
}
function showEvidenceChoice(guess){const c=current(),found=getClues().filter(k=>state.clues.includes(k.id));if(found.length<3||!c.guesses.some(g=>g[0]===guess))return;
 const g=c.guesses.find(v=>v[0]===guess);
 openModal(`${heading('⌁','Подкрепи версию',`${c.title} · ЗАКЛЮЧЕНИЕ`)}<span class="theory-tag">ВЫБРАНА ГИПОТЕЗА</span><div class="clue"><b>${cleanTheoryLabel(g[1])}</b><p>${g[2]}</p></div><p class="sheet-copy">Выбери одно доказательство, которое считаешь главным. Сопоставление фактов повысит качество заключения.</p>${evidenceCards(found,true)}<div class="actions">${actionButton('← Назад к версиям','Пересмотреть собранные факты','back','НАЗАД')}</div>`);
 $('sheet').querySelectorAll('[data-evidence]').forEach(b=>b.addEventListener('click',()=>showEvidenceConfirmation(guess,b.dataset.evidence)));
 $('sheet').querySelector('[data-action="back"]').addEventListener('click',showTheories);
}
function showEvidenceConfirmation(guess,evidence){const c=current(),g=c.guesses.find(v=>v[0]===guess),k=getClues().find(v=>v.id===evidence);
 if(!g||!k||!state.clues.includes(k.id))return;
 openModal(`${heading('⌁','Закрыть дело?',`ДЕЛО ${state.chapter+1} · ОКОНЧАТЕЛЬНАЯ ВЕРСИЯ`)}<span class="theory-tag">ВЕРСИЯ</span><div class="clue"><b>${cleanTheoryLabel(g[1])}</b><p>${g[2]}</p></div><span class="theory-tag">ГЛАВНЫЙ АРГУМЕНТ</span>${evidenceCards([k])}<p class="choice-footnote">После заключения изменить версию нельзя. Ошибочный вывод скажется на судьбах соседей и итоговой оценке.</p><div class="actions">${actionButton('Подписать заключение','Завершить дело и увидеть последствия','confirm','ПОДПИСАТЬ',false,'primary')}${actionButton('← Вернуться к уликам','Хочу перепроверить доказательства','back','НАЗАД')}</div>`);
 $('sheet').querySelector('[data-action="confirm"]').addEventListener('click',()=>{finish(guess,evidence);closeModal();showEnding()});
 $('sheet').querySelector('[data-action="back"]').addEventListener('click',()=>showEvidenceChoice(guess));
}
function finish(ending,evidenceId=null){if(state.closed)return;const c=current();state.closed=true;state.pendingEvent=null;state.ending=ending;const win=ending===c.correct,supported=win&&!!evidenceId&&keyEvidence[state.chapter].includes(evidenceId),brilliant=win&&supported&&state.clues.length>=5&&state.tension<=55&&state.kindness>=1;
 const earned=Math.max(0,state.clues.length*70+state.kindness*25+Math.max(0,100-state.tension)*2+(win?270:0)+(supported?90:0)+(brilliant?230:0)-(ending==='timeout'?150:0));state.score+=earned;
 state.history.push({chapter:state.chapter,guess:ending,win,supported,evidenceId,brilliant,earned,clues:state.clues.length,tension:state.tension});
 addLog(win?'Дело раскрыто. Жильцы благодарят тебя за помощь.':'Дело завершено, но все ли тайны удалось раскрыть?',win?'🏆':'📜');render()}
function showEnding(){if(!state.closed)return;const h=state.history[state.history.length-1],c=current(),last=state.chapter===cases.length-1;
 const title=h.brilliant?'Блестящее расследование!':h.win?'Дело раскрыто!':h.guess==='timeout'?'Время вышло':h.guess==='panic'?'Дом взбунтовался':'Ложная версия';
 const endingText=h.win?`${c.reveal} Ты успел разобраться и сохранить доверие соседей.`:`Твоя версия не выдержала проверки. В доме остались нестыковки и противоречивые свидетельства. Придётся двигаться дальше, так и не узнав всю правду об этом деле.`;
 const conclusion=last?`<div class="clue"><b>КАМПАНИЯ ПРОЙДЕНА · ${state.score} ОЧКОВ</b><p>Разгадано дел: ${state.history.filter(v=>v.win).length}/${cases.length} · Добрых дел: ${state.kindness}<br>${state.history.every(v=>v.win)?'🏅 Ты раскрыл главную тайну дома №13!':'История дома не завершилась идеально — попробуй другие решения.'}<br>${state.choices.filter(v=>v.action==='protect').length>=3?'Соседи доверяют тебе: дом снова стал одной семьёй.':'Секреты раскрыты, но соседям ещё предстоит научиться доверять друг другу.'}</p></div>`:'';
 openModal(`${heading(h.win?'🏆':'📜',title,`Дело ${state.chapter+1}/${cases.length} · +${h.earned} очков`)}<div class="final-score">${h.brilliant?'🌟':h.win?'🎉':'🌧'}</div><p class="sheet-copy">${endingText}</p><div class="clue"><b>Результат дела</b><p>Улики: ${h.clues}/6 · ${h.supported?'Доказательство подтверждено':'Аргумент не подтверждён'} · Добрые дела: ${state.kindness}<br>Напряжение: ${h.tension}% · Начислено: ${h.earned}</p></div>${conclusion}<div class="actions">${last?actionButton('🔄 Новая кампания','Начать все пять дел сначала','again','ИГРАТЬ',false,'primary'):actionButton('➡️ Следующее дело','Продолжить историю жильцов','next','ПРОДОЛЖИТЬ',false,'primary')}</div>`);
 $('sheet').querySelectorAll('[data-action]').forEach(b=>b.addEventListener('click',()=>{if(b.dataset.action==='again'){state=makeState();state.seenTutorial=true}else{state.chapter++;state.day=1;state.hour=19;state.focus=4;state.clues=[];state.helped={};state.hints=0;state.closed=false;state.ending=null;state.pendingEvent=null;state.tension=clamp(Math.round(state.tension*.45)+12,12,55);addLog(`Дело №0${state.chapter+1}: ${current().title}. Начинается новое расследование.`,current().icon)}closeModal();render();notify('Открыто новое дело') }));}
function showHelp(){if(state.pendingEvent){showEvent();return}state.seenTutorial=true;save();openModal(`${heading('🏢','Добро пожаловать','Дом №13 · Пять ночных дел')}<p class="sheet-copy">Ты — ночной смотритель странного дома. В нём повторяется время 22:13, исчезает особая посылка, с чердака говорит металлический голос, а прошлое самого дома возвращается через письма и скрытую комнату №0.</p><div class="clue"><b>Как играть</b><p>1. Нажимай на квартиры и выбирай действия.<br>2. Собирай хотя бы 3 из 6 улик для каждого дела.<br>3. Если жильца нет дома, можно проверить пустую квартиру, но показания он даст после возвращения. Каждое действие стоит 1 внимание и 1 час.<br>4. Кнопка «След. час» восстанавливает 2 внимания.<br>5. Помогай жильцам и принимай решения в событиях.<br>6. На доске улик выбери версию и подтверждающее доказательство до конца третьей ночи. Решение повлияет на общий счёт. Все пять дел связаны между собой и ведут к одной большой тайне.</p></div><p class="sheet-copy">Прогресс сохраняется на этом телефоне. Помогай жильцам, находи личные истории и открывай достижения. В журнале доступны подсказки и настройки звука.</p><div class="actions">${actionButton('Начать игру','Открыть дом','start','ВПЕРЁД',false,'primary')}</div>`);$('sheet').querySelector('[data-action="start"]').addEventListener('click',closeModal)}
$('help').addEventListener('click',showHelp);$('journal').addEventListener('click',showJournal);$('theories').addEventListener('click',showTheories);$('wait').addEventListener('click',waitHour);
modal.addEventListener('cancel',e=>{if(state.pendingEvent){e.preventDefault();notify('Сначала выбери действие')}});
modal.addEventListener('click',e=>{if(e.target===modal){if(state.pendingEvent){notify('Сначала выбери решение')}else closeModal()}});
render();if(state.closed)showEnding();else if(state.pendingEvent)showEvent();else if(!state.seenTutorial)showHelp();
window.__dom13={getState:()=>JSON.parse(JSON.stringify(state)),reset:()=>{state=makeState();state.seenTutorial=true;closeModal();render()},act,waitHour,showTheories,showEnding,chooseEvent,showJournal,askHint};
})();
