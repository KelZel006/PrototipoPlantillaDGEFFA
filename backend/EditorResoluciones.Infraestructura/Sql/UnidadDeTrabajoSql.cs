using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Servicios;
using Microsoft.Data.SqlClient;

namespace EditorResoluciones.Infraestructura.Sql;

/// <summary>
/// Transacciones de varias colecciones sobre SQL Server: una transacción del motor. Si la operación falla, se revierte
/// y no queda nada aplicado. Igual que con los archivos JSON, se ejecuta una transacción a la vez y cada colección se lee
/// una sola vez: lo creado o actualizado dentro de la transacción se ve en las lecturas siguientes.
/// </summary>
public sealed class UnidadDeTrabajoSql(AlmacenSql almacen) : IUnidadDeTrabajo
{
    private readonly SemaphoreSlim _una = new(1, 1);

    public async Task<TResultado> EjecutarAsync<TResultado>(Func<ITransaccion, Task<TResultado>> operacion, CancellationToken ct = default)
    {
        await _una.WaitAsync(ct);
        try
        {
            await using var conexion = await almacen.AbrirAsync(ct);
            await using var tx = (SqlTransaction)await conexion.BeginTransactionAsync(ct);
            var transaccion = new TransaccionSql(almacen, conexion, tx);
            var resultado = await operacion(transaccion);
            await almacen.ActualizarDerivadasAsync(conexion, tx, transaccion.Modificadas, ct);
            await tx.CommitAsync(ct);
            return resultado;
        }
        finally
        {
            _una.Release();
        }
    }

    private sealed class TransaccionSql(AlmacenSql almacen, SqlConnection conexion, SqlTransaction tx) : ITransaccion
    {
        /// <summary>Registros leídos en la transacción, por colección, con lo creado y actualizado en ella.</summary>
        private readonly Dictionary<Type, object> _abiertas = [];

        public HashSet<string> Modificadas { get; } = new(StringComparer.Ordinal);

        public async Task<IReadOnlyList<T>> ListarAsync<T>(CancellationToken ct = default) where T : class, IEntidad =>
            await AbrirAsync<T>(ct);

        public async Task<T> CrearAsync<T>(T entidad, CancellationToken ct = default) where T : class, IEntidad
        {
            var registros = await AbrirAsync<T>(ct);
            var tabla = MapeoSql.De<T>();
            if (string.IsNullOrWhiteSpace(entidad.Id))
                entidad.Id = await IdentificadoresSql.NuevoAsync(conexion, tx, tabla, ct);
            else if (registros.Any(e => e.Id == entidad.Id))
                throw new ConflictoException($"Ya existe un registro con el identificador '{entidad.Id}' en {tabla.Coleccion.Nombre}.");

            entidad.Version = 1;
            await almacen.InsertarAsync(conexion, tx, entidad, ct);
            registros.Add(entidad);
            Modificadas.Add(tabla.Coleccion.Nombre);
            return entidad;
        }

        public async Task<T> ActualizarAsync<T>(T entidad, CancellationToken ct = default) where T : class, IEntidad
        {
            var registros = await AbrirAsync<T>(ct);
            var tabla = MapeoSql.De<T>();
            var indice = registros.FindIndex(e => e.Id == entidad.Id);
            if (indice < 0)
                throw new EntidadNoEncontradaException(tabla.Coleccion.Nombre, entidad.Id);
            var guardada = registros[indice].Version;
            if (guardada != entidad.Version)
                throw new ConflictoException(ServicioEntidades<T>.MensajeVersion(guardada, entidad.Version));

            entidad.Version = guardada + 1;
            await almacen.ActualizarAsync(conexion, tx, entidad, guardada, ct);
            registros[indice] = entidad;
            Modificadas.Add(tabla.Coleccion.Nombre);
            return entidad;
        }

        private async Task<List<T>> AbrirAsync<T>(CancellationToken ct) where T : class, IEntidad
        {
            if (_abiertas.TryGetValue(typeof(T), out var abierta))
                return (List<T>)abierta;
            var registros = await almacen.LeerAsync<T>(conexion, tx, ct);
            _abiertas[typeof(T)] = registros;
            return registros;
        }
    }
}
