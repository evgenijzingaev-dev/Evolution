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
  ]},
 {icon:'📦',title:'Пропавшие посылки',hook:'По утрам исчезают посылки из коридора. Соседи подозревают друг друга.',correct:'storage',reveal:'После протечки коробки переносили в сухую подсобку рядом с лифтом. Сообщение в домовом чате затерялось, поэтому все подумали о краже.',
  guesses:[['courier','🛵 Виноват курьер','Кто-то присваивает заказы при доставке'],['storage','🗝 Посылки в подсобке','Кто-то временно убрал коробки от воды'],['cat','🐈 Кот разносит пакеты','Коробки исчезают из-за животных']],
  clues:[
   ['log','101','knock','Запись в журнале','Тамара получила записку: «Не оставлять коробки в сыром коридоре».'],
   ['tape','102','knock','Лента доставки','Олег узнаёт фирменный скотч на тележке у лифта. Все посылки были доставлены.'],
   ['wheels','201','listen','Колёса в полночь','Майя слышала, как ночью по коридору осторожно катили тележку.'],
   ['key','202','knock','Ключ от подсобки','Виктор признаётся, что дал ключ старшей по дому после затопления.'],
   ['clip','301','observe','Отражение на видео','На записи Киры видно, как коробки заносят к служебной двери.'],
   ['stream','302','listen','Запись стрима','В эфир попала фраза: «Главное — уберечь их от сырости».']
  ]},
 {icon:'📻',title:'Голос на чердаке',hook:'Ровно в полночь чердак передаёт странный радиосигнал. Жильцы боятся, что за ними следят.',correct:'beacon',reveal:'На чердаке сохранился старый радиомаяк аварийной связи. После перебоев питания его резервный блок начал автоматически включать тестовую передачу.',
  guesses:[['spies','🕵️ За жильцами следят','В доме установили тайный передатчик'],['beacon','📡 Старый радиомаяк','На чердаке ожил забытый прибор'],['ghost','👁 Голоса призраков','Сигнал приходит из потустороннего мира']],
  clues:[
   ['archive','101','knock','Домовой архив','В старой папке Тамары есть акт установки аварийной радиосвязи.'],
   ['antenna','102','observe','Антенна на крыше','Олег замечает под коньком крыши тонкий металлический прут.'],
   ['melody','201','listen','Повторяющийся ритм','Майя слышит, что сигнал повторяется ровно раз в час.'],
   ['wire','202','knock','Кабель к чердаку','Виктор указывает на старый кабель от резервного аккумулятора.'],
   ['photo','301','observe','Снимок из окна','На фото Киры виден маленький мигающий индикатор на чердаке.'],
   ['signal','302','listen','Метка эфира','Лев расшифровывает запись: «Тест системы. Канал тринадцать».']
  ]}
];
function makeState(){return {version:2,chapter:0,day:1,hour:19,focus:4,tension:18,kindness:0,trust:{},clues:[],actions:0,helped:{},log:[{icon:'📻',text:'Началась первая ночная смена в доме №13.'}],history:[],score:0,closed:false,ending:null,pendingEvent:null,eventsSeen:{},sound:false,seenTutorial:false,chapterStart:18}}
let state;
try{const old=JSON.parse(localStorage.getItem(STORE));state=old&&old.version===2&&Number.isInteger(old.chapter)&&old.chapter>=0&&old.chapter<=2&&Array.isArray(old.clues)&&Array.isArray(old.history)?{...makeState(),...old}:makeState()}catch{state=makeState()}
let toastTimer, audio;
const modal=$('modal');
const current=()=>cases[state.chapter];
const getClues=()=>current().clues.map(c=>({id:c[0],resident:c[1],action:c[2],title:c[3],body:c[4]}));
const home=p=>p.home(state.hour);
const addLog=(text,icon='📻')=>{state.log.unshift({text,icon});state.log=state.log.slice(0,30)};
const save=()=>{try{localStorage.setItem(STORE,JSON.stringify(state))}catch{}};
function tone(freq=500,duration=.06){if(!state.sound)return;try{audio??=new (window.AudioContext||window.webkitAudioContext)();const osc=audio.createOscillator(),gain=audio.createGain();osc.type='sine';osc.frequency.value=freq;gain.gain.setValueAtTime(.05,audio.currentTime);gain.gain.exponentialRampToValueAtTime(.001,audio.currentTime+duration);osc.connect(gain).connect(audio.destination);osc.start();osc.stop(audio.currentTime+duration)}catch{}}
function notify(msg){const el=$('toast');el.textContent=msg;el.classList.add('show');clearTimeout(toastTimer);toastTimer=setTimeout(()=>el.classList.remove('show'),2300);tone(570)}
function closeModal(){if(modal.open)modal.close()}
function openModal(html){$('sheet').innerHTML='<div class="sheet-handle"></div>'+html;if(!modal.open)modal.showModal();$('sheet').querySelectorAll('[data-close]').forEach(b=>b.addEventListener('click',closeModal))}
function heading(icon,title,sub){return `<div class="sheet-title-row"><div class="sheet-icon">${icon}</div><div><div class="sheet-title">${title}</div><p class="sheet-sub">${sub}</p></div><button class="sheet-close" data-close aria-label="Закрыть">✕</button></div>`}
function actionButton(name,desc,action,cost='−1 👁',disabled=false,css=''){return `<button class="action ${css}" data-action="${action}" ${disabled?'disabled':''}><span><strong>${name}</strong><small>${desc}</small></span><span class="action-cost">${cost}</span></button>`}
function render(){const c=current();$('time').innerHTML=`День ${Math.min(3,state.day)} <small>${String(state.hour).padStart(2,'0')}:00</small>`;
 $('focus').innerHTML=`${state.focus} <small>/ 4</small>`;$('cluecount').innerHTML=`${state.clues.length} <small>/ 6</small>`;
 $('progress-n').textContent=`${Math.min(state.clues.length,3)} из 3`;$('clueprogress').style.width=`${clamp(state.clues.length/3*100)}%`;
 $('tensionlabel').textContent=`${state.tension}%`;$('tensionbar').style.width=`${state.tension}%`;
 $('journal-n').textContent=state.clues.length;$('wait').disabled=state.closed;
 $('theories').classList.toggle('ready',state.clues.length>=3&&!state.closed);
 $('mission').textContent=state.closed?'Дело закрыто. Посмотри результаты и перейди к следующему.':c.hook;
 $('case-name').textContent=`Дело №0${state.chapter+1}: ${c.title}`;$('case-icon').textContent=c.icon;
 $('campaign').textContent=`ДЕЛО ${state.chapter+1} / 3 · ${state.score} ОЧКОВ`;
 const house=$('house');house.replaceChildren();
 for(let floor=3;floor>=1;floor--){const block=document.createElement('div');block.className='floor';const n=document.createElement('div');n.className='floor-number';n.textContent=`${floor} ЭТАЖ`;block.append(n);const rooms=document.createElement('div');rooms.className='rooms';
  for(const p of residents.filter(r=>r.id.startsWith(String(floor)))){const here=home(p),found=getClues().some(k=>k.resident===p.id&&state.clues.includes(k.id));const tr=state.trust[p.id]||0;const btn=document.createElement('button');btn.type='button';btn.className=`room ${here?'home':'out'} ${state.hour===22?'flicker':''}`;
   btn.setAttribute('aria-label',`${p.name}, квартира ${p.id}, ${here?'дома':'нет дома'}`);
   btn.innerHTML=`<span class="room-badge ${found?'discovered':''}">${found?'★ УЛИКА':p.id}</span><span class="window-view"><span class="curtain left"></span><span class="curtain right"></span><span class="window-cross"></span><span class="window-lights">•••</span><span class="avatar">${here?p.emoji:'🌙'}</span></span><span class="room-info"><b>${p.name}</b><em>${tr>=2?'♥ доверие':here?'● дома':'○ нет дома'}</em></span>`;
   btn.addEventListener('click',()=>state.closed?showEnding():showPerson(p.id));rooms.append(btn)}block.append(rooms);house.append(block)}
 const recent=state.log[0]||{icon:'📻',text:'Тишина.'};$('feed-icon').textContent=recent.icon;$('feed-text').textContent=recent.text;
 $('foot').textContent=`КАМПАНИЯ · ${state.history.length} ДЕЛ ЗАКРЫТО · ПРОГРЕСС СОХРАНЁН`;save()}
