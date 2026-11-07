// Conexion del formulario de inventarios con el servidor (carpeta Servidor, dotnet run)
// Si la pagina se abre desde el servidor usa la misma direccion; si se abre como archivo, va al puerto 5000
const API = location.protocol.startsWith('http') ? '/api/monturas' : 'http://localhost:5000/api/monturas';

const campo = (id) => document.getElementById(id);
const mensaje = campo('mensaje');
const cuerpoTabla = campo('tablaMonturas');

const formatoPesos = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 });

function mostrarMensaje(texto, tipo) {
    mensaje.textContent = texto;
    mensaje.className = 'mensaje ' + tipo;
}

// Acepta valores como "120.000" o "120000,50"
function leerNumero(id) {
    const texto = campo(id).value.trim();
    if (!texto) return null;
    return Number(texto.replace(/\./g, '').replace(',', '.'));
}

function leerFormulario() {
    return {
        Ubicacion: campo('ubicacion').value.trim(),
        Referencia: campo('Referencia').value.trim(),
        FechaIngreso: campo('FechaIngreso').value || null,
        Cantidad: campo('cantidad').value ? Number(campo('cantidad').value) : 0,
        ValorCosto: leerNumero('ValorCosto'),
        ValorCliente: leerNumero('ValorCliente'),
        Material: campo('Material').value.trim(),
        Forma: campo('Forma').value.trim(),
        EstadoMontura: campo('EstadoMontura').value.trim() || 'Nueva',
        Marca: campo('Marca').value.trim(),
        EsReplica: campo('EsReplica').value.trim() === 'Si',
        NombreReplica: campo('NombreReplica').value.trim(),
        Color: campo('Color').value.trim()
    };
}

function validar(m) {
    if (!m.Ubicacion || !m.Referencia) return 'La ubicación y la referencia son obligatorias.';
    if (m.ValorCosto === null || m.ValorCliente === null) return 'El valor de costo y el valor al cliente son obligatorios.';
    if (Number.isNaN(m.ValorCosto) || Number.isNaN(m.ValorCliente)) return 'Los valores deben ser números.';
    if (m.ValorCosto < 0 || m.ValorCliente < 0 || m.Cantidad < 0) return 'Los valores y la cantidad no pueden ser negativos.';
    if (!['Nueva', 'Usada', 'Exhibición', 'Dañada', 'Vendida'].includes(m.EstadoMontura)) return 'Estado de montura no válido.';
    return null;
}

function limpiarFormulario() {
    document.querySelectorAll('input').forEach((input) => { input.value = ''; });
    campo('ubicacion').focus();
}

async function agregarMontura() {
    const montura = leerFormulario();
    const error = validar(montura);
    if (error) return mostrarMensaje(error, 'error');

    const boton = campo('agregar');
    boton.disabled = true;
    try {
        const respuesta = await fetch(API, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(montura)
        });
        const datos = await respuesta.json();
        if (!respuesta.ok) throw new Error(datos.mensaje || 'No se pudo guardar la montura.');
        mostrarMensaje(`Montura "${datos.referencia}" guardada.`, 'exito');
        limpiarFormulario();
        cargarMonturas();
    } catch (err) {
        mostrarMensaje(err.message === 'Failed to fetch' ? 'No hay conexión con el servidor. Enciéndalo con dotnet run en la carpeta Servidor.' : err.message, 'error');
    } finally {
        boton.disabled = false;
    }
}

async function borrarMontura(id, referencia) {
    if (!confirm(`¿Borrar la montura "${referencia}"?`)) return;
    try {
        const respuesta = await fetch(`${API}/${id}`, { method: 'DELETE' });
        if (!respuesta.ok) throw new Error('No se pudo borrar la montura.');
        mostrarMensaje(`Montura "${referencia}" borrada.`, 'exito');
        cargarMonturas();
    } catch (err) {
        mostrarMensaje(err.message, 'error');
    }
}

function celda(texto, clase) {
    const td = document.createElement('td');
    td.textContent = texto ?? '';
    if (clase) td.className = clase;
    return td;
}

async function cargarMonturas() {
    try {
        const respuesta = await fetch(API);
        if (!respuesta.ok) throw new Error();
        const monturas = await respuesta.json();
        cuerpoTabla.innerHTML = '';
        if (!monturas.length) {
            cuerpoTabla.innerHTML = '<tr><td colspan="11" class="vacio">Todavía no hay monturas registradas.</td></tr>';
            return;
        }
        monturas.forEach((m) => {
            const fila = document.createElement('tr');
            fila.append(
                celda(m.Ubicacion),
                celda(m.Referencia),
                celda((m.Marca || '') + (m.EsReplica ? ` (réplica ${m.NombreReplica || ''})` : '')),
                celda(m.Material),
                celda(m.Forma),
                celda(m.Color),
                celda(m.EstadoMontura),
                celda(m.Cantidad, 'numero'),
                celda(formatoPesos.format(m.ValorCosto), 'numero'),
                celda(formatoPesos.format(m.ValorCliente), 'numero')
            );
            const acciones = document.createElement('td');
            const botonBorrar = document.createElement('button');
            botonBorrar.textContent = 'Borrar';
            botonBorrar.className = 'btn-borrar';
            botonBorrar.addEventListener('click', () => borrarMontura(m.IdMontura, m.Referencia));
            acciones.append(botonBorrar);
            fila.append(acciones);
            cuerpoTabla.append(fila);
        });
    } catch {
        cuerpoTabla.innerHTML = '<tr><td colspan="11" class="vacio">No se pudo cargar el inventario. ¿Está encendido el servidor?</td></tr>';
    }
}

campo('agregar').addEventListener('click', agregarMontura);
cargarMonturas();
