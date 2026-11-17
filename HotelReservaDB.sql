IF DB_ID(N'HotelReservasDb') IS NULL
    EXEC(N'CREATE DATABASE HotelReservasDb');
GO
USE HotelReservasDb;
GO

IF OBJECT_ID(N'dbo.Clientes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clientes (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Clientes PRIMARY KEY,
        Nombres NVARCHAR(90) NOT NULL,
        Apellidos NVARCHAR(90) NOT NULL
    );
END;
GO
IF OBJECT_ID(N'dbo.Habitaciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Habitaciones (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Habitaciones PRIMARY KEY,
        Numero NVARCHAR(10) NOT NULL,
        Tipo NVARCHAR(40) NOT NULL,
        CostoDia DECIMAL(12,2) NOT NULL,
        Estado NVARCHAR(15) NOT NULL
            CONSTRAINT DF_Habitaciones_Estado DEFAULT N'Disponible',
        CONSTRAINT CK_Habitaciones_Precio CHECK (CostoDia >= 0),
        CONSTRAINT CK_Habitaciones_Estado CHECK
            (Estado IN (N'Disponible', N'Reservado', N'Ocupado'))
    );
END;
GO
IF OBJECT_ID(N'dbo.Reservas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Reservas (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Reservas PRIMARY KEY,
        ClienteId INT NOT NULL,
        FechaLlegada DATETIME2 NOT NULL,
        FechaSalida DATETIME2 NOT NULL,
        FechaRegistro DATETIME2 NOT NULL,
        Dias INT NOT NULL,
        Subtotal DECIMAL(12,2) NOT NULL,
        Igv DECIMAL(12,2) NOT NULL,
        Total DECIMAL(12,2) NOT NULL,
        CONSTRAINT FK_Reservas_Clientes FOREIGN KEY (ClienteId)
            REFERENCES dbo.Clientes(Id),
        CONSTRAINT CK_Reservas_Fechas CHECK (FechaSalida > FechaLlegada),
        CONSTRAINT CK_Reservas_Dias CHECK (Dias > 0)
    );
