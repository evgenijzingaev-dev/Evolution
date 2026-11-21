(() => {
'use strict';
const STORE='dom13-nightshift-v2';
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
 {icon:'⚡',title:'Кто гасит свет?',hook:'Третью ночь подряд в доме пропадает электричество. Что происходит в подвале?',correct:'leak',reveal:'Протекала старая труба прямо над электрощитом. Виктор пытался устранить проблему один, но требовалась помощь всего дома.',
  guesses:[['stolen','🔌 Кто-то ворует свет','Тайное подключение к электричеству'],['leak','💧 Протечка в щитке','Вода замыкает старую проводку'],['ghost','👻 Дом проклят','Всё объясняется сверхъестественным']],
  clues:[
   ['receipt','101','knock','Квитанция сантехника','Тамара показывает оплаченный вызов: «протечка в подвале, нужен повторный осмотр».'],
   ['tracks','102','observe','Мокрые следы','К подвальной двери тянутся мокрые отпечатки, хотя дождя на улице ещё нет.'],
   ['music','201','listen','Щелчок под музыку','Перед отключением Майя слышала звонкий щелчок из электрощитка.'],
   ['scheme','202','knock','Старая схема','Электрический щит находится прямо под водопроводной трубой.'],
   ['video','301','knock','Видео с искрами','Кира сняла искры у подвала. Рядом никого не было.'],
   ['water','302','listen','Бульканье в записи','Микрофон Льва записал журчание воды за закрытой подвальной дверью.']
  ],deduction:'Вода, схема электрощита и момент отключения связаны одним местом — подвалом.'},
 {icon:'📦',title:'Пропавшие посылки',hook:'По утрам исчезают посылки из коридора. Соседи подозревают друг друга.',correct:'storage',reveal:'После протечки коробки переносили в сухую подсобку рядом с лифтом. Сообщение в домовом чате затерялось, поэтому все подумали о краже.',
  guesses:[['courier','🛵 Виноват курьер','Кто-то присваивает заказы при доставке'],['storage','🗝 Посылки в подсобке','Кто-то временно убрал коробки от воды'],['cat','🐈 Кот разносит пакеты','Коробки исчезают из-за животных']],
  clues:[
   ['log','101','knock','Запись в журнале','Тамара получила записку: «Не оставлять коробки в сыром коридоре».'],
   ['tape','102','knock','Лента доставки','Олег узнаёт фирменный скотч на тележке у лифта. Все посылки были доставлены.'],
   ['wheels','201','listen','Колёса в полночь','Майя слышала, как ночью по коридору осторожно катили тележку.'],
   ['key','202','knock','Ключ от подсобки','Виктор признаётся, что дал ключ старшей по дому после затопления.'],
   ['clip','301','observe','Отражение на видео','На записи Киры видно, как коробки заносят к служебной двери.'],
   ['stream','302','listen','Запись стрима','В эфир попала фраза: «Главное — уберечь их от сырости».']
  ],deduction:'Тележка, ключ и слова о сырости указывают на временное хранилище, а не кражу.'},
 {icon:'📻',title:'Голос на чердаке',hook:'Ровно в полночь чердак передаёт странный радиосигнал. Жильцы боятся, что за ними следят.',correct:'beacon',reveal:'На чердаке сохранился старый радиомаяк аварийной связи. После перебоев питания его резервный блок начал автоматически включать тестовую передачу.',
  guesses:[['spies','🕵️ За жильцами следят','В доме установили тайный передатчик'],['beacon','📡 Старый радиомаяк','На чердаке ожил забытый прибор'],['ghost','👁 Голоса призраков','Сигнал приходит из потустороннего мира']],
  clues:[
   ['archive','101','knock','Домовой архив','В старой папке Тамары есть акт установки аварийной радиосвязи.'],
   ['antenna','102','observe','Антенна на крыше','Олег замечает под коньком крыши тонкий металлический прут.'],
   ['melody','201','listen','Повторяющийся ритм','Майя слышит, что сигнал повторяется ровно раз в час.'],
   ['wire','202','knock','Кабель к чердаку','Виктор указывает на старый кабель от резервного аккумулятора.'],
   ['photo','301','observe','Снимок из окна','На фото Киры виден маленький мигающий индикатор на чердаке.'],
   ['signal','302','listen','Метка эфира','Лев расшифровывает запись: «Тест системы. Канал тринадцать».']
  ],deduction:'Записи, антенный кабель и повторяющийся сигнал похожи на работу старого технического устройства.'},
 {icon:'✉️',title:'Письма без отправителя',hook:'Под дверями появляются конверты с предупреждением: «Вспомните тринадцатую ночь». Кто их оставил?',correct:'archive',reveal:'Тамара нашла потерянные письма бывшего смотрителя. Он предупреждал о старой системе аварийной связи и скрытом помещении в подвале. Кто-то разнёс копии, чтобы жильцы наконец обратили внимание.',
  guesses:[['threat','🕵️ Чужие угрозы','Неизвестный шантажирует жильцов'],['archive','📚 Старый архив','Письма оставлены теми, кто хочет предупредить соседей'],['prank','🎭 Чья-то шутка','Это розыгрыш, устроенный ради просмотров']],
  clues:[
   ['stamp','101','knock','Дата на конверте','Тамара узнаёт штемпель 1998 года, когда в доме работал первый смотритель.'],
   ['stack','102','observe','Одинаковые копии','В почтовых ящиках аккуратно разложены ксерокопии, а не новые письма.'],
   ['phrase','201','listen','Знакомый голос','Майя слышит, как кто-то повторяет: «Людей надо предупредить, а не напугать».'],
   ['paper','202','knock','Ветхий чертёж','Виктор показывает схему аварийного выхода под лестницей с тем же почерком, что на письмах.'],
   ['camera','301','observe','Кадр с папкой','На видео Киры в подъезд входит Тамара с архивной папкой, а не незнакомец.'],
   ['scan','302','listen','Старая запись','В старом стриме Льва сохранились слова о папке первого смотрителя, найденной при уборке.']
  ],
  deduction:'Штемпель, старые чертежи и копии говорят о прошлом дома — но кто-то хотел, чтобы об этом узнали сейчас.'},
 {icon:'🚪',title:'Квартира, которой нет',hook:'На схеме подвала обнаружена дверь с номером 0. Ночью за ней загорается свет. Открой последнюю тайну дома.',correct:'shelter',reveal:'За дверью №0 находится законсервированное аварийное убежище, построенное первым смотрителем. Протечка повредила щит, посылки спасали от сырости, радиомаяк включился на резервном питании, а письма предупреждали об этом. Никакой мистики — лишь забытая забота о людях.',
  guesses:[['criminals','🔒 Тайный склад','Помещением пользуется неизвестная преступная группа'],['shelter','🏠 Старое убежище','За дверью находится забытая система безопасности дома'],['portal','🌀 Портал за стеной','Дверь ведёт в другое измерение']],
  clues:[
   ['plan','101','knock','План эвакуации','Тамара показывает чертёж: подвал №0 обозначен как помещение гражданской защиты.'],
   ['handle','102','observe','Ручка снаружи','Олег видит на двери знак аварийного выхода, стёртый краской.'],
   ['heartbeat','201','listen','Ритм батареи','Майя слышит характерный стук резервного генератора — такой же, как в ночь радиосигнала.'],
   ['powerbox','202','knock','Запасной щит','Виктор узнаёт проводку автономной системы питания за дверью №0.'],
   ['photo0','301','observe','Снимок стены','Кира сравнивает фотографии: шов в стене указывает на замурованный вход.'],
   ['code0','302','listen','Код допуска','На записи Льва слышна фраза: «Тринадцать — код аварийной службы».']
  ],
  deduction:'Планы, резервное питание и аварийные обозначения складываются в историю старой системы безопасности.'}

];
function makeState(){return {version:3,chapter:0,day:1,hour:19,focus:4,tension:18,kindness:0,trust:{},clues:[],actions:0,helped:{},log:[{icon:'📻',text:'Началась первая ночная смена в доме №13.'}],history:[],score:0,closed:false,ending:null,pendingEvent:null,eventsSeen:{},sound:false,rainAudio:false,seenTutorial:false,chapterStart:18,achievements:[],lifeEvents:0,choices:[],hints:0,storiesSeen:{}}}
let state;
try{
 const old=JSON.parse(localStorage.getItem(STORE));
 const compatible=old&&(old.version===2||old.version===3)&&Number.isInteger(old.chapter)&&old.chapter>=0&&old.chapter<cases.length&&Array.isArray(old.clues)&&Array.isArray(old.history);
 state=compatible?{...makeState(),...old,version:3}:makeState();
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
 ['⚡ У подвала погас свет','Виктор зовёт помочь, а Кира уже снимает видео. Что важнее сейчас?',
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
function lifeScene(){if(state.closed)return;const i=(state.chapter*7+state.day*3+state.hour)%dailyScenes.length,scene=dailyScenes[i];if(state.hour===20||state.hour===23){addLog(scene[2],scene[1]);state.lifeEvents++}}

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
 const palette={
  '301':['#edbd9b','#563349','#b77c98'],
  '302':['#c68d69','#302939','#7087aa'],
  '201':['#94694e','#302437','#b77b9e'],
  '202':['#e0ad8e','#343339','#7d98a0'],
  '101':['#e7bea4','#b1a5ac','#a393b3'],
  '102':['#bf8b6a','#44332f','#9aa99a']
 };
 const [skin,hair,shirt]=palette[p.id];
 const hairdo={
  '301':'<path d="M20 41Q13 9 39 11Q66 9 60 42L58 59Q54 53 53 42L23 43Q25 61 19 60Z" fill="H"/><path d="M23 31Q22 17 43 17Q53 18 57 27Q44 19 35 25Q27 24 23 31Z" fill="H"/>',
  '302':'<path d="M22 36Q15 15 34 13Q52 7 59 29L55 35Q47 22 34 23Q27 30 22 36Z" fill="H"/><rect x="22" y="44" width="34" height="11" rx="6" fill="none" stroke="#26354b" stroke-width="3" opacity=".8"/>',
  '201':'<path d="M21 37Q9 14 28 11Q47 4 60 25L57 63L51 49L52 32Q44 22 35 27Q27 23 24 40L23 58Z" fill="H"/><path d="M23 26Q35 16 51 24" stroke="#63404c" stroke-width="3" fill="none"/>',
  '202':'<path d="M21 32Q24 14 41 15Q58 12 59 35L53 29Q36 29 23 33Z" fill="H"/><path d="M29 51Q39 60 50 51Q49 66 39 64Q27 64 29 51Z" fill="H" opacity=".92"/>',
  '101':'<path d="M19 35Q14 22 25 17Q24 7 35 13Q45 5 54 16Q63 19 59 36L52 31Q42 22 29 29L25 34Z" fill="H"/><path d="M18 42Q11 52 22 60L26 45M58 42Q67 53 56 61L53 45" fill="H"/>',
  '102':'<path d="M22 31Q19 14 37 14Q54 11 58 30L54 31Q45 24 34 26Q27 29 22 31Z" fill="H"/><path d="M26 47Q29 62 40 63Q51 62 54 47Q49 56 39 56Q31 56 26 47Z" fill="H"/>'
 }[p.id].replaceAll('H',hair);
 const accessory=p.id==='201'?'<circle cx="18" cy="49" r="2" fill="#ffe1a8"/><circle cx="60" cy="49" r="2" fill="#ffe1a8"/>':p.id==='302'?'<path d="M20 39h16m7 0h16" stroke="#273447" stroke-width="2"/><circle cx="29" cy="40" r="9" fill="none" stroke="#273447" stroke-width="2"/><circle cx="49" cy="40" r="9" fill="none" stroke="#273447" stroke-width="2"/>':p.id==='101'?'<path d="M22 41h14m8 0h13" stroke="#b9ad9e" stroke-width="2"/><circle cx="30" cy="43" r="8" fill="none" stroke="#a9a2a3" stroke-width="2"/><circle cx="49" cy="43" r="8" fill="none" stroke="#a9a2a3" stroke-width="2"/>':'';
 const chest=p.id==='202'?'<path d="M38 70v15m-17-9h35" stroke="#d0d9dc" stroke-width="2" opacity=".5"/>':p.id==='102'?'<path d="M30 70l9 10 9-10" stroke="#d5d6b1" stroke-width="3" fill="none"/>':p.id==='301'?'<path d="M25 69l13 13 15-13" stroke="#ebd7cf" fill="none" stroke-width="3"/>':'';
 return `<svg class="resident-svg" viewBox="0 0 80 84" aria-hidden="true" focusable="false">
 <path d="M5 84Q4 66 23 63L32 60H47L56 63Q74 66 75 84Z" fill="${shirt}"/>
 <path d="M28 65L39 78L49 65" fill="#f3d6bf" opacity=".55"/>
 <rect x="32" y="52" width="15" height="15" rx="6" fill="${skin}"/>
 <ellipse cx="39" cy="41" rx="20" ry="24" fill="${skin}"/><circle cx="20" cy="43" r="5" fill="${skin}"/><circle cx="59" cy="43" r="5" fill="${skin}"/>
 ${hairdo}
 <path d="M27 39q4-2 8 0m10 0q4-2 8 0" stroke="#493644" opacity=".7" stroke-width="1.4" fill="none" stroke-linecap="round"/>
 <circle cx="32" cy="43" r="1.6" fill="#252b3b"/><circle cx="48" cy="43" r="1.6" fill="#252b3b"/>
 <path d="M36 54Q40 57 44 54" stroke="#9a5b58" stroke-width="1.7" fill="none" stroke-linecap="round"/>
 <ellipse cx="27" cy="50" rx="4" ry="2" fill="#ee8587" opacity=".15"/><ellipse cx="51" cy="50" rx="4" ry="2" fill="#ee8587" opacity=".15"/>
 ${accessory}${chest}</svg>`;
}

function render(){evaluateAchievements();const c=current();$('time').innerHTML=`День ${Math.min(3,state.day)} <small>${String(state.hour).padStart(2,'0')}:00</small>`;
 $('focus').innerHTML=`${state.focus} <small>/ 4</small>`;$('cluecount').innerHTML=`${state.clues.length} <small>/ 6</small>`;
 $('progress-n').textContent=`${Math.min(state.clues.length,3)} из 3`;$('clueprogress').style.width=`${clamp(state.clues.length/3*100)}%`;
 $('tensionlabel').textContent=`${state.tension}%`;$('tensionbar').style.width=`${state.tension}%`;
 $('journal-n').textContent=state.clues.length;$('wait').disabled=state.closed;
 $('theories').classList.toggle('ready',state.clues.length>=3&&!state.closed);
 $('mission').textContent=state.closed?'Дело закрыто. Посмотри результаты и перейди к следующему.':c.hook;
 $('case-name').textContent=`Дело №0${state.chapter+1}: ${c.title}`;$('case-icon').textContent=c.icon;
 $('campaign').textContent=`ДЕЛО ${state.chapter+1} / ${cases.length} · ${state.score} ОЧКОВ`;
 document.body.dataset.chapter=String(state.chapter);
 document.body.dataset.night=(state.day>=2||state.tension>=64)?'storm':'quiet';
 $('stage-time').textContent=`${String(state.hour).padStart(2,'0')}:00 · ${state.day>=2?'ливень':'морось'}`;
 $('chapter-dots').innerHTML=cases.map((v,i)=>`<span class="chapter-dot ${i<state.chapter?'done':i===state.chapter?'current':''}" title="Дело ${i+1}: ${v.title}" aria-label="${i<state.chapter?'Пройдено':i===state.chapter?'Сейчас':'Ожидает'}: ${v.title}"></span>`).join('');
 $('lore').textContent=state.chapter<3?'В доме ещё немало секретов.':'Старые тайны начинают складываться в единую историю.';
 const house=$('house');house.replaceChildren();
 for(let floor=3;floor>=1;floor--){const block=document.createElement('div');block.className='floor';const n=document.createElement('div');n.className='floor-number';n.textContent=`${floor} ЭТАЖ`;block.append(n);const rooms=document.createElement('div');rooms.className='rooms';
  for(const p of residents.filter(r=>r.id.startsWith(String(floor)))){const here=home(p),found=getClues().some(k=>k.resident===p.id&&state.clues.includes(k.id));const tr=state.trust[p.id]||0;const btn=document.createElement('button');btn.type='button';btn.dataset.unit=p.id;btn.className=`room ${here?'home':'out'} ${here&&state.hour===22?'window-flash':''}`;
   btn.setAttribute('aria-label',`${p.name}, квартира ${p.id}, ${here?'дома':'нет дома'}`);
   btn.innerHTML=`<span class="room-badge ${found?'discovered':''}">${found?'★ УЛИКА':p.id}</span><span class="window-view"><span class="curtain left"></span><span class="curtain right"></span><span class="window-cross"></span><span class="window-lights">•••</span><span class="room-furniture"><span class="frame"></span><span class="shelf"></span><span class="plant"></span><span class="lamp"></span></span>${here?`<span class="avatar">${residentArt(p)}</span>`:'<span class="room-empty">☾</span>'}</span><span class="room-info"><b>${p.name}</b><em>${tr>=2?'♥ доверие':here?(state.hour===23?'◉ не спит':'● дома'):'○ нет дома'}</em></span>`;
   btn.addEventListener('click',()=>state.closed?showEnding():showPerson(p.id));rooms.append(btn)}block.append(rooms);house.append(block)}
 const recent=state.log[0]||{icon:'📻',text:'Тишина.'};$('feed-icon').textContent=recent.icon;$('feed-text').textContent=recent.text;
 $('foot').textContent=`КАМПАНИЯ · ${state.history.length} ДЕЛ · ${state.achievements.length} ДОСТИЖЕНИЙ · СОХРАНЕНО`;save()}
function showPerson(id){if(state.pendingEvent){showEvent();return}const p=residents.find(r=>r.id===id);if(!p)return;const here=home(p),can=state.focus>0&&!state.closed;const helped=!!state.helped[`${state.chapter}-${state.day}-${id}`],tr=state.trust[id]||0;
 openModal(`${heading(p.emoji,p.name,`Кв. ${p.id} · ${p.role}`)}<p class="sheet-copy">${p.habit}</p><div class="pill">${here?'🟢 Сейчас дома':'🌙 Сейчас не дома'}</div><div class="pill">${p.trait}</div><div class="pill">💛 Доверие: ${tr>0?'+':''}${tr}</div><div class="actions" style="margin-top:12px">${actionButton('👀 Наблюдать','Присмотреться к окну и коридору','observe','−1 👁',!can)}${actionButton('👂 Прислушаться','Разобрать звуки из квартиры','listen','−1 👁',!can)}${actionButton('🚪 Поговорить','Задать вопрос жильцу','knock','−1 👁',!can||!here)}${actionButton('🤝 Помочь',helped?'Сегодня уже помогал':'Повысить доверие и снизить напряжение','help','−1 👁',!can||!here||helped)}</div><p class="sheet-copy muted">${!here?'Для разговора подожди возвращения жильца.':'Помощь укрепляет доверие на всю кампанию.'}</p>`);
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
 const generic={observe:[`${p.name} смотрит на улицу, словно кого-то ждёт.`,`Ты замечаешь привычный вечерний ритуал в квартире ${id}.`],listen:[`Слышится скрип двери и далёкие шаги.`,`За стеной кто-то разговаривает о происшествии.`],knock:[`${p.name} внимательно выслушивает тебя и обещает вспомнить подробности.`,`Разговор проходит спокойно: важнее всего теперь факты.`],help:[`Ты помог жильцу ${p.name} решить бытовую проблему. Теперь он больше тебе доверяет.`]};
 let result=generic[type][(state.actions+Number(id))%generic[type].length];
 if(type==='help'){state.helped[key]=true;state.kindness++;state.trust[id]=clamp((state.trust[id]||0)+2,-2,4);state.tension=clamp(state.tension-11);
 if(!state.storiesSeen[id]&&state.trust[id]>=2){state.storiesSeen[id]=true;addLog(`${p.name} доверяет тебе. ${stories[id]}`,'💬')}
 }
 if(type==='knock'){state.trust[id]=clamp((state.trust[id]||0)+1,-2,4);state.tension=clamp(state.tension-3)}
 if(type==='listen')state.tension=clamp(state.tension+3);
 if(type==='observe')state.tension=clamp(state.tension+1);
 const match=getClues().find(k=>k.resident===id&&k.action===type&&!state.clues.includes(k.id));
 if(match){state.clues.push(match.id);result=`Улика «${match.title}»: ${match.body}`;icon='🔎'}
 addLog(result,icon);tick();closeModal();render();if(state.closed){showEnding();return}if(state.pendingEvent){showEvent();return}notify(match?`🔎 Улика: ${match.title}`:type==='help'?'💛 Доверие выросло':`+1 час · ${p.name}`)}
function waitHour(){if(state.pendingEvent){showEvent();return}if(state.closed){showEnding();return}state.focus=clamp(state.focus+2,0,4);addLog('Ты отдыхаешь, сортируешь записи и восстанавливаешь внимание.','☕');tick();render();if(state.closed){showEnding();return}if(state.pendingEvent){showEvent();return}notify('☕ +2 внимания · +1 час')}
function showEvent(){const e=state.pendingEvent;if(!e||state.closed)return;
 if(e==='special'){
  const data=specialEvents[state.chapter];
  openModal(`${heading(data[0].split(' ')[0],data[0].slice(3),'Событие текущего расследования')}<p class="sheet-copy">${data[1]}</p><div class="actions">${actionButton(data[2][0],data[2][1],'protect','💛')}${actionButton(data[3][0],data[3][1],'expose','⚠️')}</div>`);
 } else if(e==='power')openModal(`${heading('⚡','Неожиданное отключение','Жильцы собрались на лестнице')}<p class="sheet-copy">Лампочки гаснут, внизу кто-то зовёт на помощь. Можно успокоить людей или попытаться добыть больше сведений.</p><div class="actions">${actionButton('🧯 Успокоить соседей','Снизить панику, потратить часть сил','calm','−1 👁')}${actionButton('🔍 Осмотреть лестницу','Получить подсказку, но усилить тревогу','inspect','+5 ⚠️')}</div>`);
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
 const candidate=pending.find(k=>k.action==='knock'&&home(residents.find(r=>r.id===k.resident)))||pending[0];
 const p=residents.find(r=>r.id===candidate.resident);
 state.hints++;state.focus--;state.tension=clamp(state.tension+4);state.actions++;
 addLog(`Подсказка из домового чата: ${p.name} (кв. ${p.id}) может помочь. Попробуй: ${candidate.action==='knock'?'поговорить с жильцом':candidate.action==='observe'?'наблюдать из окна':'прислушаться к звукам'}.`,'💡');
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
function showTheories(){if(state.pendingEvent){showEvent();return}if(state.closed){showEnding();return}const enough=state.clues.length>=3;const c=current();
 openModal(`${heading('🧩','Доска версий',`Дело ${state.chapter+1}: ${c.title}`)}<p class="sheet-copy">${enough?'Улик достаточно для решения. Внимательно сравни факты перед окончательным выводом.':'Найди ещё '+(3-state.clues.length)+' улик(и), чтобы выдвинуть версию.'}</p><div class="clue"><b>📌 Анализ фактов</b><p>${state.clues.length>=4?c.deduction:'Пока рано делать выводы. Сравни показания хотя бы четырёх соседей, чтобы увидеть связь.'}</p></div><div class="actions">${c.guesses.map(g=>actionButton(g[1],g[2],g[0],'РЕШИТЬ',!enough,g[0]===c.correct?'':'')).join('')}</div><p class="sheet-copy muted" style="font-size:11px">Выбор закроет дело. Можно продолжить расследование, чтобы заработать больше очков.</p>`);
 $('sheet').querySelectorAll('[data-action]').forEach(b=>b.addEventListener('click',()=>{finish(b.dataset.action);closeModal();showEnding()}))}
function finish(ending){if(state.closed)return;const c=current();state.closed=true;state.pendingEvent=null;state.ending=ending;const win=ending===c.correct,brilliant=win&&state.clues.length>=5&&state.tension<=55&&state.kindness>=1;
 const earned=Math.max(0,state.clues.length*70+state.kindness*25+Math.max(0,100-state.tension)*2+(win?270:0)+(brilliant?230:0)-(ending==='timeout'?150:0));state.score+=earned;
 state.history.push({chapter:state.chapter,guess:ending,win,brilliant,earned,clues:state.clues.length,tension:state.tension});
 addLog(win?'Дело раскрыто. Жильцы благодарят тебя за помощь.':'Дело завершено, но все ли тайны удалось раскрыть?',win?'🏆':'📜');render()}
function showEnding(){if(!state.closed)return;const h=state.history[state.history.length-1],c=current(),last=state.chapter===cases.length-1;
 const title=h.brilliant?'Блестящее расследование!':h.win?'Дело раскрыто!':h.guess==='timeout'?'Время вышло':h.guess==='panic'?'Дом взбунтовался':'Ложная версия';
 const endingText=h.win?`${c.reveal} Ты успел разобраться и сохранить доверие соседей.`:`${c.reveal} Твоя версия оказалась неверной, но история дома на этом не заканчивается.`;
 const conclusion=last?`<div class="clue"><b>КАМПАНИЯ ПРОЙДЕНА · ${state.score} ОЧКОВ</b><p>Разгадано дел: ${state.history.filter(v=>v.win).length}/${cases.length} · Добрых дел: ${state.kindness}<br>${state.history.every(v=>v.win)?'🏅 Ты раскрыл главную тайну дома №13!':'История дома не завершилась идеально — попробуй другие решения.'}<br>${state.choices.filter(v=>v.action==='protect').length>=3?'Соседи доверяют тебе: дом снова стал одной семьёй.':'Секреты раскрыты, но соседям ещё предстоит научиться доверять друг другу.'}</p></div>`:'';
 openModal(`${heading(h.win?'🏆':'📜',title,`Дело ${state.chapter+1}/${cases.length} · +${h.earned} очков`)}<div class="final-score">${h.brilliant?'🌟':h.win?'🎉':'🌧'}</div><p class="sheet-copy">${endingText}</p><div class="clue"><b>Результат дела</b><p>Улики: ${h.clues}/6 · Доверие/добрые дела: ${state.kindness}<br>Напряжение: ${h.tension}% · Начислено: ${h.earned}</p></div>${conclusion}<div class="actions">${last?actionButton('🔄 Новая кампания','Начать все пять дел сначала','again','ИГРАТЬ',false,'primary'):actionButton('➡️ Следующее дело','Продолжить историю жильцов','next','ПРОДОЛЖИТЬ',false,'primary')}</div>`);
 $('sheet').querySelectorAll('[data-action]').forEach(b=>b.addEventListener('click',()=>{if(b.dataset.action==='again'){state=makeState();state.seenTutorial=true}else{state.chapter++;state.day=1;state.hour=19;state.focus=4;state.clues=[];state.helped={};state.hints=0;state.closed=false;state.ending=null;state.pendingEvent=null;state.tension=clamp(Math.round(state.tension*.45)+12,12,55);addLog(`Дело №0${state.chapter+1}: ${current().title}. Начинается новое расследование.`,current().icon)}closeModal();render();notify('Открыто новое дело') }));}
function showHelp(){if(state.pendingEvent){showEvent();return}state.seenTutorial=true;save();openModal(`${heading('🏢','Добро пожаловать','Дом №13 · Пять ночных дел')}<p class="sheet-copy">Ты — смотритель странного дома. У каждого соседа своя жизнь, а сначала ты столкнёшься с отключениями электричества, пропавшими посылками и голосом с чердака. Все загадки связаны.</p><div class="clue"><b>Как играть</b><p>1. Нажимай на квартиры и выбирай действия.<br>2. Собирай хотя бы 3 из 6 улик для каждого дела.<br>3. У действий есть цена: 1 внимание и 1 час.<br>4. Кнопка «След. час» восстанавливает 2 внимания.<br>5. Помогай жильцам и принимай решения в событиях.<br>6. Выбери версию до конца третьей ночи. Решение повлияет на общий счёт. Впереди пять связанных дел.</p></div><p class="sheet-copy">Прогресс сохраняется на этом телефоне. Помогай жильцам, находи личные истории и открывай достижения. В журнале доступны подсказки и настройки звука.</p><div class="actions">${actionButton('Начать игру','Открыть дом','start','ВПЕРЁД',false,'primary')}</div>`);$('sheet').querySelector('[data-action="start"]').addEventListener('click',closeModal)}
$('help').addEventListener('click',showHelp);$('journal').addEventListener('click',showJournal);$('theories').addEventListener('click',showTheories);$('wait').addEventListener('click',waitHour);
modal.addEventListener('cancel',e=>{if(state.pendingEvent){e.preventDefault();notify('Сначала выбери действие')}});
modal.addEventListener('click',e=>{if(e.target===modal){if(state.pendingEvent){notify('Сначала выбери решение')}else closeModal()}});
render();if(state.closed)showEnding();else if(state.pendingEvent)showEvent();else if(!state.seenTutorial)showHelp();
window.__dom13={getState:()=>JSON.parse(JSON.stringify(state)),reset:()=>{state=makeState();state.seenTutorial=true;closeModal();render()},act,waitHour,showTheories,showEnding,chooseEvent,showJournal,askHint};
})();
