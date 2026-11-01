-- =============================================================
-- Base de datos de facturación - Centro Óptico San Antonio
-- Motor: MySQL 8 (también funciona en MariaDB 10.5 o superior)
-- Campos tomados del formulario Facturacion.html
-- =============================================================

CREATE DATABASE IF NOT EXISTS Optica_Optica_San_Antonio
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_spanish_ci;

USE Optica_Optica_San_Antonio;

-- -------------------------------------------------------------
-- Clientes (sección "Cliente" del formulario)
-- Un cliente se registra una sola vez y se usa en muchas facturas
-- -------------------------------------------------------------
CREATE TABLE clientes (
    id_cliente      INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    nombre          VARCHAR(120),                          -- Cliente
    tipo_documento  ENUM('CC', 'CE', 'TI', 'NIT') DEFAULT 'CC',  -- Tipo_documento
    documento       VARCHAR(20),                           -- Documento
    telefono        VARCHAR(20),                           -- Telefono
    email           VARCHAR(120),                          -- email
    creado_en       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_cliente_documento (tipo_documento, documento)
);

-- -------------------------------------------------------------
-- Facturas (encabezado, valores, entrega y vendedor)
-- -------------------------------------------------------------
CREATE TABLE facturas (
    id_factura      INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    numero_factura  VARCHAR(20),                           -- N_Factura (se puede repetir)
    fecha_venta     DATE,                                  -- fecha
    id_cliente      INT UNSIGNED,

    -- Valores en pesos colombianos
    total           DECIMAL(12,2) DEFAULT 0,      -- Total
    abono           DECIMAL(12,2) DEFAULT 0,      -- Abono
    saldo           DECIMAL(12,2) AS (total - abono) STORED,  -- Saldo (se calcula solo)

    -- Entrega
    fecha_entrega   DATE,                                  -- FechaEntrega
    hora_entrega    TIME,                                  -- HoraEntrega
    detalle_entrega VARCHAR(255),                          -- Detalle

    -- Vendedor
    vendedor        VARCHAR(120),                          -- Vendedor

    creado_en       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_factura_cliente FOREIGN KEY (id_cliente)
        REFERENCES clientes (id_cliente),
    CONSTRAINT chk_valores_positivos CHECK (total >= 0 AND abono >= 0),
    CONSTRAINT chk_abono_no_mayor CHECK (abono <= total)
);

-- -------------------------------------------------------------
-- Productos de cada factura (sección "Datos de entrega")
-- Una factura puede tener varias monturas: botón "Agregar otro producto"
-- -------------------------------------------------------------
CREATE TABLE factura_productos (
    id_producto     INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    id_factura      INT UNSIGNED NOT NULL,
    tipo_montura    ENUM(
                        'TR90', 'Acetato', 'Metalica', '3Piezas',
                        'SobrepuestoTR90', 'SobrepuestoMetalica',
                        'AireMetalica', 'AireAcetato', 'AireTr90'
                    ),                                     -- Montura
    color           VARCHAR(60),                           -- Color
    referencia      VARCHAR(60),                           -- Referencia
    tipo_lente      TEXT,                                  -- Lente (texto largo)

    CONSTRAINT fk_producto_factura FOREIGN KEY (id_factura)
        REFERENCES facturas (id_factura)
        ON DELETE CASCADE                                  -- si se borra la factura, se borran sus productos
);

-- Búsquedas frecuentes: facturas por número, por fecha y por cliente
CREATE INDEX idx_facturas_numero  ON facturas (numero_factura);
CREATE INDEX idx_facturas_fecha   ON facturas (fecha_venta);
CREATE INDEX idx_facturas_cliente ON facturas (id_cliente);


-- =============================================================
-- DATOS DE EJEMPLO (puede borrar esta parte si no los necesita)
-- =============================================================
INSERT INTO clientes (nombre, tipo_documento, documento, telefono, email)
VALUES ('María Fernanda Rojas', 'CC', '52384117', '3104567821', 'maria.rojas@ejemplo.com');

INSERT INTO facturas (numero_factura, fecha_venta, id_cliente, total, abono,
                      fecha_entrega, hora_entrega, detalle_entrega, vendedor)
VALUES ('0001', '2026-11-01', 1, 655000, 330000,
        '2026-11-08', '15:00', 'Entregar en el local', 'Laura Pérez');

INSERT INTO factura_productos (id_factura, tipo_montura, color, referencia, tipo_lente)
VALUES (1, 'Acetato', 'Carey', 'AC-2231', 'Progresivo policarbonato, antirreflejo y filtro de luz azul'),
       (1, 'TR90',    'Negro', 'TR-1045', 'Monofocal de descanso');


-- =============================================================
-- CONSULTA DE PRUEBA: ver la factura completa con sus productos
-- =============================================================
SELECT f.numero_factura, f.fecha_venta, c.nombre AS cliente,
       c.tipo_documento, c.documento,
       f.total, f.abono, f.saldo,
       p.tipo_montura, p.color, p.referencia, p.tipo_lente,
       f.fecha_entrega, f.hora_entrega, f.vendedor
FROM facturas f
LEFT JOIN clientes c          ON c.id_cliente = f.id_cliente
LEFT JOIN factura_productos p ON p.id_factura = f.id_factura
WHERE f.numero_factura = '0001';
