-- ============================================================================
-- Script de datos de demostración para GuarinoApp
-- Flujo completo: los 4 puntos del análisis de Contador Senior
-- ============================================================================
-- Ejecutar contra la base de datos de Desarrollo (LocalDB o SQL Server).
-- Este script es idempotente: usa IF NOT EXISTS para no duplicar datos.
-- ============================================================================

USE [GuarinoAppDb];  -- o tu nombre de base de desarrollo
GO

-- ----------------------------------------------------------------------------
-- 1. COSTO DESGLOSE KG (Punto 1: costo real por kg exportable)
-- ----------------------------------------------------------------------------
-- Insert solo si no existe un desglose para la zafra actual (2026)
IF NOT EXISTS (SELECT 1 FROM [CostosDesgloseKg] WHERE [Zafra] = 2026)
BEGIN
    INSERT INTO [CostosDesgloseKg] (
        [Zafra], [CostoFrutaPlanta], [CostoPackingManoObra],
        [CostoCartonEnvases], [CostoEnergiaFrio], [CostoFletePuerto],
        [CostoOtros], [KgExportablesProyectados], [CargadoPor],
        [FechaCarga], [Observaciones], [CreatedAt], [CreatedBy]
    )
    VALUES (
        2026,
        0.62,   -- Fruta puesta en planta (precio pagado al productor)
        0.18,   -- Packing - mano de obra
        0.12,   -- Cartón y envases
        0.08,   -- Energía y frío
        0.15,   -- Flete a Montevideo + gastos de puerto
        0.05,   -- Otros costos
        120000, -- Kg exportables proyectados
        'sistema',
        GETDATE(),
        'Valores de demostración para la zafra 2026. Actualizar con cifras reales de Contaduría.',
        GETDATE(),
        'DbInitializer'
    );
END
GO

-- ----------------------------------------------------------------------------
-- 2. VOLCADOS (Puntos 2 y 3: rendimiento packing + comparativa interzafra)
-- ----------------------------------------------------------------------------
-- Insert volcados para los últimos 12 meses, distribuidos en dos zafras
-- (2025 y 2026) para permitir la comparación interzafra semanal.
-- Se vinculan a lotes existentes.
IF NOT EXISTS (SELECT 1 FROM [Volcados] WHERE [CreatedBy] = 'DbInitializer')
BEGIN
    DECLARE @fechaHoy DATE = CAST(GETDATE() AS DATE);
    DECLARE @contador INT = 0;

    -- Bucle simple: insertar 200 volcados distribuidos
    WHILE @contador < 200
    BEGIN
        DECLARE @zafra VARCHAR(4) = CASE WHEN @contador % 2 = 0 THEN '2025' ELSE '2026' END;
        DECLARE @loteId INT = (
            SELECT TOP 1 l.[Id]
            FROM [Lotes] l
            WHERE l.[Zafra] = @zafra
              AND l.[IsDeleted] = 0
            ORDER BY NEWID()
        );

        IF @loteId IS NOT NULL
        BEGIN
            DECLARE @mes INT = CASE 
                WHEN @contador % 5 < 3 THEN 5 + (@contador % 5)  -- meses 5-9 (temporada pico)
                ELSE 1 + (@contador % 12)
            END;
            DECLARE @año INT = CAST(@zafra AS INT);
            DECLARE @dia INT = 1 + (@contador % 28);
            DECLARE @fecha DATE = DATEFROMPARTS(@año, @mes, @dia);

            -- Asegurar que no sea fecha futura lejana
            IF @fecha > DATEADD(MONTH, 1, @fechaHoy)
                SET @fecha = DATEADD(MONTH, -12, @fecha);

            DECLARE @cantidadBins INT = 10 + (@contador % 51);  -- 10..60
            DECLARE @pesoKilos DECIMAL(18,2) = CAST(4000 + (@contador % 20000) + (@contador % 1000) * 0.5 AS DECIMAL(18,2));
            DECLARE @canal INT = 1 + (@contador % 2);  -- 1=Exportacion, 2=MercadoInterno

            INSERT INTO [Volcados] (
                [LoteId], [FechaVolcado], [CantidadBins], [PesoKilos],
                [Canal], [CreatedAt], [CreatedBy], [IsDeleted]
            )
            VALUES (
                @loteId, @fecha, @cantidadBins, @pesoKilos,
                @canal, @fecha, 'DbInitializer', 0
            );
        END

        SET @contador = @contador + 1;
    END
END
GO

