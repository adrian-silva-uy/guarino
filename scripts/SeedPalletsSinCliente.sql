SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.Pallets', N'U') IS NULL
BEGIN
    THROW 50001, 'No existe la tabla dbo.Pallets.', 1;
END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.Pallets
    WHERE Codigo LIKE N'PSC-SEED-%'
)
BEGIN
    INSERT INTO dbo.Pallets
        (Codigo, PalletNumero, VolcadoId, Orden, Contramarca, Calibre, Envase, Marca, Cliente,
         Variedad, Categoria, EstadoTexto, Estado, Productor, TipoPallet, FormaEmbalado, Frio,
         Fecha, Hora, SemanaProd, Embalaje, Especie, Produccion, O_Obs3, SPA, Lote, Cuadros,
         FecCosecha, Cantidad, CantidadEnvases, PesoNeto, CreatedAt, CreatedBy, UpdatedAt,
         UpdatedBy, IsDeleted)
    VALUES
        (N'PSC-SEED-001', 9001, NULL, N'OP-2025-0915', N'NANAK',     N'100', N'Caja 18kg', N'Guarino Premium', NULL, N'Granny Smith',  N'Primera', N'F', 2, N'Frutas del Sol',      N'Exportacion', N'Embalado Manual',     N'Camara 1', '2025-09-15', N'08:15', 38, N'Caja', N'Manzana', N'Linea 1', N'Pendiente asignacion comercial', N'SPA-01', N'L-2509-01', N'C-12', N'2025-09-10', 1, 56, 1008.00, '2025-09-15', N'SeedPalletsSinCliente', NULL, NULL, 0),
        (N'PSC-SEED-002', 9002, NULL, N'OP-2025-1008', N'SUNKIST',   N'110', N'Caja 18kg', N'Guarino Premium', N'',   N'Fuji',          N'Primera', N'F', 2, N'Agricola San Juan',  N'Exportacion', N'Embalado Automatico', N'Camara 2', '2025-10-08', N'10:30', 41, N'Caja', N'Manzana', N'Linea 2', N'Pendiente confirmacion cliente', N'SPA-02', N'L-2510-02', N'C-08', N'2025-10-02', 1, 58, 1044.00, '2025-10-08', N'SeedPalletsSinCliente', NULL, NULL, 0),
        (N'PSC-SEED-003', 9003, NULL, N'OP-2025-1119', N'DEL MONTE', N'120', N'Caja 18kg', N'Guarino Premium', N'',   N'Gala',          N'Primera', N'P', 3, N'Vina del Valle',     N'Exportacion', N'Embalado Manual',     N'Pre frio', '2025-11-19', N'14:10', 47, N'Caja', N'Manzana', N'Linea 1', N'Pendiente destino', N'SPA-03', N'L-2511-03', N'C-15', N'2025-11-13', 1, 60, 1080.00, '2025-11-19', N'SeedPalletsSinCliente', NULL, NULL, 0),
        (N'PSC-SEED-004', 9004, NULL, N'OP-2025-1204', N'DOLCE',     N'135', N'Caja 18kg', N'Guarino Premium', N'',   N'Pink Lady',     N'Primera', N'F', 2, N'Huerta Organica',    N'Mercado',     N'Embalado Automatico', N'Camara 3', '2025-12-04', N'09:45', 49, N'Caja', N'Manzana', N'Linea 3', N'Pendiente asignacion comercial', N'SPA-04', N'L-2512-04', N'C-04', N'2025-11-29', 1, 57, 1026.00, '2025-12-04', N'SeedPalletsSinCliente', NULL, NULL, 0),
        (N'PSC-SEED-005', 9005, NULL, N'OP-2026-0116', N'NANAK',     N'100', N'Caja 18kg', N'Guarino Premium', NULL, N'Red Delicious', N'Primera', N'F', 2, N'Cosechas del Este',   N'Exportacion', N'Embalado Manual',     N'Camara 1', '2026-01-16', N'11:20', 3,  N'Caja', N'Manzana', N'Linea 2', N'Pendiente cliente final', N'SPA-05', N'L-2601-05', N'C-11', N'2026-01-10', 1, 59, 1062.00, '2026-01-16', N'SeedPalletsSinCliente', NULL, NULL, 0),
        (N'PSC-SEED-006', 9006, NULL, N'OP-2026-0210', N'SUNKIST',   N'110', N'Caja 18kg', N'Guarino Premium', N'',   N'Bartlett',      N'Primera', N'P', 3, N'Frutas del Sol',      N'Mercado',     N'Embalado Automatico', N'Pre frio', '2026-02-10', N'16:05', 7,  N'Caja', N'Pera',    N'Linea 1', N'Pendiente destino', N'SPA-01', N'L-2602-06', N'C-09', N'2026-02-04', 1, 61, 1098.00, '2026-02-10', N'SeedPalletsSinCliente', NULL, NULL, 0),
        (N'PSC-SEED-007', 9007, NULL, N'OP-2026-0322', N'DEL MONTE', N'120', N'Caja 18kg', N'Guarino Premium', N'',   N'Thompson',      N'Primera', N'F', 2, N'Agricola San Juan',  N'Exportacion', N'Embalado Manual',     N'Camara 2', '2026-03-22', N'13:35', 12, N'Caja', N'Uva',     N'Linea 3', N'Pendiente asignacion comercial', N'SPA-02', N'L-2603-07', N'C-06', N'2026-03-17', 1, 56, 1008.00, '2026-03-22', N'SeedPalletsSinCliente', NULL, NULL, 0),
        (N'PSC-SEED-008', 9008, NULL, N'OP-2026-0406', N'DOLCE',     N'135', N'Caja 18kg', N'Guarino Premium', N'',   N'Granny Smith',  N'Primera', N'F', 2, N'Vina del Valle',     N'Mercado',     N'Embalado Automatico', N'Camara 3', '2026-04-06', N'12:00', 15, N'Caja', N'Manzana', N'Linea 2', N'Pendiente confirmacion cliente', N'SPA-03', N'L-2604-08', N'C-13', N'2026-04-01', 1, 58, 1044.00, '2026-04-06', N'SeedPalletsSinCliente', NULL, NULL, 0),
        (N'PSC-SEED-009', 9009, NULL, N'OP-2026-0518', N'NANAK',     N'100', N'Caja 18kg', N'Guarino Premium', NULL, N'Fuji',          N'Primera', N'P', 3, N'Huerta Organica',    N'Exportacion', N'Embalado Manual',     N'Pre frio', '2026-05-18', N'15:25', 21, N'Caja', N'Manzana', N'Linea 1', N'Pendiente destino', N'SPA-04', N'L-2605-09', N'C-10', N'2026-05-12', 1, 60, 1080.00, '2026-05-18', N'SeedPalletsSinCliente', NULL, NULL, 0),
        (N'PSC-SEED-010', 9010, NULL, N'OP-2026-0627', N'SUNKIST',   N'110', N'Caja 18kg', N'Guarino Premium', N'',   N'Gala',          N'Primera', N'F', 2, N'Cosechas del Este',   N'Exportacion', N'Embalado Automatico', N'Camara 1', '2026-06-27', N'08:50', 26, N'Caja', N'Manzana', N'Linea 3', N'Pendiente cliente final', N'SPA-05', N'L-2606-10', N'C-07', N'2026-06-20', 1, 57, 1026.00, '2026-06-27', N'SeedPalletsSinCliente', NULL, NULL, 0),
        (N'PSC-SEED-011', 9011, NULL, N'OP-2026-0709', N'DEL MONTE', N'120', N'Caja 18kg', N'Guarino Premium', N'',   N'Pink Lady',     N'Primera', N'F', 2, N'Frutas del Sol',      N'Mercado',     N'Embalado Manual',     N'Camara 2', '2026-07-09', N'10:15', 28, N'Caja', N'Manzana', N'Linea 2', N'Pendiente asignacion comercial', N'SPA-01', N'L-2607-11', N'C-16', N'2026-07-03', 1, 59, 1062.00, '2026-07-09', N'SeedPalletsSinCliente', NULL, NULL, 0),
        (N'PSC-SEED-012', 9012, NULL, N'OP-2026-0805', N'DOLCE',     N'135', N'Caja 18kg', N'Guarino Premium', N'',   N'Red Delicious', N'Primera', N'P', 3, N'Agricola San Juan',  N'Exportacion', N'Embalado Automatico', N'Pre frio', '2026-08-05', N'17:40', 32, N'Caja', N'Manzana', N'Linea 1', N'Pendiente confirmacion cliente', N'SPA-02', N'L-2608-12', N'C-05', N'2026-07-29', 1, 61, 1098.00, '2026-08-05', N'SeedPalletsSinCliente', NULL, NULL, 0);
END;

SELECT COUNT(*) AS PalletsSinCliente
FROM dbo.Pallets
WHERE IsDeleted = 0
  AND (Cliente IS NULL OR LTRIM(RTRIM(Cliente)) = N'')
  AND COALESCE(Fecha, CreatedAt) < '2026-08-12';