function showPerson(id){const p=residents.find(r=>r.id===id);if(!p)return;const here=home(p),can=state.focus>0&&!state.closed;const helped=!!state.helped[`${state.chapter}-${state.day}-${id}`],tr=state.trust[id]||0;
 openModal(`${heading(p.emoji,p.name,`Кв. ${p.id} · ${p.role}`)}<p class="sheet-copy">${p.habit}</p><div class="pill">${here?'🟢 Сейчас дома':'🌙 Сейчас не дома'}</div><div class="pill">${p.trait}</div><div class="pill">💛 Доверие: ${tr>0?'+':''}${tr}</div><div class="actions" style="margin-top:12px">${actionButton('👀 Наблюдать','Присмотреться к окну и коридору','observe','−1 👁',!can)}${actionButton('👂 Прислушаться','Разобрать звуки из квартиры','listen','−1 👁',!can)}${actionButton('🚪 Поговорить','Задать вопрос жильцу','knock','−1 👁',!can||!here)}${actionButton('🤝 Помочь',helped?'Сегодня уже помогал':'Повысить доверие и снизить напряжение','help','−1 👁',!can||!here||helped)}</div><p class="sheet-copy muted">${!here?'Для разговора подожди возвращения жильца.':'Помощь укрепляет доверие на всю кампанию.'}</p>`);
 $('sheet').querySelectorAll('[data-action]').forEach(b=>b.addEventListener('click',()=>act(id,b.dataset.action)))}
