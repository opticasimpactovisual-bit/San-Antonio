// Servidor de facturación - Centro Óptico San Antonio
// Muestra la página Facturacion.html y guarda las facturas en SQL Server.
//
//   POST /api/facturas                  guarda una factura con su cliente y sus productos
//   PUT  /api/facturas/{id}             guarda los cambios de una factura anterior
//   GET  /api/facturas                  lista las últimas 50 facturas
//   GET  /api/facturas/buscar?texto=..  busca por número de factura, documento o nombre
//   GET  /api/facturas/{id}             muestra una factura con todos sus productos
//   DELETE /api/facturas/{id}           elimina una factura con sus productos y fórmulas
//   GET  /api/formulas/factura?numero=.. busca una factura por número y trae sus fórmulas
//   POST /api/formulas                  guarda las fórmulas de la página Formulas_clientes.html
//   GET  /api/trazabilidad/buscar?orden=.. busca las planillas de un número de orden
//   POST /api/trazabilidad              guarda una planilla de la página trasavilidad.html
//   PUT  /api/trazabilidad/{id}         guarda los cambios de una planilla ya guardada

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var conexion = builder.Configuration.GetConnectionString("Optica")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'Optica' en appsettings.json");

// La página web vive en la carpeta Facturacion, al lado de esta carpeta
var carpetaPagina = new PhysicalFileProvider(
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "Facturacion")));
var paginaInicio = new DefaultFilesOptions { FileProvider = carpetaPagina };
paginaInicio.DefaultFileNames.Clear();
paginaInicio.DefaultFileNames.Add("Facturacion.html");
app.UseDefaultFiles(paginaInicio);
app.UseStaticFiles(new StaticFileOptions { FileProvider = carpetaPagina });

// La página de fórmulas (carpeta Formulas) se abre en /Formulas/Formulas_clientes.html.
// Facturacion también queda en /Facturacion para que la página de fórmulas encuentre el logo.
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


// ---------- Guardar una factura nueva ----------
app.MapPost("/api/facturas", (FacturaDto f) => GuardarFactura(f, null));


// ---------- Guardar los cambios de una factura anterior ----------
app.MapPut("/api/facturas/{id:int}", (int id, FacturaDto f) => GuardarFactura(f, id));