END;
GO
IF OBJECT_ID(N'dbo.DetallesReserva', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DetallesReserva (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DetallesReserva PRIMARY KEY,
        ReservaId INT NOT NULL,
        HabitacionId INT NOT NULL,
        NombreHuesped NVARCHAR(150) NOT NULL,
        TarifaDia DECIMAL(12,2) NOT NULL,
        Dias INT NOT NULL,
        Monto DECIMAL(12,2) NOT NULL,
        CONSTRAINT FK_DetallesReserva_Reservas FOREIGN KEY (ReservaId)
            REFERENCES dbo.Reservas(Id) ON DELETE CASCADE,
        CONSTRAINT FK_DetallesReserva_Habitaciones FOREIGN KEY (HabitacionId)
            REFERENCES dbo.Habitaciones(Id),
        CONSTRAINT CK_DetallesReserva_Dias CHECK (Dias > 0)
    );
END;
GO
IF OBJECT_ID(N'dbo.Recepcionistas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Recepcionistas (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Recepcionistas PRIMARY KEY,
        Usuario NVARCHAR(50) NOT NULL,
        NombreCompleto NVARCHAR(120) NOT NULL,
        PasswordHash NVARCHAR(500) NOT NULL,
        Activo BIT NOT NULL CONSTRAINT DF_Recepcionistas_Activo DEFAULT (1),
        FechaCreacion DATETIME2 NOT NULL
            CONSTRAINT DF_Recepcionistas_FechaCreacion DEFAULT (SYSDATETIME())
    );
END;
GO
INSERT INTO dbo.Habitaciones (Numero, Tipo, CostoDia, Estado)
SELECT v.Numero, v.Tipo, v.CostoDia, N'Disponible'
FROM (VALUES
    (N'101', N'Simple', CAST(100.00 AS DECIMAL(12,2))),
    (N'102', N'Doble', CAST(150.00 AS DECIMAL(12,2))),
    (N'103', N'Matrimonial', CAST(180.00 AS DECIMAL(12,2))),
    (N'201', N'Suite', CAST(250.00 AS DECIMAL(12,2))),
    (N'202', N'Doble', CAST(150.00 AS DECIMAL(12,2)))
) v(Numero, Tipo, CostoDia)
WHERE NOT EXISTS
    (SELECT 1 FROM dbo.Habitaciones h WHERE h.Numero = v.Numero);
GO
DECLARE @Hash NVARCHAR(500) =
 N'AQAAAAIAAYagAAAAEK7yT2chDNV0oaToGnjDglkssbHDpvJl4XlicQn4UC16nt1yRcYZwinjY3VvyUMPbA==';
IF EXISTS (SELECT 1 FROM dbo.Recepcionistas WHERE Usuario = N'Recepcionista')
    UPDATE dbo.Recepcionistas
       SET PasswordHash = @Hash, NombreCompleto = N'Recepcionista', Activo = 1
     WHERE Usuario = N'Recepcionista';
ELSE
    INSERT INTO dbo.Recepcionistas(Usuario, NombreCompleto, PasswordHash, Activo)
    VALUES(N'Recepcionista', N'Recepcionista', @Hash, 1);
GO

SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Personas TABLE (
        Numero NVARCHAR(10) PRIMARY KEY,
        Nombres NVARCHAR(90) NOT NULL,
        Apellidos NVARCHAR(90) NOT NULL,
        NombreCompleto NVARCHAR(150) NOT NULL
    );
    INSERT INTO @Personas (Numero, Nombres, Apellidos, NombreCompleto)
    VALUES
        (N'101', N'Mariana', N'Rojas Salazar', N'Mariana Rojas Salazar'),
        (N'102', N'José Luis', N'Fernández Castillo', N'José Luis Fernández Castillo'),
        (N'103', N'Camila', N'Torres Vargas', N'Camila Torres Vargas'),
        (N'201', N'Diego Alejandro', N'Ramírez León', N'Diego Alejandro Ramírez León'),
        (N'202', N'Luciana', N'Mendoza Flores', N'Luciana Mendoza Flores');

    DECLARE @DetallesAnteriores TABLE (
        DetalleId INT PRIMARY KEY,
        ClienteId INT NOT NULL,
        Numero NVARCHAR(10) NOT NULL
    );
    INSERT INTO @DetallesAnteriores(DetalleId, ClienteId, Numero)
    SELECT d.Id, r.ClienteId, h.Numero
    FROM dbo.DetallesReserva d
    JOIN dbo.Reservas r ON r.Id=d.ReservaId
    JOIN dbo.Clientes c ON c.Id=r.ClienteId
    JOIN dbo.Habitaciones h ON h.Id=d.HabitacionId
    JOIN @Personas p ON p.Numero=h.Numero
    WHERE c.Nombres=N'Cliente Demo'
      AND c.Apellidos=N'Reserva ' + h.Numero
      AND d.NombreHuesped=N'Huésped demo habitación ' + h.Numero;

    UPDATE c SET c.Nombres=p.Nombres, c.Apellidos=p.Apellidos
    FROM dbo.Clientes c
    JOIN @DetallesAnteriores ant ON ant.ClienteId=c.Id
    JOIN @Personas p ON p.Numero=ant.Numero;

    UPDATE d SET d.NombreHuesped=p.NombreCompleto
    FROM dbo.DetallesReserva d
    JOIN @DetallesAnteriores ant ON ant.DetalleId=d.Id
    JOIN @Personas p ON p.Numero=ant.Numero;

    DECLARE @ReservasEncontradas INT;
    SELECT @ReservasEncontradas=COUNT(*)
    FROM dbo.DetallesReserva d
    JOIN dbo.Reservas r ON r.Id=d.ReservaId
    JOIN dbo.Clientes c ON c.Id=r.ClienteId
    JOIN dbo.Habitaciones h ON h.Id=d.HabitacionId
    JOIN @Personas p ON p.Numero=h.Numero
    WHERE d.NombreHuesped=p.NombreCompleto
      AND c.Nombres=p.Nombres AND c.Apellidos=p.Apellidos;

    IF @ReservasEncontradas > 0 AND @ReservasEncontradas <> 4
        THROW 51001, N'Se identificaron menos o mas de cuatro reservas de ejemplo: revise antes de continuar.', 1;

    IF @ReservasEncontradas = 4
    BEGIN
        PRINT N'Ya existen cuatro reservas con nombres completos; no se duplicaron.';
    END
    ELSE
    BEGIN
        IF (SELECT COUNT(*) FROM dbo.Habitaciones
            WHERE Numero IN (N'101',N'102',N'103',N'201',N'202')) <> 5
            THROW 51002, N'Deben existir las cinco habitaciones 101, 102, 103, 201 y 202.', 1;

        DECLARE @NumeroLibre NVARCHAR(10);
        SELECT TOP (1) @NumeroLibre = h.Numero
        FROM dbo.Habitaciones h WITH (UPDLOCK, HOLDLOCK)
        WHERE h.Numero IN (N'101',N'102',N'103',N'201',N'202')
          AND h.Estado = N'Disponible'
          AND NOT EXISTS (
              SELECT 1 FROM dbo.DetallesReserva d
              JOIN dbo.Reservas r ON r.Id = d.ReservaId
              WHERE d.HabitacionId = h.Id
                AND r.FechaSalida > CAST(GETDATE() AS DATE)
          )
        ORDER BY CASE WHEN h.Numero=N'202' THEN 0 ELSE 1 END, h.Numero DESC;

        IF @NumeroLibre IS NULL
            THROW 51003,
                N'No hay ninguna de las cinco habitaciones libre. No se cambiaron reservas previas.', 1;

        IF EXISTS (
            SELECT 1 FROM dbo.Habitaciones h WITH (UPDLOCK, HOLDLOCK)
            WHERE h.Numero IN (N'101',N'102',N'103',N'201',N'202')
              AND h.Numero <> @NumeroLibre AND h.Estado = N'Ocupado'
        )
            THROW 51004,
                N'Una de las otras cuatro habitaciones esta Ocupado. No es seguro reservarla.', 1;

        DECLARE @Llegada DATE = DATEADD(DAY, 1, CAST(GETDATE() AS DATE));
        DECLARE @UltimaSalida DATE;
        SELECT @UltimaSalida = MAX(CAST(r.FechaSalida AS DATE))
        FROM dbo.Reservas r
        JOIN dbo.DetallesReserva d ON d.ReservaId = r.Id
        JOIN dbo.Habitaciones h ON h.Id = d.HabitacionId
        WHERE h.Numero IN (N'101',N'102',N'103',N'201',N'202');

        IF @UltimaSalida >= @Llegada
            SET @Llegada = DATEADD(DAY, 1, @UltimaSalida);

        DECLARE @Salida DATE = DATEADD(DAY, 3, @Llegada);
        DECLARE @Dias INT = DATEDIFF(DAY, @Llegada, @Salida);
        DECLARE @Numero NVARCHAR(10), @IdHabitacion INT, @Tarifa DECIMAL(12,2);
        DECLARE @IdCliente INT, @IdReserva INT;
        DECLARE @Nombres NVARCHAR(90), @Apellidos NVARCHAR(90), @NombreCompleto NVARCHAR(150);
        DECLARE @Subtotal DECIMAL(12,2), @Igv DECIMAL(12,2);
        DECLARE @i INT=1;
        DECLARE @Cuartos TABLE (Fila INT IDENTITY(1,1), Numero NVARCHAR(10));
        INSERT INTO @Cuartos(Numero)
        SELECT h.Numero FROM dbo.Habitaciones h
        WHERE h.Numero IN (N'101',N'102',N'103',N'201',N'202')
          AND h.Numero <> @NumeroLibre
        ORDER BY h.Numero;

        IF (SELECT COUNT(*) FROM @Cuartos) <> 4
            THROW 51005, N'No se pudieron seleccionar cuatro habitaciones distintas.', 1;

        IF EXISTS (
          SELECT 1
          FROM @Cuartos c
          JOIN dbo.Habitaciones h ON h.Numero = c.Numero
          JOIN dbo.DetallesReserva d ON d.HabitacionId = h.Id
          JOIN dbo.Reservas r ON r.Id = d.ReservaId
          WHERE r.FechaLlegada < @Salida AND r.FechaSalida > @Llegada
        )
            THROW 51006, N'Las fechas elegidas coinciden con otra reserva existente.', 1;

        WHILE @i <= 4
        BEGIN
            SELECT @Numero = Numero FROM @Cuartos WHERE Fila = @i;
            SELECT @IdHabitacion = Id, @Tarifa = CostoDia
              FROM dbo.Habitaciones WHERE Numero = @Numero;

            SELECT @Nombres=Nombres, @Apellidos=Apellidos,
                   @NombreCompleto=NombreCompleto
            FROM @Personas WHERE Numero=@Numero;

            SET @IdCliente=NULL;
            SELECT TOP (1) @IdCliente=Id FROM dbo.Clientes
            WHERE Nombres=@Nombres AND Apellidos=@Apellidos
            ORDER BY Id;

            IF @IdCliente IS NULL
            BEGIN
                INSERT INTO dbo.Clientes(Nombres, Apellidos)
                VALUES(@Nombres, @Apellidos);
                SET @IdCliente=CONVERT(INT, SCOPE_IDENTITY());
            END;

            SET @Subtotal = @Tarifa * @Dias;
            SET @Igv = ROUND(@Subtotal * 0.18, 2);

            INSERT INTO dbo.Reservas
                (ClienteId, FechaLlegada, FechaSalida, FechaRegistro,
                 Dias, Subtotal, Igv, Total)
            VALUES
                (@IdCliente, @Llegada, @Salida, SYSDATETIME(),
                 @Dias, @Subtotal, @Igv, @Subtotal + @Igv);

            SET @IdReserva = CONVERT(INT, SCOPE_IDENTITY());

            INSERT INTO dbo.DetallesReserva
                (ReservaId, HabitacionId, NombreHuesped, TarifaDia, Dias, Monto)
            VALUES
                (@IdReserva, @IdHabitacion,
                 @NombreCompleto, @Tarifa, @Dias, @Subtotal);

            UPDATE dbo.Habitaciones SET Estado = N'Reservado' WHERE Id = @IdHabitacion;

            SET @IdCliente = NULL;
            SET @i = @i + 1;
        END;

        PRINT N'Se registraron cuatro reservas de tres noches. Habitación sin reservar: ' + @NumeroLibre;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO