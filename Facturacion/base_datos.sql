-- =============================================================
-- Base de datos de facturación - Centro Óptico San Antonio
-- Motor: Microsoft SQL Server (Express 2016 o superior)
-- Ejecutar en SQL Server Management Studio con F5
-- Campos tomados del formulario Facturacion.html
-- Se puede ejecutar varias veces: no borra ni duplica lo que ya existe
-- =============================================================

-- Opciones necesarias para el índice filtrado y la columna calculada "saldo"
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF DB_ID(N'Optica_Optica_San_Antonio') IS NULL
    CREATE DATABASE Optica_Optica_San_Antonio COLLATE Modern_Spanish_CI_AS;
GO

USE Optica_Optica_San_Antonio;
GO

-- -------------------------------------------------------------
-- Clientes (sección "Cliente" del formulario)
-- Un cliente se registra una sola vez y se usa en muchas facturas
-- -------------------------------------------------------------
IF OBJECT_ID(N'dbo.clientes', N'U') IS NULL
CREATE TABLE dbo.clientes (
    id_cliente      INT IDENTITY(1,1) PRIMARY KEY,
    nombre          NVARCHAR(120),                         -- Cliente
    tipo_documento  VARCHAR(3) DEFAULT 'CC'                -- Tipo_documento
                    CONSTRAINT chk_tipo_documento CHECK (tipo_documento IN ('CC', 'CE', 'TI', 'NIT')),
    documento       VARCHAR(20),                           -- Documento
    telefono        VARCHAR(20),                           -- Telefono
    email           NVARCHAR(120),                         -- email
    creado_en       DATETIME2(0) NOT NULL DEFAULT SYSDATETIME()
);
GO

-- No se repite el mismo tipo y número de documento (solo cuando el documento está lleno)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'uq_cliente_documento')
    CREATE UNIQUE INDEX uq_cliente_documento
        ON dbo.clientes (tipo_documento, documento)
        WHERE documento IS NOT NULL;
GO

-- -------------------------------------------------------------
-- Facturas (encabezado, valores, entrega y vendedor)
-- -------------------------------------------------------------
IF OBJECT_ID(N'dbo.facturas', N'U') IS NULL
CREATE TABLE dbo.facturas (
    id_factura      INT IDENTITY(1,1) PRIMARY KEY,
    numero_factura  VARCHAR(20),                           -- N_Factura (se puede repetir)
    fecha_venta     DATE,                                  -- fecha
    id_cliente      INT
                    CONSTRAINT fk_factura_cliente REFERENCES dbo.clientes (id_cliente),

    -- Valores en pesos colombianos
    total           DECIMAL(12,2) DEFAULT 0,               -- Total
    abono           DECIMAL(12,2) DEFAULT 0,               -- Abono
    saldo           AS (total - abono) PERSISTED,          -- Saldo (se calcula solo)

    -- Entrega
    fecha_entrega   DATE,                                  -- FechaEntrega
    hora_entrega    TIME(0),                               -- HoraEntrega
    detalle_entrega NVARCHAR(255),                         -- Detalle

    -- Vendedor
    vendedor        NVARCHAR(120),                         -- Vendedor

    creado_en       DATETIME2(0) NOT NULL DEFAULT SYSDATETIME(),

    CONSTRAINT chk_valores_positivos CHECK (total >= 0 AND abono >= 0),
    CONSTRAINT chk_abono_no_mayor CHECK (abono <= total)
);
GO

-- -------------------------------------------------------------
-- Productos de cada factura (sección "Datos de entrega")
-- Una factura puede tener varias monturas: botón "Agregar otro producto"
-- -------------------------------------------------------------
IF OBJECT_ID(N'dbo.factura_productos', N'U') IS NULL
CREATE TABLE dbo.factura_productos (
    id_producto     INT IDENTITY(1,1) PRIMARY KEY,
    id_factura      INT NOT NULL
                    CONSTRAINT fk_producto_factura REFERENCES dbo.facturas (id_factura)
                    ON DELETE CASCADE,                     -- si se borra la factura, se borran sus productos
    tipo_montura    VARCHAR(30)                            -- Montura
                    CONSTRAINT chk_tipo_montura CHECK (tipo_montura IN (
                        'TR90', 'Acetato', 'Metalica', '3Piezas',
                        'SobrepuestoTR90', 'SobrepuestoMetalica',
                        'AireMetalica', 'AireAcetato', 'AireTr90',
                        'Pacientedejamontura')),
    color           NVARCHAR(60),                          -- Color
    referencia      NVARCHAR(60),                          -- Referencia
    tipo_lente      NVARCHAR(MAX)                          -- Lente (texto largo)
);
GO

-- Búsquedas frecuentes: facturas por número, por fecha y por cliente
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_facturas_numero')
    CREATE INDEX idx_facturas_numero  ON dbo.facturas (numero_factura);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_facturas_fecha')
    CREATE INDEX idx_facturas_fecha   ON dbo.facturas (fecha_venta);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_facturas_cliente')
    CREATE INDEX idx_facturas_cliente ON dbo.facturas (id_cliente);
GO


-- =============================================================
-- DATOS DE EJEMPLO (puede borrar esta parte si no los necesita)
-- Solo se agregan si la tabla de facturas está vacía
-- =============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.facturas)
BEGIN
    DECLARE @id_cliente INT, @id_factura INT;

    INSERT INTO dbo.clientes (nombre, tipo_documento, documento, telefono, email)
    VALUES (N'María Fernanda Rojas', 'CC', '52384117', '3104567821', N'maria.rojas@ejemplo.com');
    SET @id_cliente = SCOPE_IDENTITY();

    INSERT INTO dbo.facturas (numero_factura, fecha_venta, id_cliente, total, abono,
                              fecha_entrega, hora_entrega, detalle_entrega, vendedor)
    VALUES ('0001', '2026-11-01', @id_cliente, 655000, 330000,
            '2026-11-08', '15:00', N'Entregar en el local', N'Laura Pérez');
    SET @id_factura = SCOPE_IDENTITY();

    INSERT INTO dbo.factura_productos (id_factura, tipo_montura, color, referencia, tipo_lente)
    VALUES (@id_factura, 'Acetato', N'Carey', N'AC-2231', N'Progresivo policarbonato, antirreflejo y filtro de luz azul'),
           (@id_factura, 'TR90',    N'Negro', N'TR-1045', N'Monofocal de descanso');
END
GO


-- =============================================================
-- CONSULTA DE PRUEBA: ver la factura completa con sus productos
-- =============================================================
SELECT f.numero_factura, f.fecha_venta, c.nombre AS cliente,
       c.tipo_documento, c.documento,
       f.total, f.abono, f.saldo,
       p.tipo_montura, p.color, p.referencia, p.tipo_lente,
       f.fecha_entrega, f.hora_entrega, f.vendedor
FROM dbo.facturas f
LEFT JOIN dbo.clientes c          ON c.id_cliente = f.id_cliente
LEFT JOIN dbo.factura_productos p ON p.id_factura = f.id_factura
WHERE f.numero_factura = '0001';
GO
