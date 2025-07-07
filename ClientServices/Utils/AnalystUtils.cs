using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnidecodeSharpFork;


namespace Analyst.Utils
{
    static class AnalystUtils
    {
        public static string SimplifyQuery(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // Приводим к нижнему регистру
            name = name.ToLowerInvariant();

            // Удаляем все символы, кроме букв, цифр, пробелов, точек и дефисов
            name = Regex.Replace(name, @"[^\w\s.\-]", "");

            // Транслитерация с кириллицы на латиницу
            name = name.Unidecode();  // метод из UnidecodeSharpFork

            // Заменяем один или несколько пробелов на дефис
            var slug = Regex.Replace(name, @"\s+", "-");

            // Убираем начальные и конечные дефисы
            return slug.Trim('-');
        }
        public static string ExtractVolumeOrWeight(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var pattern = @"(\d+(\.\d+)?)(\s)?(ml|мл|g|гр|kg|кг|л|l)\b";
            var match = Regex.Match(name.ToLower(), pattern, RegexOptions.IgnoreCase);

            return match.Success ? match.Value.Replace(" ", "") : string.Empty;
        }
    }
}
