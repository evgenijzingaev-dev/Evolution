// Run: node --test tests/dom13-corridor.test.cjs
const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const path=require('node:path');
const source=fs.readFileSync(path.join(__dirname,'../game.js'),'utf8');
const section=source.slice(source.indexOf('function act('),source.indexOf('function act(')+8500);
test('Лев доступен вечером, Олег — только после 22:00',()=>{
 const residents=source.slice(source.indexOf('const residents='),source.indexOf('const cases='));
 assert.match(residents,/id:'302'[\s\S]*?home:h=>h>=19/);
 assert.match(residents,/id:'102'[\s\S]*?home:h=>h>=22/);
});
test('Отсутствующим жильцам нельзя звонить или помогать',()=>{
 assert.match(section,/if\(\(type==='knock'\|\|type==='help'\)&&!here\)return/);
});
test('Общий коридор не выдаёт след за показания Олега',()=>{
 assert.match(section,/Осмотр общего коридора у кв\./);
 assert.match(section,/!here&&corridorClue\(p,type\)/);
});
test('Повторный разговор с Олегом использует только найденную улику',()=>{
 assert.match(section,/type==='knock'&&id==='102'&&!refused/);
 assert.match(section,/state\.clues\.includes\(k\.id\)/);
 assert.match(section,/не знаю, чья была коробка/);
});
test('Прежняя структура сохранений не заменена',()=>{
 assert.match(source,/localStorage\.setItem\(STORE,JSON\.stringify\(state\)\)/);
 assert.match(source,/dailyConversations/);
});
test('Открытая коридорная улика доступна без жильца, разговор — нет',()=>{
 const snippet=source.slice(source.indexOf('const home='),source.indexOf('const conversationKey='));
 const context={state:{hour:20}};
 vm.runInNewContext(snippet+';globalThis.check={home,corridorClue};',context);
 const oleg={id:'102',home:h=>h>=22};
 const lev={id:'302',home:h=>h>=19};
 assert.equal(context.check.home(oleg),false);
 assert.equal(context.check.home(lev),true);
 assert.equal(context.check.corridorClue(oleg,'observe'),true);
 assert.equal(context.check.corridorClue(oleg,'knock'),false);
});