// Si idExistente es null crea la factura; si no, la actualiza y reemplaza sus productos
async Task<IResult> GuardarFactura(FacturaDto f, int? idExistente)
{
    try
    {
        await using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();

        int? idCliente = await GuardarCliente(cn, tx, f.Cliente);

        var cmd = idExistente is null
            ? new SqlCommand(@"
                INSERT INTO dbo.facturas (numero_factura, fecha_venta, id_cliente, total, abono, medio_pago,
                                          fecha_entrega, hora_entrega, detalle_entrega, vendedor)
                OUTPUT INSERTED.id_factura
                VALUES (@numero, @fecha, @cliente, @total, @abono, @medioPago,
                        @fechaEntrega, @horaEntrega, @detalle, @vendedor);", cn, tx)
            : new SqlCommand(@"
                UPDATE dbo.facturas
                SET numero_factura = @numero, fecha_venta = @fecha, id_cliente = @cliente,
                    total = @total, abono = @abono, medio_pago = @medioPago,
                    fecha_entrega = @fechaEntrega, hora_entrega = @horaEntrega,
                    detalle_entrega = @detalle, vendedor = @vendedor
                OUTPUT INSERTED.id_factura
                WHERE id_factura = @id;", cn, tx);
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
        if (await cmd.ExecuteScalarAsync() is not int idFactura)
            return Results.NotFound(new { mensaje = "No existe la factura " + idExistente });

        // Al editar, los productos anteriores se borran y se guardan los del formulario
        if (idExistente is not null)
        {
            var borrar = new SqlCommand("DELETE FROM dbo.factura_productos WHERE id_factura = @id;", cn, tx);
            borrar.Parameters.AddWithValue("@id", idFactura);
            await borrar.ExecuteNonQueryAsync();
        }

        // Una fila por cada grupo de "Agregar otro producto"
        foreach (var p in f.Productos ?? [])
        {
            var cmdProducto = new SqlCommand(@"
                INSERT INTO dbo.factura_productos (id_factura, tipo_montura, color, referencia, tipo_lente)
                VALUES (@factura, @montura, @color, @referencia, @lente);", cn, tx);
            cmdProducto.Parameters.AddWithValue("@factura", idFactura);
            cmdProducto.Parameters.AddWithValue("@montura", Valor(p.TipoMontura));
            cmdProducto.Parameters.AddWithValue("@color", Valor(p.Color));
            cmdProducto.Parameters.AddWithValue("@referencia", Valor(p.Referencia));
            cmdProducto.Parameters.AddWithValue("@lente", Valor(p.TipoLente));
            await cmdProducto.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        return Results.Ok(new { idFactura, mensaje = idExistente is null ? "Factura guardada." : "Factura actualizada." });
    }
    catch (SqlException ex) when (ex.Number == 547)
    {
        // 547 = no cumple una regla de la tabla (CHECK)
        var mensaje = ex.Message.Contains("chk_abono_no_mayor")
            ? "El abono no puede ser mayor que el total."
            : "Algún valor no es válido: " + ex.Message;
        return Results.BadRequest(new { mensaje });
    }
    catch (SqlException ex)
    {
        app.Logger.LogError(ex, "Error de SQL Server al guardar la factura");
        return Results.Json(new { mensaje = "No se pudo guardar en SQL Server: " + ex.Message }, statusCode: 500);
    }
}


// ---------- Buscar facturas por número, documento o nombre del cliente ----------
app.MapGet("/api/facturas/buscar", async (string? texto) =>
{
    if (string.IsNullOrWhiteSpace(texto)) return Results.Ok(new List<Dictionary<string, object?>>());

    await using var cn = new SqlConnection(conexion);
    await cn.OpenAsync();
    var cmd = new SqlCommand(@"
        SELECT TOP 30 f.id_factura, f.numero_factura, f.fecha_venta, c.nombre AS cliente,
               c.tipo_documento, c.documento, f.total, f.saldo
        FROM dbo.facturas f
        LEFT JOIN dbo.clientes c ON c.id_cliente = f.id_cliente
        WHERE f.numero_factura = @texto
           OR c.documento = @texto
           OR c.nombre LIKE '%' + @texto + '%'
        ORDER BY f.id_factura DESC;", cn);
    cmd.Parameters.AddWithValue("@texto", texto.Trim());
    return Results.Ok(await LeerFilas(cmd));
});


// ---------- Listar las últimas facturas ----------
app.MapGet("/api/facturas", async () =>
{
    await using var cn = new SqlConnection(conexion);
    await cn.OpenAsync();
    var cmd = new SqlCommand(@"
        SELECT TOP 50 f.id_factura, f.numero_factura, f.fecha_venta, c.nombre AS cliente,
               f.total, f.abono, f.saldo,
               (SELECT COUNT(*) FROM dbo.factura_productos p WHERE p.id_factura = f.id_factura) AS productos
        FROM dbo.facturas f
        LEFT JOIN dbo.clientes c ON c.id_cliente = f.id_cliente
        ORDER BY f.id_factura DESC;", cn);
    return Results.Ok(await LeerFilas(cmd));
});


// ---------- Ver una factura con sus productos ----------
app.MapGet("/api/facturas/{id:int}", async (int id) =>
{
    await using var cn = new SqlConnection(conexion);
    await cn.OpenAsync();

    var cmd = new SqlCommand(@"
        SELECT f.*, c.nombre AS cliente, c.tipo_documento, c.documento, c.telefono, c.email
        FROM dbo.facturas f
        LEFT JOIN dbo.clientes c ON c.id_cliente = f.id_cliente
        WHERE f.id_factura = @id;", cn);
    cmd.Parameters.AddWithValue("@id", id);
    var factura = (await LeerFilas(cmd)).FirstOrDefault();
    if (factura is null) return Results.NotFound(new { mensaje = "No existe la factura " + id });

    var cmdProductos = new SqlCommand(
        "SELECT * FROM dbo.factura_productos WHERE id_factura = @id ORDER BY id_producto;", cn);
    cmdProductos.Parameters.AddWithValue("@id", id);
    factura["productos"] = await LeerFilas(cmdProductos);

    return Results.Ok(factura);
});


// ---------- Eliminar una factura ----------
// Sus productos y sus fórmulas se borran solos (ON DELETE CASCADE). El cliente no se borra.
app.MapDelete("/api/facturas/{id:int}", async (int id) =>
{
    await using var cn = new SqlConnection(conexion);
    await cn.OpenAsync();
    var cmd = new SqlCommand("DELETE FROM dbo.facturas WHERE id_factura = @id;", cn);
    cmd.Parameters.AddWithValue("@id", id);
    return await cmd.ExecuteNonQueryAsync() == 0
        ? Results.NotFound(new { mensaje = "No existe la factura " + id })
        : Results.Ok(new { mensaje = "Factura eliminada." });
});


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

app.Run();


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

record FacturaDto(
    string? NumeroFactura,
    DateOnly? FechaVenta,
    decimal? Total,
    decimal? Abono,
    string? MedioPago,
    ClienteDto? Cliente,
    List<ProductoDto>? Productos,
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
