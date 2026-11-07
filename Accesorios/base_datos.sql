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
            N'Plaquetas', N'Tornillos', N'Pañitos', N'Líquido antiempañante',
            N'Terminales', N'Cordón', N'Cadenas', N'Colas de ratón',
            N'Reparación', N'Otros')),
        CONSTRAINT CK_Accesorios_Cantidad CHECK (Cantidad >= 0),
        CONSTRAINT CK_Accesorios_ValorCosto CHECK (ValorCosto >= 0),
        CONSTRAINT CK_Accesorios_ValorCliente CHECK (ValorCliente >= 0)
    );

    CREATE INDEX IX_Accesorios_Tipo ON dbo.Accesorios (TipoProducto);
END
GO
