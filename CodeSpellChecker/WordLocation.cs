namespace CodeSpellChecker
{
    public class WordLocation
    {
        public WordLocation(string filePath, string line, string suggestions)
        {
            FilePath = filePath;
            Line = line;
            Suggestions = suggestions;
        }

        public string FilePath { get; set; }
        public string Line { get; set; }
        public string Suggestions { get; set; }

        public override string ToString()
        {
            return FilePath + ": " + Line;
        }
    }
}
