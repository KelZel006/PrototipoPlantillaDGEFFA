using System.Data;
using EditorResoluciones.Dominio.Servicios;
using Microsoft.Data.SqlClient;

namespace EditorResoluciones.Infraestructura.Sql;

/// <summary>
/// Guarda el contenido de los archivos (logos) en la columna <c>Contenido</c> de <c>SEFH_ARCHIVOS</c>. El registro con sus
/// datos lo crea antes el repositorio de archivos; aquí solo se escribe, lee o vacía su contenido.
/// </summary>
public sealed class AlmacenArchivosSql(AlmacenSql almacen) : IAlmacenArchivos
{
    public async Task GuardarAsync(string id, byte[] contenido, CancellationToken ct = default)
    {
        await using var conexion = await almacen.AbrirAsync(ct);
        await using var comando = new SqlCommand("UPDATE dbo.SEFH_ARCHIVOS SET Contenido = @contenido, USU_MOD = @usuarioAuditoria, FEC_MOD = SYSDATETIME() WHERE ID_ARCH_SEQ = @id", conexion);
        comando.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 60) { Value = id });
        comando.Parameters.Add(new SqlParameter("@contenido", SqlDbType.VarBinary, -1) { Value = contenido });
        comando.Parameters.Add(almacen.UsuarioAuditoria());
        if (await AlmacenSql.EjecutarAsync(() => comando.ExecuteNonQueryAsync(ct)) == 0)
            throw new Dominio.Comun.AlmacenamientoException($"No existe el registro del archivo '{id}' para guardar su contenido.");
    }

    public async Task<byte[]?> LeerAsync(string id, CancellationToken ct = default)
    {
        await using var conexion = await almacen.AbrirAsync(ct);
        await using var comando = new SqlCommand("SELECT Contenido FROM dbo.SEFH_ARCHIVOS WHERE ID_ARCH_SEQ = @id", conexion);
        comando.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, 60) { Value = id });
        return await AlmacenSql.EjecutarAsync(() => comando.ExecuteScalarAsync(ct)) is byte[] { Length: > 0 } contenido ? contenido : null;
    }

    /// <summary>El contenido se elimina junto con su registro (lo hace el repositorio de archivos).</summary>
    public Task EliminarAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
}
