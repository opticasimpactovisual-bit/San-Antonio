        const total = document.getElementById("Total");
        const abono = document.getElementById("Abono");
        const saldo = document.getElementById("Saldo");

        // Convierte "1.500.000" en el número 1500000
        function leerPesos(campo) {
            return Number(campo.value.replace(/\D/g, "")) || 0;
        }

        // Formatea un campo como pesos colombianos: 1500000 -> 1.500.000
        function formatearPesos(campo) {
            const digitos = campo.value.replace(/\D/g, "");
            campo.value = digitos ? Number(digitos).toLocaleString("es-CO") : "";
        }

        // Saldo = Total - Abono
        function calcularSaldo() {
            const resultado = leerPesos(total) - leerPesos(abono);
            saldo.value = resultado.toLocaleString("es-CO");
        }

        total.addEventListener("input", function () {
            formatearPesos(total);
            calcularSaldo();
        });

        abono.addEventListener("input", function () {
            formatearPesos(abono);
            calcularSaldo();
        });

        // Devuelve el valor de un campo, o null si está vacío
        function valor(id) {
            const texto = document.getElementById(id).value.trim();
            return texto === "" ? null : texto;
        }

        // Envía la factura al servidor, que la guarda en SQL Server.
        // Ningún campo es obligatorio y el número de factura se puede repetir.
        async function guardarFactura() {
            const factura = {
                numeroFactura: valor("N_Factura"),
                fechaVenta: valor("fecha"),
                total: leerPesos(total),
                abono: leerPesos(abono),
                cliente: {
                    nombre: valor("Cliente"),
                    tipoDocumento: valor("Tipo_documento"),
                    documento: valor("Documento"),
                    telefono: valor("Telefono"),
                    email: valor("email")
                },
                // Un producto por cada grupo de "Agregar otro producto"
                productos: Array.from(document.querySelectorAll(".producto")).map(function (grupo) {
                    return {
                        tipoMontura: grupo.querySelector("select").value || null,
                        color: grupo.querySelector("[id^='Color']").value.trim() || null,
                        referencia: grupo.querySelector("[id^='Referencia']").value.trim() || null,
                        tipoLente: grupo.querySelector("textarea").value.trim() || null
                    };
                }),
                fechaEntrega: valor("FechaEntrega"),
                horaEntrega: valor("HoraEntrega"),
                detalleEntrega: valor("Detalle"),
                vendedor: valor("Vendedor")
            };

            try {
                const respuesta = await fetch("/api/facturas", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(factura)
                });
                const datos = await respuesta.json();
                if (!respuesta.ok) {
                    alert("No se guardó la factura. " + datos.mensaje);
                    return;
                }
                const numero = factura.numeroFactura ? "No. " + factura.numeroFactura + " " : "";
                alert("Factura " + numero + "guardada en la base de datos (código interno " + datos.idFactura + ").");
            } catch (error) {
                alert("No hay conexión con el servidor. Inicie el servidor y abra la página en http://localhost:5000");
            }
        }

        document.getElementById("guardar").addEventListener("click", guardarFactura);

        // Abre la ventana de impresión del navegador (también permite guardar como PDF)
        document.getElementById("imprimir").addEventListener("click", function () {
            window.print();
        });


        // Agrega otro grupo de montura, color, referencia y lente copiando el primero
        const productos = document.getElementById("productos");
        let contadorProductos = 1;

        document.getElementById("agregarProducto").addEventListener("click", function () {
            contadorProductos++;
            const nuevo = productos.querySelector(".producto").cloneNode(true);

            // Cada campo necesita un id distinto: Montura_2, Color_2, ...
            nuevo.querySelectorAll("input, select, textarea").forEach(function (campo) {
                campo.id = campo.id + "_" + contadorProductos;
                campo.name = campo.id;
                campo.value = "";
                if (campo.tagName === "SELECT") campo.selectedIndex = 0;
            });
            nuevo.querySelectorAll("label").forEach(function (etiqueta) {
                etiqueta.htmlFor = etiqueta.htmlFor + "_" + contadorProductos;
            });

            const quitar = nuevo.querySelector(".quitar");
            quitar.hidden = false;
            quitar.addEventListener("click", function () {
                nuevo.remove();
            });

            productos.appendChild(nuevo);
        });
