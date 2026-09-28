/*
=====================================================================
 Script de carga de catalogo de metadata - Proyecto NORIDEL (ScriptCase)
 Generado automaticamente a partir de 63 archivos .bkp (ScriptCase export)
 Fecha de generacion: 2026-08-11
 Destino: Server=(localdb)\mssqllocaldb;Database=GuarinoAppDb

 IMPORTANTE (nota de DBA):
 - Los archivos .bkp NO contienen credenciales reales (usuario/contrasena)
   ni cadenas de conexion (host, puerto). Solo referencian nombres logicos
   de conexion definidos en el proyecto ScriptCase: conn_mssql,
   conn_mssql_gpacking, conn_mssql_informes. Esos datos sensibles viven en
   la configuracion del proyecto (fuera de estos archivos) y NO deben
   commitearse en texto plano. La tabla Cat_Conexiones deja Host/Usuario/
   Password como NULL para completar manualmente o via Secret Manager.
 - Las relaciones se dividen en dos fuentes:
     1) Cat_Relaciones_Navegacion: links de drill-down entre aplicaciones
        ScriptCase (metadata 'ligacoes').
     2) Cat_Relaciones_SQL: relaciones JOIN inferidas por parseo de las
        sentencias SELECT reales embebidas en cada app (FK logicas).
=====================================================================
*/

USE [GuarinoAppDb];
GO

-------------------------------------------------------------------
-- 1. DDL: creacion de tablas de catalogo (si no existen)
-------------------------------------------------------------------


IF OBJECT_ID('dbo.Cat_Conexiones','U') IS NULL
BEGIN
    CREATE TABLE dbo.Cat_Conexiones (
        IdConexion      INT IDENTITY(1,1) PRIMARY KEY,
        NombreConexion  NVARCHAR(100) NOT NULL UNIQUE,
        Servidor        NVARCHAR(200) NULL,
        BaseDatos       NVARCHAR(200) NULL,
        Usuario         NVARCHAR(200) NULL,
        Password        NVARCHAR(200) NULL,
        Motor           NVARCHAR(50)  NULL DEFAULT ('MSSQL'),
        Observaciones   NVARCHAR(400) NULL
    );
END
GO

IF OBJECT_ID('dbo.Cat_Aplicaciones','U') IS NULL
BEGIN
    CREATE TABLE dbo.Cat_Aplicaciones (
        IdAplicacion    INT IDENTITY(1,1) PRIMARY KEY,
        CodProyecto     NVARCHAR(50)   NULL,
        CodAplicacion   NVARCHAR(150)  NOT NULL,
        TipoAplicacion  NVARCHAR(30)   NULL,
        Carpeta         NVARCHAR(200)  NULL,
        IdConexion      INT            NULL FOREIGN KEY REFERENCES dbo.Cat_Conexiones(IdConexion),
        NombreTabla     NVARCHAR(200)  NULL,
        CamposClave     NVARCHAR(300)  NULL,
        ComandoSelect   NVARCHAR(MAX)  NULL,
        FechaCreacion   NVARCHAR(8)    NULL,
        FechaUltAcceso  NVARCHAR(8)    NULL,
        ArchivoOrigen   NVARCHAR(300)  NULL
    );
END
GO

IF OBJECT_ID('dbo.Cat_Tablas','U') IS NULL
BEGIN
    CREATE TABLE dbo.Cat_Tablas (
        IdTabla         INT IDENTITY(1,1) PRIMARY KEY,
        NombreTabla     NVARCHAR(200) NOT NULL,
        BaseOrigen      NVARCHAR(200) NULL,
        IdConexion      INT NULL FOREIGN KEY REFERENCES dbo.Cat_Conexiones(IdConexion),
        CamposClave     NVARCHAR(300) NULL,
        Observaciones   NVARCHAR(400) NULL
    );
END
GO

IF OBJECT_ID('dbo.Cat_Relaciones_Navegacion','U') IS NULL
BEGIN
    CREATE TABLE dbo.Cat_Relaciones_Navegacion (
        IdRelacion      INT IDENTITY(1,1) PRIMARY KEY,
        AppOrigen       NVARCHAR(150) NOT NULL,
        AppDestino      NVARCHAR(150) NULL,
        TipoDestino     NVARCHAR(30)  NULL,
        CampoEnlace     NVARCHAR(200) NULL,
        Parametros      NVARCHAR(500) NULL
    );
END
GO

IF OBJECT_ID('dbo.Cat_Relaciones_SQL','U') IS NULL
BEGIN
    CREATE TABLE dbo.Cat_Relaciones_SQL (
        IdRelacionSql   INT IDENTITY(1,1) PRIMARY KEY,
        AppOrigen       NVARCHAR(150)  NOT NULL,
        TipoJoin        NVARCHAR(20)   NULL,
        TablaRelacionada NVARCHAR(200) NULL,
        AliasTabla      NVARCHAR(50)   NULL,
        CondicionOn     NVARCHAR(500)  NULL
    );
END
GO

-------------------------------------------------------------------
-- 2. Conexiones logicas (definidas en el proyecto ScriptCase)
--    Servidor/Usuario/Password: completar manualmente (no presentes
--    en los archivos .bkp analizados)
-------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql')
    INSERT INTO dbo.Cat_Conexiones (NombreConexion, Servidor, BaseDatos, Usuario, Password, Motor, Observaciones)
    VALUES (N'conn_mssql', NULL, NULL, NULL, NULL, N'MSSQL', N'Conexion principal de produccion (base transaccional NORIDEL)');
