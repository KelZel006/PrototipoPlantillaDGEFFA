using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Servicios;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace EditorResoluciones.Infraestructura.Sql;

public sealed class OpcionesAlmacenSql
{
    /// <summary>Cadena de conexión a la base con las tablas SEFH_ (por ejemplo, PlantillasDGEFFA).</summary>
    public string CadenaConexion { get; set; } = "";
}

/// <summary>
/// Lee y escribe las colecciones del sistema en las tablas SEFH_ de SQL Server.
/// <list type="bullet">
/// <item>Cada entidad es una fila; sus listas (roles, bloques de un tipo, contextos, observaciones) van en tablas hijas.</item>
/// <item>La concurrencia optimista se resuelve en el motor: <c>UPDATE … WHERE Version = @anterior</c>.</item>
/// <item>Auditoría: al insertar se guarda USU_CRE (FEC_CRE la pone el motor) y al actualizar, USU_MOD y FEC_MOD, con el
/// usuario que ejecuta la operación.</item>
/// <item><c>SEFH_BLOQUEOS_PLANTILLA</c> y <c>SEFH_SECUENCIAS_PLAN</c> no son colecciones del sistema: se recalculan en la misma
/// transacción cada vez que cambian los documentos o sus plantillas.</item>
/// <item>Los rechazos del motor (claves, reglas CHECK, triggers de auditoría) llegan como conflictos con su mensaje.</item>
/// </list>
/// </summary>
public sealed class AlmacenSql(IOptions<OpcionesAlmacenSql> opciones, IUsuarioActual usuarioActual)
{
    /// <summary>Usuario que se guarda en USU_CRE y USU_MOD (la columna admite 60 caracteres).</summary>
    internal SqlParameter UsuarioAuditoria()
    {
        var nombre = usuarioActual.Nombre;
        return new SqlParameter("@usuarioAuditoria", SqlDbType.VarChar, 60) { Value = nombre.Length > 60 ? nombre[..60] : nombre };
    }

    /// <summary>JSON compacto para las columnas JSON: mismas reglas que el resto del sistema, sin sangría.</summary>
    private static readonly JsonSerializerOptions JsonCompacto = new(OpcionesJson.Predeterminadas) { WriteIndented = false };

    /// <summary>Colecciones de las que dependen las tablas derivadas.</summary>
    private static readonly HashSet<string> AfectanDerivadas = ["propuestas", "consolidados"];

    /// <summary>Nombre de la base de la cadena de conexión, para el diagnóstico.</summary>
    public string BaseDeDatos =>
        string.IsNullOrWhiteSpace(opciones.Value.CadenaConexion) ? "(sin configurar)" : new SqlConnectionStringBuilder(opciones.Value.CadenaConexion).InitialCatalog;

