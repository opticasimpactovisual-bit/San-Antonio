// Servidor de facturación - Centro Óptico San Antonio
// Muestra la página Facturacion.html y guarda las facturas en SQL Server.
//
//   POST /api/facturas        guarda una factura con su cliente y sus productos
//   GET  /api/facturas        lista las últimas 50 facturas
//   GET  /api/facturas/{id}   muestra una factura con todos sus productos

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


// ---------- Guardar una factura ----------
app.MapPost("/api/facturas", async (FacturaDto f) =>
{
    try
    {
        await using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();

        int? idCliente = await GuardarCliente(cn, tx, f.Cliente);

        var cmd = new SqlCommand(@"
            INSERT INTO dbo.facturas (numero_factura, fecha_venta, id_cliente, total, abono, medio_pago,
                                      fecha_entrega, hora_entrega, detalle_entrega, vendedor)
            OUTPUT INSERTED.id_factura
            VALUES (@numero, @fecha, @cliente, @total, @abono, @medioPago,
                    @fechaEntrega, @horaEntrega, @detalle, @vendedor);", cn, tx);
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
        var idFactura = (int)(await cmd.ExecuteScalarAsync())!;

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
        return Results.Ok(new { idFactura, mensaje = "Factura guardada." });
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