-- ----------------------------------------------------------------------------
-- 3. VINCULAR PALLETS A VOLCADOS (Punto 2: rendimiento de packing)
-- ----------------------------------------------------------------------------
-- Asigna VolcadoId a pallets que no lo tienen, clasificándolos como:
--   - Exportable (55%): cliente asignado, categoría "Export", estado EnFrio
--   - Mercado interno (25%): cliente nacional, categoría "Primera", estado EnFrio
--   - Descarte (20%): sin cliente, categoría "Descarte", estado Bajado
UPDATE TOP (30) p
SET p.[VolcadoId] = (
    SELECT TOP 1 v.[Id]
    FROM [Volcados] v
    INNER JOIN [Lotes] l ON v.[LoteId] = l.[Id]
    WHERE l.[Variedad] = p.[Variedad]
      AND v.[CreatedBy] = 'DbInitializer'
    ORDER BY NEWID()
),
    p.[Cliente] = CASE 
        WHEN (ABS(CAST(NEWID() AS BINARY(6))) % 100) < 55 THEN 'Importadora San Martín'
        WHEN (ABS(CAST(NEWID() AS BINARY(6))) % 100) < 80 THEN 'Frutas del Sur S.A.'
        ELSE NULL
    END,
    p.[Categoria] = CASE 
        WHEN (ABS(CAST(NEWID() AS BINARY(6))) % 100) < 55 THEN 'Export'
        WHEN (ABS(CAST(NEWID() AS BINARY(6))) % 100) < 80 THEN 'Primera'
        ELSE 'Descarte'
    END,
    p.[Estado] = CASE 
        WHEN (ABS(CAST(NEWID() AS BINARY(6))) % 100) < 80 THEN 2  -- EnFrio
        ELSE 5  -- Bajado
    END,
    p.[UpdatedAt] = GETDATE(),
    p.[UpdatedBy] = 'DbInitializer'
FROM [Pallets] p
WHERE p.[IsDeleted] = 0
  AND p.[VolcadoId] IS NULL;
GO

-- ----------------------------------------------------------------------------
-- 4. FINANCIALDATA (Punto 4: proyección de caja a 13 semanas)
-- ----------------------------------------------------------------------------
-- Ya existe desde el DbInitializer, pero aquí está el valor de referencia:
--   CostoPorKg = 0.58, FlujoCaja = 45000, MargenBruto = 34, CostoFlete = 4800
-- Si necesitas recargar:
IF NOT EXISTS (SELECT 1 FROM [FinancialData])
BEGIN
    INSERT INTO [FinancialData] (
        [CostoPorKg], [FlujoCaja], [MargenBruto], [CostoFlete],
        [CapacidadInstalada], [FechaActualizacion], [ActualizadoPor],
        [Observaciones], [CreatedAt], [CreatedBy]
    )
    VALUES (
        0.58, 45000, 34, 4800, 130000,
        GETDATE(), 'sistema',
        'Valores iniciales de referencia para la proyección de caja.',
        GETDATE(), 'DbInitializer'
    );
END
GO

-- ----------------------------------------------------------------------------
-- VERIFICACIÓN: consultas para validar que los datos están cargados
-- ----------------------------------------------------------------------------

-- Costo por kg exportable
SELECT 
    [Zafra],
    [CostoFrutaPlanta],
    [CostoPackingManoObra],
    [CostoCartonEnvases],
    [CostoEnergiaFrio],
    [CostoFletePuerto],
    [CostoOtros],
    [CostoFrutaPlanta] + [CostoPackingManoObra] + [CostoCartonEnvases] +
    [CostoEnergiaFrio] + [CostoFletePuerto] + [CostoOtros] AS [CostoTotal],
    [KgExportablesProyectados],
    ROUND(
        ([CostoFrutaPlanta] + [CostoPackingManoObra] + [CostoCartonEnvases] +
         [CostoEnergiaFrio] + [CostoFletePuerto] + [CostoOtros]) / [KgExportablesProyectados],
        2
    ) AS [CostoPorKgExportable]
FROM [CostosDesgloseKg]
WHERE [Zafra] = 2026;

-- Volcados por zafra y mes (para comparativa interzafra)
SELECT 
    [Zafra] = l.[Zafra],
    [Mes] = MONTH(v.[FechaVolcado]),
    [Año] = YEAR(v.[FechaVolcado]),
    [TotalKg] = SUM(v.[PesoKilos]),
    [TotalBins] = SUM(v.[CantidadBins]),
    [CantidadVolcados] = COUNT(*)
FROM [Volcados] v
INNER JOIN [Lotes] l ON v.[LoteId] = l.[Id]
WHERE v.[CreatedBy] = 'DbInitializer'
GROUP BY l.[Zafra], YEAR(v.[FechaVolcado]), MONTH(v.[FechaVolcado])
ORDER BY l.[Zafra], YEAR(v.[FechaVolcado]), MONTH(v.[FechaVolcado]);

-- Rendimiento de packing por clasificación
SELECT 
    [Categoria],
    [Estado],
    [Cliente],
    COUNT(*) AS [CantidadPallets],
    SUM(ISNULL(p.[PesoNeto], 0)) AS [TotalKg]
FROM [Pallets] p
WHERE p.[IsDeleted] = 0
  AND p.[VolcadoId] IS NOT NULL
GROUP BY [Categoria], [Estado], [Cliente]
ORDER BY [Categoria];

-- Datos financieros para proyección de caja
SELECT * FROM [FinancialData];
GO

-- ============================================================================
-- FIN DEL SCRIPT
-- ============================================================================