    public async Task<SqlConnection> AbrirAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(opciones.Value.CadenaConexion))
            throw new AlmacenamientoException("Falta la cadena de conexión a SQL Server (Almacenamiento:CadenaConexion).");
        var conexion = new SqlConnection(opciones.Value.CadenaConexion);
        try
        {
            await conexion.OpenAsync(ct);
            return conexion;
        }
        catch (SqlException ex)
        {
            await conexion.DisposeAsync();
            throw new AlmacenamientoException("No se pudo conectar con la base de datos SQL Server.", ex);
        }
    }

    // ---------- Lectura ----------

    internal async Task<List<T>> LeerAsync<T>(SqlConnection conexion, SqlTransaction? tx, CancellationToken ct) where T : class, IEntidad
    {
        var tabla = MapeoSql.De<T>();
        var filas = await LeerFilasAsync(conexion, tx, tabla, null, ct);
        return filas.Select(f => f.Deserialize<T>(OpcionesJson.Predeterminadas)!).ToList();
    }

    internal async Task<T?> ObtenerAsync<T>(SqlConnection conexion, SqlTransaction? tx, string id, CancellationToken ct) where T : class, IEntidad
    {
        var filas = await LeerFilasAsync(conexion, tx, MapeoSql.De<T>(), id, ct);
        return filas.Count == 0 ? null : filas[0].Deserialize<T>(OpcionesJson.Predeterminadas);
    }

    /// <summary>Cantidad de filas de una colección, para el diagnóstico.</summary>
    internal async Task<int> ContarAsync(SqlConnection conexion, TablaColeccion tabla, CancellationToken ct)
    {
        await using var comando = new SqlCommand($"SELECT COUNT(*) FROM dbo.{tabla.Tabla}", conexion);
        return (int)(await EjecutarAsync(() => comando.ExecuteScalarAsync(ct)))!;
    }

    private static async Task<List<JsonObject>> LeerFilasAsync(SqlConnection conexion, SqlTransaction? tx, TablaColeccion tabla, string? id, CancellationToken ct)
    {
        var columnas = string.Join(", ", tabla.Columnas.Select(c => c.Nombre));
        var filtro = id is null ? "" : $" WHERE {tabla.ColumnaId} = @id";
        // OrdenRegistro conserva el orden en que se agregaron, igual que la lista de un archivo JSON.
        await using var comando = new SqlCommand($"SELECT {columnas} FROM dbo.{tabla.Tabla}{filtro} ORDER BY OrdenRegistro", conexion, tx);
        if (id is not null)
            comando.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 60) { Value = id });

        var filas = new List<JsonObject>();
        var porId = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        await using (var lector = await EjecutarAsync(() => comando.ExecuteReaderAsync(ct)))
        {
            while (await lector.ReadAsync(ct))
            {
                var fila = new JsonObject();
                for (var i = 0; i < tabla.Columnas.Length; i++)
                    Asignar(fila, tabla.Columnas[i], lector.IsDBNull(i) ? null : lector.GetValue(i));
                filas.Add(fila);
                porId[(string)fila["id"]!] = fila;
            }
        }

        foreach (var hija in tabla.Hijas)
        {
            foreach (var fila in filas)
                fila[hija.Propiedad] = new JsonArray();
            if (filas.Count == 0)
                continue;
            var columnasHija = hija.Escalar is { } escalar ? escalar : string.Join(", ", hija.Columnas.Select(c => c.Nombre));
            var orden = $"{hija.ColumnaPadre}, {hija.ColumnaOrden ?? "OrdenRegistro"}";
            await using var consulta = new SqlCommand(
                $"SELECT {hija.ColumnaPadre}, {columnasHija} FROM dbo.{hija.Tabla}{(id is null ? "" : $" WHERE {hija.ColumnaPadre} = @id")} ORDER BY {orden}",
                conexion, tx);
            if (id is not null)
                consulta.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 60) { Value = id });
            await using var lector = await EjecutarAsync(() => consulta.ExecuteReaderAsync(ct));
            while (await lector.ReadAsync(ct))
            {
                if (!porId.TryGetValue(lector.GetString(0), out var padre))
                    continue;
                var lista = padre[hija.Propiedad]!.AsArray();
                if (hija.Escalar is not null)
                {
                    lista.Add(JsonValue.Create(lector.GetString(1)));
                    continue;
                }
                var elemento = new JsonObject();
                for (var i = 0; i < hija.Columnas.Length; i++)
                    Asignar(elemento, hija.Columnas[i], lector.IsDBNull(i + 1) ? null : lector.GetValue(i + 1));
                lista.Add(elemento);
            }
        }
        return filas;
    }

    /// <summary>Pasa el valor de una columna a la propiedad del modelo (creando los objetos anidados que falten).</summary>
    private static void Asignar(JsonObject fila, Columna columna, object? valor)
    {
        JsonNode? nodo = valor switch
        {
            null when columna.Tipo == TipoColumna.Opcional => JsonValue.Create(""),
            null => null,
            string texto when columna.Tipo == TipoColumna.Json => JsonNode.Parse(texto),
            string texto when columna.Tipo == TipoColumna.Estado => JsonValue.Create(texto == "ACTIVO"),
            string texto => JsonValue.Create(texto),
            bool logico => JsonValue.Create(logico),
            int entero => JsonValue.Create(entero),
            long largo => JsonValue.Create(largo),
            decimal numero => JsonValue.Create(numero),
            double real => JsonValue.Create(real),
            DateTimeOffset fecha => JsonValue.Create(fecha),
            DateTime dia => JsonValue.Create(dia.ToString("yyyy-MM-dd")),
            _ => throw new AlmacenamientoException($"Tipo de dato no soportado en la columna {columna.Nombre}: {valor.GetType().Name}."),
        };
        if (nodo is null)
            return;
        var destino = fila;
        foreach (var parte in columna.Ruta[..^1])
            destino = (destino[parte] ??= new JsonObject()).AsObject();
        destino[columna.Ruta[^1]] = nodo;
    }

    // ---------- Escritura ----------

    internal async Task InsertarAsync<T>(SqlConnection conexion, SqlTransaction tx, T entidad, CancellationToken ct) where T : class, IEntidad
    {
        var tabla = MapeoSql.De<T>();
        var datos = ComoJson(entidad, tabla);
        var columnas = tabla.Columnas.Select(c => c.Nombre).Concat(tabla.AlInsertar.Keys).Append("USU_CRE").ToList();
        var valores = tabla.Columnas.Select((_, i) => $"@p{i}").Concat(tabla.AlInsertar.Values).Append("@usuarioAuditoria");
        await using var comando = new SqlCommand($"INSERT INTO dbo.{tabla.Tabla} ({string.Join(", ", columnas)}) VALUES ({string.Join(", ", valores)})", conexion, tx);
        AgregarParametros(comando, tabla.Columnas, datos);
        comando.Parameters.Add(UsuarioAuditoria());
        await EjecutarAsync(() => comando.ExecuteNonQueryAsync(ct));
        await InsertarHijasAsync(conexion, tx, tabla, entidad.Id, datos, ct);
    }

    /// <summary>Actualiza la fila si su versión sigue siendo <paramref name="versionAnterior"/>; si no, es un conflicto.</summary>
    internal async Task ActualizarAsync<T>(SqlConnection conexion, SqlTransaction tx, T entidad, int versionAnterior, CancellationToken ct) where T : class, IEntidad
    {
        var tabla = MapeoSql.De<T>();
        var datos = ComoJson(entidad, tabla);
        var asignaciones = tabla.Columnas.Select((c, i) => (c, i)).Where(x => x.c.Nombre != tabla.ColumnaId).Select(x => $"{x.c.Nombre} = @p{x.i}")
            .Concat(["USU_MOD = @usuarioAuditoria", "FEC_MOD = SYSDATETIME()"]);
        var posicionId = Array.FindIndex(tabla.Columnas, c => c.Nombre == tabla.ColumnaId);
        await using var comando = new SqlCommand(
            $"UPDATE dbo.{tabla.Tabla} SET {string.Join(", ", asignaciones)} WHERE {tabla.ColumnaId} = @p{posicionId} AND Version = @versionAnterior", conexion, tx);
        AgregarParametros(comando, tabla.Columnas, datos);
        comando.Parameters.Add(UsuarioAuditoria());
        comando.Parameters.Add(new SqlParameter("@versionAnterior", SqlDbType.Int) { Value = versionAnterior });
        if (await EjecutarAsync(() => comando.ExecuteNonQueryAsync(ct)) == 0)
            throw new ConflictoException(ServicioEntidades<T>.MensajeVersion(await VersionGuardadaAsync(conexion, tx, tabla, entidad.Id, ct) ?? 0, versionAnterior));
        foreach (var hija in tabla.Hijas)
        {
            await using var borrar = new SqlCommand($"DELETE FROM dbo.{hija.Tabla} WHERE {hija.ColumnaPadre} = @id", conexion, tx);
            borrar.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 60) { Value = entidad.Id });
            await EjecutarAsync(() => borrar.ExecuteNonQueryAsync(ct));
        }
        await InsertarHijasAsync(conexion, tx, tabla, entidad.Id, datos, ct);
    }

    internal async Task EliminarAsync<T>(SqlConnection conexion, SqlTransaction tx, string id, CancellationToken ct) where T : class, IEntidad
    {
        var tabla = MapeoSql.De<T>();
        foreach (var hija in tabla.Hijas)
        {
            await using var borrarHija = new SqlCommand($"DELETE FROM dbo.{hija.Tabla} WHERE {hija.ColumnaPadre} = @id", conexion, tx);
            borrarHija.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 60) { Value = id });
            await EjecutarAsync(() => borrarHija.ExecuteNonQueryAsync(ct));
        }
        await using var borrar = new SqlCommand($"DELETE FROM dbo.{tabla.Tabla} WHERE {tabla.ColumnaId} = @id", conexion, tx);
        borrar.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 60) { Value = id });
        await EjecutarAsync(() => borrar.ExecuteNonQueryAsync(ct));
    }

    internal static async Task<int?> VersionGuardadaAsync(SqlConnection conexion, SqlTransaction? tx, TablaColeccion tabla, string id, CancellationToken ct)
    {
        await using var comando = new SqlCommand($"SELECT Version FROM dbo.{tabla.Tabla} WHERE {tabla.ColumnaId} = @id", conexion, tx);
        comando.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 60) { Value = id });
        return await EjecutarAsync(() => comando.ExecuteScalarAsync(ct)) as int?;
    }

    /// <summary>
    /// Recalcula las tablas derivadas si cambiaron los documentos o sus plantillas: los bloqueos D4 (una plantilla abierta
    /// en un solo documento) y el último número PLAN de cada año.
    /// </summary>
    internal async Task ActualizarDerivadasAsync(SqlConnection conexion, SqlTransaction tx, IEnumerable<string> colecciones, CancellationToken ct)
    {
        if (!colecciones.Any(AfectanDerivadas.Contains))
            return;
        await using var comando = new SqlCommand("""
            DELETE FROM dbo.SEFH_BLOQUEOS_PLANTILLA;
            INSERT INTO dbo.SEFH_BLOQUEOS_PLANTILLA (ID_UNI_SEQ, ID_RUBRO_SEQ, ID_SUBRUBRO_SEQ, ID_TIPDOC_SEQ, ID_CLADOC_SEQ, ID_BEN_SEQ, ID_CONS_SEQ, ID_PLADOC_SEQ, Fecha, USU_CRE)
            SELECT p.ID_UNI_SEQ, p.ID_RUBRO_SEQ, p.ID_SUBRUBRO_SEQ, p.ID_TIPDOC_SEQ, p.ID_CLADOC_SEQ, p.ID_BEN_SEQ, p.ID_CONS_SEQ, p.ID_PLADOC_SEQ, p.FechaCreacion, @usuarioAuditoria
            FROM dbo.SEFH_PLANTILLA_DOC p
            JOIN dbo.SEFH_PLANTILLAS_DOC_CAB c ON c.ID_CONS_SEQ = p.ID_CONS_SEQ
            WHERE c.ESTADO_DOCUMENTO IN ('EnRegistro', 'Verificado') AND p.Retirada = 0;
            MERGE dbo.SEFH_SECUENCIAS_PLAN AS destino
            USING (SELECT Anio, MAX(Secuencia) AS Ultimo FROM dbo.SEFH_PLANTILLAS_DOC_CAB GROUP BY Anio) AS origen ON destino.Anio = origen.Anio
            WHEN MATCHED AND destino.Ultimo < origen.Ultimo THEN
                UPDATE SET Ultimo = origen.Ultimo, USU_MOD = @usuarioAuditoria, FEC_MOD = SYSDATETIME()
            WHEN NOT MATCHED THEN INSERT (Anio, Ultimo, USU_CRE) VALUES (origen.Anio, origen.Ultimo, @usuarioAuditoria);
            """, conexion, tx);
        comando.Parameters.Add(UsuarioAuditoria());
        await EjecutarAsync(() => comando.ExecuteNonQueryAsync(ct));
    }

    private async Task InsertarHijasAsync(SqlConnection conexion, SqlTransaction tx, TablaColeccion tabla, string id, JsonObject datos, CancellationToken ct)
    {
        foreach (var hija in tabla.Hijas)
        {
            var elementos = datos[hija.Propiedad]?.AsArray() ?? [];
            for (var orden = 0; orden < elementos.Count; orden++)
            {
                var elemento = elementos[orden];
                if (hija.Escalar is { } escalar)
                {
                    await using var simple = new SqlCommand(
                        $"INSERT INTO dbo.{hija.Tabla} ({hija.ColumnaPadre}, {escalar}, USU_CRE) VALUES (@padre, @valor, @usuarioAuditoria)", conexion, tx);
                    simple.Parameters.Add(new SqlParameter("@padre", SqlDbType.NVarChar, 60) { Value = id });
                    simple.Parameters.Add(UsuarioAuditoria());
                    simple.Parameters.Add(new SqlParameter("@valor", SqlDbType.NVarChar, 200) { Value = elemento!.GetValue<string>() });
                    await EjecutarAsync(() => simple.ExecuteNonQueryAsync(ct));
                    continue;
                }
                // Si la lista ya trae su propio orden (como los bloques de un tipo), se respeta; si no, la posición.
                var conOrden = hija.ColumnaOrden is { } o && hija.Columnas.All(c => c.Nombre != o);
                var nombres = hija.Columnas.Select(c => c.Nombre).Prepend(hija.ColumnaPadre).Concat(conOrden ? [hija.ColumnaOrden!] : []).Append("USU_CRE");
                var valores = hija.Columnas.Select((_, i) => $"@p{i}").Prepend("@padre").Concat(conOrden ? ["@orden"] : []).Append("@usuarioAuditoria");
                await using var comando = new SqlCommand($"INSERT INTO dbo.{hija.Tabla} ({string.Join(", ", nombres)}) VALUES ({string.Join(", ", valores)})", conexion, tx);
                comando.Parameters.Add(new SqlParameter("@padre", SqlDbType.NVarChar, 60) { Value = id });
                comando.Parameters.Add(UsuarioAuditoria());
                if (conOrden)
                    comando.Parameters.Add(new SqlParameter("@orden", SqlDbType.Int) { Value = orden + 1 });
                AgregarParametros(comando, hija.Columnas, elemento!.AsObject());
                await EjecutarAsync(() => comando.ExecuteNonQueryAsync(ct));
            }
        }
    }

    /// <summary>
    /// La entidad como JSON, con las mismas reglas que el resto del sistema. Si tiene una propiedad que ninguna columna guarda,
    /// se detiene: ningún dato se pierde sin avisar.
    /// </summary>
    private static JsonObject ComoJson<T>(T entidad, TablaColeccion tabla)
    {
        var datos = JsonSerializer.SerializeToNode(entidad, OpcionesJson.Predeterminadas)!.AsObject();
        var guardadas = tabla.Columnas.Select(c => c.Ruta[0]).Concat(tabla.Hijas.Select(h => h.Propiedad)).ToHashSet(StringComparer.Ordinal);
        if (datos.Select(p => p.Key).FirstOrDefault(k => !guardadas.Contains(k)) is { } sinColumna)
            throw new AlmacenamientoException($"La propiedad «{sinColumna}» de {tabla.Coleccion.Nombre} no tiene columna en {tabla.Tabla}.");
        return datos;
    }

    private static void AgregarParametros(SqlCommand comando, Columna[] columnas, JsonObject datos)
    {
        for (var i = 0; i < columnas.Length; i++)
        {
            var columna = columnas[i];
            JsonNode? nodo = datos;
            foreach (var parte in columna.Ruta)
                nodo = nodo?[parte];
            object valor = columna.Tipo switch
            {
                _ when nodo is null => DBNull.Value,
                TipoColumna.Json => nodo.ToJsonString(JsonCompacto),
                TipoColumna.Logico => nodo.GetValue<bool>(),
                TipoColumna.Estado => nodo.GetValue<bool>() ? "ACTIVO" : "INACTIVO",
                TipoColumna.Numero => nodo.GetValue<decimal>(),
                TipoColumna.Opcional when nodo.GetValue<string>() == "" => DBNull.Value,
                _ => nodo.GetValue<string>(),
            };
            comando.Parameters.Add(valor switch
            {
                string texto => new SqlParameter($"@p{i}", SqlDbType.NVarChar, -1) { Value = texto },
                _ => new SqlParameter($"@p{i}", valor),
            });
        }
    }

    /// <summary>Traduce los rechazos del motor a los errores del sistema (409 con el motivo, o 503).</summary>
    internal static async Task<TResultado> EjecutarAsync<TResultado>(Func<Task<TResultado>> operacion)
    {
        try
        {
            return await operacion();
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            throw new ConflictoException($"Ya existe un registro con esos datos. ({ex.Message})");
        }
        catch (SqlException ex) when (ex.Number is 547 or 50015 or 50017)
        {
            throw new ConflictoException($"La base de datos rechazó la operación: {ex.Message}");
        }
        catch (SqlException ex)
        {
            throw new AlmacenamientoException($"Error de la base de datos SQL Server: {ex.Message}", ex);
        }
    }
}
