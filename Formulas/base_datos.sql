USE Opticas_San_Antonio;
GO

IF OBJECT_ID(N'dbo.formulas', N'U') IS NULL
CREATE TABLE dbo.formulas (
    id_formula      INT IDENTITY(1,1) PRIMARY KEY,
    id_factura      INT
                    CONSTRAINT fk_formula_factura REFERENCES dbo.facturas (id_factura)
                    ON DELETE CASCADE,
    nombre_cliente  NVARCHAR(120),
    ojo_derecho     NVARCHAR(60),
    ojo_izquierdo   NVARCHAR(60),
    adicion         NVARCHAR(20),
    distancia       NVARCHAR(20),
    altura          NVARCHAR(20),
    tipo_lente      NVARCHAR(MAX),
    tipo_montura    NVARCHAR(60),
    doctor          NVARCHAR(120),
    fecha_entrega   DATE,
    hora_entrega    TIME(0),
    creado_en       DATETIME2(0) DEFAULT SYSDATETIME()
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_formulas_factura')
    CREATE INDEX idx_formulas_factura ON dbo.formulas (id_factura);
GO

-- Si la tabla ya existía con campos obligatorios, se vuelven opcionales (no borra datos)
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.formulas') AND name = N'id_factura' AND is_nullable = 0)
BEGIN
    DROP INDEX idx_formulas_factura ON dbo.formulas;
    ALTER TABLE dbo.formulas ALTER COLUMN id_factura INT NULL;
    CREATE INDEX idx_formulas_factura ON dbo.formulas (id_factura);
END
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.formulas') AND name = N'creado_en' AND is_nullable = 0)
    ALTER TABLE dbo.formulas ALTER COLUMN creado_en DATETIME2(0) NULL;
GO

-- Las fórmulas también se pueden unir a una venta de accesorios o directamente a un cliente.
-- (Ejecute antes Accesorios/base_datos.sql, que crea dbo.ventas_accesorios.)
IF COL_LENGTH(N'dbo.formulas', N'id_venta_accesorios') IS NULL
    ALTER TABLE dbo.formulas ADD id_venta_accesorios INT NULL
        CONSTRAINT fk_formula_venta_acc REFERENCES dbo.ventas_accesorios (id_venta)
        ON DELETE CASCADE;
GO
IF COL_LENGTH(N'dbo.formulas', N'id_cliente') IS NULL
    ALTER TABLE dbo.formulas ADD id_cliente INT NULL
        CONSTRAINT fk_formula_cliente REFERENCES dbo.clientes (id_cliente);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_formulas_venta_acc')
    CREATE INDEX idx_formulas_venta_acc ON dbo.formulas (id_venta_accesorios);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_formulas_cliente')
    CREATE INDEX idx_formulas_cliente ON dbo.formulas (id_cliente);
GO

SELECT * FROM dbo.formulas;