function tick(){state.hour++;
 if(state.hour===22&&state.day===1&&!state.eventsSeen[`${state.chapter}-power`]){state.pendingEvent='power';state.eventsSeen[`${state.chapter}-power`]=true}
 if(state.hour>=24){state.hour=19;state.day++;state.focus=clamp(state.focus+2,0,4);
  if(state.day===2){state.tension=clamp(state.tension+5);addLog('🌧 Начался дождь. Соседи тревожатся из-за нового происшествия.','🌧');if(!state.eventsSeen[`${state.chapter}-meeting`]){state.pendingEvent='meeting';state.eventsSeen[`${state.chapter}-meeting`]=true}}
  if(state.day===3){state.tension=clamp(state.tension+8);addLog('🚨 Последняя ночь! Скоро придётся объявить свою версию.','🚨')}
  if(state.day>3){finish('timeout');return}}
 if(state.tension>=100)finish('panic')}
function act(id,type){if(state.closed||state.focus<1)return;const p=residents.find(r=>r.id===id);if(!p)return;const here=home(p);if((type==='knock'||type==='help')&&!here)return;const key=`${state.chapter}-${state.day}-${id}`;if(type==='help'&&state.helped[key])return;
 state.focus--;state.actions++;let icon={observe:'👀',listen:'👂',knock:'🚪',help:'🤝'}[type];
 const generic={observe:[`${p.name} смотрит на улицу, словно кого-то ждёт.`,`Ты замечаешь привычный вечерний ритуал в квартире ${id}.`],listen:[`Слышится скрип двери и далёкие шаги.`,`За стеной кто-то разговаривает о происшествии.`],knock:[`${p.name} внимательно выслушивает тебя и обещает вспомнить подробности.`,`Разговор проходит спокойно: важнее всего теперь факты.`],help:[`Ты помог жильцу ${p.name} решить бытовую проблему. Теперь он больше тебе доверяет.`]};
 let result=generic[type][(state.actions+Number(id))%generic[type].length];
 if(type==='help'){state.helped[key]=true;state.kindness++;state.trust[id]=clamp((state.trust[id]||0)+2,-2,4);state.tension=clamp(state.tension-11)}
 if(type==='knock'){state.trust[id]=clamp((state.trust[id]||0)+1,-2,4);state.tension=clamp(state.tension-3)}
 if(type==='listen')state.tension=clamp(state.tension+3);
 if(type==='observe')state.tension=clamp(state.tension+1);
 const match=getClues().find(k=>k.resident===id&&k.action===type&&!state.clues.includes(k.id));
 if(match){state.clues.push(match.id);result=`Улика «${match.title}»: ${match.body}`;icon='🔎'}
 addLog(result,icon);tick();closeModal();render();if(state.closed){showEnding();return}if(state.pendingEvent){showEvent();return}notify(match?`🔎 Улика: ${match.title}`:type==='help'?'💛 Доверие выросло':`+1 час · ${p.name}`)}
