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


        // ---------- Buscar la factura y guardar las fórmulas en SQL Server ----------
        const numeroFactura = document.getElementById("numero_factura");
        const facturaEncontrada = document.getElementById("factura_encontrada");
        const sinServidor = "No hay conexión con el servidor. Inicie el servidor y abra la página en http://localhost:5000/Formulas/Formulas_clientes.html";

        // Factura buscada con el botón Buscar (null = no se ha buscado ninguna)
        let facturaCargada = null;

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

        // Muestra de quién es la factura y carga las fórmulas que ya tenga guardadas
        async function buscarFactura() {
            const numero = numeroFactura.value.trim();
            if (!numero) return;

            let f;
            try {
                const respuesta = await fetch("/api/formulas/factura?numero=" + encodeURIComponent(numero));
                f = await respuesta.json();
                if (!respuesta.ok) {
                    facturaCargada = null;
                    facturaEncontrada.hidden = true;
                    alert(f.mensaje);
                    return;
                }
            } catch (error) {
                alert(sinServidor);
                return;
            }

            facturaCargada = f;
            limpiarFormulas();

            const guardadas = f.formulas || [];
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

            // Factura sin fórmulas: se pone el nombre del cliente de la factura para empezar
            if (guardadas.length === 0 && f.cliente) {
                campo(contenedor.querySelector(".formula"), "nombre_cliente").value = f.cliente;
            }

            facturaEncontrada.textContent = "Factura No. " + f.numero_factura
                + (f.cliente ? " de " + f.cliente : "")
                + (f.fecha_venta ? " · " + fechaCorta(f.fecha_venta) : "")
                + " · " + (guardadas.length === 1 ? "1 fórmula guardada" : guardadas.length + " fórmulas guardadas");
            facturaEncontrada.hidden = false;
        }

        // Ningún campo es obligatorio. Si se buscó la factura, sus fórmulas se reemplazan
        // por las del formulario; si solo se escribió el número, se agregan a esa factura;
        // si el número está vacío, se guardan sin factura.
        async function guardarFormulas() {
            const numero = numeroFactura.value.trim();
            const mismaFactura = facturaCargada && facturaCargada.numero_factura === numero;

            const datos = {
                idFactura: mismaFactura ? facturaCargada.id_factura : null,
                numeroFactura: numero || null,
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
                alert(cuantas + (numero ? " en la factura No. " + numero : " sin factura") + ".");
            } catch (error) {
                alert(sinServidor);
                return;
            }

            // Deja la página lista para la siguiente
            limpiarFormulas();
            numeroFactura.value = "";
            facturaCargada = null;
            facturaEncontrada.hidden = true;
            window.scrollTo(0, 0);
        }

        document.getElementById("buscar").addEventListener("click", buscarFactura);
        numeroFactura.addEventListener("keydown", function (evento) {
            if (evento.key === "Enter") buscarFactura();
        });
        document.getElementById("guardar").addEventListener("click", guardarFormulas);
