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

        // Guarda la factura en el navegador (localStorage), usando el número de factura como clave
        function guardarFactura() {
            const numero = document.getElementById("N_Factura").value.trim();
            const cliente = document.getElementById("Cliente").value.trim();

            if (!numero || !cliente || !leerPesos(total)) {
                alert("Para guardar, llene el número de factura, el nombre del cliente y el total.");
                return;
            }

            const factura = {};
            document.querySelectorAll("input, select").forEach(function (campo) {
                factura[campo.id] = campo.value;
            });

            const facturas = JSON.parse(localStorage.getItem("facturas") || "{}");
            if (facturas[numero] && !confirm("La factura No. " + numero + " ya existe. ¿Desea reemplazarla?")) {
                return;
            }
            facturas[numero] = factura;
            localStorage.setItem("facturas", JSON.stringify(facturas));
            alert("Factura No. " + numero + " guardada.");
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
            nuevo.querySelectorAll("input, select").forEach(function (campo) {
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
