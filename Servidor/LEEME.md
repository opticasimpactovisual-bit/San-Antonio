# Servidor de facturación

Muestra la página de inicio (`Inicio/index.html`), desde donde se abren todos los programas, y guarda en SQL Server.

## Lo que se necesita (una sola vez)

1. **SDK de .NET 10**: descárguelo de https://dotnet.microsoft.com/download/dotnet/10.0
   (elija "SDK", Windows x64) e instálelo.
2. **Las tablas creadas**: en SQL Server Management Studio conéctese a
   `DESKTOP-C8L1T8M\SQLEXPRESS` y ejecute estos scripts en este orden:
   `Facturacion/base_datos.sql`, `Formulas/base_datos.sql`,
   `Trasavilidad/base_datos.sql`, `Invetarios/base_datos.sql` y
   `Accesorios/base_datos.sql`. Todos trabajan sobre
   `Opticas_San_Antonio`.

## Iniciar el servidor

En una terminal (PowerShell o CMD), dentro de esta carpeta `Servidor`:

```
dotnet run
```

Cuando aparezca `Now listening on: http://localhost:5000`, abra en el navegador:

**http://localhost:5000** (página de inicio con todos los programas)

Deje la terminal abierta mientras usa la página. Para apagar el servidor presione `Ctrl + C`.

> Abra las páginas siempre desde http://localhost:5000. Si abre un archivo
> .html con doble clic, el botón Guardar no podrá conectarse.

## Conexión a SQL Server

La configuración inicial en `appsettings.json` apunta a la instancia y base de
datos mostradas en SQL Server Management Studio:

```
Server=DESKTOP-C8L1T8M\SQLEXPRESS;Database=Opticas_San_Antonio;Trusted_Connection=True;TrustServerCertificate=True
```

- `Trusted_Connection=True` usa la cuenta de Windows que ejecuta el servidor.
- Si Management Studio se conectó con autenticación de SQL Server (por ejemplo,
  el usuario `appuser` que aparece en la captura), configure la cadena solo para
  la sesión actual de PowerShell, sustituyendo `<CLAVE>` por la contraseña local:

  ```
  $env:ConnectionStrings__Optica = "Server=DESKTOP-C8L1T8M\SQLEXPRESS;Database=Opticas_San_Antonio;User ID=appuser;Password=<CLAVE>;TrustServerCertificate=True"
  dotnet run
  ```

No escriba la contraseña en `appsettings.json` ni la comparta. Si el nombre de
instancia cambia, use el nombre que aparece al conectarse en Management Studio.

## Direcciones del servidor

| Dirección | Qué hace |
|---|---|
| `GET /` | Muestra la página de inicio con todos los programas |
| `GET /Facturacion/Facturacion.html` | Muestra la página de facturación |
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
| `GET /Invetarios/Inventarios.html` | Muestra el inventario de monturas |
| `GET /api/monturas` | Lista las monturas del inventario |
| `POST /api/monturas` | Guarda una montura en el inventario |
| `DELETE /api/monturas/{id}` | Borra una montura del inventario |
| `GET /Accesorios/Acesorios.html` | Muestra el inventario de accesorios |
| `GET /api/accesorios` | Lista los accesorios del inventario |
| `POST /api/accesorios` | Guarda un accesorio en el inventario |
| `DELETE /api/accesorios/{id}` | Borra un accesorio del inventario |
