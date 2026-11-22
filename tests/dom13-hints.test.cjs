// Run with: node --test tests/dom13-hints.test.cjs
const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const source=fs.readFileSync(require('node:path').join(__dirname,'../game.js'),'utf8');
const start=source.indexOf('function askHint(){');
const end=source.indexOf('function showJournal()',start);
assert.ok(start>=0&&end>start,'askHint source must exist');
const askHintSource=source.slice(start,end);
function run({hour=19,trust={},clues=[]}={}){
 const state={hour,focus:4,hints:0,tension:0,actions:0,clues,pendingEvent:null,closed:false};
 const residents=[{id:'302',name:'Лев',home:h=>h>=19},{id:'102',name:'Олег',home:h=>h>=22}];
 const evidence=[{id:'lev',resident:'302',action:'knock'},{id:'oleg',resident:'102',action:'knock'}];
 const notices=[],logs=[];
 const ctx={state,residents,notify:x=>notices.push(x),addLog:x=>logs.push(x),
 getClues:()=>evidence,trustFor:id=>trust[id]??0,
 isClueAccessible:(p)=>!!p&&p.home(state.hour),
 clamp:n=>Math.max(0,Math.min(100,n)),tick:()=>{},closeModal:()=>{},render:()=>{},
 showEvent:()=>{},showEnding:()=>{}};
 vm.runInNewContext(askHintSource+';askHint();',ctx);
 return {state,notices,logs};
}
test('Лев дома в 19:00: разговор доступен, отсутствующий Олег не предлагается',()=>{
 const r=run();
 assert.equal(r.state.hints,1);
 assert.match(r.logs[0],/Лев/);
 assert.doesNotMatch(r.logs[0],/Олег/);
});
test('Отсутствующий Олег не предлагается для диалога и не расходует подсказку',()=>{
 const r=run({clues:['lev']});
 assert.equal(r.state.hints,0);
 assert.equal(r.state.focus,4);
 assert.equal(r.state.actions,0);
 assert.equal(r.logs.length,0);
 assert.match(r.notices[0],/отсутствуют/);
});
test('Недоверчивый Лев не выдаёт показания через подсказку',()=>{
 const r=run({trust:{'302':-3}});
 assert.equal(r.state.hints,0);
 assert.equal(r.state.focus,4);
 assert.match(r.notices[0],/не доверяет/);
});
test('После возвращения Олега доступна подсказка о разговоре',()=>{
 const r=run({hour:22,clues:['lev']});
 assert.equal(r.state.hints,1);
 assert.match(r.logs[0],/Олег/);
});
test('Сохранённый прогресс не очищается при подсказке',()=>{
 const r=run({clues:['lev']});
 assert.deepEqual(r.state.clues,['lev']);
});
