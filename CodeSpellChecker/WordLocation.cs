namespace CodeSpellChecker
{
    public class WordLocation
    {
        public WordLocation(string filePath, string line)
        {
            FilePath = filePath;
            Line = line;
        }

        public string FilePath { get; set; }
        public string Line { get; set; }

        public override string ToString()
        {
            return FilePath + ": " + Line;
        }
    }
}