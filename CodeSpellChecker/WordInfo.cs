using System.Collections.Generic;

namespace CodeSpellChecker
{
    public class WordInfo
    {
        public string Word { get; set; }

        public string Location { get; set; }

        public WordInfo(string word)
        {
            Word = word;
        }

        public WordInfo(string word, List<WordLocation> list)
        {
            Word = word;
            Location = string.Join("\n", list);
        }
    }
}
