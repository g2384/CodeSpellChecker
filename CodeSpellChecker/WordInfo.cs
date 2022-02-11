using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace CodeSpellChecker
{
    public class WordInfo : ObservableObject
    {
        public string Word { get; set; }

        public List<WordLocation> Locations { get; set; }

        private string _location;

        public string Location
        {
            get => _location ?? (_location = WordLocationsToString(Locations));
            set => SetProperty(ref _location, value);
        }

        public string Suggestions { get; set; }

        public WordInfo(string word, string suggestions)
        {
            Word = word;
            Suggestions = suggestions;
        }

        public WordInfo(string word, List<WordLocation> list, string suggestions)
        {
            Word = word;
            Locations = list;
            Suggestions = suggestions;
        }

        public string WordLocationsToString(List<WordLocation> list)
        {
            var files = new Dictionary<string, List<string>>();
            if (list != null)
            {
                foreach (var line in list)
                {
                    if (files.ContainsKey(line.FilePath))
                    {
                        files[line.FilePath].Add(line.Line);
                    }
                    else
                    {
                        files[line.FilePath] = new List<string> { line.Line };
                    }
                }
            }

            var results = new List<string>();
            foreach (var key in files.Keys)
            {
                results.Add(key);
                if (files[key].Count > 1)
                {
                    for (var i = 0; i < files[key].Count; i++)
                    {
                        results.Add("    [" + (i + 1) + "] " + files[key][i]);
                    }
                }
                else
                {
                    results.Add("    " + files[key][0]);
                }

                results.Add(string.Empty);
            }

            if (results.Count > 0)
            {
                results.RemoveAt(results.Count - 1);
            }

            return string.Join(Environment.NewLine, results);
        }
    }
}
