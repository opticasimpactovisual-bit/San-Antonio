const contenedor = document.getElementById("formulas");
        let contador = 1;

        // Copia la primera fórmula, la deja vacía y le pone ids nuevos (OD_2, OI_2, ...)
        document.getElementById("agregar_formula").addEventListener("click", function () {
            contador++;
            const nueva = contenedor.querySelector(".formula").cloneNode(true);

            nueva.querySelectorAll("input").forEach(function (campo) {
                campo.value = "";
                campo.id = campo.name + "_" + contador;
            });
            nueva.querySelectorAll("label").forEach(function (etiqueta) {
                etiqueta.htmlFor = etiqueta.htmlFor + "_" + contador;
            });

            nueva.querySelector(".quitar_formula").hidden = false;
            contenedor.appendChild(nueva);
            numerarFormulas();
        });

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