IF NOT EXISTS (SELECT 1 FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking')
    INSERT INTO dbo.Cat_Conexiones (NombreConexion, Servidor, BaseDatos, Usuario, Password, Motor, Observaciones)
    VALUES (N'conn_mssql_gpacking', NULL, NULL, NULL, NULL, N'MSSQL', N'Conexion al modulo GPacking (seguridad/usuarios y proceso de empaque)');
IF NOT EXISTS (SELECT 1 FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_informes')
    INSERT INTO dbo.Cat_Conexiones (NombreConexion, Servidor, BaseDatos, Usuario, Password, Motor, Observaciones)
    VALUES (N'conn_mssql_informes', NULL, NULL, NULL, NULL, N'MSSQL', N'Conexion de solo lectura para informes / reportes');
GO

-------------------------------------------------------------------
-- 3. Aplicaciones / formularios / consultas ScriptCase
-------------------------------------------------------------------
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'CAMIONES', N'form', N'root/Balanza', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_informes'), N'dbo.CAMIONES', N'IDTRANSPORTISTA;IDCAMION', N'SELECT * FROM dbo.CAMIONES', N'20250813', N'20260525', N'NORIDEL__NM__CAMIONES__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'CHOFERES', N'form', N'root/Balanza', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.CHOFERES', N'IDCHOFER', N'SELECT * FROM dbo.CHOFERES', N'20250813', N'20260525', N'NORIDEL__NM__CHOFERES__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'Fruta_a_Volcar', N'cons', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, N'SELECT
L.idLote as Lote,Em.descripcion as Playa,E.Descripcion as Especie,V.Descripcion as Variedad,
Ba.descripcion as Balanza,Di.descripcion as Destino,
convert(varchar,L.FechaIngreso,102) as FIngreso,
P.RazonSocial as Productor,PC.Descripcion as Quinta,L.IdRemito as Remitos,TL.Descripcion as TipoLote,
(L.CantidadBins - L.cantidadBinsDesp) as CantBins,L.CantidadBinsEmb as Volcados,
(L.CantidadBins - L.cantidadBinsDesp -L.CantidadBinsEmb) as Saldo,
convert(numeric(18,0),L.KilosNetos) as KgIngreso,
convert(numeric(18,0),(L.KilosNetos/L.CantidadBins)*L.CantidadBinsEmb) as KgVolcados,
convert(numeric(18,0),(L.KilosNetos/L.CantidadBins)*(L.CantidadBins - L.cantidadBinsDesp - L.CantidadBinsEmb)) as KgSaldo

FROM  Lote L WITH (NOLOCK)
INNER JOIN TipoLote TL WITH (NOLOCK) ON L.IdTipoLote = TL.IdTipoLote
INNER JOIN Variedad V WITH (NOLOCK) ON L.idVariedad = V.idVariedad AND L.idEspecie = V.idEspecie
INNER JOIN Especie E WITH (NOLOCK) ON L.idEspecie = E.idEspecie
INNER JOIN Productor P WITH (NOLOCK) ON L.idProductor = P.idProductor
INNER JOIN Empaque Em WITH (NOLOCK) ON L.IdSeccion = Em.idEmpaque
LEFT  JOIN ProductorChacra PC WITH (NOLOCK) ON (L.IdProductor = PC.IdProductor AND L.IdChacra = PC.IdChacra)  
LEFT  JOIN Responsable R WITH (NOLOCK) ON R.IdResponsable = L.idCapataz
LEFT  JOIN Bascula Ba WITH (NOLOCK) ON L.IdBascula = Ba.idBascula 
LEFT  JOIN DestinoInterno Di WITH (NOLOCK) ON l.IdDestinoSugerido = Di.idDestinoInterno 

WHERE L.Estado = ''E'' and L.IdBascula = 3 and l.idseccion = 2', N'20250806', N'20251217', N'NORIDEL__NM__Fruta_a_Volcar__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'LUGARES', N'form', N'root/Balanza', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_informes'), N'dbo.LUGARES', N'IDLUGAR', N'SELECT * FROM dbo.LUGARES', N'20250813', N'20260508', N'NORIDEL__NM__LUGARES__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'Lotes_Volcados', N'cons', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, N'Select
DATEPART(ISO_Week,LE.FechaProduccion) as Semana,
TL.Descripcion as TipoLote,
LE.IdLote	as Lote, 
Convert(varchar,LE.FechaProduccion,103) as FechaProduccion, 
Convert(varchar,LE.FechaHoraComienzo,103) as FechaVolcado, 
Convert(varchar,HI.HoraIni,108)		as HoraIni,
Convert(varchar,HF.HoraFin,108)		as HoraFin,
CONVERT(NUMERIC(6,2), ROUND(datediff(mi,HI.HoraIni,HF.Horafin)/60.0,2)) as TotalHoras,
L.Referencia  as Cosecha,
ISNULL(LE.Dividido,''X'')	as Dividido,
ISNULL(LE.KilosNetos,0) as KgNetoVolcados,
LE.CantidadBins as BinsVolcados,
L.Estado as Estado,
Em.Descripcion as  Empaque,
Eb.Descripcion as Volcado,
E.Descripcion as Especie,
V.NombreCorto as Var,
V.Descripcion as Variedad,
P.RazonSocial as Productor,
PC.SPA as Quinta,
ARSEC.DescripcionSeccion + '' '' + ISNULL(FC.Descripcion,'''') as Ubicación

from LoteEmboquille LE WITH (NOLOCK)
Inner Join Lote L  WITH (NOLOCK) ON LE.idLote = L.IdLote
Inner Join TipoLote TL  WITH (NOLOCK) ON L.idTipoLote = TL.idTipoLote
Inner Join Empaque Em   WITH (NOLOCK) ON LE.IdEmpaque = Em.IdEmpaque
Inner Join Emboquille Eb   WITH (NOLOCK) ON LE.IdEmboquille = Eb.IdEmboquille
Inner Join Especie E   WITH (NOLOCK) ON L.IdEspecie = E.IdEspecie
Inner Join Variedad V   WITH (NOLOCK) ON L.IdEspecie = V.IdEspecie AND L.IdVariedad = V.IdVariedad
Inner join Productor P   WITH (NOLOCK) ON L.IdProductor = P.IdProductor
Inner join ProductorChacra PC   WITH (NOLOCK) ON L.IdProductor = PC.IdProductor AND L.IdChacra = PC.IdChacra

Inner Join (SELECT MIN(LEI.FechaHoraComienzo) as HoraIni, LEI.FechaProduccion, LI.IdEspecie 
			FROM LoteEmboquille LEI   WITH (NOLOCK)  INNER JOIN Lote LI   WITH (NOLOCK) ON LEI.IdLote = LI.IdLote
			WHERE Year(fechaproduccion) = Year(FechaHoraComienzo) AND Month(fechaproduccion) = Month(FechaHoraComienzo) 
			and Day(fechaproduccion) = Day(FechaHoraComienzo)
			GROUP BY LEI.FechaProduccion,LI.IdEspecie ) as HI 
			ON HI.IdEspecie = L.IdEspecie AND HI.fechaProduccion = LE.fechaProduccion

Inner Join (SELECT MAX(LEF.FechaHoraFin) as HoraFin, LEF.FechaProduccion, LF.IdEspecie 
			FROM LoteEmboquille LEF  WITH (NOLOCK)  INNER JOIN Lote LF  WITH (NOLOCK)  ON LEF.IdLote = LF.IdLote
			WHERE Year(fechaproduccion) = Year(FechaHoraComienzo) AND Month(fechaproduccion) = Month(FechaHoraComienzo) 
			and Day(fechaproduccion) = Day(FechaHoraComienzo)
			GROUP BY LEF.FechaProduccion,LF.IdEspecie ) as HF 
			ON HF.IdEspecie = L.IdEspecie AND HF.fechaProduccion = LE.fechaProduccion

LEFT JOIN AreaSecciones ARSEC WITH (NOLOCK)  ON ARSEC.IdArea = L.IdArea and ARSEC.IdSeccion = L.IdSeccion
LEFT JOIN FrioCamara FC WITH (NOLOCK)  ON L.idSeccion = FC.idFrio AND L.IdCamara = FC.IdCamara
where L.Estado = ''M''
AND Em.Descripcion =  ''[glo_empaque]'' 
AND CAST(LE.FechaHoraComienzo AS date) BETWEEN ''[glo_fecha_desde]'' AND ''[glo_fecha_hasta]'' ', N'20250829', N'20251217', N'NORIDEL__NM__Lotes_Volcados__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'MOVBALANZAS', N'form', N'root/Balanza', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_informes'), N'dbo.MOVBALANZAS', N'IDMOV', N'SELECT * FROM dbo.MOVBALANZAS ORDER BY IDMOV DESC', N'20250813', N'20260508', N'NORIDEL__NM__MOVBALANZAS__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'Pallets_Definitivos_Sin_Clientes', N'cons', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), N'dbo.Catalogo_Pallets_Definitivos', NULL, N'SELECT  isnull(convert(varchar,OrdenCarga.IdOrdenCarga),''Sin Información'') AS OrdenCarga, 
	ContraMarca.Descripcion AS Contramarca,
	Calibre.Descripcion as Calibre, 
	PP.IdProduccionPallet AS Pallet, 
    Envase.Descripcion AS Envase,
	Marca.Descripcion AS Marca ,
	isnull(Cliente.RazonSocial,''Sin Información'') AS Cliente, 
	Variedad.Descripcion AS Variedad, 
	case PP.Estado 
		WHEN ''D'' THEN ''Despachado'' 
		WHEN ''F'' THEN ''En Frío''
		WHEN ''P'' THEN ''Pre Frío'' 
		WHEN ''R'' THEN ''Rearmado''
	end 	as Estado, 
	ISNULL(P.RazonSocial,'''') as Productor,
	CONVERT(VARCHAR,Frio.IdFrio) +''-''+ Frio.Descripcion as Frio,
	Embalaje.Descripcion AS Embalaje, 
	Especie.Descripcion AS Especie, 
	Categoria.Descripcion AS Categoria,
	CONVERT(VARCHAR,PP.FechaProduccion,103) AS FechaProduccion,
	(SELECT MIN(Referencia) FROM Lote WHERE IdLote IN (SELECT * FROM .dbo.StringSplit(.dbo.Lotes_Pallet(PP.idProduccionPallet),'',''))) as FechaCosecha,
	1 AS Cantidad,
	PP.CantidadEnvases AS CantEnvases, 
	PP.peso AS PesoNeto
	
FROM  ProduccionPallet PP  WITH (NOLOCK) 
	INNER JOIN	Variedad  WITH (NOLOCK) ON PP.IdEspecie = Variedad.IdEspecie AND PP.IdVariedad = Variedad.IdVariedad 
	INNER JOIN	Especie  WITH (NOLOCK) ON Variedad.IdEspecie = Especie.IdEspecie 
	INNER JOIN	Envase  WITH (NOLOCK) ON PP.IdEnvase = Envase.IdEnvase 
	INNER JOIN	Embalaje  WITH (NOLOCK) ON PP.IdEmbalaje = Embalaje.idEmbalaje 
	INNER JOIN	Categoria  WITH (NOLOCK) ON PP.IdCategoria = Categoria.IdCategoria 
	INNER JOIN	TipoPallet  WITH (NOLOCK) ON PP.IdTipoPallet = TipoPallet.IdTipoPallet 
	LEFT OUTER JOIN	ContraMarca  WITH (NOLOCK) ON PP.IdContraMarca = ContraMarca.IdContraMarca 
	INNER JOIN	Marca  WITH (NOLOCK) ON PP.IdMarca = Marca.IdMarca 
	LEFT JOIN	Frio  WITH (NOLOCK) ON PP.IdSeccion = Frio.IdFrio
	LEFT JOIN   FrioCamara FC  WITH (NOLOCK) ON Frio.IdFrio = FC.IdFrio AND PP.IdCamara = FC.IdCamara
	INNER JOIN	Empaque  WITH (NOLOCK) ON PP.IdEmpaque = Empaque.idEmpaque 
	INNER JOIN	Calibre  WITH (NOLOCK) ON PP.IdCalibre = Calibre.IdCalibre 
				AND PP.IdEnvase = Calibre.IdEnvase
				AND PP.IdEspecie = Calibre.IdEspecie

	LEFT  JOIN 	OrdenCarga	 WITH (NOLOCK) ON PP.IdOrdenCarga = OrdenCarga.IdOrdenCarga 
	INNER JOIN 	Cliente  WITH (NOLOCK) ON OrdenCarga.idCliente = Cliente.IdCliente 
	LEFT JOIN (SELECT idProduccionPallet, MAX(IdLote) as idLote FROM ProduccionPalletLote WITH (NOLOCK)
		        GROUP BY idProduccionPallet ) PL ON PL.IdProduccionPallet = PP.IdProduccionPallet
	LEFT JOIN Lote L ON PL.IdLote = L.IdLote
	LEFT JOIN Productor P ON P.IdProductor = L.IdProductor

WHERE UPPER(PP.Estado) not in(''B'',''D'') and TipoPallet.PoD = ''D'' and OrdenCarga.IdOrdenCarga IN(''1125'',''2225'',''3325'')', N'20250620', N'20251217', N'NORIDEL__NM__Pallets_Definitivos_Sin_Clientes__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'Pallets_Definitivos', N'cons', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), N'dbo.Catalogo_Pallets_Definitivos', NULL, N'SELECT 1 as id,    
    ISNULL(CONVERT(VARCHAR, OrdenCarga.IdOrdenCarga), ''Sin Información'') AS Orden, 
    ContraMarca.Descripcion AS Contramarca,
    Calibre.Descripcion AS Calibre, 
    Envase.Descripcion AS Envase,
    Marca.Descripcion AS Marca,
    ISNULL(Cliente.RazonSocial, ''Sin Información'') AS Cliente, 
    Variedad.Descripcion AS Variedad, 
    CASE PP.Estado 
        WHEN ''D'' THEN ''Despachado'' 
        WHEN ''F'' THEN ''En Frío''
        WHEN ''P'' THEN ''Pre Frío'' 
        WHEN ''R'' THEN ''Rearmado''
    END AS Estado, 
    ISNULL(P.RazonSocial, '''') AS Productor,
    TipoPallet.Descripcion AS TipoPallet,
    CASE 
        WHEN UPPER(ISNULL(PP.Obs2, '''')) LIKE '''' THEN ''Manual'' 
        ELSE ''Automatico'' 
    END AS FormaEmbalado,
    CONVERT(VARCHAR, Frio.IdFrio) + ''-'' + Frio.Descripcion AS Frio,
    PP.FechaHora AS Fecha,
    RIGHT(''00'' + CONVERT(VARCHAR(2), DATEPART(hh, PP.FechaHora)), 2) + '':'' +
    RIGHT(''00'' + CONVERT(VARCHAR(2), DATEPART(n, PP.FechaHora)), 2) AS Hora, 
    DATEPART(ISO_WEEK, PP.FechaProduccion) AS SemanaProd,
    Embalaje.Descripcion AS Embalaje, 
    Especie.Descripcion AS Especie, 
    Categoria.Descripcion AS Categoria,
    CONVERT(VARCHAR, PP.FechaProduccion, 103) AS Produccion,
    ISNULL(PP.Obs3, '''') AS O_Obs3,
    ISNULL(L.IdCuadro, '''') AS SPA,
    .dbo.Lotes_Pallet(PP.idProduccionPallet) AS Lote,
    .dbo.Cuadros_Pallet(PP.IdProduccionPallet) AS Cuadros,
    (SELECT MIN(Referencia) 
     FROM Lote 
     WHERE IdLote IN (
         SELECT * 
         FROM .dbo.StringSplit(.dbo.Lotes_Pallet(PP.idProduccionPallet), '','')
     )
    ) AS FecCosecha,
    1 AS Cantidad,
    PP.CantidadEnvases AS CantEnvases, 
    PP.Peso AS PesoNeto,
    PP.IdProduccionPallet as Pallet
	
FROM ProduccionPallet PP WITH (NOLOCK) 
    INNER JOIN Variedad  WITH (NOLOCK) 
        ON PP.IdEspecie = Variedad.IdEspecie 
        AND PP.IdVariedad = Variedad.IdVariedad 
    INNER JOIN Especie  WITH (NOLOCK) 
        ON Variedad.IdEspecie = Especie.IdEspecie 
    INNER JOIN Envase  WITH (NOLOCK) 
        ON PP.IdEnvase = Envase.IdEnvase 
    INNER JOIN Embalaje  WITH (NOLOCK) 
        ON PP.IdEmbalaje = Embalaje.IdEmbalaje 
    INNER JOIN Categoria  WITH (NOLOCK) 
        ON PP.IdCategoria = Categoria.IdCategoria 
    INNER JOIN TipoPallet  WITH (NOLOCK) 
        ON PP.IdTipoPallet = TipoPallet.IdTipoPallet 
    LEFT OUTER JOIN ContraMarca  WITH (NOLOCK) 
        ON PP.IdContraMarca = ContraMarca.IdContraMarca 
    INNER JOIN Marca  WITH (NOLOCK) 
        ON PP.IdMarca = Marca.IdMarca 
    LEFT JOIN Frio  WITH (NOLOCK) 
        ON PP.IdSeccion = Frio.IdFrio
    LEFT JOIN FrioCamara FC  WITH (NOLOCK) 
        ON Frio.IdFrio = FC.IdFrio 
        AND PP.IdCamara = FC.IdCamara
    INNER JOIN Empaque  WITH (NOLOCK) 
        ON PP.IdEmpaque = Empaque.IdEmpaque 
    INNER JOIN Calibre  WITH (NOLOCK) 
        ON PP.IdCalibre = Calibre.IdCalibre 
        AND PP.IdEnvase = Calibre.IdEnvase
        AND PP.IdEspecie = Calibre.IdEspecie
    LEFT JOIN OrdenCarga WITH (NOLOCK) 
        ON PP.IdOrdenCarga = OrdenCarga.IdOrdenCarga 
    LEFT JOIN OrdenCargaLinea OCL  WITH (NOLOCK) 
        ON PP.IdOrdenCarga = OCL.IdOrdenCarga 
        AND PP.IdEspecie = OCL.IdEspecie 
        AND PP.IdVariedad = OCL.IdVariedad
        AND PP.IdEnvase = OCL.IdEnvase  
        AND PP.IdCalibre = OCL.IdCalibre
    INNER JOIN Cliente  WITH (NOLOCK) 
        ON OrdenCarga.IdCliente = Cliente.IdCliente 
    LEFT JOIN (
        SELECT idProduccionPallet, MAX(IdLote) AS IdLote 
        FROM ProduccionPalletLote WITH (NOLOCK)
        GROUP BY idProduccionPallet
    ) PL ON PL.IdProduccionPallet = PP.IdProduccionPallet
    LEFT JOIN Lote L 
        ON PL.IdLote = L.IdLote
    LEFT JOIN Pr ...(truncado)', N'20250620', N'20260415', N'NORIDEL__NM__Pallets_Definitivos__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'Resumen_Men_Volcado', N'cons', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, N'with detalle
as(Select year(LE.fechaProduccion) as Año ,MONTH(LE.fechaProduccion) as Mes,
		   DATENAME(month,LE.fechaProduccion) as Nombre,
		case when Ba.IdBascula = 3 then ISNULL(LE.KilosNetos,0) else 0 end as Kgs_Exportacion,
		case when Ba.IdBascula <> 3 then ISNULL(LE.KilosNetos,0) else 0 end as Kgs_MInterno,
		LE.CantidadBins as BinsVolcados,
		case when Ba.IdBascula = 3 then ''Exportacion'' else ''Mercado Interno'' end as Balanza
	from LoteEmboquille LE WITH (NOLOCK)
	Inner Join Lote L  WITH (NOLOCK) ON LE.idLote = L.IdLote
	Inner Join TipoLote TL  WITH (NOLOCK) ON L.idTipoLote = TL.idTipoLote
	Inner Join Emboquille Eb   WITH (NOLOCK) ON LE.IdEmboquille = Eb.IdEmboquille
	Inner Join Especie E   WITH (NOLOCK) ON L.IdEspecie = E.IdEspecie
	Inner Join Variedad V   WITH (NOLOCK) ON L.IdEspecie = V.IdEspecie AND L.IdVariedad = V.IdVariedad
	LEFT JOIN Bascula Ba WITH (NOLOCK) ON L.IdBascula = Ba.idBascula
	where L.Estado = ''M'' and l.IdEspecie IN(10,20,40)
), detalle1 as (
	Select year(LE.fechaProduccion) as Año ,MONTH(LE.fechaProduccion) as Mes,
		   DATENAME(month,LE.fechaProduccion) as Nombre,
		case when Ba.IdBascula = 3 then ISNULL(LE.KilosNetos,0) else 0 end as Kgs_Exportacion,
		case when Ba.IdBascula <> 3 then ISNULL(LE.KilosNetos,0) else 0 end as Kgs_MInterno,
		LE.CantidadBins as BinsVolcados,
		case when Ba.IdBascula = 3 then ''Exportacion'' else ''Mercado Interno'' end as Balanza
	from zafra2024..LoteEmboquille LE WITH (NOLOCK)
	Inner Join zafra2024..Lote L  WITH (NOLOCK) ON LE.idLote = L.IdLote
	Inner Join zafra2024..TipoLote TL  WITH (NOLOCK) ON L.idTipoLote = TL.idTipoLote
	Inner Join zafra2024..Emboquille Eb   WITH (NOLOCK) ON LE.IdEmboquille = Eb.IdEmboquille
	Inner Join zafra2024..Especie E   WITH (NOLOCK) ON L.IdEspecie = E.IdEspecie
	Inner Join zafra2024..Variedad V   WITH (NOLOCK) ON L.IdEspecie = V.IdEspecie AND L.IdVariedad = V.IdVariedad
	LEFT JOIN zafra2024..Bascula Ba WITH (NOLOCK) ON L.IdBascula = Ba.idBascula
	where L.Estado = ''M'' and l.IdEspecie IN(10,20,40)
), detalle2 as (
	Select year(LE.fechaProduccion) as Año ,MONTH(LE.fechaProduccion) as Mes,
		   DATENAME(month,LE.fechaProduccion) as Nombre,
		case when Ba.IdBascula = 3 then ISNULL(LE.KilosNetos,0) else 0 end as Kgs_Exportacion,
		case when Ba.IdBascula <> 3 then ISNULL(LE.KilosNetos,0) else 0 end as Kgs_MInterno,
		LE.CantidadBins as BinsVolcados,
		case when Ba.IdBascula = 2 then ''Exportacion'' else ''Mercado Interno'' end as Balanza
	from zafra2023..LoteEmboquille LE WITH (NOLOCK)
	Inner Join zafra2023..Lote L  WITH (NOLOCK) ON LE.idLote = L.IdLote
	Inner Join zafra2023..TipoLote TL  WITH (NOLOCK) ON L.idTipoLote = TL.idTipoLote
	Inner Join zafra2023..Emboquille Eb   WITH (NOLOCK) ON LE.IdEmboquille = Eb.IdEmboquille
	Inner Join zafra2023..Especie E   WITH (NOLOCK) ON L.IdEspecie = E.IdEspecie
	Inner Join zafra2023..Variedad V   WITH (NOLOCK) ON L.IdEspecie = V.IdEspecie AND L.IdVariedad = V.IdVariedad
	LEFT JOIN zafra2023..Bascula Ba WITH (NOLOCK) ON L.IdBascula = Ba.idBascula
	where L.Estado = ''M'' and l.IdEspecie IN(10,20,40)
)
select
dt.balanza as O_Balanza,
dt.año as H_Año,
dt.mes as O_Mes,
max(dt.nombre) as V_NombreMes,
sum(dt.Kgs_Exportacion) as C_exportacion,
sum(dt.Kgs_MInterno) as C_MInterno
from detalle as dt
group by dt.balanza,dt.año,dt.mes
union
select
dt1.balanza as O_Balanza,
dt1.año as H_Año,
dt1.mes as O_Mes,
max(dt1.nombre) as V_NombreMes,
sum(dt1.Kgs_Exportacion) as C_exportacion,
sum(dt1.Kgs_MInterno) as C_MInterno
from detalle1 as dt1
group by dt1.balanza,dt1.año,dt1.mes
union
select
dt2.balanza as O_Balanza,
dt2.año as H_Año,
dt2.mes as O_Mes,
max(dt2.nombre) as V_NombreMes,
sum(dt2.Kgs_Exportacion) as C_exportacion,
sum(dt2.Kgs_MInterno) as C_MInterno
from d ...(truncado)', N'20260307', N'20260307', N'NORIDEL__NM__Resumen_Men_Volcado__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'Resumen_Mensual_Volcado', N'cons', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, N'	Select year(LE.fechaProduccion) as Año ,MONTH(LE.fechaProduccion) as Mes,
		   max(DATENAME(month,LE.fechaProduccion)) as Nombre,
		   sum(ISNULL(LE.KilosNetos,0)) as Kgs_Volcados,
		   sum(LE.CantidadBins) as BinsVolcados,
		   max(Ba.descripcion) as Balanza
	from LoteEmboquille LE WITH (NOLOCK)
	Inner Join Lote L  WITH (NOLOCK) ON LE.idLote = L.IdLote
	Inner Join TipoLote TL  WITH (NOLOCK) ON L.idTipoLote = TL.idTipoLote
	Inner Join Emboquille Eb   WITH (NOLOCK) ON LE.IdEmboquille = Eb.IdEmboquille
	Inner Join Especie E   WITH (NOLOCK) ON L.IdEspecie = E.IdEspecie
	Inner Join Variedad V   WITH (NOLOCK) ON L.IdEspecie = V.IdEspecie AND L.IdVariedad = V.IdVariedad
	LEFT JOIN Bascula Ba WITH (NOLOCK) ON L.IdBascula = Ba.idBascula
	where L.Estado = ''M'' and l.IdEspecie IN(10,20,40)
	group by year(LE.fechaProduccion),Ba.IdBascula,MONTH(LE.fechaProduccion)
	union
	Select year(LE.fechaProduccion) as Año ,MONTH(LE.fechaProduccion) as Mes,
		   max(DATENAME(month,LE.fechaProduccion)) as Nombre,
		   sum(ISNULL(LE.KilosNetos,0)) as Kgs_Volcados,
		   sum(LE.CantidadBins) as BinsVolcados,
		   max(Ba.descripcion) as Balanza
	from Zafra2024..LoteEmboquille LE WITH (NOLOCK)
	Inner Join Zafra2024..Lote L  WITH (NOLOCK) ON LE.idLote = L.IdLote
	Inner Join Zafra2024..TipoLote TL  WITH (NOLOCK) ON L.idTipoLote = TL.idTipoLote
	Inner Join Zafra2024..Emboquille Eb   WITH (NOLOCK) ON LE.IdEmboquille = Eb.IdEmboquille
	Inner Join Zafra2024..Especie E   WITH (NOLOCK) ON L.IdEspecie = E.IdEspecie
	Inner Join Zafra2024..Variedad V   WITH (NOLOCK) ON L.IdEspecie = V.IdEspecie AND L.IdVariedad = V.IdVariedad
	LEFT JOIN Zafra2024..Bascula Ba WITH (NOLOCK) ON L.IdBascula = Ba.idBascula
	where L.Estado = ''M'' and l.IdEspecie IN(10,20,40)
	group by year(LE.fechaProduccion),Ba.IdBascula,MONTH(LE.fechaProduccion)
	union
	Select year(LE.fechaProduccion) as Año ,MONTH(LE.fechaProduccion) as Mes,
		   max(DATENAME(month,LE.fechaProduccion)) as Nombre,
		   sum(ISNULL(LE.KilosNetos,0)) as Kgs_Volcados,
		   sum(LE.CantidadBins) as BinsVolcados,
		   max(Ba.descripcion) as Balanza
	from Zafra2023..LoteEmboquille LE WITH (NOLOCK)
	Inner Join Zafra2023..Lote L  WITH (NOLOCK) ON LE.idLote = L.IdLote
	Inner Join Zafra2023..TipoLote TL  WITH (NOLOCK) ON L.idTipoLote = TL.idTipoLote
	Inner Join Zafra2023..Emboquille Eb   WITH (NOLOCK) ON LE.IdEmboquille = Eb.IdEmboquille
	Inner Join Zafra2023..Especie E   WITH (NOLOCK) ON L.IdEspecie = E.IdEspecie
	Inner Join Zafra2023..Variedad V   WITH (NOLOCK) ON L.IdEspecie = V.IdEspecie AND L.IdVariedad = V.IdVariedad
	LEFT JOIN Zafra2023..Bascula Ba WITH (NOLOCK) ON L.IdBascula = Ba.idBascula
	where L.Estado = ''M'' and l.IdEspecie IN(10,20,40)
	group by year(LE.fechaProduccion),Ba.IdBascula,MONTH(LE.fechaProduccion)
', N'20250801', N'20251217', N'NORIDEL__NM__Resumen_Mensual_Volcado__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'Ritmos_Procesos', N'container', N'root/Consultas', NULL, NULL, NULL, NULL, N'20251016', N'20251217', N'NORIDEL__NM__Ritmos_Procesos__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'Stock_Playa', N'cons', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, N'Em.descripcion;V.Descripcion', N'SELECT 
	Em.descripcion as Empaque,
	E.Descripcion as Especie, 
	V.Descripcion as Variedad,
	L.idLote as Lote,
	Ba.descripcion as Balanza,
	Di.descripcion as Destino,
	convert(varchar,L.FechaIngreso,102) as  Feccha,  
	PC.Descripcion as  Chacra, 
	P.RazonSocial as Productor,
	TL.Descripcion  as TipoLote,
	(L.CantidadBins - L.cantidadBinsDesp) AS Ingresados, 
	L.CantidadBinsEmb as Volcados,
	(L.CantidadBins - L.cantidadBinsDesp -L.CantidadBinsEmb) as Saldo,
	convert(numeric(18,0),L.KilosNetos) as KgIngreso,
	convert(numeric(18,0),(L.KilosNetos/L.CantidadBins)*L.CantidadBinsEmb) as KgVolcados,
	convert(numeric(18,0),(L.KilosNetos/L.CantidadBins)*(L.CantidadBins - L.cantidadBinsDesp -	L.CantidadBinsEmb)) as KgStock

FROM  Lote L WITH (NOLOCK)
INNER JOIN TipoLote TL WITH (NOLOCK) ON L.IdTipoLote = TL.IdTipoLote
INNER JOIN Variedad V WITH (NOLOCK) ON L.idVariedad = V.idVariedad AND L.idEspecie = V.idEspecie
INNER JOIN Especie E WITH (NOLOCK) ON L.idEspecie = E.idEspecie
INNER JOIN Productor P WITH (NOLOCK) ON L.idProductor = P.idProductor
INNER JOIN Empaque Em WITH (NOLOCK) ON L.IdSeccion = Em.idEmpaque
LEFT  JOIN ProductorChacra PC WITH (NOLOCK) ON (L.IdProductor = PC.IdProductor AND L.IdChacra = PC.IdChacra)  
LEFT  JOIN Responsable R WITH (NOLOCK) ON R.IdResponsable = L.idCapataz
LEFT  JOIN Bascula Ba WITH (NOLOCK) ON L.IdBascula = Ba.idBascula 
LEFT  JOIN DestinoInterno Di WITH (NOLOCK) ON l.IdDestinoSugerido = Di.idDestinoInterno 

WHERE L.Estado = ''E''
and l.IdBascula = 3 and Em.idEmpaque = 2', N'20250726', N'20260307', N'NORIDEL__NM__Stock_Playa__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'TRANSPORTISTAS', N'form', N'root/Balanza', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_informes'), N'dbo.TRANSPORTISTAS', N'IDTRANSPORTISTA', N'SELECT * FROM dbo.TRANSPORTISTAS', N'20250813', N'20260508', N'NORIDEL__NM__TRANSPORTISTAS__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'abm_consultas_form', N'blank', N'root/concentrador_consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), NULL, NULL, NULL, N'20260407', N'20260413', N'NORIDEL__NM__abm_consultas_form__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'abm_consultas_lista', N'blank', N'root/concentrador_consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), NULL, NULL, NULL, N'20260407', N'20260408', N'NORIDEL__NM__abm_consultas_lista__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'app_menu', N'menutree', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), N'___NM_APL_MEN___', NULL, N'a:1:{s:10:"menu_itens";a:6:{i:0;a:18:{s:2:"id";s:6:"item_1";s:5:"label";s:8:"INFORMES";s:4:"link";s:0:"";s:6:"target";s:5:"_self";s:7:"icon_on";s:0:"";s:4:"hint";s:0:"";s:7:"display";s:20:"text_fontawesomeicon";s:16:"display_position";s:10:"text_right";s:4:"type";s:0:"";s:8:"icon_aba";s:0:"";s:17:"icon_aba_inactive";s:0:"";s:7:"icon_fa";s:21:"fas fa-bars-staggered";s:10:"group_type";s:0:"";s:9:"css_style";s:0:"";s:10:"icon_color";s:7:"#fa6603";s:16:"icon_color_hover";s:0:"";s:19:"icon_color_disabled";s:7:"#8a878a";s:10:"menu_itens";a:3:{i:0;a:18:{s:2:"id";s:6:"item_3";s:5:"label";s:10:"Operativos";s:4:"link";s:0:"";s:6:"target";s:5:"_self";s:7:"icon_on";s:0:"";s:4:"hint";s:0:"";s:7:"display";s:20:"text_fontawesomeicon";s:16:"display_position";s:10:"text_right";s:4:"type";s:0:"";s:8:"icon_aba";s:0:"";s:17:"icon_aba_inactive";s:0:"";s:7:"icon_fa";s:18:"fas fa-align-right";s:10:"group_type";s:0:"";s:9:"css_style";s:0:"";s:10:"icon_color";s:0:"";s:16:"icon_color_hover";s:0:"";s:19:"icon_color_disabled";s:0:"";s:10:"menu_itens";a:5:{i:0;a:18:{s:2:"id";s:7:"item_19";s:5:"label";s:14:"Fruta a Volcar";s:4:"link";s:14:"Fruta_a_Volcar";s:6:"target";s:5:"_self";s:7:"icon_on";s:0:"";s:4:"hint";s:0:"";s:7:"display";s:8:"text_img";s:16:"display_position";s:10:"text_right";s:4:"type";s:0:"";s:8:"icon_aba";s:0:"";s:17:"icon_aba_inactive";s:0:"";s:7:"icon_fa";s:10:"fas fa-cog";s:10:"group_type";s:0:"";s:9:"css_style";s:0:"";s:10:"icon_color";s:0:"";s:16:"icon_color_hover";s:0:"";s:19:"icon_color_disabled";s:0:"";s:10:"menu_itens";a:0:{}}i:1;a:18:{s:2:"id";s:7:"item_17";s:5:"label";s:20:"Pallets Sin Clientes";s:4:"link";s:32:"Pallets_Definitivos_Sin_Clientes";s:6:"target";s:5:"_self";s:7:"icon_on";s:0:"";s:4:"hint";s:0:"";s:7:"display";s:8:"text_img";s:16:"display_position";s:10:"text_right";s:4:"type";s:0:"";s:8:"icon_aba";s:0:"";s:17:"icon_aba_inactive";s:0:"";s:7:"icon_fa";s:10:"fas fa-cog";s:10:"group_type";s:0:"";s:9:"css_style";s:0:"";s:10:"icon_color";s:0:"";s:16:"icon_color_hover";s:0:"";s:19:"icon_color_disabled";s:0:"";s:10:"menu_itens";a:0:{}}i:2;a:18:{s:2:"id";s:7:"item_16";s:5:"label";s:19:"Pallets Definitivos";s:4:"link";s:16:"ctrl_rangofechas";s:6:"target";s:5:"_self";s:7:"icon_on";s:0:"";s:4:"hint";s:0:"";s:7:"display";s:8:"text_img";s:16:"display_position";s:10:"text_right";s:4:"type";s:0:"";s:8:"icon_aba";s:0:"";s:17:"icon_aba_inactive";s:0:"";s:7:"icon_fa";s:10:"fas fa-cog";s:10:"group_type";s:0:"";s:9:"css_style";s:0:"";s:10:"icon_color";s:0:"";s:16:"icon_color_hover";s:0:"";s:19:"icon_color_disabled";s:0:"";s:10:"menu_itens";a:0:{}}i:3;a:18:{s:2:"id";s:7:"item_27";s:5:"label";s:14:"Lotes Volcados";s:4:"link";s:18:"ctrl_fechasempaque";s:6:"target";s:5:"_self";s:7:"icon_on";s:0:"";s:4:"hint";s:0:"";s:7:"display";s:8:"text_img";s:16:"display_position";s:10:"text_right";s:4:"type";s:0:"";s:8:"icon_aba";s:0:"";s:17:"icon_aba_inactive";s:0:"";s:7:"icon_fa";s:10:"fas fa-cog";s:10:"group_type";s:0:"";s:9:"css_style";s:0:"";s:10:"icon_color";s:0:"";s:16:"icon_color_hover";s:0:"";s:19:"icon_color_disabled";s:0:"";s:10:"menu_itens";a:0:{}}i:4;a:18:{s:2:"id";s:7:"item_10";s:5:"label";s:27:"Stock en Playa Exportación";s:4:"link";s:11:"Stock_Playa";s:6:"target";s:5:"_self";s:7:"icon_on";s:0:"";s:4:"hint";s:0:"";s:7:"display";s:8:"text_img";s:16:"display_position";s:10:"text_right";s:4:"type";s:0:"";s:8:"icon_aba";s:0:"";s:17:"icon_aba_inactive";s:0:"";s:7:"icon_fa";s:10:"fas fa-cog";s:10:"group_type";s:0:"";s:9:"css_style";s:0:"";s:10:"icon_color";s:0:"";s:16:"icon_color_hover";s:0:"";s:19:"icon_color_disabled";s:0:"";s:10:"menu_itens";a:0:{}}}}i:1;a:18:{s:2:"id";s:6:"item_4";s:5:"label";s:8:"Gerencia";s:4:"link";s:0:"";s:6:"target";s:5:"_self";s:7:"icon_on";s:0:"";s:4:"hint";s:0:"";s:7:"display";s:8:"text_img";s:16:"display_position";s:10:" ...(truncado)', N'20250730', N'20251113', N'NORIDEL__NM__app_menu__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'blank', N'blank', N'root/Camaras', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_informes'), NULL, NULL, NULL, N'20251020', N'20251217', N'NORIDEL__NM__blank__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'blank_dashboard1', N'blank', N'root/dashboard', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, NULL, N'20251229', N'20251229', N'NORIDEL__NM__blank_dashboard1__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'blank_dashboard_24022026', N'blank', N'root', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, NULL, N'20260116', N'20260116', N'NORIDEL__NM__blank_dashboard_24022026__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'blank_dashboard', N'blank', N'root', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, NULL, N'20260116', N'20260414', N'NORIDEL__NM__blank_dashboard__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'concentrador_ejecutor', N'blank', N'root/concentrador_consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), NULL, NULL, NULL, N'20260407', N'20260414', N'NORIDEL__NM__concentrador_ejecutor__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'concentrador_ejecutor_copy', N'blank', N'root/concentrador_consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), NULL, NULL, NULL, N'20260407', N'20260408', N'NORIDEL__NM__concentrador_ejecutor_copy__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'control_Pallets_Definitivos', N'contr', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, NULL, N'20260307', N'20260415', N'NORIDEL__NM__control_Pallets_Definitivos__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'control_paradas_form_editable', N'form', N'root/aplicaciones', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.CONTROLPARADAS', NULL, N'SELECT * FROM dbo.CONTROLPARADAS', N'20260317', N'20260525', N'NORIDEL__NM__control_paradas_form_editable__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'ctrl_fechasempaque', N'contr', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, NULL, N'20250829', N'20251217', N'NORIDEL__NM__ctrl_fechasempaque__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'ctrl_rangofechas', N'contr', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, NULL, N'20250813', N'20251217', N'NORIDEL__NM__ctrl_rangofechas__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'dashboard', N'container', N'root/Consultas', NULL, NULL, NULL, NULL, N'20251016', N'20251217', N'NORIDEL__NM__dashboard__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'form_dbo_CAMIONES', N'form', N'root/abm', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.CAMIONES', N'IDCAMION;IDTRANSPORTISTA', N'SELECT * FROM dbo.CAMIONES', N'20260410', N'20260525', N'NORIDEL__NM__form_dbo_CAMIONES__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'form_dbo_CHOFERES', N'form', N'root/abm', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.CHOFERES', N'IDCHOFER', N'SELECT * FROM dbo.CHOFERES', N'20260410', N'20260525', N'NORIDEL__NM__form_dbo_CHOFERES__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'form_dbo_CONTROLPARADAS', N'form', N'root/Balanza', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_informes'), N'dbo.CONTROLPARADAS', NULL, N'SELECT * FROM dbo.CONTROLPARADAS', N'20260317', N'20260525', N'NORIDEL__NM__form_dbo_CONTROLPARADAS__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'form_dbo_LUGARES', N'form', N'root/abm', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.LUGARES', N'IDLUGAR', N'SELECT * FROM dbo.LUGARES', N'20260410', N'20260525', N'NORIDEL__NM__form_dbo_LUGARES__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'form_dbo_OPERARIOS', N'form', N'root/abm', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.OPERARIOS', N'IDOPERARIO', N'SELECT * FROM dbo.OPERARIOS', N'20260410', N'20260525', N'NORIDEL__NM__form_dbo_OPERARIOS__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'form_dbo_TURNOS', N'form', N'root/abm', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.TURNOS', NULL, N'SELECT * FROM dbo.TURNOS', N'20260410', N'20260525', N'NORIDEL__NM__form_dbo_TURNOS__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'grafica_procesos', N'chart', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_informes'), NULL, NULL, N'select r.loteroda,r.fecha,r.idespecie,e.Descripcion as especie,v.Descripcion as variedad,r.idvariedad,isnull(r.calibradora,0) as calibradora,isnull(r.dcte_sincera,0) as dcte_sincera,
isnull(r.volcado,0) as volcado,isnull(r.hscal,0) as hscal,isnull(r.hspar,0) as hspar,isnull(r.ritmo1,0) as ritmo1,
isnull(r.ritmo2,0) as ritmo2,isnull(r.observacion,'''') as observacion 
from informes..RITMOCALIBRADORA as r
left join dtrace..variedad as v on r.idvariedad=v.idvariedad and r.idespecie=v.idespecie 
left join dtrace..especie as e on r.idespecie=e.idespecie 
', N'20251016', N'20251217', N'NORIDEL__NM__grafica_procesos__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'movbalanzas_form_editable', N'form', N'root/aplicaciones', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.MOVBALANZAS', N'IDMOV', N'SELECT * FROM dbo.MOVBALANZAS ORDER BY IDMOV DESC', N'20250813', N'20260508', N'NORIDEL__NM__movbalanzas_form_editable__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'pruebas_totales_grid', N'cons', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, N'SELECT top 10 1 as id,    
    ISNULL(CONVERT(VARCHAR, OrdenCarga.IdOrdenCarga), ''Sin Información'') AS Orden, 
    ContraMarca.Descripcion AS Contramarca,
    Calibre.Descripcion AS Calibre, 
    Envase.Descripcion AS Envase,
    Marca.Descripcion AS Marca,
    ISNULL(Cliente.RazonSocial, ''Sin Información'') AS Cliente, 
    Variedad.Descripcion AS Variedad, 
    CASE PP.Estado 
        WHEN ''D'' THEN ''Despachado'' 
        WHEN ''F'' THEN ''En Frío''
        WHEN ''P'' THEN ''Pre Frío'' 
        WHEN ''R'' THEN ''Rearmado''
    END AS Estado, 
    ISNULL(P.RazonSocial, '''') AS Productor,
    TipoPallet.Descripcion AS TipoPallet,
    CASE 
        WHEN UPPER(ISNULL(PP.Obs2, '''')) LIKE '''' THEN ''Manual'' 
        ELSE ''Automatico'' 
    END AS FormaEmbalado,
    CONVERT(VARCHAR, Frio.IdFrio) + ''-'' + Frio.Descripcion AS Frio,
    PP.FechaHora AS Fecha,
    RIGHT(''00'' + CONVERT(VARCHAR(2), DATEPART(hh, PP.FechaHora)), 2) + '':'' +
    RIGHT(''00'' + CONVERT(VARCHAR(2), DATEPART(n, PP.FechaHora)), 2) AS Hora, 
    DATEPART(ISO_WEEK, PP.FechaProduccion) AS SemanaProd,
    Embalaje.Descripcion AS Embalaje, 
    Especie.Descripcion AS Especie, 
    Categoria.Descripcion AS Categoria,
    CONVERT(VARCHAR, PP.FechaProduccion, 103) AS Produccion,
    ISNULL(PP.Obs3, '''') AS O_Obs3,
    ISNULL(L.IdCuadro, '''') AS SPA,
    .dbo.Lotes_Pallet(PP.idProduccionPallet) AS Lote,
    .dbo.Cuadros_Pallet(PP.IdProduccionPallet) AS Cuadros,
    (SELECT MIN(Referencia) 
     FROM Lote 
     WHERE IdLote IN (
         SELECT * 
         FROM .dbo.StringSplit(.dbo.Lotes_Pallet(PP.idProduccionPallet), '','')
     )
    ) AS FecCosecha,
    1 AS Cantidad,
    PP.CantidadEnvases AS CantEnvases, 
    PP.Peso AS PesoNeto,
    PP.IdProduccionPallet as Pallet
	
FROM ProduccionPallet PP WITH (NOLOCK) 
    INNER JOIN Variedad  WITH (NOLOCK) 
        ON PP.IdEspecie = Variedad.IdEspecie 
        AND PP.IdVariedad = Variedad.IdVariedad 
    INNER JOIN Especie  WITH (NOLOCK) 
        ON Variedad.IdEspecie = Especie.IdEspecie 
    INNER JOIN Envase  WITH (NOLOCK) 
        ON PP.IdEnvase = Envase.IdEnvase 
    INNER JOIN Embalaje  WITH (NOLOCK) 
        ON PP.IdEmbalaje = Embalaje.IdEmbalaje 
    INNER JOIN Categoria  WITH (NOLOCK) 
        ON PP.IdCategoria = Categoria.IdCategoria 
    INNER JOIN TipoPallet  WITH (NOLOCK) 
        ON PP.IdTipoPallet = TipoPallet.IdTipoPallet 
    LEFT OUTER JOIN ContraMarca  WITH (NOLOCK) 
        ON PP.IdContraMarca = ContraMarca.IdContraMarca 
    INNER JOIN Marca  WITH (NOLOCK) 
        ON PP.IdMarca = Marca.IdMarca 
    LEFT JOIN Frio  WITH (NOLOCK) 
        ON PP.IdSeccion = Frio.IdFrio
    LEFT JOIN FrioCamara FC  WITH (NOLOCK) 
        ON Frio.IdFrio = FC.IdFrio 
        AND PP.IdCamara = FC.IdCamara
    INNER JOIN Empaque  WITH (NOLOCK) 
        ON PP.IdEmpaque = Empaque.IdEmpaque 
    INNER JOIN Calibre  WITH (NOLOCK) 
        ON PP.IdCalibre = Calibre.IdCalibre 
        AND PP.IdEnvase = Calibre.IdEnvase
        AND PP.IdEspecie = Calibre.IdEspecie
    LEFT JOIN OrdenCarga WITH (NOLOCK) 
        ON PP.IdOrdenCarga = OrdenCarga.IdOrdenCarga 
    LEFT JOIN OrdenCargaLinea OCL  WITH (NOLOCK) 
        ON PP.IdOrdenCarga = OCL.IdOrdenCarga 
        AND PP.IdEspecie = OCL.IdEspecie 
        AND PP.IdVariedad = OCL.IdVariedad
        AND PP.IdEnvase = OCL.IdEnvase  
        AND PP.IdCalibre = OCL.IdCalibre
    INNER JOIN Cliente  WITH (NOLOCK) 
        ON OrdenCarga.IdCliente = Cliente.IdCliente 
    LEFT JOIN (
        SELECT idProduccionPallet, MAX(IdLote) AS IdLote 
        FROM ProduccionPalletLote WITH (NOLOCK)
        GROUP BY idProduccionPallet
    ) PL ON PL.IdProduccionPallet = PP.IdProduccionPallet
    LEFT JOIN Lote L 
        ON PL.IdLote = L.IdLote
    LEFT  ...(truncado)', N'20250908', N'20251217', N'NORIDEL__NM__pruebas_totales_grid__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'pwbi_Indicadores_Gerencia', N'blank', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, NULL, N'20250730', N'20251217', N'NORIDEL__NM__pwbi_Indicadores_Gerencia__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'pwbi_Ingresos', N'blank', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, NULL, N'20250730', N'20251217', N'NORIDEL__NM__pwbi_Ingresos__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'pwbi_Liquidaciones_Exportacion', N'blank', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, NULL, N'20250730', N'20251217', N'NORIDEL__NM__pwbi_Liquidaciones_Exportacion__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'pwbi_Resumen_Liquidaciones', N'blank', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, NULL, N'20250808', N'20251217', N'NORIDEL__NM__pwbi_Resumen_Liquidaciones__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'pwbi_Stock_Playa', N'blank', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, NULL, N'20250730', N'20251217', N'NORIDEL__NM__pwbi_Stock_Playa__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'pwbi_Stock_Sin_Clientes', N'blank', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, NULL, N'20250730', N'20251217', N'NORIDEL__NM__pwbi_Stock_Sin_Clientes__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'pwbi_Volcado_Diario', N'blank', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, NULL, N'20250730', N'20251217', N'NORIDEL__NM__pwbi_Volcado_Diario__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'pwbi_embalado_cajas', N'blank', N'root/Consultas', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL, NULL, N'20250730', N'20251217', N'NORIDEL__NM__pwbi_embalado_cajas__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_Login', N'contrusr', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), NULL, NULL, N'SELECT * FROM ', N'20251217', N'20260410', N'NORIDEL__NM__sec_Login__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_add_2fa', N'contr', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), NULL, NULL, NULL, N'20251217', N'20251217', N'NORIDEL__NM__sec_add_2fa__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_change_pswd', N'contr', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), NULL, NULL, NULL, N'20251217', N'20251217', N'NORIDEL__NM__sec_change_pswd__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_control_2fa', N'contr', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), NULL, NULL, NULL, N'20251217', N'20251217', N'NORIDEL__NM__sec_control_2fa__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_form_add_users', N'form', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.sec_users', N'login', N'SELECT * FROM dbo.sec_users', N'20251217', N'20260508', N'NORIDEL__NM__sec_form_add_users__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_form_edit_users', N'form', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.sec_users', N'login', N'SELECT * FROM dbo.sec_users', N'20251217', N'20260508', N'NORIDEL__NM__sec_form_edit_users__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_form_sec_apps', N'form', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.sec_apps', N'app_name', N'SELECT * FROM dbo.sec_apps', N'20251217', N'20260508', N'NORIDEL__NM__sec_form_sec_apps__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_form_sec_groups', N'form', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.sec_groups', N'group_id', N'SELECT * FROM dbo.sec_groups', N'20251217', N'20260508', N'NORIDEL__NM__sec_form_sec_groups__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_form_sec_groups_apps', N'form', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.sec_groups_apps', NULL, N'SELECT * FROM dbo.sec_groups_apps', N'20251217', N'20260508', N'NORIDEL__NM__sec_form_sec_groups_apps__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_grid_sec_apps', N'cons', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.sec_apps', N'app_name', N'SELECT 
    app_name,
    description
FROM 
    dbo.sec_apps', N'20251217', N'20251217', N'NORIDEL__NM__sec_grid_sec_apps__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_grid_sec_groups', N'cons', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.sec_groups', N'group_id', N'SELECT 
    group_id,
    description
FROM 
    dbo.sec_groups', N'20251217', N'20251217', N'NORIDEL__NM__sec_grid_sec_groups__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_grid_sec_users', N'cons', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.sec_users', N'login', N'SELECT 
    picture,
    login,
    name,
    email,
    active,
    priv_admin
FROM 
    dbo.sec_users', N'20251217', N'20251217', N'NORIDEL__NM__sec_grid_sec_users__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_grid_sec_users_groups', N'cons', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.sec_users', N'login', N'SELECT
    login,
    name,
    email,
    active
FROM
    dbo.sec_users', N'20251217', N'20251217', N'NORIDEL__NM__sec_grid_sec_users_groups__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_menu', N'menu2', N'root', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'___NM_APL_MEN___', NULL, NULL, N'20251113', N'20260411', N'NORIDEL__NM__sec_menu__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_my_info', N'form', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.sec_users', N'login', N'SELECT * FROM dbo.sec_users WHERE [str_where]', N'20251217', N'20260508', N'NORIDEL__NM__sec_my_info__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_retrieve_pswd', N'contr', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), NULL, NULL, NULL, N'20251217', N'20251217', N'NORIDEL__NM__sec_retrieve_pswd__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_search_sec_groups', N'filter', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'dbo.sec_groups', N'group_id', N'SELECT 
    group_id
FROM 
    dbo.sec_groups', N'20251217', N'20251217', N'NORIDEL__NM__sec_search_sec_groups__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_settings', N'contr', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), NULL, NULL, NULL, N'20251217', N'20251217', N'NORIDEL__NM__sec_settings__NM__1.bkp');
INSERT INTO dbo.Cat_Aplicaciones (CodProyecto, CodAplicacion, TipoAplicacion, Carpeta, IdConexion, NombreTabla, CamposClave, ComandoSelect, FechaCreacion, FechaUltAcceso, ArchivoOrigen)
VALUES (N'NORIDEL', N'sec_sync_apps', N'contr', N'root/Security', (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), NULL, NULL, NULL, N'20251217', N'20251217', N'NORIDEL__NM__sec_sync_apps__NM__1.bkp');
GO

-------------------------------------------------------------------
-- 4. Tablas fisicas identificadas (nometabela de forms + FROM de consultas)
-------------------------------------------------------------------
INSERT INTO dbo.Cat_Tablas (NombreTabla, BaseOrigen, IdConexion, CamposClave, Observaciones)
VALUES (N'Lote', NULL, NULL, NULL, NULL);
INSERT INTO dbo.Cat_Tablas (NombreTabla, BaseOrigen, IdConexion, CamposClave, Observaciones)
VALUES (N'LoteEmboquille', NULL, NULL, NULL, NULL);
INSERT INTO dbo.Cat_Tablas (NombreTabla, BaseOrigen, IdConexion, CamposClave, Observaciones)
VALUES (N'dbo.CAMIONES', NULL, (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_informes'), N'IDTRANSPORTISTA;IDCAMION', NULL);
INSERT INTO dbo.Cat_Tablas (NombreTabla, BaseOrigen, IdConexion, CamposClave, Observaciones)
VALUES (N'dbo.CHOFERES', NULL, (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'IDCHOFER', NULL);
INSERT INTO dbo.Cat_Tablas (NombreTabla, BaseOrigen, IdConexion, CamposClave, Observaciones)
VALUES (N'dbo.CONTROLPARADAS', NULL, (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), NULL, NULL);
INSERT INTO dbo.Cat_Tablas (NombreTabla, BaseOrigen, IdConexion, CamposClave, Observaciones)
VALUES (N'dbo.Catalogo_Pallets_Definitivos', NULL, (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql'), NULL, NULL);
INSERT INTO dbo.Cat_Tablas (NombreTabla, BaseOrigen, IdConexion, CamposClave, Observaciones)
VALUES (N'dbo.LUGARES', NULL, (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_informes'), N'IDLUGAR', NULL);
INSERT INTO dbo.Cat_Tablas (NombreTabla, BaseOrigen, IdConexion, CamposClave, Observaciones)
VALUES (N'dbo.MOVBALANZAS', NULL, (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_informes'), N'IDMOV', NULL);
INSERT INTO dbo.Cat_Tablas (NombreTabla, BaseOrigen, IdConexion, CamposClave, Observaciones)
VALUES (N'dbo.OPERARIOS', NULL, (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'IDOPERARIO', NULL);
INSERT INTO dbo.Cat_Tablas (NombreTabla, BaseOrigen, IdConexion, CamposClave, Observaciones)
VALUES (N'dbo.TRANSPORTISTAS', NULL, (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_informes'), N'IDTRANSPORTISTA', NULL);
INSERT INTO dbo.Cat_Tablas (NombreTabla, BaseOrigen, IdConexion, CamposClave, Observaciones)
VALUES (N'dbo.TURNOS', NULL, (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), NULL, NULL);
INSERT INTO dbo.Cat_Tablas (NombreTabla, BaseOrigen, IdConexion, CamposClave, Observaciones)
VALUES (N'dbo.sec_apps', NULL, (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'app_name', NULL);
INSERT INTO dbo.Cat_Tablas (NombreTabla, BaseOrigen, IdConexion, CamposClave, Observaciones)
VALUES (N'dbo.sec_groups', NULL, (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'group_id', NULL);
INSERT INTO dbo.Cat_Tablas (NombreTabla, BaseOrigen, IdConexion, CamposClave, Observaciones)
VALUES (N'dbo.sec_groups_apps', NULL, (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), NULL, NULL);
INSERT INTO dbo.Cat_Tablas (NombreTabla, BaseOrigen, IdConexion, CamposClave, Observaciones)
VALUES (N'dbo.sec_users', NULL, (SELECT IdConexion FROM dbo.Cat_Conexiones WHERE NombreConexion = N'conn_mssql_gpacking'), N'login', NULL);
INSERT INTO dbo.Cat_Tablas (NombreTabla, BaseOrigen, IdConexion, CamposClave, Observaciones)
VALUES (N'informes..RITMOCALIBRADORA', NULL, NULL, NULL, NULL);
GO

-------------------------------------------------------------------
-- 5. Relaciones de navegacion (drill-down) entre aplicaciones ScriptCase
-------------------------------------------------------------------
INSERT INTO dbo.Cat_Relaciones_Navegacion (AppOrigen, AppDestino, TipoDestino, CampoEnlace, Parametros)
VALUES (N'MOVBALANZAS', N'CAMIONES', N'form', N'MATCAMION', N'C?#?IDMOV?#?IDTRANSPORTISTA?@?C?#?IDMOV?#?IDCAMION');
INSERT INTO dbo.Cat_Relaciones_Navegacion (AppOrigen, AppDestino, TipoDestino, CampoEnlace, Parametros)
VALUES (N'MOVBALANZAS', N'CAMIONES', N'form', N'MATZORRA', N'C?#?IDMOV?#?IDTRANSPORTISTA?@?C?#?IDMOV?#?IDCAMION');
INSERT INTO dbo.Cat_Relaciones_Navegacion (AppOrigen, AppDestino, TipoDestino, CampoEnlace, Parametros)
VALUES (N'control_Pallets_Definitivos', N'Pallets_Definitivos', N'cons', NULL, N'C?#?fecha_desde?#?glo_fecha_desde?@?C?#?fecha_hasta?#?glo_fecha_hasta?@?N?#??#?glo_app_destino');
INSERT INTO dbo.Cat_Relaciones_Navegacion (AppOrigen, AppDestino, TipoDestino, CampoEnlace, Parametros)
VALUES (N'movbalanzas_form_editable', N'CAMIONES', N'form', N'MATCAMION', N'C?#?IDMOV?#?IDTRANSPORTISTA?@?C?#?IDMOV?#?IDCAMION');
INSERT INTO dbo.Cat_Relaciones_Navegacion (AppOrigen, AppDestino, TipoDestino, CampoEnlace, Parametros)
VALUES (N'movbalanzas_form_editable', N'CAMIONES', N'form', N'MATZORRA', N'C?#?IDMOV?#?IDTRANSPORTISTA?@?C?#?IDMOV?#?IDCAMION');
INSERT INTO dbo.Cat_Relaciones_Navegacion (AppOrigen, AppDestino, TipoDestino, CampoEnlace, Parametros)
VALUES (N'sec_grid_sec_apps', N'sec_form_sec_apps', N'form', NULL, N'C?#?app_name?#?app_name');
INSERT INTO dbo.Cat_Relaciones_Navegacion (AppOrigen, AppDestino, TipoDestino, CampoEnlace, Parametros)
VALUES (N'sec_grid_sec_groups', N'sec_form_sec_groups', N'F', NULL, N'C?#?group_id?#?group_id');
INSERT INTO dbo.Cat_Relaciones_Navegacion (AppOrigen, AppDestino, TipoDestino, CampoEnlace, Parametros)
VALUES (N'sec_grid_sec_users', N'sec_form_edit_users', N'form', NULL, N'C?#?login?#?login?@?N?#?dbo.sec_users.?#?dbo.sec_users.usr_login');
INSERT INTO dbo.Cat_Relaciones_Navegacion (AppOrigen, AppDestino, TipoDestino, CampoEnlace, Parametros)
VALUES (N'sec_grid_sec_users_groups', N'sec_form_edit_users', N'form', NULL, N'C?#?login?#?login');
INSERT INTO dbo.Cat_Relaciones_Navegacion (AppOrigen, AppDestino, TipoDestino, CampoEnlace, Parametros)
VALUES (N'sec_search_sec_groups', N'sec_form_sec_groups_apps', N'F', NULL, N'C?#?group_id?#?group_id?@?N?#?dbo.sec_groups_apps.?#?dbo.sec_groups_apps.arr_apps');
GO

-------------------------------------------------------------------
-- 6. Relaciones inferidas desde JOINs de las sentencias SQL reales
-------------------------------------------------------------------
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Fruta_a_Volcar', N'INNER JOIN', N'TipoLote', N'TL', N'L.IdTipoLote = TL.IdTipoLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Fruta_a_Volcar', N'INNER JOIN', N'Variedad', N'V', N'L.idVariedad = V.idVariedad AND L.idEspecie = V.idEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Fruta_a_Volcar', N'INNER JOIN', N'Especie', N'E', N'L.idEspecie = E.idEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Fruta_a_Volcar', N'INNER JOIN', N'Productor', N'P', N'L.idProductor = P.idProductor');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Fruta_a_Volcar', N'INNER JOIN', N'Empaque', N'Em', N'L.IdSeccion = Em.idEmpaque');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Fruta_a_Volcar', N'LEFT  JOIN', N'ProductorChacra', N'PC', N'(L.IdProductor = PC.IdProductor AND L.IdChacra = PC.IdChacra)');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Fruta_a_Volcar', N'LEFT  JOIN', N'Responsable', N'R', N'R.IdResponsable = L.idCapataz');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Fruta_a_Volcar', N'LEFT  JOIN', N'Bascula', N'Ba', N'L.IdBascula = Ba.idBascula');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Fruta_a_Volcar', N'LEFT  JOIN', N'DestinoInterno', N'Di', N'l.IdDestinoSugerido = Di.idDestinoInterno');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Lotes_Volcados', N'INNER JOIN', N'Lote', N'L', N'LE.idLote = L.IdLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Lotes_Volcados', N'INNER JOIN', N'TipoLote', N'TL', N'L.idTipoLote = TL.idTipoLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Lotes_Volcados', N'INNER JOIN', N'Empaque', N'Em', N'LE.IdEmpaque = Em.IdEmpaque');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Lotes_Volcados', N'INNER JOIN', N'Emboquille', N'Eb', N'LE.IdEmboquille = Eb.IdEmboquille');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Lotes_Volcados', N'INNER JOIN', N'Especie', N'E', N'L.IdEspecie = E.IdEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Lotes_Volcados', N'INNER JOIN', N'Variedad', N'V', N'L.IdEspecie = V.IdEspecie AND L.IdVariedad = V.IdVariedad');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Lotes_Volcados', N'INNER JOIN', N'Productor', N'P', N'L.IdProductor = P.IdProductor');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Lotes_Volcados', N'INNER JOIN', N'ProductorChacra', N'PC', N'L.IdProductor = PC.IdProductor AND L.IdChacra = PC.IdChacra');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Lotes_Volcados', N'INNER JOIN', N'Lote', N'LI', N'LEI.IdLote = LI.IdLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Lotes_Volcados', N'INNER JOIN', N'Lote', N'LF', N'LEF.IdLote = LF.IdLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Lotes_Volcados', N'LEFT JOIN', N'AreaSecciones', N'ARSEC', N'ARSEC.IdArea = L.IdArea and ARSEC.IdSeccion = L.IdSeccion');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Lotes_Volcados', N'LEFT JOIN', N'FrioCamara', N'FC', N'L.idSeccion = FC.idFrio AND L.IdCamara = FC.IdCamara');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos_Sin_Clientes', N'INNER JOIN', N'Variedad', NULL, N'PP.IdEspecie = Variedad.IdEspecie AND PP.IdVariedad = Variedad.IdVariedad');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos_Sin_Clientes', N'INNER JOIN', N'Especie', NULL, N'Variedad.IdEspecie = Especie.IdEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos_Sin_Clientes', N'INNER JOIN', N'Envase', NULL, N'PP.IdEnvase = Envase.IdEnvase');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos_Sin_Clientes', N'INNER JOIN', N'Embalaje', NULL, N'PP.IdEmbalaje = Embalaje.idEmbalaje');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos_Sin_Clientes', N'INNER JOIN', N'Categoria', NULL, N'PP.IdCategoria = Categoria.IdCategoria');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos_Sin_Clientes', N'INNER JOIN', N'TipoPallet', NULL, N'PP.IdTipoPallet = TipoPallet.IdTipoPallet LEFT OUTER');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos_Sin_Clientes', N'JOIN', N'ContraMarca', NULL, N'PP.IdContraMarca = ContraMarca.IdContraMarca');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos_Sin_Clientes', N'INNER JOIN', N'Marca', NULL, N'PP.IdMarca = Marca.IdMarca');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos_Sin_Clientes', N'LEFT JOIN', N'Frio', NULL, N'PP.IdSeccion = Frio.IdFrio');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos_Sin_Clientes', N'LEFT JOIN', N'FrioCamara', N'FC', N'Frio.IdFrio = FC.IdFrio AND PP.IdCamara = FC.IdCamara');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos_Sin_Clientes', N'INNER JOIN', N'Empaque', NULL, N'PP.IdEmpaque = Empaque.idEmpaque');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos_Sin_Clientes', N'INNER JOIN', N'Calibre', NULL, N'PP.IdCalibre = Calibre.IdCalibre AND PP.IdEnvase = Calibre.IdEnvase AND PP.IdEspecie = Calibre.IdEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos_Sin_Clientes', N'LEFT  JOIN', N'OrdenCarga', NULL, N'PP.IdOrdenCarga = OrdenCarga.IdOrdenCarga');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos_Sin_Clientes', N'INNER JOIN', N'Cliente', NULL, N'OrdenCarga.idCliente = Cliente.IdCliente');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos_Sin_Clientes', N'LEFT JOIN', N'Lote', N'L', N'PL.IdLote = L.IdLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos_Sin_Clientes', N'LEFT JOIN', N'Productor', N'P', N'P.IdProductor = L.IdProductor');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'INNER JOIN', N'Variedad', NULL, N'PP.IdEspecie = Variedad.IdEspecie AND PP.IdVariedad = Variedad.IdVariedad');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'INNER JOIN', N'Especie', NULL, N'Variedad.IdEspecie = Especie.IdEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'INNER JOIN', N'Envase', NULL, N'PP.IdEnvase = Envase.IdEnvase');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'INNER JOIN', N'Embalaje', NULL, N'PP.IdEmbalaje = Embalaje.IdEmbalaje');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'INNER JOIN', N'Categoria', NULL, N'PP.IdCategoria = Categoria.IdCategoria');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'INNER JOIN', N'TipoPallet', NULL, N'PP.IdTipoPallet = TipoPallet.IdTipoPallet LEFT OUTER');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'JOIN', N'ContraMarca', NULL, N'PP.IdContraMarca = ContraMarca.IdContraMarca');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'INNER JOIN', N'Marca', NULL, N'PP.IdMarca = Marca.IdMarca');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'LEFT JOIN', N'Frio', NULL, N'PP.IdSeccion = Frio.IdFrio');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'LEFT JOIN', N'FrioCamara', N'FC', N'Frio.IdFrio = FC.IdFrio AND PP.IdCamara = FC.IdCamara');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'INNER JOIN', N'Empaque', NULL, N'PP.IdEmpaque = Empaque.IdEmpaque');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'INNER JOIN', N'Calibre', NULL, N'PP.IdCalibre = Calibre.IdCalibre AND PP.IdEnvase = Calibre.IdEnvase AND PP.IdEspecie = Calibre.IdEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'LEFT JOIN', N'OrdenCarga', NULL, N'PP.IdOrdenCarga = OrdenCarga.IdOrdenCarga');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'LEFT JOIN', N'OrdenCargaLinea', N'OCL', N'PP.IdOrdenCarga = OCL.IdOrdenCarga AND PP.IdEspecie = OCL.IdEspecie AND PP.IdVariedad = OCL.IdVariedad AND PP.IdEnvase = OCL.IdEnvase AND PP.IdCalibre = OCL.IdCalibre');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'INNER JOIN', N'Cliente', NULL, N'OrdenCarga.IdCliente = Cliente.IdCliente');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'LEFT JOIN', N'Lote', N'L', N'PL.IdLote = L.IdLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Pallets_Definitivos', N'LEFT JOIN', N'Productor', N'P', N'P.IdProductor = L.IdProductor');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'INNER JOIN', N'Lote', N'L', N'LE.idLote = L.IdLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'INNER JOIN', N'TipoLote', N'TL', N'L.idTipoLote = TL.idTipoLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'INNER JOIN', N'Emboquille', N'Eb', N'LE.IdEmboquille = Eb.IdEmboquille');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'INNER JOIN', N'Especie', N'E', N'L.IdEspecie = E.IdEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'INNER JOIN', N'Variedad', N'V', N'L.IdEspecie = V.IdEspecie AND L.IdVariedad = V.IdVariedad');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'LEFT JOIN', N'Bascula', N'Ba', N'L.IdBascula = Ba.idBascula');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'INNER JOIN', N'zafra2024..Lote', N'L', N'LE.idLote = L.IdLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'INNER JOIN', N'zafra2024..TipoLote', N'TL', N'L.idTipoLote = TL.idTipoLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'INNER JOIN', N'zafra2024..Emboquille', N'Eb', N'LE.IdEmboquille = Eb.IdEmboquille');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'INNER JOIN', N'zafra2024..Especie', N'E', N'L.IdEspecie = E.IdEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'INNER JOIN', N'zafra2024..Variedad', N'V', N'L.IdEspecie = V.IdEspecie AND L.IdVariedad = V.IdVariedad');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'LEFT JOIN', N'zafra2024..Bascula', N'Ba', N'L.IdBascula = Ba.idBascula');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'INNER JOIN', N'zafra2023..Lote', N'L', N'LE.idLote = L.IdLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'INNER JOIN', N'zafra2023..TipoLote', N'TL', N'L.idTipoLote = TL.idTipoLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'INNER JOIN', N'zafra2023..Emboquille', N'Eb', N'LE.IdEmboquille = Eb.IdEmboquille');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'INNER JOIN', N'zafra2023..Especie', N'E', N'L.IdEspecie = E.IdEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'INNER JOIN', N'zafra2023..Variedad', N'V', N'L.IdEspecie = V.IdEspecie AND L.IdVariedad = V.IdVariedad');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Men_Volcado', N'LEFT JOIN', N'zafra2023..Bascula', N'Ba', N'L.IdBascula = Ba.idBascula');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'INNER JOIN', N'Lote', N'L', N'LE.idLote = L.IdLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'INNER JOIN', N'TipoLote', N'TL', N'L.idTipoLote = TL.idTipoLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'INNER JOIN', N'Emboquille', N'Eb', N'LE.IdEmboquille = Eb.IdEmboquille');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'INNER JOIN', N'Especie', N'E', N'L.IdEspecie = E.IdEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'INNER JOIN', N'Variedad', N'V', N'L.IdEspecie = V.IdEspecie AND L.IdVariedad = V.IdVariedad');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'LEFT JOIN', N'Bascula', N'Ba', N'L.IdBascula = Ba.idBascula');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'INNER JOIN', N'Zafra2024..Lote', N'L', N'LE.idLote = L.IdLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'INNER JOIN', N'Zafra2024..TipoLote', N'TL', N'L.idTipoLote = TL.idTipoLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'INNER JOIN', N'Zafra2024..Emboquille', N'Eb', N'LE.IdEmboquille = Eb.IdEmboquille');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'INNER JOIN', N'Zafra2024..Especie', N'E', N'L.IdEspecie = E.IdEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'INNER JOIN', N'Zafra2024..Variedad', N'V', N'L.IdEspecie = V.IdEspecie AND L.IdVariedad = V.IdVariedad');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'LEFT JOIN', N'Zafra2024..Bascula', N'Ba', N'L.IdBascula = Ba.idBascula');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'INNER JOIN', N'Zafra2023..Lote', N'L', N'LE.idLote = L.IdLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'INNER JOIN', N'Zafra2023..TipoLote', N'TL', N'L.idTipoLote = TL.idTipoLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'INNER JOIN', N'Zafra2023..Emboquille', N'Eb', N'LE.IdEmboquille = Eb.IdEmboquille');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'INNER JOIN', N'Zafra2023..Especie', N'E', N'L.IdEspecie = E.IdEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'INNER JOIN', N'Zafra2023..Variedad', N'V', N'L.IdEspecie = V.IdEspecie AND L.IdVariedad = V.IdVariedad');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Resumen_Mensual_Volcado', N'LEFT JOIN', N'Zafra2023..Bascula', N'Ba', N'L.IdBascula = Ba.idBascula');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Stock_Playa', N'INNER JOIN', N'TipoLote', N'TL', N'L.IdTipoLote = TL.IdTipoLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Stock_Playa', N'INNER JOIN', N'Variedad', N'V', N'L.idVariedad = V.idVariedad AND L.idEspecie = V.idEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Stock_Playa', N'INNER JOIN', N'Especie', N'E', N'L.idEspecie = E.idEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Stock_Playa', N'INNER JOIN', N'Productor', N'P', N'L.idProductor = P.idProductor');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Stock_Playa', N'INNER JOIN', N'Empaque', N'Em', N'L.IdSeccion = Em.idEmpaque');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Stock_Playa', N'LEFT  JOIN', N'ProductorChacra', N'PC', N'(L.IdProductor = PC.IdProductor AND L.IdChacra = PC.IdChacra)');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Stock_Playa', N'LEFT  JOIN', N'Responsable', N'R', N'R.IdResponsable = L.idCapataz');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Stock_Playa', N'LEFT  JOIN', N'Bascula', N'Ba', N'L.IdBascula = Ba.idBascula');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'Stock_Playa', N'LEFT  JOIN', N'DestinoInterno', N'Di', N'l.IdDestinoSugerido = Di.idDestinoInterno');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'grafica_procesos', N'LEFT JOIN', N'dtrace..variedad', N'v', N'r.idvariedad=v.idvariedad and r.idespecie=v.idespecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'grafica_procesos', N'LEFT JOIN', N'dtrace..especie', N'e', N'r.idespecie=e.idespecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'INNER JOIN', N'Variedad', NULL, N'PP.IdEspecie = Variedad.IdEspecie AND PP.IdVariedad = Variedad.IdVariedad');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'INNER JOIN', N'Especie', NULL, N'Variedad.IdEspecie = Especie.IdEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'INNER JOIN', N'Envase', NULL, N'PP.IdEnvase = Envase.IdEnvase');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'INNER JOIN', N'Embalaje', NULL, N'PP.IdEmbalaje = Embalaje.IdEmbalaje');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'INNER JOIN', N'Categoria', NULL, N'PP.IdCategoria = Categoria.IdCategoria');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'INNER JOIN', N'TipoPallet', NULL, N'PP.IdTipoPallet = TipoPallet.IdTipoPallet LEFT OUTER');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'JOIN', N'ContraMarca', NULL, N'PP.IdContraMarca = ContraMarca.IdContraMarca');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'INNER JOIN', N'Marca', NULL, N'PP.IdMarca = Marca.IdMarca');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'LEFT JOIN', N'Frio', NULL, N'PP.IdSeccion = Frio.IdFrio');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'LEFT JOIN', N'FrioCamara', N'FC', N'Frio.IdFrio = FC.IdFrio AND PP.IdCamara = FC.IdCamara');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'INNER JOIN', N'Empaque', NULL, N'PP.IdEmpaque = Empaque.IdEmpaque');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'INNER JOIN', N'Calibre', NULL, N'PP.IdCalibre = Calibre.IdCalibre AND PP.IdEnvase = Calibre.IdEnvase AND PP.IdEspecie = Calibre.IdEspecie');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'LEFT JOIN', N'OrdenCarga', NULL, N'PP.IdOrdenCarga = OrdenCarga.IdOrdenCarga');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'LEFT JOIN', N'OrdenCargaLinea', N'OCL', N'PP.IdOrdenCarga = OCL.IdOrdenCarga AND PP.IdEspecie = OCL.IdEspecie AND PP.IdVariedad = OCL.IdVariedad AND PP.IdEnvase = OCL.IdEnvase AND PP.IdCalibre = OCL.IdCalibre');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'INNER JOIN', N'Cliente', NULL, N'OrdenCarga.IdCliente = Cliente.IdCliente');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'LEFT JOIN', N'Lote', N'L', N'PL.IdLote = L.IdLote');
INSERT INTO dbo.Cat_Relaciones_SQL (AppOrigen, TipoJoin, TablaRelacionada, AliasTabla, CondicionOn)
VALUES (N'pruebas_totales_grid', N'LEFT JOIN', N'Productor', N'P', N'P.IdProductor = L.IdProductor');
GO

PRINT 'Carga de catalogo de metadata NORIDEL finalizada.';
GO