const contenedor = document.getElementById("formulas");
        let contador = 1;

        // Copia la primera fórmula, la deja vacía y le pone ids nuevos (OD_2, OI_2, ...)
        function agregarFormula() {
            contador++;
            const nueva = contenedor.querySelector(".formula").cloneNode(true);

            nueva.querySelectorAll("input").forEach(function (campo) {
                campo.value = "";
                campo.id = campo.name + "_" + contador;
            });
            // Cada etiqueta apunta al campo que tiene debajo en su misma fórmula
            nueva.querySelectorAll("label").forEach(function (etiqueta) {
                const campo = etiqueta.nextElementSibling;
                if (campo) etiqueta.htmlFor = campo.id;
            });

            nueva.querySelector(".quitar_formula").hidden = false;
            contenedor.appendChild(nueva);
            numerarFormulas();
            return nueva;
        }

        document.getElementById("agregar_formula").addEventListener("click", agregarFormula);

        // Quitar una fórmula agregada (la primera no se puede quitar)
        contenedor.addEventListener("click", function (evento) {
            if (evento.target.classList.contains("quitar_formula")) {
                evento.target.closest(".formula").remove();
                numerarFormulas();
            }
        });

        function numerarFormulas() {
            contenedor.querySelectorAll(".titulo_formula").forEach(function (titulo, i) {
                titulo.textContent = "Fórmula " + (i + 1);
            });
        }


        // ---------- Buscar factura, venta de accesorios o cliente y guardar las fórmulas ----------
        const buscador = document.getElementById("numero_factura");
        const resultados = document.getElementById("resultados");
        const facturaEncontrada = document.getElementById("factura_encontrada");
        const sinServidor = "No hay conexión con el servidor. Inicie el servidor y abra la página desde http://localhost:5000";

        // Lo que se eligió en la lista: { tipo: "factura" | "accesorios" | "cliente", id, numero, cliente, ... }
        // null = no se ha elegido nada (las fórmulas se guardan sin factura)
        let elegido = null;

        const nombresTipo = { factura: "Factura", accesorios: "Accesorios", cliente: "Cliente" };

        // Columna de SQL Server -> nombre del campo en cada fórmula
        const camposFormula = {
            nombre_cliente: "nombre_cliente",
            ojo_derecho: "OD",
            ojo_izquierdo: "OI",
            adicion: "adicion",
            distancia: "distancia",
            altura: "altura",
            tipo_lente: "tipo_lente",
            tipo_montura: "tipo_montura",
            doctor: "Doctor",
            fecha_entrega: "fecha_entrega",
            hora_entrega: "hora_entrega"
        };

        function campo(formula, nombre) {
            return formula.querySelector("[name='" + nombre + "']");
        }

        // Devuelve el valor de un campo de la fórmula, o null si está vacío
        function valor(formula, nombre) {
            const texto = campo(formula, nombre).value.trim();
            return texto === "" ? null : texto;
        }

        // "2026-11-01T00:00:00" -> "01/11/2026"
        function fechaCorta(texto) {
            return texto ? texto.substring(8, 10) + "/" + texto.substring(5, 7) + "/" + texto.substring(0, 4) : "";
        }

        function cuantasFormulas(n) {
            return n === 1 ? "1 fórmula" : (n || 0) + " fórmulas";
        }

        // "Factura No. 9099 de Ana Pérez" / "Venta de accesorios No. 12 ..." / "Cliente Ana Pérez"
        function describir(r) {
            if (r.tipo === "cliente") return "Cliente " + (r.cliente || "sin nombre");
            const nombre = r.tipo === "accesorios" ? "Venta de accesorios" : "Factura";
            return nombre + " No. " + (r.numero || "—") + (r.cliente ? " de " + r.cliente : "");
        }

        // Deja una sola fórmula vacía
        function limpiarFormulas() {
            contenedor.querySelectorAll(".formula").forEach(function (formula, i) {
                if (i > 0) formula.remove();
            });
            contenedor.querySelectorAll("input").forEach(function (campo) {
                campo.value = "";
            });
            contador = 1;
            numerarFormulas();
        }

        // Busca facturas, ventas de accesorios y clientes registrados y los muestra en una lista
        async function buscar() {
            const texto = buscador.value.trim();
            if (!texto) return;

            let lista;
            try {
                const respuesta = await fetch("/api/formulas/buscar?texto=" + encodeURIComponent(texto));
                lista = await respuesta.json();
            } catch (error) {
                alert(sinServidor);
                return;
            }

            resultados.innerHTML = "";
            if (lista.length === 0) {
                const vacio = document.createElement("li");
                vacio.className = "vacio";
                vacio.textContent = "No se encontró ninguna factura, venta de accesorios ni cliente con \"" + texto + "\".";
                resultados.appendChild(vacio);
            }
            lista.forEach(function (r) {
                const boton = document.createElement("button");
                boton.type = "button";
                boton.className = "abrir";
                const partes = [
                    ["tipo " + r.tipo, nombresTipo[r.tipo]],
                    ["num", r.tipo === "cliente" ? "" : "No. " + (r.numero || "—")],
                    ["fec", fechaCorta(r.fecha)],
                    ["cli", (r.cliente || "Sin cliente") + (r.documento ? " · " + (r.tipo_documento || "") + " " + r.documento : "")],
                    ["cnt", cuantasFormulas(r.formulas)]
                ];
                partes.forEach(function (p) {
                    const span = document.createElement("span");
                    span.className = p[0];
                    span.textContent = p[1];
                    boton.appendChild(span);
                });
                boton.addEventListener("click", function () {
                    cargar(r.tipo, r.id);
                });
                const li = document.createElement("li");
                li.appendChild(boton);
                resultados.appendChild(li);
            });
            resultados.hidden = false;
        }

        // Trae las fórmulas de lo que se eligió y las pone en el formulario
        async function cargar(tipo, id) {
            let r;
            try {
                const respuesta = await fetch("/api/formulas/" + tipo + "/" + id);
                r = await respuesta.json();
                if (!respuesta.ok) {
                    alert(r.mensaje);
                    return;
                }
            } catch (error) {
                alert(sinServidor);
                return;
            }

            elegido = r;
            limpiarFormulas();

            const guardadas = r.formulas || [];
            for (let i = 1; i < guardadas.length; i++) agregarFormula();
            contenedor.querySelectorAll(".formula").forEach(function (formula, i) {
                const fo = guardadas[i];
                if (!fo) return;
                Object.keys(camposFormula).forEach(function (columna) {
                    let texto = fo[columna] || "";
                    if (columna === "fecha_entrega") texto = texto.substring(0, 10);
                    if (columna === "hora_entrega") texto = texto.substring(0, 5);
                    campo(formula, camposFormula[columna]).value = texto;
                });
            });

            // Sin fórmulas: se pone el nombre del cliente para empezar
            if (guardadas.length === 0 && r.cliente) {
                campo(contenedor.querySelector(".formula"), "nombre_cliente").value = r.cliente;
            }

            resultados.hidden = true;
            facturaEncontrada.textContent = describir(r)
                + (r.fecha ? " · " + fechaCorta(r.fecha) : "")
                + " · " + cuantasFormulas(guardadas.length) + (guardadas.length === 1 ? " guardada" : " guardadas")
                + (r.tipo === "cliente" ? " a su nombre" : "");
            facturaEncontrada.hidden = false;
        }

        // Ningún campo es obligatorio. Si se eligió una factura, venta o cliente, sus fórmulas
        // se reemplazan por las del formulario; si no se eligió nada, se guardan sin factura.
        async function guardarFormulas() {
            if (!elegido && buscador.value.trim()) {
                alert("Elija en la lista a qué factura, venta de accesorios o cliente pertenecen las fórmulas.\n\n"
                    + "Si no pertenecen a ninguno, borre el texto del buscador y vuelva a guardar.");
                buscar();
                return;
            }

            const datos = {
                idFactura: elegido && elegido.tipo === "factura" ? elegido.id : null,
                idVentaAccesorios: elegido && elegido.tipo === "accesorios" ? elegido.id : null,
                idCliente: elegido && elegido.tipo === "cliente" ? elegido.id : null,
                formulas: Array.from(contenedor.querySelectorAll(".formula")).map(function (formula) {
                    return {
                        nombreCliente: valor(formula, "nombre_cliente"),
                        ojoDerecho: valor(formula, "OD"),
                        ojoIzquierdo: valor(formula, "OI"),
                        adicion: valor(formula, "adicion"),
                        distancia: valor(formula, "distancia"),
                        altura: valor(formula, "altura"),
                        tipoLente: valor(formula, "tipo_lente"),
                        tipoMontura: valor(formula, "tipo_montura"),
                        doctor: valor(formula, "Doctor"),
                        fechaEntrega: valor(formula, "fecha_entrega"),
                        horaEntrega: valor(formula, "hora_entrega")
                    };
                })
            };

            try {
                const respuesta = await fetch("/api/formulas", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(datos)
                });
                const resultado = await respuesta.json();
                if (!respuesta.ok) {
                    alert("No se guardaron las fórmulas. " + resultado.mensaje);
                    return;
                }
                const cuantas = resultado.guardadas === 1 ? "1 fórmula guardada" : resultado.guardadas + " fórmulas guardadas";
                alert(cuantas + (elegido ? " en: " + describir(elegido) : " sin factura") + ".");
            } catch (error) {
                alert(sinServidor);
                return;
            }

            // Deja la página lista para la siguiente
            limpiarFormulas();
            buscador.value = "";
            elegido = null;
            resultados.hidden = true;
            facturaEncontrada.hidden = true;
            window.scrollTo(0, 0);
        }

        document.getElementById("buscar").addEventListener("click", buscar);
        buscador.addEventListener("keydown", function (evento) {
            if (evento.key === "Enter") buscar();
        });
        // Si se cambia el texto del buscador, se suelta lo que se había elegido
        buscador.addEventListener("input", function () {
            if (!elegido) return;
            elegido = null;
            facturaEncontrada.hidden = true;
        });
        document.getElementById("guardar").addEventListener("click", guardarFormulas);
