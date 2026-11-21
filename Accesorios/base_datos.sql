-- Inventario de accesorios - Centro Óptico San Antonio
-- Se puede ejecutar varias veces: solo crea lo que falte y no borra datos.
USE Opticas_San_Antonio;
GO

IF OBJECT_ID(N'dbo.Accesorios', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Accesorios (
        IdAccesorio     INT IDENTITY(1,1)  NOT NULL,
        TipoProducto    NVARCHAR(40)       NOT NULL,
        Referencia      NVARCHAR(60)       NULL,
        Cantidad        INT                NOT NULL CONSTRAINT DF_Accesorios_Cantidad DEFAULT (0),
        FechaIngreso    DATE               NOT NULL CONSTRAINT DF_Accesorios_FechaIngreso DEFAULT (CAST(GETDATE() AS DATE)),
        ValorCosto      DECIMAL(12,2)      NULL,
        ValorCliente    DECIMAL(12,2)      NULL,
        Observaciones   NVARCHAR(255)      NULL,
        CreadoEn        DATETIME2(0)       NOT NULL CONSTRAINT DF_Accesorios_CreadoEn DEFAULT (SYSDATETIME()),

        CONSTRAINT PK_Accesorios PRIMARY KEY (IdAccesorio),
        CONSTRAINT CK_Accesorios_Tipo CHECK (TipoProducto IN (
            N'Estuche', N'Plaquetas', N'Tornillos', N'Pañitos', N'Líquido antiempañante',
            N'Terminales', N'Cordón', N'Cadenas', N'Colas de ratón',
            N'Reparación', N'Otros')),
        CONSTRAINT CK_Accesorios_Cantidad CHECK (Cantidad >= 0),
        CONSTRAINT CK_Accesorios_ValorCosto CHECK (ValorCosto >= 0),
        CONSTRAINT CK_Accesorios_ValorCliente CHECK (ValorCliente >= 0)
    );

    CREATE INDEX IX_Accesorios_Tipo ON dbo.Accesorios (TipoProducto);
END
GO

-- Bases de datos creadas antes: actualizar la lista para aceptar 'Estuche'
IF EXISTS (SELECT 1 FROM sys.check_constraints
           WHERE name = 'CK_Accesorios_Tipo' AND definition NOT LIKE '%Estuche%')
BEGIN
    ALTER TABLE dbo.Accesorios DROP CONSTRAINT CK_Accesorios_Tipo;
    ALTER TABLE dbo.Accesorios ADD CONSTRAINT CK_Accesorios_Tipo CHECK (TipoProducto IN (
        N'Estuche', N'Plaquetas', N'Tornillos', N'Pañitos', N'Líquido antiempañante',
        N'Terminales', N'Cordón', N'Cadenas', N'Colas de ratón',
        N'Reparación', N'Otros'));
END
GO

-- dbo.Accesorios es solo el inventario. Las ventas de la página Acesorios.html se guardan
-- en sus propias tablas (abajo). Se quita la columna IdFactura que se había agregado antes.
IF COL_LENGTH(N'dbo.Accesorios', N'IdFactura') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'dbo.FK_Accesorios_Facturas', N'F') IS NOT NULL
        ALTER TABLE dbo.Accesorios DROP CONSTRAINT FK_Accesorios_Facturas;
    ALTER TABLE dbo.Accesorios DROP COLUMN IdFactura;
END
GO


