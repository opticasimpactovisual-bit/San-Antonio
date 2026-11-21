// Servidor de la óptica - Centro Óptico San Antonio
// Muestra la página de inicio (Inicio/index.html) y todos los programas, y guarda en SQL Server.
//
//   POST /api/facturas                  guarda una factura con su cliente y sus productos
//   PUT  /api/facturas/{id}             guarda los cambios de una factura anterior
//   GET  /api/facturas                  lista las últimas 50 facturas
//   GET  /api/facturas/buscar?texto=..  busca por número de factura, documento o nombre
//   GET  /api/facturas/{id}             muestra una factura con todos sus productos
//   DELETE /api/facturas/{id}           elimina una factura con sus productos y fórmulas
//   /api/ventas-accesorios ...          lo mismo que /api/facturas, para las ventas de Acesorios.html
//                                       (tablas ventas_accesorios, venta_accesorios_detalle y venta_accesorios_productos)
//   GET  /api/formulas/factura?numero=.. busca una factura por número y trae sus fórmulas
//   POST /api/formulas                  guarda las fórmulas de la página Formulas_clientes.html
//   GET  /api/trazabilidad/buscar?orden=.. busca las planillas de un número de orden
//   POST /api/trazabilidad              guarda una planilla de la página trasavilidad.html
//   PUT  /api/trazabilidad/{id}         guarda los cambios de una planilla ya guardada
//   GET  /api/monturas                  lista las monturas del inventario
//   POST /api/monturas                  guarda una montura de la página Inventarios.html
//   DELETE /api/monturas/{id}           borra una montura del inventario
//   GET  /api/accesorios                lista los accesorios del inventario
//   POST /api/accesorios                guarda un accesorio en el inventario
//   DELETE /api/accesorios/{id}         borra un accesorio del inventario

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var conexion = builder.Configuration.GetConnectionString("Optica")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'Optica' en appsettings.json");

await PrepararTablaAccesorios();

// La página de inicio (carpeta Inicio) se abre en http://localhost:5000 y lleva a todos los programas
var carpetaInicio = new PhysicalFileProvider(
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "Inicio")));
var paginaInicio = new DefaultFilesOptions { FileProvider = carpetaInicio };
paginaInicio.DefaultFileNames.Clear();
paginaInicio.DefaultFileNames.Add("index.html");
app.UseDefaultFiles(paginaInicio);
app.UseStaticFiles(new StaticFileOptions { FileProvider = carpetaInicio });

// La factura (carpeta Facturacion) se abre en /Facturacion/Facturacion.html
var carpetaPagina = new PhysicalFileProvider(
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "Facturacion")));

// La página de fórmulas (carpeta Formulas) se abre en /Formulas/Formulas_clientes.html.
// Facturacion queda en /Facturacion; las demás páginas también toman el logo de ahí.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "Formulas"))),
    RequestPath = "/Formulas"
});
app.UseStaticFiles(new StaticFileOptions { FileProvider = carpetaPagina, RequestPath = "/Facturacion" });

// La planilla de trazabilidad (carpeta Trasavilidad) se abre en /Trasavilidad/trasavilidad.html.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "Trasavilidad"))),
    RequestPath = "/Trasavilidad"
});

// El inventario de monturas (carpeta Invetarios) se abre en /Invetarios/Inventarios.html.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "Invetarios"))),
    RequestPath = "/Invetarios"
});

// La página de accesorios (carpeta Accesorios) se abre en /Accesorios/Acesorios.html.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "Accesorios"))),
    RequestPath = "/Accesorios"
});


// ---------- Facturas (Facturacion.html) y ventas de accesorios (Acesorios.html) ----------
// Las dos páginas usan el mismo formulario, pero cada una guarda en sus propias tablas.
var tablasFacturas = new TablasVenta("dbo.facturas", "id_factura", "dbo.factura_productos", null, "factura", false);
var tablasAccesorios = new TablasVenta("dbo.ventas_accesorios", "id_venta", "dbo.venta_accesorios_productos",
    "dbo.venta_accesorios_detalle", "venta de accesorios", true);
MapearVentas("/api/facturas", tablasFacturas);
MapearVentas("/api/ventas-accesorios", tablasAccesorios);