function waitHour(){if(state.closed){showEnding();return}state.focus=clamp(state.focus+2,0,4);addLog('Ты отдыхаешь, сортируешь записи и восстанавливаешь внимание.','☕');tick();render();if(state.closed){showEnding();return}if(state.pendingEvent){showEvent();return}notify('☕ +2 внимания · +1 час')}
function showEvent(){const e=state.pendingEvent;if(!e||state.closed)return;
 if(e==='power')openModal(`${heading('⚡','Неожиданное отключение','Жильцы собрались на лестнице')}<p class="sheet-copy">Лампочки гаснут, внизу кто-то зовёт на помощь. Можно успокоить людей или попытаться добыть больше сведений.</p><div class="actions">${actionButton('🧯 Успокоить соседей','Снизить панику, потратить часть сил','calm','−1 👁')}${actionButton('🔍 Осмотреть лестницу','Получить подсказку, но усилить тревогу','inspect','+5 ⚠️')}</div>`);
 else openModal(`${heading('🗣','Собрание у лифта','Соседи требуют ответа')}<p class="sheet-copy">Жильцы спорят, кто виноват. Ты можешь наладить разговор или отстраниться и заняться своим делом.</p><div class="actions">${actionButton('🤝 Организовать разговор','Снять напряжение и заслужить доверие','talk','−1 👁')}${actionButton('📋 Проверить записи','Не терять время, получить напоминание','notes','+3 ⚠️')}</div>`);
 $('sheet').querySelectorAll('[data-action]').forEach(b=>b.addEventListener('click',()=>chooseEvent(b.dataset.action)))}
function chooseEvent(ch){if(!state.pendingEvent)return;const e=state.pendingEvent;state.pendingEvent=null;
 if(ch==='calm'){state.focus=clamp(state.focus-1,0,4);state.tension=clamp(state.tension-14);addLog('Ты вывел соседей из темноты и успокоил их.','🤝')}
 if(ch==='inspect'){state.tension=clamp(state.tension+5);addLog(`Подсказка: присмотрись к уликам в квартирах ${current().clues[0][1]} и ${current().clues[3][1]}.`,'🔍')}
 if(ch==='talk'){state.focus=clamp(state.focus-1,0,4);state.tension=clamp(state.tension-12);state.kindness++;addLog('Соседи наконец выслушали друг друга.','💛')}
 if(ch==='notes'){state.tension=clamp(state.tension+3);addLog(`Подсказка: поговори с ${residents.find(r=>r.id===current().clues[3][1]).name} и проверь записи соседей.`,'📋')}
 closeModal();render();notify('Решение повлияло на дом')}