-- =============================================================
-- Ventas de accesorios (página Acesorios.html)
-- Separadas de Facturación: tienen su propia numeración y sus propias tablas.
-- El cliente se guarda en dbo.clientes, la misma lista de clientes de Facturación.
-- =============================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Encabezado de la venta: número, fecha, cliente, valores, entrega y vendedor
IF OBJECT_ID(N'dbo.ventas_accesorios', N'U') IS NULL
CREATE TABLE dbo.ventas_accesorios (
    id_venta        INT IDENTITY(1,1) PRIMARY KEY,
    numero_factura  VARCHAR(20),                           -- N_Factura (numeración propia de Accesorios)
    fecha_venta     DATE,                                  -- fecha
    id_cliente      INT
                    CONSTRAINT fk_venta_acc_cliente REFERENCES dbo.clientes (id_cliente),

    total           DECIMAL(12,2) DEFAULT 0,               -- Total
    abono           DECIMAL(12,2) DEFAULT 0,               -- Abono
    saldo           AS (total - abono) PERSISTED,          -- Saldo (se calcula solo)
    medio_pago      VARCHAR(20)                            -- MedioPago
                    CONSTRAINT chk_venta_acc_medio_pago CHECK (medio_pago IN (
                        'Efectivo', 'TC', 'TD', 'Sistecredito', 'ADDI',
                        'Nequi', 'Daviplata', 'BoldQR')),

    fecha_entrega   DATE,                                  -- FechaEntrega
    hora_entrega    TIME(0),                               -- HoraEntrega
    detalle_entrega NVARCHAR(255),                         -- Detalle
    vendedor        NVARCHAR(120),                         -- Vendedor

    creado_en       DATETIME2(0) NOT NULL DEFAULT SYSDATETIME(),

    CONSTRAINT chk_venta_acc_valores CHECK (total >= 0 AND abono >= 0),
    CONSTRAINT chk_venta_acc_abono_no_mayor CHECK (abono <= total)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_ventas_acc_numero')
    CREATE INDEX idx_ventas_acc_numero ON dbo.ventas_accesorios (numero_factura);
GO

-- Accesorios vendidos en cada venta (botón "Agregar accesorio")
IF OBJECT_ID(N'dbo.venta_accesorios_detalle', N'U') IS NULL
CREATE TABLE dbo.venta_accesorios_detalle (
    id_detalle      INT IDENTITY(1,1) PRIMARY KEY,
    id_venta        INT NOT NULL
                    CONSTRAINT fk_detalle_venta_acc REFERENCES dbo.ventas_accesorios (id_venta)
                    ON DELETE CASCADE,                     -- si se borra la venta, se borran sus accesorios
    tipo_producto   NVARCHAR(40) NOT NULL                  -- TipoAccesorio
                    CONSTRAINT chk_detalle_venta_acc_tipo CHECK (tipo_producto IN (
                        N'Estuche', N'Plaquetas', N'Tornillos', N'Pañitos', N'Líquido antiempañante',
                        N'Terminales', N'Cordón', N'Cadenas', N'Colas de ratón',
                        N'Reparación', N'Otros')),
    referencia      NVARCHAR(60),                          -- AccReferencia
    cantidad        INT NOT NULL DEFAULT 1                 -- AccCantidad
                    CONSTRAINT chk_detalle_venta_acc_cantidad CHECK (cantidad > 0)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_detalle_venta_acc')
    CREATE INDEX idx_detalle_venta_acc ON dbo.venta_accesorios_detalle (id_venta);
GO

-- Monturas y lentes de la venta de accesorios (sección "Datos de entrega" de Acesorios.html)
IF OBJECT_ID(N'dbo.venta_accesorios_productos', N'U') IS NULL
CREATE TABLE dbo.venta_accesorios_productos (
    id_producto     INT IDENTITY(1,1) PRIMARY KEY,
    id_venta        INT NOT NULL
                    CONSTRAINT fk_producto_venta_acc REFERENCES dbo.ventas_accesorios (id_venta)
                    ON DELETE CASCADE,
    tipo_montura    VARCHAR(30)                            -- Montura
                    CONSTRAINT chk_producto_venta_acc_montura CHECK (tipo_montura IN (
                        'TR90', 'Acetato', 'Metalica', '3Piezas', 'Pasta', 'Deportivas', 'Inyectada',
                        'SobrepuestoTR90', 'SobrepuestoMetalica',
                        'AireMetalica', 'AireAcetato', 'AireTr90',
                        'Pacientedejamontura')),
    color           NVARCHAR(60),                          -- Color
    referencia      NVARCHAR(60),                          -- Referencia
    tipo_lente      NVARCHAR(MAX)                          -- Lente
);
GO

-- Tablas creadas antes: actualizar la lista para aceptar 'Pasta' y 'Deportivas'
IF EXISTS (SELECT 1 FROM sys.check_constraints
           WHERE name = 'chk_producto_venta_acc_montura' AND definition NOT LIKE '%Deportivas%')
BEGIN
    ALTER TABLE dbo.venta_accesorios_productos DROP CONSTRAINT chk_producto_venta_acc_montura;
    ALTER TABLE dbo.venta_accesorios_productos ADD CONSTRAINT chk_producto_venta_acc_montura CHECK (tipo_montura IN (
        'TR90', 'Acetato', 'Metalica', '3Piezas', 'Pasta', 'Deportivas', 'Inyectada',
        'SobrepuestoTR90', 'SobrepuestoMetalica',
        'AireMetalica', 'AireAcetato', 'AireTr90',
        'Pacientedejamontura'));
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_producto_venta_acc')
    CREATE INDEX idx_producto_venta_acc ON dbo.venta_accesorios_productos (id_venta);
GO
