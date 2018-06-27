using System;

namespace CodeSpellChecker
{
    public class WordLocation : IEquatable<WordLocation>
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

        public bool Equals(WordLocation other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(FilePath, other.FilePath) && string.Equals(Line, other.Line);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((WordLocation)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((FilePath != null ? FilePath.GetHashCode() : 0) * 397) ^ (Line != null ? Line.GetHashCode() : 0);
            }
        }
    }
}
