const sinServidor = "No hay conexión con el servidor. Inicie el servidor y abra la página en http://localhost:5000/Trasavilidad/trasavilidad.html";

// Planilla ya guardada en esta página (null = todavía no se ha guardado)
let idGuardado = null;

// true cuando hay algo escrito que todavía no se ha guardado
let hayCambios = false;

const buscarOrden = document.getElementById("buscar_orden");
const resultados = document.getElementById("resultados");
const planillaCargada = document.getElementById("planilla_cargada");
const avisoFactura = document.getElementById("aviso_factura");
const numeroOrden = document.getElementById("numero_orden");
const planilla = document.querySelector(".planilla");

// Cada campo de la planilla tiene como id el nombre de su columna en SQL Server
const camposTexto = planilla.querySelectorAll("input[id], textarea");
const opciones = planilla.querySelectorAll("input[type='radio']");

planilla.addEventListener("input", function () { hayCambios = true; });
planilla.addEventListener("change", function () { hayCambios = true; });

// Pregunta antes de reemplazar lo que se escribió y no se ha guardado
function sePuedeReemplazar() {
    return !hayCambios || confirm("Hay cambios sin guardar en la planilla. ¿Desea descartarlos?");
}

// "2026-11-01T00:00:00" -> "01/11/2026"
function fechaCorta(texto) {
    return texto ? texto.substring(8, 10) + "/" + texto.substring(5, 7) + "/" + texto.substring(0, 4) : "";
}

function mostrarPlanillaAbierta() {
    const orden = valor("numero_orden");
    planillaCargada.textContent = "Editando la planilla" + (orden ? " de la orden " + orden : " sin N° de orden")
        + ". Al guardar se actualiza esta planilla.";
    planillaCargada.hidden = false;
}

// Deja la página lista para llenar una planilla nueva
function limpiarPlanilla() {
    camposTexto.forEach(function (campo) { campo.value = ""; });
    opciones.forEach(function (opcion) { opcion.checked = false; });
    idGuardado = null;
    hayCambios = false;
    planillaCargada.hidden = true;
    avisoFactura.hidden = true;
    resultados.hidden = true;
}

function cargarPlanilla(fila) {
    camposTexto.forEach(function (campo) {
        const texto = fila[campo.id] || "";
        campo.value = campo.type === "date" ? texto.substring(0, 10) : texto;
    });
    // La columna guarda true (Si), false (No) o null (sin marcar)
    opciones.forEach(function (opcion) {
        opcion.checked = fila[opcion.name] === (opcion.value === "si");
    });
    idGuardado = fila.id_trazabilidad;
    hayCambios = false;
    resultados.hidden = true;
    avisoFactura.hidden = true;
    mostrarPlanillaAbierta();
}

// Muestra una lista de botones para elegir entre varias opciones
function mostrarLista(titulo, elementos) {
    resultados.replaceChildren();
    const aviso = document.createElement("p");
    aviso.textContent = titulo;
    resultados.appendChild(aviso);
    elementos.forEach(function (elemento) {
        const boton = document.createElement("button");
        boton.type = "button";
        boton.textContent = elemento.texto;
        boton.addEventListener("click", elemento.alElegir);
        resultados.appendChild(boton);
    });
    resultados.hidden = false;
}

// Con varias planillas del mismo N° de orden se muestra la lista para elegir
function mostrarResultados(filas) {
    mostrarLista(filas.length + " planillas con este N° de orden. Elija una:", filas.map(function (fila) {
        return {
            texto: [
                "Orden " + fila.numero_orden,
                fila.nombre_paciente,
                fechaCorta(fila.fecha_adquirencia),
                "guardada el " + fechaCorta(fila.creado_en)
            ].filter(Boolean).join(" · "),
            alElegir: function () {
                if (sePuedeReemplazar()) cargarPlanilla(fila);
            }
        };
    }));
}

function mostrarAvisoFactura(texto) {
    avisoFactura.textContent = texto;
    avisoFactura.hidden = false;
}

// El lente sale de la fórmula. Si no lo tiene, se usa el de los productos de la factura
// solo cuando hay uno; con varios no se sabe cuál corresponde y se deja vacío.
function lenteDeFactura(factura, formula) {
    if (formula && formula.tipo_lente) return formula.tipo_lente;
    const lentes = factura.productos.filter(function (producto) { return producto.tipo_lente; });
    return lentes.length === 1 ? lentes[0].tipo_lente : "";
}

// Pasa los datos de la factura a la planilla. Pregunta antes de cambiar lo que ya esté lleno.
function aplicarFactura(factura, formula) {
    const f = formula || {};
    const datos = {
        nombre_paciente: f.nombre_cliente || factura.cliente || "",
        fecha_adquirencia: (factura.fecha_venta || "").substring(0, 10),
        prescripcion_od: f.ojo_derecho || "",
        prescripcion_oi: f.ojo_izquierdo || "",
        prescripcion_add: f.adicion || "",
        material: lenteDeFactura(factura, formula)
    };

    const ids = Object.keys(datos);
    const cambiaAlgo = ids.some(function (id) {
        const actual = document.getElementById(id).value.trim();
        return actual !== "" && actual !== datos[id];
    });
    resultados.hidden = true;
    if (cambiaAlgo && !confirm("La planilla ya tiene datos. ¿Desea reemplazarlos con los de la factura No. " + factura.numero_factura + "?")) {
        mostrarAvisoFactura("No se cambiaron los datos de la planilla.");
        return;
    }

    ids.forEach(function (id) { document.getElementById(id).value = datos[id]; });
    hayCambios = true;
    mostrarAvisoFactura("Datos traídos de la factura No. " + factura.numero_factura + "."
        + (formula ? "" : " Esta factura no tiene fórmula guardada: llene la prescripción a mano."));
}

