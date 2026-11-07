USE Opticas_San_Antonio;
GO

IF OBJECT_ID(N'dbo.trazabilidad', N'U') IS NULL
CREATE TABLE dbo.trazabilidad (
    id_trazabilidad   INT IDENTITY(1,1) PRIMARY KEY,
    numero_orden      NVARCHAR(30),
    fecha_adquirencia DATE,
    nombre_paciente   NVARCHAR(120),
    proveedor         NVARCHAR(120),
    fecha_envio       DATE,
    prescripcion_od   NVARCHAR(60),
    prescripcion_oi   NVARCHAR(60),
    prescripcion_add  NVARCHAR(60),
    fecha_llegada     DATE,
    material          NVARCHAR(MAX),
    montura           NVARCHAR(120),
    invima_1          NVARCHAR(60),
    invima_2          NVARCHAR(60),
    lote_1            NVARCHAR(60),
    lote_2            NVARCHAR(60),
    lensometria_od    NVARCHAR(60),
    lensometria_oi    NVARCHAR(60),
    lensometria_add   NVARCHAR(60),
    aprobado          BIT,
    devolucion        BIT,
    creado_en         DATETIME2(0) DEFAULT SYSDATETIME()
);
GO

SELECT * FROM dbo.trazabilidad;
