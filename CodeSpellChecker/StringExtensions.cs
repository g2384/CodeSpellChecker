using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CodeSpellChecker
{
    public static class StringExtensions
    {
        public static IEnumerable<string> SplitCamelCase(this string value)
        {
            var words = Regex.Matches(value, "(^[a-z]+|[A-Z]+(?![a-z])|[A-Z][a-z]+)")
                .OfType<Match>()
                .Select(m => m.Value);
            return words;
        }

        public static IEnumerable<string> Split(this string value, string separator)
        {
            return value.Split(new[] {separator}, StringSplitOptions.None).ToList();
        }
    }
}