// Al escribir el N° de orden se buscan los datos de la factura con ese mismo número
async function traerFactura() {
    const numero = numeroOrden.value.trim();
    avisoFactura.hidden = true;
    resultados.hidden = true;
    if (!numero) return;

    let factura;
    try {
        const respuesta = await fetch("/api/formulas/factura?numero=" + encodeURIComponent(numero));
        factura = await respuesta.json();
        // Se cambió el número mientras llegaba la respuesta: esta ya no sirve
        if (numeroOrden.value.trim() !== numero) return;
        if (!respuesta.ok) {
            mostrarAvisoFactura(factura.mensaje + " Llene los datos a mano.");
            return;
        }
    } catch (error) {
        alert(sinServidor);
        return;
    }

    // Una factura puede tener varias fórmulas y la planilla lleva solo una
    if (factura.formulas.length > 1) {
        mostrarLista("La factura No. " + factura.numero_factura + " tiene " + factura.formulas.length + " fórmulas. Elija una:",
            factura.formulas.map(function (formula) {
                return {
                    texto: [
                        formula.nombre_cliente,
                        "OD " + (formula.ojo_derecho || "-"),
                        "OI " + (formula.ojo_izquierdo || "-"),
                        "ADD " + (formula.adicion || "-"),
                        formula.tipo_lente
                    ].filter(Boolean).join(" · "),
                    alElegir: function () { aplicarFactura(factura, formula); }
                };
            }));
    } else {
        aplicarFactura(factura, factura.formulas[0] || null);
    }
}

numeroOrden.addEventListener("change", traerFactura);
numeroOrden.addEventListener("keydown", function (evento) {
    if (evento.key === "Enter") numeroOrden.blur();
});

async function buscarPlanilla() {
    const orden = buscarOrden.value.trim();
    if (!orden) return;

    let filas;
    try {
        const respuesta = await fetch("/api/trazabilidad/buscar?orden=" + encodeURIComponent(orden));
        filas = await respuesta.json();
    } catch (error) {
        alert(sinServidor);
        return;
    }

    resultados.hidden = true;
    if (filas.length === 0) {
        alert("No hay planillas con el N° de orden " + orden + ".");
    } else if (filas.length > 1) {
        mostrarResultados(filas);
    } else if (sePuedeReemplazar()) {
        cargarPlanilla(filas[0]);
    }
}

document.getElementById("buscar").addEventListener("click", buscarPlanilla);
buscarOrden.addEventListener("keydown", function (evento) {
    if (evento.key === "Enter") buscarPlanilla();
});

document.getElementById("nueva").addEventListener("click", function () {
    if (!sePuedeReemplazar()) return;
    limpiarPlanilla();
    buscarOrden.value = "";
    window.scrollTo(0, 0);
});

// Devuelve el valor de un campo, o null si está vacío
function valor(id) {
    const texto = document.getElementById(id).value.trim();
    return texto === "" ? null : texto;
}

// Si -> true, No -> false, sin marcar -> null
function decision(nombre) {
    const marcado = document.querySelector("input[name='" + nombre + "']:checked");
    return marcado ? marcado.value === "si" : null;
}

function leerPlanilla() {
    return {
        numeroOrden: valor("numero_orden"),
        fechaAdquirencia: valor("fecha_adquirencia"),
        nombrePaciente: valor("nombre_paciente"),
        proveedor: valor("proveedor"),
        fechaEnvio: valor("fecha_envio"),
        prescripcionOd: valor("prescripcion_od"),
        prescripcionOi: valor("prescripcion_oi"),
        prescripcionAdd: valor("prescripcion_add"),
        fechaLlegada: valor("fecha_llegada"),
        material: valor("material"),
        montura: valor("montura"),
        invima1: valor("invima_1"),
        invima2: valor("invima_2"),
        lote1: valor("lote_1"),
        lote2: valor("lote_2"),
        lensometriaOd: valor("lensometria_od"),
        lensometriaOi: valor("lensometria_oi"),
        lensometriaAdd: valor("lensometria_add"),
        aprobado: decision("aprobado"),
        devolucion: decision("devolucion")
    };
}

// Ningún campo es obligatorio, pero no se guarda una planilla vacía. Los datos quedan
// en pantalla para poder imprimirlos; si se guarda otra vez, se actualiza la misma planilla.
async function guardarPlanilla() {
    const datos = leerPlanilla();
    if (Object.values(datos).every(function (dato) { return dato === null; })) {
        alert("La planilla está vacía. Llene al menos un campo antes de guardar.");
        return;
    }

    try {
        const respuesta = await fetch(idGuardado === null ? "/api/trazabilidad" : "/api/trazabilidad/" + idGuardado, {
            method: idGuardado === null ? "POST" : "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(datos)
        });
        const resultado = await respuesta.json();
        if (!respuesta.ok) {
            alert("No se guardó la planilla. " + resultado.mensaje);
            return;
        }
        idGuardado = resultado.idTrazabilidad;
        hayCambios = false;
        mostrarPlanillaAbierta();
        alert(resultado.mensaje);
    } catch (error) {
        alert(sinServidor);
    }
}

document.getElementById("guardar").addEventListener("click", guardarPlanilla);

// Abre la ventana de impresión del navegador (también permite guardar como PDF)
document.getElementById("imprimir").addEventListener("click", function () {
    window.print();
});
