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

        // Guarda la factura en el navegador (localStorage) como una lista.
        // Ningún campo es obligatorio y el número de factura se puede repetir.
        function guardarFactura() {
            const numero = document.getElementById("N_Factura").value.trim();

            const factura = { guardadaEn: new Date().toISOString() };
            document.querySelectorAll("input, select, textarea").forEach(function (campo) {
                factura[campo.id] = campo.value;
            });

            let facturas = JSON.parse(localStorage.getItem("facturas") || "[]");
            // Las facturas guardadas con la versión anterior estaban por número: se pasan a la lista
            if (!Array.isArray(facturas)) facturas = Object.values(facturas);

            facturas.push(factura);
            localStorage.setItem("facturas", JSON.stringify(facturas));
            alert(numero ? "Factura No. " + numero + " guardada." : "Factura guardada (sin número).");
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
