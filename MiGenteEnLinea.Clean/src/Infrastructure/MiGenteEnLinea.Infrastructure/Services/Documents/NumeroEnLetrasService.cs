using MiGenteEnLinea.Application.Common.Interfaces;

namespace MiGenteEnLinea.Infrastructure.Services.Documents;

/// <summary>
/// Implementación del servicio para convertir números a letras en español.
/// Port directo del código Legacy NumeroEnLetras.cs para garantizar compatibilidad 100%.
/// </summary>
public class NumeroEnLetrasService : INumeroEnLetrasService
{
    /// <inheritdoc />
    public string ConvertirALetras(decimal numero, bool incluirMoneda = true)
    {
        var entero = Convert.ToInt64(Math.Truncate(numero));
        var decimales = Convert.ToInt32(Math.Round((numero - entero) * 100, 2));

        if (incluirMoneda)
        {
            // Formato: "MIL PESOS DOMINICANOS 56/100" (sin espacio antes de /)
            var parteEntera = ConvertirNumeroALetras(Convert.ToDouble(entero));
            var resultado = $"{parteEntera} PESOS DOMINICANOS {decimales:00}/100";
            return resultado;
        }
        else
        {
            // Solo el número sin moneda
            return ConvertirNumeroALetras(Convert.ToDouble(entero));
        }
    }

    /// <inheritdoc />
    public string ConvertirEnteroALetras(long numero)
    {
        if (numero < 0 || numero > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(numero), 
                "El número debe estar entre 0 y 10,000");
        }

        if (numero == 0)
        {
            return "CERO";
        }

        string[] unidades = { "", "UN", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE" };
        string[] especiales = { "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE" };
        string[] decenas = { "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA" };
        string[] centenas = { "", "CIEN", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS" };

        string resultado = "";

        if (numero == 10000)
        {
            resultado = "DIEZ MIL";
        }
        else if (numero < 10)
        {
            resultado = unidades[numero];
        }
        else if (numero < 20)
        {
            resultado = especiales[numero - 10];
        }
        else if (numero < 100)
        {
            resultado = decenas[(numero / 10) - 2];
            if (numero % 10 > 0)
            {
                resultado += " Y " + unidades[numero % 10];
            }
        }
        else if (numero < 1000)
        {
            resultado = centenas[(numero / 100)];
            if (numero % 100 > 0)
            {
                resultado += " " + ConvertirEnteroALetras(numero % 100);
            }
        }
        else if (numero < 10000)
        {
            resultado = unidades[(numero / 1000)] + " MIL";
            if (numero % 1000 > 0)
            {
                resultado += " " + ConvertirEnteroALetras(numero % 1000);
            }
        }

        return resultado.Trim();
    }

    /// <summary>
    /// Método privado recursivo para convertir números a letras.
    /// Port exacto del Legacy para garantizar compatibilidad.
    /// </summary>
    private string ConvertirNumeroALetras(double value)
    {
        string resultado;
        value = Math.Truncate(value);

        if (value == 0) resultado = "CERO";
        else if (value == 1) resultado = "UNO";
        else if (value == 2) resultado = "DOS";
        else if (value == 3) resultado = "TRES";
        else if (value == 4) resultado = "CUATRO";
        else if (value == 5) resultado = "CINCO";
        else if (value == 6) resultado = "SEIS";
        else if (value == 7) resultado = "SIETE";
        else if (value == 8) resultado = "OCHO";
        else if (value == 9) resultado = "NUEVE";
        else if (value == 10) resultado = "DIEZ";
        else if (value == 11) resultado = "ONCE";
        else if (value == 12) resultado = "DOCE";
        else if (value == 13) resultado = "TRECE";
        else if (value == 14) resultado = "CATORCE";
        else if (value == 15) resultado = "QUINCE";
        else if (value < 20) resultado = "DIECI" + ConvertirNumeroALetras(value - 10);
        else if (value == 20) resultado = "VEINTE";
        else if (value < 30) resultado = "VEINTI" + (value % 10 == 1 ? "UN" : ConvertirNumeroALetras(value - 20));
        else if (value == 30) resultado = "TREINTA";
        else if (value == 40) resultado = "CUARENTA";
        else if (value == 50) resultado = "CINCUENTA";
        else if (value == 60) resultado = "SESENTA";
        else if (value == 70) resultado = "SETENTA";
        else if (value == 80) resultado = "OCHENTA";
        else if (value == 90) resultado = "NOVENTA";
        else if (value < 100) resultado = ConvertirNumeroALetras(Math.Truncate(value / 10) * 10) + " Y " + ConvertirNumeroALetras(value % 10);
        else if (value == 100) resultado = "CIEN";
        else if (value < 200) resultado = "CIENTO " + ConvertirNumeroALetras(value - 100);
        else if ((value == 200) || (value == 300) || (value == 400) || (value == 600) || (value == 800)) 
            resultado = ConvertirNumeroALetras(Math.Truncate(value / 100)) + "CIENTOS";
        else if (value == 500) resultado = "QUINIENTOS";
        else if (value == 700) resultado = "SETECIENTOS";
        else if (value == 900) resultado = "NOVECIENTOS";
        else if (value < 1000) resultado = ConvertirNumeroALetras(Math.Truncate(value / 100) * 100) + " " + ConvertirNumeroALetras(value % 100);
        else if (value == 1000) resultado = "MIL";
        else if (value < 2000) resultado = "MIL " + ConvertirNumeroALetras(value % 1000);
        else if (value < 1000000)
        {
            resultado = ConvertirNumeroALetras(Math.Truncate(value / 1000)) + " MIL";
            if ((value % 1000) > 0)
            {
                resultado = resultado + " " + ConvertirNumeroALetras(value % 1000);
            }
        }
        else if (value == 1000000)
        {
            resultado = "UN MILLON";
        }
        else if (value < 2000000)
        {
            resultado = "UN MILLON " + ConvertirNumeroALetras(value % 1000000);
        }
        else if (value < 1000000000000)
        {
            resultado = ConvertirNumeroALetras(Math.Truncate(value / 1000000)) + " MILLONES ";
            if ((value - Math.Truncate(value / 1000000) * 1000000) > 0)
            {
                resultado = resultado + " " + ConvertirNumeroALetras(value - Math.Truncate(value / 1000000) * 1000000);
            }
        }
        else if (value == 1000000000000) resultado = "UN BILLON";
        else if (value < 2000000000000) 
            resultado = "UN BILLON " + ConvertirNumeroALetras(value - Math.Truncate(value / 1000000000000) * 1000000000000);
        else
        {
            resultado = ConvertirNumeroALetras(Math.Truncate(value / 1000000000000)) + " BILLONES";
            if ((value - Math.Truncate(value / 1000000000000) * 1000000000000) > 0)
            {
                resultado = resultado + " " + ConvertirNumeroALetras(value - Math.Truncate(value / 1000000000000) * 1000000000000);
            }
        }

        return resultado;
    }
}