function showJournal(){const found=getClues().filter(k=>state.clues.includes(k.id));const body=found.length?found.map((c,i)=>`<div class="clue"><b>${i+1}. ${c.title}</b><p>${c.body}</p></div>`).join(''):'<div class="empty">Пока нет улик. Исследуй окна, слушай звуки и разговаривай с жильцами.</div>';
 const past=state.history.map((h,i)=>`<div class="clue"><b>${h.win?'✅':'❌'} Дело ${i+1}: ${cases[i].title}</b><p>${h.win?'Разгадано':'Не разгадано'} · ${h.earned} очков · улик ${h.clues}/6</p></div>`).join('');
 const events=state.log.slice(0,5).map(l=>`<p class="sheet-copy" style="margin:6px 0">${l.icon} ${l.text}</p>`).join('');
 openModal(`${heading('📓','Журнал детектива',`Дело ${state.chapter+1}/3 · ${state.score} очков`)}<p class="sheet-copy">Собери минимум 3 улики, затем выбери версию. Доверие жильцов сохраняется между делами.</p>${body}${past?'<h3 class="part-title">ПРОШЛЫЕ ДЕЛА</h3>'+past:''}<h3 class="part-title">ПОСЛЕДНИЕ СОБЫТИЯ</h3>${events}<div class="actions" style="margin-top:17px">${actionButton(state.sound?'🔊 Звук включён':'🔇 Включить звук','Короткие сигналы при действиях','sound','⚙️')}${actionButton('↻ Начать заново','Сбросить всю кампанию и улики','reset','СБРОС','', 'danger')}</div>`);
 $('sheet').querySelector('[data-action="sound"]').addEventListener('click',()=>{state.sound=!state.sound;render();showJournal();notify(state.sound?'🔊 Звук включён':'🔇 Звук выключен')});
 $('sheet').querySelector('[data-action="reset"]').addEventListener('click',()=>{if(confirm('Начать всю кампанию заново? Прогресс трёх дел будет потерян.')){state=makeState();state.seenTutorial=true;closeModal();render();notify('Новая кампания началась')}})}
function showTheories(){if(state.closed){showEnding();return}const enough=state.clues.length>=3;const c=current();
 openModal(`${heading('🧩','Доска версий',`Дело ${state.chapter+1}: ${c.title}`)}<p class="sheet-copy">${enough?'Улик достаточно для решения. Внимательно сравни факты перед окончательным выводом.':'Найди ещё '+(3-state.clues.length)+' улик(и), чтобы выдвинуть версию.'}</p><div class="actions">${c.guesses.map(g=>actionButton(g[1],g[2],g[0],'РЕШИТЬ',!enough,g[0]===c.correct?'':'')).join('')}</div><p class="sheet-copy muted" style="font-size:11px">Выбор закроет дело. Можно продолжить расследование, чтобы заработать больше очков.</p>`);
 $('sheet').querySelectorAll('[data-action]').forEach(b=>b.addEventListener('click',()=>{finish(b.dataset.action);closeModal();showEnding()}))}
function finish(ending){if(state.closed)return;const c=current();state.closed=true;state.pendingEvent=null;state.ending=ending;const win=ending===c.correct,brilliant=win&&state.clues.length>=5&&state.tension<=55&&state.kindness>=1;
 const earned=Math.max(0,state.clues.length*70+state.kindness*25+Math.max(0,100-state.tension)*2+(win?270:0)+(brilliant?230:0)-(ending==='timeout'?150:0));state.score+=earned;
 state.history.push({chapter:state.chapter,guess:ending,win,brilliant,earned,clues:state.clues.length,tension:state.tension});
 addLog(win?'Дело раскрыто. Жильцы благодарят тебя за помощь.':'Дело завершено, но все ли тайны удалось раскрыть?',win?'🏆':'📜');render()}
