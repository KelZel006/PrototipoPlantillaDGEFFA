using System.Globalization;
using EditorResoluciones.Dominio.Modelos;

namespace EditorResoluciones.Dominio.Documento;

/// <summary>
/// Resultado de evaluar una condición. <see cref="Indeterminado"/> significa que faltan datos para decidir:
/// nunca se trata como verdadero.
/// </summary>
public enum ResultadoCondicion
{
    Verdadero,
    Falso,
    Indeterminado
}

/// <summary>Evalúa condiciones de visualización con los valores de un registro (por ejemplo, una solicitud).</summary>
public static class EvaluadorCondiciones
{
    /// <summary>
    /// Y: falso si alguna es falsa; si no, indeterminado si alguna lo es; si no, verdadero.
    /// O: verdadero si alguna es verdadera; si no, indeterminado si alguna lo es; si no, falso.
    /// </summary>
    public static ResultadoCondicion Evaluar(
        GrupoCondiciones grupo,
        Func<string, (Campo? Campo, object? Valor)> obtenerValor)
    {
        var resultados = grupo.Reglas.Select(r => EvaluarRegla(r, obtenerValor))
            .Concat(grupo.Grupos.Select(g => Evaluar(g, obtenerValor)))
            .ToList();

        if (resultados.Count == 0)
            return ResultadoCondicion.Verdadero;

        return grupo.OperadorLogico == OperadorLogico.Y
            ? resultados.Contains(ResultadoCondicion.Falso) ? ResultadoCondicion.Falso
              : resultados.Contains(ResultadoCondicion.Indeterminado) ? ResultadoCondicion.Indeterminado
              : ResultadoCondicion.Verdadero
            : resultados.Contains(ResultadoCondicion.Verdadero) ? ResultadoCondicion.Verdadero
              : resultados.Contains(ResultadoCondicion.Indeterminado) ? ResultadoCondicion.Indeterminado
              : ResultadoCondicion.Falso;
    }

    public static ResultadoCondicion EvaluarRegla(
        ReglaCondicion regla,
        Func<string, (Campo? Campo, object? Valor)> obtenerValor)
    {
        var (campo, valor) = obtenerValor(regla.CampoId);
        if (campo is null)
            return ResultadoCondicion.Indeterminado;

        var vacio = valor is null || (valor is string texto && string.IsNullOrWhiteSpace(texto));
        switch (regla.Operador)
        {
            case OperadorComparacion.EsNulo:
                return Booleano(vacio);
            case OperadorComparacion.NoEsNulo:
                return Booleano(!vacio);
        }

        if (vacio || regla.Valor is null)
            return ResultadoCondicion.Indeterminado;

        return campo.TipoDato switch
        {
            TipoDato.Numero or TipoDato.Moneda => CompararNumeros(regla, valor!),
            TipoDato.Fecha => CompararFechas(regla, valor!),
            TipoDato.Booleano => CompararBooleanos(regla, valor!),
            _ => CompararTextos(regla, Convert.ToString(valor, CultureInfo.InvariantCulture) ?? ""),
        };
    }

    private static ResultadoCondicion CompararTextos(ReglaCondicion regla, string valor)
    {
        var comparador = CultureInfo.InvariantCulture.CompareInfo;
        const CompareOptions opciones = CompareOptions.IgnoreCase;
        var esperado = regla.Valor!.Trim();
        var actual = valor.Trim();
        return regla.Operador switch
        {
            OperadorComparacion.IgualA => Booleano(comparador.Compare(actual, esperado, opciones) == 0),
            OperadorComparacion.DistintoDe => Booleano(comparador.Compare(actual, esperado, opciones) != 0),
            OperadorComparacion.Contiene => Booleano(comparador.IndexOf(actual, esperado, opciones) >= 0),
            OperadorComparacion.NoContiene => Booleano(comparador.IndexOf(actual, esperado, opciones) < 0),
            _ => ResultadoCondicion.Indeterminado,
        };
    }

    private static ResultadoCondicion CompararNumeros(ReglaCondicion regla, object valor)
    {
        if (!decimal.TryParse(regla.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var esperado))
            return ResultadoCondicion.Indeterminado;
        var actual = Convert.ToDecimal(valor, CultureInfo.InvariantCulture);
        return Ordenar(regla.Operador, actual.CompareTo(esperado));
    }

    private static ResultadoCondicion CompararFechas(ReglaCondicion regla, object valor)
    {
        if (!DateOnly.TryParseExact(regla.Valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var esperado) ||
            valor is not DateOnly actual)
        {
            return ResultadoCondicion.Indeterminado;
        }
        return Ordenar(regla.Operador, actual.CompareTo(esperado));
    }

    private static ResultadoCondicion CompararBooleanos(ReglaCondicion regla, object valor)
    {
        if (!bool.TryParse(regla.Valor, out var esperado) || valor is not bool actual)
            return ResultadoCondicion.Indeterminado;
        return regla.Operador switch
        {
            OperadorComparacion.IgualA => Booleano(actual == esperado),
            OperadorComparacion.DistintoDe => Booleano(actual != esperado),
            _ => ResultadoCondicion.Indeterminado,
        };
    }

    private static ResultadoCondicion Ordenar(OperadorComparacion operador, int comparacion) => operador switch
    {
        OperadorComparacion.IgualA => Booleano(comparacion == 0),
        OperadorComparacion.DistintoDe => Booleano(comparacion != 0),
        OperadorComparacion.MayorQue => Booleano(comparacion > 0),
        OperadorComparacion.MenorQue => Booleano(comparacion < 0),
        OperadorComparacion.MayorOIgualQue => Booleano(comparacion >= 0),
        OperadorComparacion.MenorOIgualQue => Booleano(comparacion <= 0),
        _ => ResultadoCondicion.Indeterminado,
    };

    private static ResultadoCondicion Booleano(bool valor) => valor ? ResultadoCondicion.Verdadero : ResultadoCondicion.Falso;
}
