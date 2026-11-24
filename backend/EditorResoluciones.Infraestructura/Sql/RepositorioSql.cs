using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Servicios;
using Microsoft.Data.SqlClient;

namespace EditorResoluciones.Infraestructura.Sql;

/// <summary>Repositorio genérico sobre una tabla SEFH_. Cada operación de escritura es una transacción del motor.</summary>
public sealed class RepositorioSql<T>(AlmacenSql almacen) : IRepositorio<T> where T : class, IEntidad
{
    private static readonly TablaColeccion Tabla = MapeoSql.De<T>();

    public string Coleccion => Tabla.Coleccion.Nombre;

    public async Task<IReadOnlyList<T>> ListarAsync(CancellationToken ct = default)
    {
        await using var conexion = await almacen.AbrirAsync(ct);
        return await almacen.LeerAsync<T>(conexion, null, ct);
    }

    public async Task<T?> ObtenerAsync(string id, CancellationToken ct = default)
    {
        await using var conexion = await almacen.AbrirAsync(ct);
        return await almacen.ObtenerAsync<T>(conexion, null, id, ct);
    }

    public Task<T> CrearAsync(T entidad, CancellationToken ct = default) =>
        EnTransaccionAsync(async (conexion, tx) =>
        {
            await PrepararCreacionAsync(conexion, tx, entidad, ct);
            await almacen.InsertarAsync(conexion, tx, entidad, ct);
            return entidad;
        }, ct);

    public Task<T> ActualizarAsync(T entidad, CancellationToken ct = default) =>
        EnTransaccionAsync(async (conexion, tx) =>
        {
            await ComprobarVersionAsync(conexion, tx, entidad.Id, entidad.Version, ct);
            var anterior = entidad.Version;
            entidad.Version = anterior + 1;
            try
            {
                await almacen.ActualizarAsync(conexion, tx, entidad, anterior, ct);
            }
            catch
            {
                entidad.Version = anterior;
                throw;
            }
            return entidad;
        }, ct);

    public Task EliminarAsync(string id, int? version, CancellationToken ct = default) =>
        EnTransaccionAsync(async (conexion, tx) =>
        {
            var guardada = await AlmacenSql.VersionGuardadaAsync(conexion, tx, Tabla, id, ct)
                ?? throw new EntidadNoEncontradaException(Coleccion, id);
            if (version is not null && guardada != version)
                throw new ConflictoException(ServicioEntidades<T>.MensajeVersion(guardada, version.Value));
            await almacen.EliminarAsync<T>(conexion, tx, id, ct);
            return true;
        }, ct);

    public Task<LoteAplicado<T>> AplicarLoteAsync(IReadOnlyList<T> crear, IReadOnlyList<T> actualizar, CancellationToken ct = default) =>
        EnTransaccionAsync(async (conexion, tx) =>
        {
            if (actualizar.Select(e => e.Id).Distinct().Count() != actualizar.Count)
                throw new ConflictoException("El lote repite registros.");
            // Primero se comprueba todo; si algo falla, la transacción se revierte y no cambia nada.
            foreach (var entidad in actualizar)
                await ComprobarVersionAsync(conexion, tx, entidad.Id, entidad.Version, ct);
            var anteriores = actualizar.Select(e => e.Version).ToList();
            try
            {
                for (var i = 0; i < actualizar.Count; i++)
                {
                    actualizar[i].Version = anteriores[i] + 1;
                    await almacen.ActualizarAsync(conexion, tx, actualizar[i], anteriores[i], ct);
                }
                foreach (var entidad in crear)
                {
                    await PrepararCreacionAsync(conexion, tx, entidad, ct);
                    await almacen.InsertarAsync(conexion, tx, entidad, ct);
                }
            }
            catch
            {
                for (var i = 0; i < actualizar.Count; i++)
                    actualizar[i].Version = anteriores[i];
                throw;
            }
            return new LoteAplicado<T>(crear, actualizar);
        }, ct);

    private async Task PrepararCreacionAsync(SqlConnection conexion, SqlTransaction tx, T entidad, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(entidad.Id))
            entidad.Id = await IdentificadoresSql.NuevoAsync(conexion, tx, Tabla, ct);
        else if (await AlmacenSql.VersionGuardadaAsync(conexion, tx, Tabla, entidad.Id, ct) is not null)
            throw new ConflictoException($"Ya existe un registro con el identificador '{entidad.Id}' en {Coleccion}.");
        entidad.Version = 1;
    }

    private async Task ComprobarVersionAsync(SqlConnection conexion, SqlTransaction tx, string id, int version, CancellationToken ct)
    {
        var guardada = await AlmacenSql.VersionGuardadaAsync(conexion, tx, Tabla, id, ct)
            ?? throw new EntidadNoEncontradaException(Coleccion, id);
        if (guardada != version)
            throw new ConflictoException(ServicioEntidades<T>.MensajeVersion(guardada, version));
    }

    private async Task<TResultado> EnTransaccionAsync<TResultado>(Func<SqlConnection, SqlTransaction, Task<TResultado>> operacion, CancellationToken ct)
    {
        await using var conexion = await almacen.AbrirAsync(ct);
        await using var tx = (SqlTransaction)await conexion.BeginTransactionAsync(ct);
        var resultado = await operacion(conexion, tx);
        await almacen.ActualizarDerivadasAsync(conexion, tx, [Coleccion], ct);
        await tx.CommitAsync(ct);
        return resultado;
    }
}

/// <summary>Identificadores generados: prefijo de la colección y 10 caracteres hexadecimales al azar, como en el JSON.</summary>
internal static class IdentificadoresSql
{
    public static async Task<string> NuevoAsync(SqlConnection conexion, SqlTransaction? tx, TablaColeccion tabla, CancellationToken ct)
    {
        string id;
        do
        {
            id = $"{tabla.Coleccion.Prefijo}-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        } while (await AlmacenSql.VersionGuardadaAsync(conexion, tx, tabla, id, ct) is not null);
        return id;
    }
}

/// <summary>Verifica que cada colección se pueda leer de su tabla.</summary>
public sealed class DiagnosticoAlmacenamientoSql(AlmacenSql almacen) : IDiagnosticoAlmacenamiento
{
    public string Descripcion => $"SQL Server, base {almacen.BaseDeDatos} (tablas SEFH_)";

    public async Task<IReadOnlyList<EstadoColeccion>> VerificarAsync(CancellationToken ct = default)
    {
        var estados = new List<EstadoColeccion>();
        SqlConnection? conexion = null;
        try
        {
            conexion = await almacen.AbrirAsync(ct);
        }
        catch (AlmacenamientoException ex)
        {
            return MapeoSql.Todas.Select(t => new EstadoColeccion(t.Coleccion.Nombre, false, 0, ex.Message)).ToList();
        }
        await using (conexion)
        {
            foreach (var tabla in MapeoSql.Todas)
            {
                try
                {
                    estados.Add(new EstadoColeccion(tabla.Coleccion.Nombre, true, await almacen.ContarAsync(conexion, tabla, ct), null));
                }
                catch (Exception ex) when (ex is AlmacenamientoException or ConflictoException)
                {
                    estados.Add(new EstadoColeccion(tabla.Coleccion.Nombre, false, 0, ex.Message));
                }
            }
        }
        return estados;
    }
}
