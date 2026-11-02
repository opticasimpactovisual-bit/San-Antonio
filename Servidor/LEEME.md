# Servidor de facturación

Muestra la página `Facturacion/Facturacion.html` y guarda las facturas en SQL Server.

## Lo que se necesita (una sola vez)

1. **SDK de .NET 10**: descárguelo de https://dotnet.microsoft.com/download/dotnet/10.0
   (elija "SDK", Windows x64) e instálelo.
2. **La base de datos creada**: ejecute `Facturacion/base_datos.sql` en
   SQL Server Management Studio con F5. Para la planilla de trazabilidad,
   ejecute también `Trasavilidad/base_datos.sql`.

## Iniciar el servidor

En una terminal (PowerShell o CMD), dentro de esta carpeta `Servidor`:

```
dotnet run
```

Cuando aparezca `Now listening on: http://localhost:5000`, abra en el navegador:

**http://localhost:5000**

Deje la terminal abierta mientras usa la página. Para apagar el servidor presione `Ctrl + C`.

> Abra la página siempre desde http://localhost:5000. Si abre el archivo
> Facturacion.html con doble clic, el botón Guardar no podrá conectarse.

## Conexión a SQL Server

Está en `appsettings.json`:

```
Server=.\SQLEXPRESS;Database=Optica_Optica_San_Antonio;Trusted_Connection=True;TrustServerCertificate=True
```

- `.\SQLEXPRESS` es el SQL Server Express de este mismo computador.
- `Trusted_Connection=True` entra con su usuario de Windows, igual que Management Studio.

Si su servidor tiene otro nombre, cambie `.\SQLEXPRESS` por el nombre que aparece
al conectarse en Management Studio (por ejemplo `FRANCY_GUAUTA\SQLEXPRESS`).

## Direcciones del servidor

| Dirección | Qué hace |
|---|---|
| `GET /` | Muestra la página de facturación |
| `POST /api/facturas` | Guarda una factura con su cliente y sus productos |
| `GET /api/facturas` | Lista las últimas 50 facturas |
| `GET /api/facturas/{id}` | Muestra una factura con todos sus productos |
| `GET /Formulas/Formulas_clientes.html` | Muestra la página de fórmulas de clientes |
| `GET /api/formulas/factura?numero=..` | Busca una factura por número y trae sus fórmulas |
| `POST /api/formulas` | Guarda las fórmulas de la página (unidas a la factura, o sin factura) |
| `GET /Trasavilidad/trasavilidad.html` | Muestra la planilla de trazabilidad |
| `GET /api/trazabilidad/buscar?orden=..` | Busca las planillas de un número de orden |
| `POST /api/trazabilidad` | Guarda una planilla de trazabilidad |
| `PUT /api/trazabilidad/{id}` | Guarda los cambios de una planilla ya guardada |
