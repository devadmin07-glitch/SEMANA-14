(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const money = n => new Intl.NumberFormat('es-PE', {
    style: 'currency', currency: 'PEN'
  }).format(Number(n) || 0);
  const safe = v => String(v ?? '').replace(/[&<>"']/g,
    c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);

  let todas = [];
  let disponibles = [];
  let busquedaVigente = false;
  let fechaConsultada = '';
  let seleccionadas = (window.initialHotelItems || []).map(x => ({
    id: Number(x.habitacionId ?? x.HabitacionId),
    huesped: x.nombreHuesped ?? x.NombreHuesped ?? '',
    numero: String(x.numero ?? x.Numero ?? x.habitacionId ?? x.HabitacionId),
    tipo: x.tipo ?? x.Tipo ?? 'Habitación',
    costoDia: Number(x.costoDia ?? x.CostoDia ?? 0)
  }));

  const periodo = () => `${$('FechaLlegada').value}|${$('FechaSalida').value}`;
  const getDias = () => {
    const a = $('FechaLlegada').value, b = $('FechaSalida').value;
    if (!a || !b) { $('dias').value = '0'; return 0; }
    const n = Math.round((new Date(`${b}T12:00:00`) - new Date(`${a}T12:00:00`)) / 86400000);
    $('dias').value = n > 0 ? String(n) : '0';
    return n > 0 ? n : 0;
  };

  const mensaje = (texto, tipo = '') => {
    const el = $('roomMessage');
    el.textContent = texto;
    el.classList.toggle('room-empty-message', tipo === 'error');
    el.classList.toggle('room-ok-message', tipo === 'ok');
  };

  const renderEstados = () => {
    const body = $('estadoHabitacionesBody');
    body.innerHTML = '';
    if (!todas.length) {
      body.innerHTML = '<tr><td colspan="4" class="empty">No hay habitaciones registradas en SQL Server.</td></tr>';
      return;
    }
    todas.forEach(r => {
      const tr = document.createElement('tr');
      const estado = r.estado === 'Disponible' ? 'Disponible' :
        r.estado === 'Ocupado' ? 'Ocupado' : 'Reservado';
      const clase = estado.toLowerCase();
      const propia = r.esPropia && estado === 'Reservado' ? ' (esta reserva)' : '';
      tr.innerHTML = `<td>${safe(r.numero)}</td><td>${safe(r.tipo)}</td><td>${money(r.costoDia)}</td><td><span class="estado-etiqueta estado-${clase}">${estado}${propia}</span></td>`;
      body.appendChild(tr);
    });
  };

  const renderSelect = () => {
    const s = $('habitacionSelect');
    const agregar = $('agregarHabitacionBtn');
    const libres = disponibles.filter(h => !seleccionadas.some(x => x.id === h.id));
    s.innerHTML = '';
    if (!busquedaVigente) {
      s.add(new Option('Buscar habitaciones primero', ''));
    } else if (disponibles.length === 0) {
      s.add(new Option('No hay habitaciones disponibles', ''));
    } else if (libres.length === 0) {
      s.add(new Option('Todas las disponibles están agregadas', ''));
    } else {
      s.add(new Option('-- Seleccione --', ''));
      libres.forEach(r => s.add(new Option(
        `${r.numero} · ${r.tipo} · ${money(r.costoDia)} / día`, String(r.id)
      )));
    }
    s.disabled = libres.length === 0 || !busquedaVigente;
    agregar.disabled = s.disabled;
  };

  const render = () => {
    const dias = getDias();
    const rows = $('filasReserva');
    rows.innerHTML = '';
    if (!seleccionadas.length) {
      rows.innerHTML = '<tr><td colspan="7" class="empty">Sin habitaciones agregadas.</td></tr>';
    }
    seleccionadas.forEach((r, i) => {
      const tr = document.createElement('tr');
      tr.innerHTML = `<td>${safe(r.numero)}</td><td>${safe(r.tipo)}</td><td>${safe(r.huesped)}</td><td>${money(r.costoDia)}</td><td>${dias}</td><td>${money(dias * r.costoDia)}</td><td><button type="button" class="remove-btn" data-i="${i}">Eliminar</button></td>`;
      rows.appendChild(tr);
    });
    rows.querySelectorAll('[data-i]').forEach(b => b.addEventListener('click', () => {
      seleccionadas.splice(Number(b.dataset.i), 1);
      render();
      if (busquedaVigente && disponibles.length > 0) {
        mensaje('Habitación retirada del detalle. Puede agregar otra.', 'ok');
      }
    }));
    const subtotal = seleccionadas.reduce((s, r) => s + Number(r.costoDia) * dias, 0);
    const igv = Math.round(subtotal * 0.18 * 100) / 100;
    $('subtotal').textContent = money(subtotal);
    $('igv').textContent = money(igv);
    $('total').textContent = money(subtotal + igv);
    const hidden = $('hiddenItems');
    hidden.innerHTML = '';
    seleccionadas.forEach((r, i) => {
      for (const [campo, valor] of [['HabitacionId', r.id], ['NombreHuesped', r.huesped]]) {
        const input = document.createElement('input');
        input.type = 'hidden';
        input.name = `Habitaciones[${i}].${campo}`;
        input.value = valor;
        hidden.appendChild(input);
      }
    });
    renderSelect();
  };

  const fechasCambiadas = () => {
    busquedaVigente = false;
    fechaConsultada = '';
    todas = [];
    disponibles = [];
    $('estadoHabitacionesBody').innerHTML = '<tr><td colspan="4" class="empty">Se modificaron las fechas. Busque habitaciones nuevamente.</td></tr>';
    mensaje('Se modificaron las fechas: presione "Buscar habitación" de nuevo.');
    render();
  };
  $('FechaLlegada').addEventListener('change', fechasCambiadas);
  $('FechaSalida').addEventListener('change', fechasCambiadas);

  $('buscarClienteBtn').addEventListener('click', async () => {
    const termino = $('busquedaCliente').value.trim();
    try {
      const response = await fetch(`/Clientes/Buscar?termino=${encodeURIComponent(termino)}`);
      if (!response.ok) throw Error('Consulta de clientes no disponible.');
      const data = await response.json();
      const select = $('ClienteId');
      select.innerHTML = '<option value="0">-- Seleccione cliente --</option>';
      data.forEach(c => select.add(new Option(`${c.id} · ${c.nombre}`, String(c.id))));
      if (data.length === 1) select.value = String(data[0].id);
      if (!data.length) alert('Cliente no existe. Puede registrarlo con "Agregar un cliente".');
    } catch (err) { alert('Error al buscar clientes: ' + err.message); }
  });

  const buscarHabitaciones = async (mostrarAviso = true) => {
    if (!getDias()) {
      mensaje('Ingrese fechas válidas: la salida debe ser posterior a la llegada.', 'error');
      if (mostrarAviso) alert('Seleccione un rango válido de fechas.');
      return;
    }
    const periodoActual = periodo();
    const params = new URLSearchParams({
      llegada: $('FechaLlegada').value, salida: $('FechaSalida').value
    });
    if (window.editingReservaId) params.set('excluirReservaId', window.editingReservaId);
    busquedaVigente = false;
    mensaje('Consultando disponibilidad en SQL Server…');
    renderSelect();
    try {
      const response = await fetch(`/Reservas/HabitacionesDisponibles?${params}`);
      if (!response.ok) throw Error(await response.text());
      const result = await response.json();
      if (!Array.isArray(result.habitaciones)) throw Error('Respuesta de consulta inválida.');
      if (periodo() !== periodoActual) return; // Evita aplicar un resultado de fechas anteriores.
      todas = result.habitaciones;
      disponibles = todas.filter(h => h.disponible);
      fechaConsultada = periodoActual;
      busquedaVigente = true;
      // Restaurar número/tipo/tarifa en reservas cargadas desde validaciones del servidor.
      seleccionadas.forEach(r => {
        const actual = todas.find(h => h.id === r.id);
        if (actual) {
          r.numero = actual.numero;
          r.tipo = actual.tipo;
          r.costoDia = Number(actual.costoDia);
        }
      });
      renderEstados();
      render();
      if (disponibles.length === 0) {
        mensaje('No hay habitaciones disponibles.', 'error');
        if (mostrarAviso) alert('No hay habitaciones disponibles.');
      } else {
        const invalidas = seleccionadas.filter(r => !disponibles.some(h => h.id === r.id));
        if (invalidas.length) {
          mensaje('Hay habitaciones del detalle que ya no están disponibles. Elimínelas antes de guardar.', 'error');
        } else {
          const libres = disponibles.filter(h => !seleccionadas.some(r => r.id === h.id));
          mensaje(libres.length ? `${libres.length} habitación(es) disponible(s) para agregar.` : 'Todas las habitaciones disponibles ya están agregadas.', 'ok');
        }
      }
    } catch (err) {
      busquedaVigente = false;
      todas = []; disponibles = [];
      renderSelect();
      mensaje(`Error al consultar habitaciones: ${err.message}`, 'error');
    }
  };
  $('buscarHabitacionesBtn').addEventListener('click', () => buscarHabitaciones());

  $('agregarHabitacionBtn').addEventListener('click', () => {
    const id = Number($('habitacionSelect').value);
    const huesped = $('huesped').value.trim();
    const room = disponibles.find(r => r.id === id);
    if (!room) { alert('Primero busque y seleccione una habitación disponible.'); return; }
    if (!huesped) { alert('Indique el nombre del huésped.'); return; }
    if (seleccionadas.some(r => r.id === id)) { alert('Esta habitación ya fue agregada.'); return; }
    seleccionadas.push({ ...room, huesped });
    $('huesped').value = '';
    render();
    const libres = disponibles.filter(h => !seleccionadas.some(r => r.id === h.id));
    mensaje(libres.length ? 'Habitación agregada. Puede buscar y agregar otra.' : 'Todas las habitaciones disponibles ya están agregadas.', 'ok');
  });

  $('formReserva').addEventListener('submit', e => {
    render();
    if (Number($('ClienteId').value) <= 0 || !getDias() || !seleccionadas.length) {
      e.preventDefault();
      alert('Seleccione un cliente, fechas válidas y al menos una habitación.');
      return;
    }
    if (!busquedaVigente || fechaConsultada !== periodo()) {
      e.preventDefault();
      alert('Presione "Buscar habitación" para verificar la disponibilidad de las fechas seleccionadas.');
      return;
    }
    if (seleccionadas.some(r => !disponibles.some(h => h.id === r.id))) {
      e.preventDefault();
      alert('Una habitación de la reserva ya no está disponible. Quítela y vuelva a buscar.');
    }
  });

  render();
  if (seleccionadas.length) buscarHabitaciones(false);
})();
