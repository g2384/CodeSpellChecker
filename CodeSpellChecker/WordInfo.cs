using System.Collections.Generic;

namespace CodeSpellChecker
{
    public class WordInfo
    {
        public string Word { get; set; }

        public string Location { get; set; }

        public string Suggestions { get; set; }

        public WordInfo(string word, string suggestions)
        {
            Word = word;
            Suggestions = suggestions;
        }

        public WordInfo(string word, List<WordLocation> list)
        {
            Word = word;
            Location = string.Join("\n", list);
            Suggestions = list[0].Suggestions;
        }
    }
}
