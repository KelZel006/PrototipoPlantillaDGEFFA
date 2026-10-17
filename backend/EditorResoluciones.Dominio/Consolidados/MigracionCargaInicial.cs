using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Servicios;

namespace EditorResoluciones.Dominio.Consolidados;

/// <summary>Resultado de la migración, para el registro de inicio y las pruebas.</summary>
public sealed record ResultadoMigracion(int VersionesCreadas, int HuellasCompletadas, IReadOnlyList<string> Inconsistencias);

/// <summary>
/// Se ejecuta al iniciar la API (decisión D5):
/// <list type="bullet">
/// <item>Cada plantilla sin versión vigente recibe una versión VIGENTE «Carga inicial» con su contenido actual.</item>
/// <item>Completa lo que los datos semilla no pueden traer escrito: las huellas y, en las propuestas MODIFICAR sin
/// versión original, la versión vigente de su plantilla (creada al iniciar).</item>
/// <item>Informa las inconsistencias de vigencia (más de una versión vigente para una plantilla o identidad).</item>
/// </list>
/// Es idempotente: si no hay nada que completar, no escribe.
/// </summary>
public sealed class MigracionCargaInicial(IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public const string UsuarioCargaInicial = "carga.inicial";

    public Task<ResultadoMigracion> EjecutarAsync(CancellationToken ct = default) => unidad.EjecutarAsync(async tx =>
    {
        var ahora = reloj.GetUtcNow();
        var plantillas = await tx.ListarAsync<Plantilla>(ct);
        var parrafos = await tx.ListarAsync<Parrafo>(ct);
        var versiones = (await tx.ListarAsync<VersionPlantilla>(ct)).ToList();
        var creadas = 0;
        var huellas = 0;

        // Huellas que faltan en las versiones de los datos semilla.
        foreach (var version in versiones.Where(v => string.IsNullOrEmpty(v.Huella)).ToList())
        {
            version.Huella = HuellaContenido.De(version.Contenido);
            await tx.ActualizarAsync(version, ct);
            huellas++;
        }

        foreach (var plantilla in plantillas)
        {
            var propias = versiones.Where(v => v.PlantillaId == plantilla.Id).ToList();
            if (propias.Any(v => v.Vigencia == Vigencia.Vigente))
                continue;

            var contenido = ConstructorContenido.Desde(plantilla, parrafos);
            var version = await tx.CrearAsync(new VersionPlantilla
            {
                PlantillaId = plantilla.Id,
                NumeroVersion = propias.Select(v => v.NumeroVersion).DefaultIfEmpty(0).Max() + 1,
                Vigencia = Vigencia.Vigente,
                Contenido = contenido,
                Huella = HuellaContenido.De(contenido),
                VersionAnteriorId = propias.OrderByDescending(v => v.NumeroVersion).FirstOrDefault()?.Id,
                CargaInicial = true,
                PublicadaPor = UsuarioCargaInicial,
                FechaPublicacion = ahora,
                InicioVigencia = ahora,
            }, ct);
            await tx.CrearAsync(ServicioHistorial.Construir(
                new CambioHistorial("versionesPlantilla", version.Id, AccionHistorial.Creacion, version.Version, null, version),
                UsuarioCargaInicial, ahora), ct);
            versiones.Add(version);
            creadas++;
        }

        // Versión original y huellas de las propuestas y presentaciones de los datos semilla.
        var porId = versiones.ToDictionary(v => v.Id, StringComparer.Ordinal);
        var vigentes = versiones.Where(v => v.Vigencia == Vigencia.Vigente)
            .GroupBy(v => v.PlantillaId).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        bool Completar(Propuesta propuesta)
        {
            var cambio = false;
            if (propuesta is { Operacion: OperacionPropuesta.Modificar, VersionOrigenId: null, PlantillaId: { } plantillaId } &&
                vigentes.TryGetValue(plantillaId, out var vigente))
            {
                propuesta.VersionOrigenId = vigente.Id;
                cambio = true;
            }
            if (string.IsNullOrEmpty(propuesta.HuellaOrigen) && propuesta.VersionOrigenId is { } origen && porId.TryGetValue(origen, out var v))
            {
                propuesta.HuellaOrigen = v.Huella;
                cambio = true;
            }
            return cambio;
        }

        foreach (var propuesta in (await tx.ListarAsync<Propuesta>(ct)).ToList())
        {
            if (Completar(propuesta))
            {
                await tx.ActualizarAsync(propuesta, ct);
                huellas++;
            }
        }
        foreach (var presentacion in (await tx.ListarAsync<Presentacion>(ct)).Where(p => string.IsNullOrEmpty(p.HuellaIntegridad)).ToList())
        {
            foreach (var enviada in presentacion.Propuestas)
                Completar(enviada);
            presentacion.HuellaIntegridad = HuellaContenido.De(presentacion.Propuestas);
            await tx.ActualizarAsync(presentacion, ct);
            huellas++;
        }

        return new ResultadoMigracion(creadas, huellas, Inconsistencias(plantillas, versiones));
    }, ct);

    /// <summary>Como máximo una versión vigente por plantilla y una plantilla por identidad funcional (regla 14).</summary>
    public static IReadOnlyList<string> Inconsistencias(IReadOnlyList<Plantilla> plantillas, IReadOnlyList<VersionPlantilla> versiones)
    {
        var problemas = new List<string>();
        foreach (var grupo in versiones.Where(v => v.Vigencia == Vigencia.Vigente).GroupBy(v => v.PlantillaId).Where(g => g.Count() > 1))
            problemas.Add($"La plantilla {grupo.Key} tiene {grupo.Count()} versiones vigentes.");
        foreach (var grupo in plantillas
                     .GroupBy(p => (p.RubroId, p.SubRubroId, p.TipoDocumentoId, p.ClaseDocumentoId))
                     .Where(g => g.Count() > 1))
        {
            problemas.Add($"Hay {grupo.Count()} plantillas con la misma identidad ({grupo.Key.RubroId} / {grupo.Key.SubRubroId} / " +
                          $"{grupo.Key.TipoDocumentoId} / {grupo.Key.ClaseDocumentoId}): {string.Join(", ", grupo.Select(p => p.Id))}.");
        }
        foreach (var version in versiones.Where(v => plantillas.All(p => p.Id != v.PlantillaId)))
            problemas.Add($"La versión {version.Id} pertenece a la plantilla {version.PlantillaId}, que no existe.");
        return problemas;
    }
}