// Crea las direcciones para guardar, editar, buscar, listar, ver y eliminar en las tablas indicadas
void MapearVentas(string ruta, TablasVenta t)
{
    app.MapPost(ruta, (FacturaDto f) => GuardarVenta(t, f, null));
    app.MapPut(ruta + "/{id:int}", (int id, FacturaDto f) => GuardarVenta(t, f, id));

    // Buscar por número, documento o nombre del cliente
    app.MapGet(ruta + "/buscar", async (string? texto) =>
    {
        if (string.IsNullOrWhiteSpace(texto)) return Results.Ok(new List<Dictionary<string, object?>>());

        await using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();
        var cmd = new SqlCommand($@"
            SELECT TOP 30 f.{t.Id} AS id_factura, f.numero_factura, f.fecha_venta, c.nombre AS cliente,
                   c.tipo_documento, c.documento, f.total, f.saldo
            FROM {t.Encabezado} f
            LEFT JOIN dbo.clientes c ON c.id_cliente = f.id_cliente
            WHERE f.numero_factura = @texto
               OR c.documento = @texto
               OR c.nombre LIKE '%' + @texto + '%'
            ORDER BY f.{t.Id} DESC;", cn);
        cmd.Parameters.AddWithValue("@texto", texto.Trim());
        return Results.Ok(await LeerFilas(cmd));
    });

    // Listar las últimas 50
    app.MapGet(ruta, async () =>
    {
        await using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();
        var cmd = new SqlCommand($@"
            SELECT TOP 50 f.{t.Id} AS id_factura, f.numero_factura, f.fecha_venta, c.nombre AS cliente,
                   f.total, f.abono, f.saldo,
                   (SELECT COUNT(*) FROM {t.Productos} p WHERE p.{t.Id} = f.{t.Id}) AS productos
            FROM {t.Encabezado} f
            LEFT JOIN dbo.clientes c ON c.id_cliente = f.id_cliente
            ORDER BY f.{t.Id} DESC;", cn);
        return Results.Ok(await LeerFilas(cmd));
    });

    // Ver una con sus productos (y sus accesorios, si la página los tiene)
    app.MapGet(ruta + "/{id:int}", async (int id) =>
    {
        await using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();

        var cmd = new SqlCommand($@"
            SELECT f.*, f.{t.Id} AS id_factura, c.nombre AS cliente, c.tipo_documento, c.documento, c.telefono, c.email
            FROM {t.Encabezado} f
            LEFT JOIN dbo.clientes c ON c.id_cliente = f.id_cliente
            WHERE f.{t.Id} = @id;", cn);
        cmd.Parameters.AddWithValue("@id", id);
        var venta = (await LeerFilas(cmd)).FirstOrDefault();
        if (venta is null) return Results.NotFound(new { mensaje = $"No existe la {t.Nombre} {id}" });

        var cmdProductos = new SqlCommand(
            $"SELECT * FROM {t.Productos} WHERE {t.Id} = @id ORDER BY id_producto;", cn);
        cmdProductos.Parameters.AddWithValue("@id", id);
        venta["productos"] = await LeerFilas(cmdProductos);

        if (t.Detalle is not null)
        {
            var cmdDetalle = new SqlCommand(
                $"SELECT * FROM {t.Detalle} WHERE {t.Id} = @id ORDER BY id_detalle;", cn);
            cmdDetalle.Parameters.AddWithValue("@id", id);
            venta["accesorios"] = await LeerFilas(cmdDetalle);
        }

        return Results.Ok(venta);
    });

    // Eliminar: sus productos, accesorios y fórmulas se borran solos (ON DELETE CASCADE). El cliente no se borra.
    app.MapDelete(ruta + "/{id:int}", async (int id) =>
    {
        await using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();
        var cmd = new SqlCommand($"DELETE FROM {t.Encabezado} WHERE {t.Id} = @id;", cn);
        cmd.Parameters.AddWithValue("@id", id);
        return await cmd.ExecuteNonQueryAsync() == 0
            ? Results.NotFound(new { mensaje = $"No existe la {t.Nombre} {id}" })
            : Results.Ok(new { mensaje = "Eliminada." });
    });
}


// Si idExistente es null crea la venta; si no, la actualiza y reemplaza sus productos y accesorios
async Task<IResult> GuardarVenta(TablasVenta t, FacturaDto f, int? idExistente)
{
    try
    {
        await using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();

        int? idCliente = await GuardarCliente(cn, tx, f.Cliente);

        var cmd = idExistente is null
            ? new SqlCommand($@"
                INSERT INTO {t.Encabezado} (numero_factura, fecha_venta, id_cliente, total, abono, medio_pago,
                                          fecha_entrega, hora_entrega, detalle_entrega, vendedor)
                OUTPUT INSERTED.{t.Id}
                VALUES (@numero, @fecha, @cliente, @total, @abono, @medioPago,
                        @fechaEntrega, @horaEntrega, @detalle, @vendedor);", cn, tx)
            : new SqlCommand($@"
                UPDATE {t.Encabezado}
                SET numero_factura = @numero, fecha_venta = @fecha, id_cliente = @cliente,
                    total = @total, abono = @abono, medio_pago = @medioPago,
                    fecha_entrega = @fechaEntrega, hora_entrega = @horaEntrega,
                    detalle_entrega = @detalle, vendedor = @vendedor
                OUTPUT INSERTED.{t.Id}
                WHERE {t.Id} = @id;", cn, tx);
        if (idExistente is not null) cmd.Parameters.AddWithValue("@id", idExistente);
        cmd.Parameters.AddWithValue("@numero", Valor(f.NumeroFactura));
        cmd.Parameters.AddWithValue("@fecha", (object?)f.FechaVenta ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cliente", (object?)idCliente ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@total", f.Total ?? 0);
        cmd.Parameters.AddWithValue("@abono", f.Abono ?? 0);
        cmd.Parameters.AddWithValue("@medioPago", Valor(f.MedioPago));
        cmd.Parameters.AddWithValue("@fechaEntrega", (object?)f.FechaEntrega ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@horaEntrega", (object?)f.HoraEntrega?.ToTimeSpan() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@detalle", Valor(f.DetalleEntrega));
        cmd.Parameters.AddWithValue("@vendedor", Valor(f.Vendedor));
        if (await cmd.ExecuteScalarAsync() is not int idVenta)
            return Results.NotFound(new { mensaje = $"No existe la {t.Nombre} {idExistente}" });

        // Al editar, los productos y accesorios anteriores se borran y se guardan los del formulario
        if (idExistente is not null)
        {
            var borrar = new SqlCommand($"DELETE FROM {t.Productos} WHERE {t.Id} = @id;", cn, tx);
            borrar.Parameters.AddWithValue("@id", idVenta);
            await borrar.ExecuteNonQueryAsync();

            if (t.Detalle is not null)
            {
                var borrarAcc = new SqlCommand($"DELETE FROM {t.Detalle} WHERE {t.Id} = @id;", cn, tx);
                borrarAcc.Parameters.AddWithValue("@id", idVenta);
                await borrarAcc.ExecuteNonQueryAsync();
            }
        }

        // Una fila por cada grupo de "Agregar otro producto".
        // En Accesorios la montura siempre trae un tipo elegido, así que solo se guarda
        // si se llenó el color, la referencia o el lente.
        var productos = (f.Productos ?? []).Where(p => !t.SoloProductosConDatos
            || new[] { p.Color, p.Referencia, p.TipoLente }.Any(v => !string.IsNullOrWhiteSpace(v)));
        foreach (var p in productos)
        {
            var cmdProducto = new SqlCommand($@"
                INSERT INTO {t.Productos} ({t.Id}, tipo_montura, color, referencia, tipo_lente)
                VALUES (@venta, @montura, @color, @referencia, @lente);", cn, tx);
            cmdProducto.Parameters.AddWithValue("@venta", idVenta);
            cmdProducto.Parameters.AddWithValue("@montura", Valor(p.TipoMontura));
            cmdProducto.Parameters.AddWithValue("@color", Valor(p.Color));
            cmdProducto.Parameters.AddWithValue("@referencia", Valor(p.Referencia));
            cmdProducto.Parameters.AddWithValue("@lente", Valor(p.TipoLente));
            await cmdProducto.ExecuteNonQueryAsync();
        }

        // Accesorios: solo se guardan las filas que tienen tipo de producto
        if (t.Detalle is not null)
        {
            foreach (var a in (f.Accesorios ?? []).Where(a => !string.IsNullOrWhiteSpace(a.TipoProducto)))
            {
                var cmdAccesorio = new SqlCommand($@"
                    INSERT INTO {t.Detalle} ({t.Id}, tipo_producto, referencia, cantidad)
                    VALUES (@venta, @tipo, @referencia, @cantidad);", cn, tx);
                cmdAccesorio.Parameters.AddWithValue("@venta", idVenta);
                cmdAccesorio.Parameters.AddWithValue("@tipo", a.TipoProducto!.Trim());
                cmdAccesorio.Parameters.AddWithValue("@referencia", Valor(a.Referencia));
                cmdAccesorio.Parameters.AddWithValue("@cantidad", a.Cantidad is > 0 ? a.Cantidad.Value : 1);
                await cmdAccesorio.ExecuteNonQueryAsync();
            }
        }

        await tx.CommitAsync();
        var hecho = idExistente is null ? "guardada" : "actualizada";
        return Results.Ok(new { idFactura = idVenta, mensaje = $"{char.ToUpper(t.Nombre[0])}{t.Nombre[1..]} {hecho}." });
    }
    catch (SqlException ex) when (ex.Number == 547)
    {
        // 547 = no cumple una regla de la tabla (CHECK)
        var mensaje = ex.Message.Contains("abono_no_mayor")
            ? "El abono no puede ser mayor que el total."
            : "Algún valor no es válido: " + ex.Message;
        return Results.BadRequest(new { mensaje });
    }
    catch (SqlException ex)
    {
        app.Logger.LogError(ex, "Error de SQL Server al guardar la {Nombre}", t.Nombre);
        return Results.Json(new { mensaje = "No se pudo guardar en SQL Server: " + ex.Message }, statusCode: 500);
    }
}


// ---------- Buscar una factura por número y traer sus fórmulas y productos ----------
// El número de factura se puede repetir: se toma la más reciente.
app.MapGet("/api/formulas/factura", async (string? numero) =>
{
    if (string.IsNullOrWhiteSpace(numero))
        return Results.BadRequest(new { mensaje = "Escriba el número de factura." });

    await using var cn = new SqlConnection(conexion);
    await cn.OpenAsync();

    var cmd = new SqlCommand(@"
        SELECT TOP 1 f.id_factura, f.numero_factura, f.fecha_venta, c.nombre AS cliente
        FROM dbo.facturas f
        LEFT JOIN dbo.clientes c ON c.id_cliente = f.id_cliente
        WHERE f.numero_factura = @numero
        ORDER BY f.id_factura DESC;", cn);
    cmd.Parameters.AddWithValue("@numero", numero.Trim());
    var factura = (await LeerFilas(cmd)).FirstOrDefault();
    if (factura is null)
        return Results.NotFound(new { mensaje = "No existe la factura No. " + numero.Trim() + "." });

    var cmdFormulas = new SqlCommand(
        "SELECT * FROM dbo.formulas WHERE id_factura = @id ORDER BY id_formula;", cn);
    cmdFormulas.Parameters.AddWithValue("@id", factura["id_factura"]);
    factura["formulas"] = await LeerFilas(cmdFormulas);

    var cmdProductos = new SqlCommand(
        "SELECT * FROM dbo.factura_productos WHERE id_factura = @id ORDER BY id_producto;", cn);
    cmdProductos.Parameters.AddWithValue("@id", factura["id_factura"]);
    factura["productos"] = await LeerFilas(cmdProductos);

    return Results.Ok(factura);
});


// ---------- Guardar las fórmulas de la página ----------
//   IdFactura lleno      -> la factura se buscó: sus fórmulas se reemplazan por las del formulario
//   solo NumeroFactura   -> se busca la factura y las fórmulas se agregan a las que ya tenga
//   ninguno de los dos   -> las fórmulas se guardan sin factura
app.MapPost("/api/formulas", async (GuardarFormulasDto d) =>
{
    try
    {
        await using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();

        int? idFactura = d.IdFactura;
        if (idFactura is not null)
        {
            var borrar = new SqlCommand("DELETE FROM dbo.formulas WHERE id_factura = @id;", cn, tx);
            borrar.Parameters.AddWithValue("@id", idFactura);
            await borrar.ExecuteNonQueryAsync();
        }
        else if (!string.IsNullOrWhiteSpace(d.NumeroFactura))
        {
            var buscar = new SqlCommand(@"
                SELECT TOP 1 id_factura FROM dbo.facturas
                WHERE numero_factura = @numero ORDER BY id_factura DESC;", cn, tx);
            buscar.Parameters.AddWithValue("@numero", d.NumeroFactura.Trim());
            idFactura = await buscar.ExecuteScalarAsync() as int?;
            if (idFactura is null)
                return Results.NotFound(new { mensaje = "No existe la factura No. " + d.NumeroFactura.Trim()
                    + ". Revise el número, o déjelo vacío para guardar las fórmulas sin factura." });
        }

        int guardadas = 0;
        foreach (var fo in d.Formulas ?? [])
        {
            var cmd = new SqlCommand(@"
                INSERT INTO dbo.formulas (id_factura, nombre_cliente, ojo_derecho, ojo_izquierdo, adicion,
                                          distancia, altura, tipo_lente, tipo_montura, doctor,
                                          fecha_entrega, hora_entrega)
                VALUES (@factura, @nombre, @od, @oi, @adicion,
                        @distancia, @altura, @lente, @montura, @doctor,
                        @fechaEntrega, @horaEntrega);", cn, tx);
            cmd.Parameters.AddWithValue("@factura", (object?)idFactura ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@nombre", Valor(fo.NombreCliente));
            cmd.Parameters.AddWithValue("@od", Valor(fo.OjoDerecho));
            cmd.Parameters.AddWithValue("@oi", Valor(fo.OjoIzquierdo));
            cmd.Parameters.AddWithValue("@adicion", Valor(fo.Adicion));
            cmd.Parameters.AddWithValue("@distancia", Valor(fo.Distancia));
            cmd.Parameters.AddWithValue("@altura", Valor(fo.Altura));
            cmd.Parameters.AddWithValue("@lente", Valor(fo.TipoLente));
            cmd.Parameters.AddWithValue("@montura", Valor(fo.TipoMontura));
            cmd.Parameters.AddWithValue("@doctor", Valor(fo.Doctor));
            cmd.Parameters.AddWithValue("@fechaEntrega", (object?)fo.FechaEntrega ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@horaEntrega", (object?)fo.HoraEntrega?.ToTimeSpan() ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
            guardadas++;
        }

        await tx.CommitAsync();
        return Results.Ok(new { idFactura, guardadas });
    }
    catch (SqlException ex)
    {
        app.Logger.LogError(ex, "Error de SQL Server al guardar las fórmulas");
        return Results.Json(new { mensaje = "No se pudo guardar en SQL Server: " + ex.Message }, statusCode: 500);
    }
});


// ---------- Buscar planillas por número de orden ----------
// El número de orden se puede repetir: se traen todas, la más reciente primero.
app.MapGet("/api/trazabilidad/buscar", async (string? orden) =>
{
    if (string.IsNullOrWhiteSpace(orden)) return Results.Ok(new List<Dictionary<string, object?>>());

    await using var cn = new SqlConnection(conexion);
    await cn.OpenAsync();
    var cmd = new SqlCommand(@"
        SELECT TOP 30 * FROM dbo.trazabilidad
        WHERE numero_orden = @orden
        ORDER BY id_trazabilidad DESC;", cn);
    cmd.Parameters.AddWithValue("@orden", orden.Trim());
    return Results.Ok(await LeerFilas(cmd));
});


// ---------- Guardar una planilla de trazabilidad ----------
app.MapPost("/api/trazabilidad", (TrazabilidadDto t) => GuardarTrazabilidad(t, null));


// ---------- Guardar los cambios de una planilla ya guardada ----------
app.MapPut("/api/trazabilidad/{id:int}", (int id, TrazabilidadDto t) => GuardarTrazabilidad(t, id));


// Si idExistente es null crea la planilla; si no, la actualiza
async Task<IResult> GuardarTrazabilidad(TrazabilidadDto t, int? idExistente)
{
    try
    {
        await using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();

        var cmd = idExistente is null
            ? new SqlCommand(@"
                INSERT INTO dbo.trazabilidad (numero_orden, fecha_adquirencia, nombre_paciente, proveedor, fecha_envio,
                                              prescripcion_od, prescripcion_oi, prescripcion_add,
                                              fecha_llegada, material, montura,
                                              invima_1, invima_2, lote_1, lote_2,
                                              lensometria_od, lensometria_oi, lensometria_add,
                                              aprobado, devolucion)
                OUTPUT INSERTED.id_trazabilidad
                VALUES (@orden, @adquirencia, @paciente, @proveedor, @envio,
                        @presOd, @presOi, @presAdd,
                        @llegada, @material, @montura,
                        @invima1, @invima2, @lote1, @lote2,
                        @lensOd, @lensOi, @lensAdd,
                        @aprobado, @devolucion);", cn)
            : new SqlCommand(@"
                UPDATE dbo.trazabilidad
                SET numero_orden = @orden, fecha_adquirencia = @adquirencia, nombre_paciente = @paciente,
                    proveedor = @proveedor, fecha_envio = @envio,
                    prescripcion_od = @presOd, prescripcion_oi = @presOi, prescripcion_add = @presAdd,
                    fecha_llegada = @llegada, material = @material, montura = @montura,
                    invima_1 = @invima1, invima_2 = @invima2, lote_1 = @lote1, lote_2 = @lote2,
                    lensometria_od = @lensOd, lensometria_oi = @lensOi, lensometria_add = @lensAdd,
                    aprobado = @aprobado, devolucion = @devolucion
                OUTPUT INSERTED.id_trazabilidad
                WHERE id_trazabilidad = @id;", cn);
        if (idExistente is not null) cmd.Parameters.AddWithValue("@id", idExistente);
        cmd.Parameters.AddWithValue("@orden", Valor(t.NumeroOrden));
        cmd.Parameters.AddWithValue("@adquirencia", (object?)t.FechaAdquirencia ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@paciente", Valor(t.NombrePaciente));
        cmd.Parameters.AddWithValue("@proveedor", Valor(t.Proveedor));
        cmd.Parameters.AddWithValue("@envio", (object?)t.FechaEnvio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@presOd", Valor(t.PrescripcionOd));
        cmd.Parameters.AddWithValue("@presOi", Valor(t.PrescripcionOi));
        cmd.Parameters.AddWithValue("@presAdd", Valor(t.PrescripcionAdd));
        cmd.Parameters.AddWithValue("@llegada", (object?)t.FechaLlegada ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@material", Valor(t.Material));
        cmd.Parameters.AddWithValue("@montura", Valor(t.Montura));
        cmd.Parameters.AddWithValue("@invima1", Valor(t.Invima1));
        cmd.Parameters.AddWithValue("@invima2", Valor(t.Invima2));
        cmd.Parameters.AddWithValue("@lote1", Valor(t.Lote1));
        cmd.Parameters.AddWithValue("@lote2", Valor(t.Lote2));
        cmd.Parameters.AddWithValue("@lensOd", Valor(t.LensometriaOd));
        cmd.Parameters.AddWithValue("@lensOi", Valor(t.LensometriaOi));
        cmd.Parameters.AddWithValue("@lensAdd", Valor(t.LensometriaAdd));
        cmd.Parameters.AddWithValue("@aprobado", (object?)t.Aprobado ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@devolucion", (object?)t.Devolucion ?? DBNull.Value);
        if (await cmd.ExecuteScalarAsync() is not int idTrazabilidad)
            return Results.NotFound(new { mensaje = "No existe la planilla " + idExistente });

        return Results.Ok(new { idTrazabilidad, mensaje = idExistente is null ? "Planilla guardada." : "Planilla actualizada." });
    }
    catch (SqlException ex)
    {
        app.Logger.LogError(ex, "Error de SQL Server al guardar la planilla de trazabilidad");
        return Results.Json(new { mensaje = "No se pudo guardar en SQL Server: " + ex.Message }, statusCode: 500);
    }
}


// ---------- Listar las monturas del inventario ----------
app.MapGet("/api/monturas", async () =>
{
    await using var cn = new SqlConnection(conexion);
    await cn.OpenAsync();
    var cmd = new SqlCommand("SELECT * FROM dbo.Monturas ORDER BY IdMontura DESC;", cn);
    return Results.Ok(await LeerFilas(cmd));
});


// ---------- Guardar una montura nueva o los cambios de una ya guardada ----------
app.MapPost("/api/monturas", (MonturaDto m) => GuardarMontura(m, null));
app.MapPut("/api/monturas/{id:int}", (int id, MonturaDto m) => GuardarMontura(m, id));

// Si idExistente es null crea la montura; si no, la actualiza
async Task<IResult> GuardarMontura(MonturaDto m, int? idExistente)
{
    if (string.IsNullOrWhiteSpace(m.Ubicacion) || string.IsNullOrWhiteSpace(m.Referencia))
        return Results.BadRequest(new { mensaje = "La ubicación y la referencia son obligatorias." });
    if (m.ValorCosto is null || m.ValorCliente is null)
        return Results.BadRequest(new { mensaje = "El valor de costo y el valor al cliente son obligatorios." });

    try
    {
        await using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();
        var cmd = idExistente is null
            ? new SqlCommand(@"
                INSERT INTO dbo.Monturas (Ubicacion, Referencia, FechaIngreso, Cantidad, ValorCosto, ValorCliente,
                                          Material, Forma, EstadoMontura, Marca, EsReplica, NombreReplica, Color)
                OUTPUT INSERTED.IdMontura
                VALUES (@ubicacion, @referencia, @fecha, @cantidad, @costo, @cliente,
                        @material, @forma, @estado, @marca, @esReplica, @nombreReplica, @color);", cn)
            : new SqlCommand(@"
                UPDATE dbo.Monturas SET
                    Ubicacion = @ubicacion, Referencia = @referencia, FechaIngreso = @fecha, Cantidad = @cantidad,
                    ValorCosto = @costo, ValorCliente = @cliente, Material = @material, Forma = @forma,
                    EstadoMontura = @estado, Marca = @marca, EsReplica = @esReplica,
                    NombreReplica = @nombreReplica, Color = @color
                OUTPUT INSERTED.IdMontura
                WHERE IdMontura = @id;", cn);
        if (idExistente is not null) cmd.Parameters.AddWithValue("@id", idExistente);
        cmd.Parameters.AddWithValue("@ubicacion", m.Ubicacion.Trim());
        cmd.Parameters.AddWithValue("@referencia", m.Referencia.Trim());
        cmd.Parameters.AddWithValue("@fecha", m.FechaIngreso ?? DateOnly.FromDateTime(DateTime.Today));
        cmd.Parameters.AddWithValue("@cantidad", m.Cantidad ?? 0);
        cmd.Parameters.AddWithValue("@costo", m.ValorCosto);
        cmd.Parameters.AddWithValue("@cliente", m.ValorCliente);
        cmd.Parameters.AddWithValue("@material", Valor(m.Material));
        cmd.Parameters.AddWithValue("@forma", Valor(m.Forma));
        cmd.Parameters.AddWithValue("@estado", string.IsNullOrWhiteSpace(m.EstadoMontura) ? "Nueva" : m.EstadoMontura.Trim());
        cmd.Parameters.AddWithValue("@marca", Valor(m.Marca));
        cmd.Parameters.AddWithValue("@esReplica", m.EsReplica == true);
        // La tabla solo acepta marca de réplica cuando la montura es réplica
        cmd.Parameters.AddWithValue("@nombreReplica", m.EsReplica == true ? Valor(m.NombreReplica) : DBNull.Value);
        cmd.Parameters.AddWithValue("@color", Valor(m.Color));
        if (await cmd.ExecuteScalarAsync() is not int idMontura)
            return Results.NotFound(new { mensaje = "No existe la montura " + idExistente });
        return Results.Ok(new { IdMontura = idMontura, Referencia = m.Referencia.Trim() });
    }
    catch (SqlException ex) when (ex.Number == 547)
    {
        return Results.BadRequest(new { mensaje = "Algún valor no es válido: " + ex.Message });
    }
    catch (SqlException ex)
    {
        app.Logger.LogError(ex, "Error de SQL Server al guardar la montura");
        return Results.Json(new { mensaje = "No se pudo guardar en SQL Server: " + ex.Message }, statusCode: 500);
    }
}


// ---------- Borrar una montura del inventario ----------
app.MapDelete("/api/monturas/{id:int}", async (int id) =>
{
    await using var cn = new SqlConnection(conexion);
    await cn.OpenAsync();
    var cmd = new SqlCommand("DELETE FROM dbo.Monturas WHERE IdMontura = @id;", cn);
    cmd.Parameters.AddWithValue("@id", id);
    return await cmd.ExecuteNonQueryAsync() == 0
        ? Results.NotFound(new { mensaje = "No existe la montura " + id })
        : Results.Ok(new { mensaje = "Montura borrada." });
});


// ---------- Listar los accesorios del inventario ----------
app.MapGet("/api/accesorios", async () =>
{
    await using var cn = new SqlConnection(conexion);
    await cn.OpenAsync();
    var cmd = new SqlCommand("SELECT * FROM dbo.Accesorios ORDER BY IdAccesorio DESC;", cn);
    return Results.Ok(await LeerFilas(cmd));
});


// ---------- Guardar un accesorio en el inventario ----------
app.MapPost("/api/accesorios", async (AccesorioInventarioDto a) =>
{
    if (string.IsNullOrWhiteSpace(a.TipoProducto))
        return Results.BadRequest(new { mensaje = "Seleccione el tipo de producto." });
    if (a.Cantidad is < 0 || a.ValorCosto is < 0 || a.ValorCliente is < 0)
        return Results.BadRequest(new { mensaje = "La cantidad y los valores no pueden ser negativos." });

    try
    {
        await using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();
        var cmd = new SqlCommand(@"
            INSERT INTO dbo.Accesorios (TipoProducto, Referencia, Cantidad, FechaIngreso,
                                        ValorCosto, ValorCliente, Observaciones)
            OUTPUT INSERTED.IdAccesorio
            VALUES (@tipo, @referencia, @cantidad, @fecha, @costo, @cliente, @observaciones);", cn);
        cmd.Parameters.AddWithValue("@tipo", a.TipoProducto.Trim());
        cmd.Parameters.AddWithValue("@referencia", Valor(a.Referencia));
        cmd.Parameters.AddWithValue("@cantidad", a.Cantidad ?? 0);
        cmd.Parameters.AddWithValue("@fecha", a.FechaIngreso ?? DateOnly.FromDateTime(DateTime.Today));
        cmd.Parameters.AddWithValue("@costo", (object?)a.ValorCosto ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cliente", (object?)a.ValorCliente ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@observaciones", Valor(a.Observaciones));
        var idAccesorio = (int)(await cmd.ExecuteScalarAsync())!;
        return Results.Ok(new { idAccesorio, mensaje = "Accesorio guardado." });
    }
    catch (SqlException ex) when (ex.Number == 547)
    {
        return Results.BadRequest(new { mensaje = "Algún valor no es válido: " + ex.Message });
    }
    catch (SqlException ex)
    {
        app.Logger.LogError(ex, "Error de SQL Server al guardar el accesorio");
        return Results.Json(new { mensaje = "No se pudo guardar en SQL Server: " + ex.Message }, statusCode: 500);
    }
});


// ---------- Borrar un accesorio del inventario ----------
app.MapDelete("/api/accesorios/{id:int}", async (int id) =>
{
    await using var cn = new SqlConnection(conexion);
    await cn.OpenAsync();
    var cmd = new SqlCommand("DELETE FROM dbo.Accesorios WHERE IdAccesorio = @id;", cn);
    cmd.Parameters.AddWithValue("@id", id);
    return await cmd.ExecuteNonQueryAsync() == 0
        ? Results.NotFound(new { mensaje = "No existe el accesorio " + id })
        : Results.Ok(new { mensaje = "Accesorio borrado." });
});

app.Run();


// Al encender el servidor ejecuta Accesorios/base_datos.sql: crea las tablas de accesorios
// (inventario y ventas) que falten. El script no borra datos y se puede ejecutar muchas veces.
async Task PrepararTablaAccesorios()
{
    var archivo = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "Accesorios", "base_datos.sql"));
    try
    {
        var lotes = System.Text.RegularExpressions.Regex.Split(
            await File.ReadAllTextAsync(archivo), @"^\s*GO\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        await using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();
        foreach (var lote in lotes.Where(l => !string.IsNullOrWhiteSpace(l)))
            await new SqlCommand(lote, cn).ExecuteNonQueryAsync();
    }
    catch (Exception ex) when (ex is SqlException or IOException)
    {
        app.Logger.LogError(ex, "No se pudieron preparar las tablas de accesorios. Ejecute Accesorios/base_datos.sql en SQL Server.");
    }
}


// ---------- Funciones de ayuda ----------

// Los campos vacíos se guardan como NULL
static object Valor(string? texto) =>
    string.IsNullOrWhiteSpace(texto) ? DBNull.Value : texto.Trim();

// Busca el cliente por tipo y número de documento; si no existe lo crea.
// Si el formulario no trae ningún dato del cliente, la factura queda sin cliente.
static async Task<int?> GuardarCliente(SqlConnection cn, SqlTransaction tx, ClienteDto? c)
{
    if (c is null || new[] { c.Nombre, c.Documento, c.Telefono, c.Email }.All(string.IsNullOrWhiteSpace))
        return null;

    var tipo = string.IsNullOrWhiteSpace(c.TipoDocumento) ? "CC" : c.TipoDocumento.Trim();

    if (!string.IsNullOrWhiteSpace(c.Documento))
    {
        // Cliente que ya existe: se actualizan los datos que vengan llenos
        var buscar = new SqlCommand(@"
            UPDATE dbo.clientes
            SET nombre   = COALESCE(@nombre, nombre),
                telefono = COALESCE(@telefono, telefono),
                email    = COALESCE(@email, email)
            OUTPUT INSERTED.id_cliente
            WHERE tipo_documento = @tipo AND documento = @documento;", cn, tx);
        buscar.Parameters.AddWithValue("@nombre", Valor(c.Nombre));
        buscar.Parameters.AddWithValue("@telefono", Valor(c.Telefono));
        buscar.Parameters.AddWithValue("@email", Valor(c.Email));
        buscar.Parameters.AddWithValue("@tipo", tipo);
        buscar.Parameters.AddWithValue("@documento", c.Documento.Trim());
        if (await buscar.ExecuteScalarAsync() is int existente) return existente;
    }

    var crear = new SqlCommand(@"
        INSERT INTO dbo.clientes (nombre, tipo_documento, documento, telefono, email)
        OUTPUT INSERTED.id_cliente
        VALUES (@nombre, @tipo, @documento, @telefono, @email);", cn, tx);
    crear.Parameters.AddWithValue("@nombre", Valor(c.Nombre));
    crear.Parameters.AddWithValue("@tipo", tipo);
    crear.Parameters.AddWithValue("@documento", Valor(c.Documento));
    crear.Parameters.AddWithValue("@telefono", Valor(c.Telefono));
    crear.Parameters.AddWithValue("@email", Valor(c.Email));
    return (int)(await crear.ExecuteScalarAsync())!;
}

// Convierte el resultado de una consulta en una lista de filas (columna -> valor)
static async Task<List<Dictionary<string, object?>>> LeerFilas(SqlCommand cmd)
{
    var filas = new List<Dictionary<string, object?>>();
    await using var lector = await cmd.ExecuteReaderAsync();
    while (await lector.ReadAsync())
    {
        var fila = new Dictionary<string, object?>();
        for (int i = 0; i < lector.FieldCount; i++)
            fila[lector.GetName(i)] = lector.IsDBNull(i) ? null : lector.GetValue(i);
        filas.Add(fila);
    }
    return filas;
}


// ---------- Datos que envía la página ----------
record ClienteDto(string? Nombre, string? TipoDocumento, string? Documento, string? Telefono, string? Email);

record ProductoDto(string? TipoMontura, string? Color, string? Referencia, string? TipoLente);

record AccesorioDto(string? TipoProducto, string? Referencia, int? Cantidad);

// Tablas donde guarda cada página: Facturación o Accesorios
record TablasVenta(string Encabezado, string Id, string Productos, string? Detalle, string Nombre, bool SoloProductosConDatos);

record FacturaDto(
    string? NumeroFactura,
    DateOnly? FechaVenta,
    decimal? Total,
    decimal? Abono,
    string? MedioPago,
    ClienteDto? Cliente,
    List<ProductoDto>? Productos,
    List<AccesorioDto>? Accesorios,
    DateOnly? FechaEntrega,
    TimeOnly? HoraEntrega,
    string? DetalleEntrega,
    string? Vendedor);

record FormulaDto(
    string? NombreCliente,
    string? OjoDerecho,
    string? OjoIzquierdo,
    string? Adicion,
    string? Distancia,
    string? Altura,
    string? TipoLente,
    string? TipoMontura,
    string? Doctor,
    DateOnly? FechaEntrega,
    TimeOnly? HoraEntrega);

record GuardarFormulasDto(int? IdFactura, string? NumeroFactura, List<FormulaDto>? Formulas);

record TrazabilidadDto(
    string? NumeroOrden,
    DateOnly? FechaAdquirencia,
    string? NombrePaciente,
    string? Proveedor,
    DateOnly? FechaEnvio,
    string? PrescripcionOd,
    string? PrescripcionOi,
    string? PrescripcionAdd,
    DateOnly? FechaLlegada,
    string? Material,
    string? Montura,
    string? Invima1,
    string? Invima2,
    string? Lote1,
    string? Lote2,
    string? LensometriaOd,
    string? LensometriaOi,
    string? LensometriaAdd,
    bool? Aprobado,
    bool? Devolucion);

record MonturaDto(
    string? Ubicacion,
    string? Referencia,
    DateOnly? FechaIngreso,
    int? Cantidad,
    decimal? ValorCosto,
    decimal? ValorCliente,
    string? Material,
    string? Forma,
    string? EstadoMontura,
    string? Marca,
    bool? EsReplica,
    string? NombreReplica,
    string? Color);

record AccesorioInventarioDto(
    string? TipoProducto,
    string? Referencia,
    int? Cantidad,
    DateOnly? FechaIngreso,
    decimal? ValorCosto,
    decimal? ValorCliente,
    string? Observaciones);