function showEnding(){if(!state.closed)return;const h=state.history[state.history.length-1],c=current(),last=state.chapter===cases.length-1;
 const title=h.brilliant?'Блестящее расследование!':h.win?'Дело раскрыто!':h.guess==='timeout'?'Время вышло':h.guess==='panic'?'Дом взбунтовался':'Ложная версия';
 const endingText=h.win?`${c.reveal} Ты успел разобраться и сохранить доверие соседей.`:`${c.reveal} Твоя версия оказалась неверной, но история дома на этом не заканчивается.`;
 const conclusion=last?`<div class="clue"><b>КАМПАНИЯ ПРОЙДЕНА · ${state.score} ОЧКОВ</b><p>Разгадано дел: ${state.history.filter(v=>v.win).length}/3 · Добрых дел: ${state.kindness}<br>${state.history.every(v=>v.win)?'🏅 Знаток дома №13':'Каждое дело можно пройти иначе.'}</p></div>`:'';
 openModal(`${heading(h.win?'🏆':'📜',title,`Дело ${state.chapter+1}/3 · +${h.earned} очков`)}<div class="final-score">${h.brilliant?'🌟':h.win?'🎉':'🌧'}</div><p class="sheet-copy">${endingText}</p><div class="clue"><b>Результат дела</b><p>Улики: ${h.clues}/6 · Доверие/добрые дела: ${state.kindness}<br>Напряжение: ${h.tension}% · Начислено: ${h.earned}</p></div>${conclusion}<div class="actions">${last?actionButton('🔄 Новая кампания','Начать все три дела сначала','again','ИГРАТЬ',false,'primary'):actionButton('➡️ Следующее дело','Продолжить историю жильцов','next','ПРОДОЛЖИТЬ',false,'primary')}</div>`);
 $('sheet').querySelectorAll('[data-action]').forEach(b=>b.addEventListener('click',()=>{if(b.dataset.action==='again'){state=makeState();state.seenTutorial=true}else{state.chapter++;state.day=1;state.hour=19;state.focus=4;state.clues=[];state.helped={};state.closed=false;state.ending=null;state.pendingEvent=null;state.tension=clamp(Math.round(state.tension*.45)+12,12,55);addLog(`Дело №0${state.chapter+1}: ${current().title}. Начинается новое расследование.`,current().icon)}closeModal();render();notify('Открыто новое дело') }));}
function showHelp(){state.seenTutorial=true;save();openModal(`${heading('🏢','Добро пожаловать','Дом №13 · Три ночных дела')}<p class="sheet-copy">Ты — смотритель странного дома. У каждого соседа своя жизнь, а у дома три тайны: отключения электричества, пропавшие посылки и голос с чердака.</p><div class="clue"><b>Как играть</b><p>1. Нажимай на квартиры и выбирай действия.<br>2. Собирай хотя бы 3 из 6 улик для каждого дела.<br>3. У действий есть цена: 1 внимание и 1 час.<br>4. Кнопка «След. час» восстанавливает 2 внимания.<br>5. Помогай жильцам и принимай решения в событиях.<br>6. Выбери версию до конца третьей ночи. Решение повлияет на общий счёт.</p></div><p class="sheet-copy">Прогресс сохраняется на этом телефоне. В журнале можно посмотреть историю и настроить звук.</p><div class="actions">${actionButton('Начать игру','Открыть дом','start','ВПЕРЁД',false,'primary')}</div>`);$('sheet').querySelector('[data-action="start"]').addEventListener('click',closeModal)}
$('help').addEventListener('click',showHelp);$('journal').addEventListener('click',showJournal);$('theories').addEventListener('click',showTheories);$('wait').addEventListener('click',waitHour);
modal.addEventListener('click',e=>{if(e.target===modal){if(state.pendingEvent){notify('Сначала выбери решение')}else closeModal()}});
render();if(state.closed)showEnding();else if(state.pendingEvent)showEvent();else if(!state.seenTutorial)showHelp();
window.__dom13={getState:()=>JSON.parse(JSON.stringify(state)),reset:()=>{state=makeState();state.seenTutorial=true;closeModal();render()},act,waitHour,showTheories,showEnding,chooseEvent,showJournal};
})();
