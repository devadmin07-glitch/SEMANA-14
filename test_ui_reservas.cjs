// Prueba sin navegador de los estados y bloqueo de habitaciones.
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const code = fs.readFileSync(path.join(__dirname, 'wwwroot/js/reservas.js'), 'utf8');

class El {
  constructor(id='') { this.id=id; this.options=[]; this.children=[]; this.listeners={}; this.value=''; this.disabled=false; this._html=''; this.textContent=''; this.classList={toggle(){}}; }
  set innerHTML(v){this._html=v;this.children=[];this.options=[];this.value='';}
  get innerHTML(){return this._html;}
  add(o){this.options.push(o);if (this.options.length===1)this.value=o.value;}
  appendChild(child){this.children.push(child);return child;}
  querySelectorAll(){return [];}
  addEventListener(name, fn){(this.listeners[name]??=[]).push(fn);}
  async click(){for(const fn of this.listeners.click??[]) await fn();}
  trigger(name){for(const fn of this.listeners[name]??[])fn({preventDefault(){this.prevented=true;}});}
}
function room(id,estado,disponible,esPropia=false){return {id,numero:String(100+id),tipo:'Simple',costoDia:100,estado,disponible,esPropia};}
async function scenario(roomData, expectedEnabled, expectedMessage, initial=[]){
  const ids=['FechaLlegada','FechaSalida','dias','filasReserva','subtotal','igv','total','hiddenItems','habitacionSelect','agregarHabitacionBtn','buscarClienteBtn','buscarHabitacionesBtn','busquedaCliente','ClienteId','roomMessage','huesped','formReserva','estadoHabitacionesBody'];
  const els=Object.fromEntries(ids.map(x=>[x,new El(x)]));
  els.FechaLlegada.value='2027-02-01';els.FechaSalida.value='2027-02-04';els.ClienteId.value='1';
  const alerts=[];const requests=[];
  const context={
    window:{initialHotelItems:initial},
    document:{getElementById:id=>els[id], createElement:type=>new El(type)},
    Option:class {constructor(text,value){this.text=text;this.value=value;}},
    fetch:async url=>{requests.push(url);return {ok:true,json:async()=>({habitaciones:roomData})}},
    alert:msg=>alerts.push(msg),
    console, Intl, URLSearchParams, Date, Math, Number, String, Error, Array
  };
  vm.runInNewContext(code,context,{filename:'reservas.js'});
  // Cargar la respuesta de búsqueda y permitir que finalice el async handler.
  await els.buscarHabitacionesBtn.click();
  assert.equal(!els.habitacionSelect.disabled,expectedEnabled);
  assert.ok(els.roomMessage.textContent.includes(expectedMessage),els.roomMessage.textContent);
  assert.equal(requests.length, initial.length ? 2 : 1);
  return {els,alerts,requests};
}
(async()=>{
  const r=await scenario([room(1,'Reservado',false),room(2,'Reservado',false),room(3,'Ocupado',false),room(4,'Reservado',false),room(5,'Disponible',true)],true,'1 habitación(es)');
  assert.equal(r.els.habitacionSelect.options.length,2); // placeholder + una libre
  assert.equal(r.els.habitacionSelect.options[1].value,'5');
  const no=await scenario([room(1,'Reservado',false),room(2,'Reservado',false),room(3,'Ocupado',false),room(4,'Reservado',false),room(5,'Reservado',false)],false,'No hay habitaciones disponibles');
  assert.equal(no.els.agregarHabitacionBtn.disabled,true);
  assert.ok(no.alerts.includes('No hay habitaciones disponibles.'));
  const ed=await scenario([room(1,'Reservado',true,true),room(2,'Reservado',false)],false,'Todas las habitaciones disponibles ya están agregadas',[{habitacionId:1,nombreHuesped:'Jose',numero:'101',tipo:'Simple',costoDia:100}]);
  assert.equal(ed.els.agregarHabitacionBtn.disabled,true);
  console.log('3 pruebas UI correctas: 4 reservadas/1 libre; todas bloqueadas; edición de habitación propia.');
})().catch(e=>{console.error(e);process.exitCode=1;});
