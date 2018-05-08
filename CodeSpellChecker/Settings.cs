using System.Collections.Generic;

namespace CodeSpellChecker
{
    public class Settings
    {
        public string SourceFilePath { get; set; }

        public List<string> ExcludeFolders { get; set; }

        public List<string> FileExtensions { get; set; }

        public bool ShowFileDetails { get; set; }

        public int IgnoreIfLengthLessThan { get; set; }

        public List<string> IgnoredContents { get; set; }

        public void Init()
        {
            IgnoreIfLengthLessThan = 3;

            IgnoredContents = new List<string>()
            {
                @"Guid\(""[0-9a-zA-Z\-]+""\)",
                @"&[a-zA-Z]+;",
                @"///   Looks up a localized string similar to.*",
                @"\b0[xX][0-9a-fA-F]+\b",
                @"#\b[0-9a-fA-F]+\b",
                @"[Ll]orem ipsum[\w ,\.]+",
                @"\b(isn|doesn|hasn|haven)'t\b",
                @"[\\/][a-zA-Z]+", // escaped key words, e.g. \n, /str
            };

            FileExtensions = new List<string>()
            {
                ".cs"
            };

            ExcludeFolders = new List<string>()
            {
                @"\obj\", @"\bin\"
            };
        }
    }
}
