using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CodeSpellChecker
{
    public class MainWindowViewModel : ObservableObject
    {
        public const string SettingFile = "settings.json";

        public Settings Settings { get; set; }

        public MainWindowViewModel()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            if (File.Exists(SettingFile))
            {
                var setting = File.ReadAllText(SettingFile);
                Settings = JsonConvert.DeserializeObject<Settings>(setting);
            }
            else
            {
                Settings = new Settings();
                Settings.Init();
            }
        }

        private RelayCommand _startCommand;

        public RelayCommand StartCommand =>
            _startCommand ?? (_startCommand = new RelayCommand(() => RunAsyncTask(AnalyseAsync), () => _isStartButtonEnabled && !string.IsNullOrWhiteSpace(SourceFilePath)));

        private bool? _canSortDictionary;

        public bool CanSortDictionary
        {
            get => (_canSortDictionary ?? (_canSortDictionary = Settings.CanSortDictionary)).Value;
            set => SetProperty(ref _canSortDictionary, value);
        }

        private void SortDictionaries()
        {
            if (!CanSortDictionary)
            {
                return;
            }

            SortDictionary(DictionaryFile);
            SortDictionary(ProgrammingDictionaryFile);
            SortDictionary(CustomDictionaryFile);
        }

        private void SortDictionary(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            var entries = File.ReadAllLines(filePath).ToList();
            entries.Sort();
            for (var i = 0; i < entries.Count; i++)
            {
                entries[i] = entries[i].ToLower();
            }

            var uniqueEntries = entries.Distinct().ToList();
            uniqueEntries.RemoveAll(string.IsNullOrWhiteSpace);
            File.WriteAllText(filePath, string.Join("\n", uniqueEntries));
            Status = "Dictionary is sorted";
        }

        private RelayCommand<WordInfo> _addToCustomDictionaryCommand;

        public RelayCommand<WordInfo> AddToCustomDictionaryCommand =>
            _addToCustomDictionaryCommand ?? (_addToCustomDictionaryCommand = new RelayCommand<WordInfo>(AddToCustomDictionary));

        private RelayCommand<WordInfo> _addToStandardDictionaryCommand;

        public RelayCommand<WordInfo> AddToStandardDictionaryCommand =>
            _addToStandardDictionaryCommand ?? (_addToStandardDictionaryCommand = new RelayCommand<WordInfo>(AddToStandardDictionary));

        private RelayCommand<WordInfo> _addToProgrammingDictionaryCommand;

        public RelayCommand<WordInfo> AddToProgrammingDictionaryCommand =>
            _addToProgrammingDictionaryCommand ?? (_addToProgrammingDictionaryCommand = new RelayCommand<WordInfo>(AddToProgrammingDictionary));

        private void AddToProgrammingDictionary(WordInfo wordInfo)
        {
            AddWordToDictionary(wordInfo, ProgrammingDictionaryFile);
        }

        private void AddToStandardDictionary(WordInfo wordInfo)
        {
            AddWordToDictionary(wordInfo, DictionaryFile);
        }

        private void AddToCustomDictionary(WordInfo wordInfo)
        {
            AddWordToDictionary(wordInfo, CustomDictionaryFile);
        }

        private void AddWordToDictionary(WordInfo wordInfo, string dictionaryFileName)
        {
            File.AppendAllText(dictionaryFileName, "\n" + wordInfo.Word);
            WordsTable.Remove(wordInfo);
            UnknownWordsDictionary.TryRemove(wordInfo.Word, out _);
            Status = wordInfo.Word + " is added to " + dictionaryFileName;
            UnknownWordsStat = GetUnknownWordsStat();
        }

        private string GetUnknownWordsStat()
        {
            return WordsTable.Count + " words";
        }

        public const string FormattedDictionaryFileName = "~Dictionary{0}.txt";
        public const string DictionaryFolder = "Dictionary";

        private void PrepareDictionaries()
        {
            SortDictionaries();
            var entries = File.ReadAllLines(DictionaryFile).ToList();
            if (File.Exists(CustomDictionaryFile))
            {
                entries.AddRange(File.ReadAllLines(CustomDictionaryFile));
            }

            if (File.Exists(ProgrammingDictionaryFile))
            {
                entries.AddRange(File.ReadAllLines(ProgrammingDictionaryFile));
            }

            entries = entries.Distinct().ToList();
            entries.RemoveAll(string.IsNullOrWhiteSpace);
            entries = entries.ConvertAll(d => d.Trim().ToLower());
            var groups = entries.GroupBy(i => i.Length);
            if (!Directory.Exists(DictionaryFolder))
            {
                Directory.CreateDirectory(DictionaryFolder);
            }

            foreach (var g in groups)
            {
                File.WriteAllLines(DictionaryFolder + "\\" + string.Format(FormattedDictionaryFileName, g.Key), g.OrderBy(i => i).ToArray());
            }

            Status = "Dictionary is updated";
        }

        private string _sourceFilePath;

        public string SourceFilePath
        {
            get => _sourceFilePath ?? (_sourceFilePath = Settings.SourceFilePath);
            set
            {
                if (SetProperty(ref _sourceFilePath, value))
                {
                    StartCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _words;

        public string Words
        {
            get => _words;
            set => SetProperty(ref _words, value);
        }

        private bool? _showFileDetails;

        public bool ShowFileDetails
        {
            get => (_showFileDetails ?? (_showFileDetails = Settings.ShowFileDetails)).Value;
            set
            {
                if (SetProperty(ref _showFileDetails, value))
                {
                    RunAsyncTask(DisplayWords);
                }
            }
        }

        private string _excludeLinesRegex;

        public string ExcludeLinesRegex
        {
            get => _excludeLinesRegex ?? (_excludeLinesRegex = string.Join(Environment.NewLine, Settings.IgnoredContents));
            set => SetProperty(ref _excludeLinesRegex, value);
        }

        private string _status;

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private double _progress;

        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        private string _fileExtensions;

        public string FileExtensions
        {
            get => _fileExtensions ?? (_fileExtensions = string.Join("; ", Settings.FileExtensions));
            set => SetProperty(ref _fileExtensions, value);
        }

        private string _excludeFolders;

        public string ExcludeFolders
        {
            get => _excludeFolders ?? (_excludeFolders = "\"" + string.Join("\"; \"", Settings.ExcludeFolders) + "\"");
            set => SetProperty(ref _excludeFolders, value);
        }


        public const string CustomDictionaryFile = "Dictionary_Custom.txt";

        public const string ProgrammingDictionaryFile = "Dictionary_Programming.txt";

        public const string DictionaryFile = "Dictionary.txt";
        public const string Indentation = "    ";
        public ConcurrentDictionary<string, HashSet<WordLocation>> UnknownWordsDictionary;
        public ConcurrentDictionary<string, HashSet<string>> Collocations;

        private bool _isStartButtonEnabled = true;

        private bool _isProgressVisible = false;

        public bool IsProgressVisible
        {
            get => _isProgressVisible;
            set => SetProperty(ref _isProgressVisible, value);
        }

        private bool _showTextBox;

        public bool ShowTextBox
        {
            get => _showTextBox;
            set
            {
                if (SetProperty(ref _showTextBox, value))
                {
                    RunAsyncTask(DisplayWords);
                    OnPropertyChanged(nameof(ShowDataGrid));
                }
            }
        }

        public bool ShowDataGrid => !ShowTextBox && WordsTable != null && WordsTable.Count > 0;

        private string _unknownWordsStat;

        public string UnknownWordsStat
        {
            get => _unknownWordsStat;
            set => SetProperty(ref _unknownWordsStat, value);
        }

        private string _minimumWordLength;

        public string MinimumWordLength
        {
            get => _minimumWordLength ?? (_minimumWordLength = Settings.IgnoreIfLengthLessThan.ToString());
            set
            {
                if (SetProperty(ref _minimumWordLength, value))
                {
                    var isInt = int.TryParse(_minimumWordLength, out var v);
                    if (isInt)
                    {
                        Settings.IgnoreIfLengthLessThan = v;
                    }
                    else
                    {
                        SetProperty(ref _minimumWordLength, Settings.IgnoreIfLengthLessThan.ToString());
                    }
                }
            }
        }

        private ObservableCollection<WordInfo> _wordsTable;

        public ObservableCollection<WordInfo> WordsTable
        {
            get => _wordsTable;
            set => SetProperty(ref _wordsTable, value);
        }

        private void ChangeStartCommandCanExecute(bool isStartButtonEnabled)
        {
            var dispatcher = GetDispatcher();
            dispatcher?.Invoke(() =>
            {
                _isStartButtonEnabled = isStartButtonEnabled;
                StartCommand.NotifyCanExecuteChanged();
            }, DispatcherPriority.Send);
        }

        private static Dispatcher GetDispatcher()
        {
            var app = Application.Current;
            return app?.Dispatcher;
        }

        private int _totalFilesCount;

        private async void RunAsyncTask(Action action)
        {
            await Task.Run(action);
        }

        private void AnalyseAsync()
        {
            try
            {
                ChangeStartCommandCanExecute(false);
                IsProgressVisible = true;
                var sp = new Stopwatch();
                sp.Start();
                Analyse();
                sp.Stop();
                IsProgressVisible = false;
                var timeElapsed = $"Time elapsed: {sp.Elapsed.Hours}h {sp.Elapsed.Minutes}m {sp.Elapsed.Seconds}s {sp.Elapsed.Milliseconds}ms";
                Status = $"Completed ({timeElapsed}, Analysed {_totalFilesCount} files)";
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK);
                Status = "Error occurred";
            }
            finally
            {
                ChangeStartCommandCanExecute(true);
            }
        }

        public Dictionary<int, List<string>> LookUpDictionary;
        public ConcurrentDictionary<string, string> CachedLookUpDictionary;

        private void Analyse()
        {
            //LoadSettings();

            Progress = 0;
            _totalFilesCount = 0;
            UnknownWordsStat = "";
            Status = "Starting...";
            if (!Directory.Exists(SourceFilePath))
            {
                MessageBox.Show($"Wrong directory \"{SourceFilePath}\"", "Error", MessageBoxButton.OK);
                return;
            }

            SaveSettings();

            var currentFolder = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            if (string.IsNullOrWhiteSpace(currentFolder))
            {
                MessageBox.Show("Cannot get current folder path", "Error", MessageBoxButton.OK);
                return;
            }

            CachedLookUpDictionary = new ConcurrentDictionary<string, string>();
            var successful = LoadDictionary(currentFolder);

            if (!successful)
            {
                MessageBox.Show("Cannot load dictionaries files", "Error", MessageBoxButton.OK);
                return;
            }

            var path = SourceFilePath;
            if (!path.EndsWith("\\"))
            {
                path += "\\";
            }

            var allFiles = new List<string>();
            foreach (var ext in Settings.FileExtensions)
            {
                var extension = ext.StartsWith("*") ? ext : "*" + ext;
                allFiles.AddRange(FileHelper.GetAllFiles(path, extension, info =>
                    Settings.FileExtensions.Any(i => Path.GetExtension(info.Name) == i)
                    && Settings.ExcludeFolders.TrueForAll(
                        i => (info.Directory?.FullName.Replace(path, @"\") + "\\").Contains(i) == false
                    )));
            }

            UnknownWordsDictionary = new ConcurrentDictionary<string, HashSet<WordLocation>>();
            Collocations = new ConcurrentDictionary<string, HashSet<string>>();
            var regexes = Settings.IgnoredContents.Select(i => new Regex(i)).ToList();
            _totalFilesCount = allFiles.Count;
            var processedFiles = 0;
            Parallel.ForEach(allFiles, file =>
            {
                var shortFilePath = file.Replace(Settings.SourceFilePath, "");
                CheckLine(shortFilePath, shortFilePath, LookUpDictionary, CachedLookUpDictionary, regexes);
                var lines = File.ReadAllLines(file);
                Parallel.ForEach(lines, line =>
                {
                    try
                    {
                        CheckLine(shortFilePath, line, LookUpDictionary, CachedLookUpDictionary, regexes);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message + Environment.NewLine + e.StackTrace, "Error", MessageBoxButton.OK);
                    }
                });

                processedFiles++;
                Status = processedFiles + "/" + _totalFilesCount;
                Progress = (double)processedFiles * 100 / _totalFilesCount;
            });

            lock (CachedLookUpDictionary)
            {
                DisplayWords();
            }
        }

        private bool LoadDictionary(string currentFolder)
        {
            var dictionaryFileNameRegex = new Regex(string.Format(FormattedDictionaryFileName, "([0-9]+)"));
            var dictionaries = GetFormattedDictionaries(currentFolder, dictionaryFileNameRegex);
            if (!dictionaries.Any())
            {
                PrepareDictionaries();
                dictionaries = GetFormattedDictionaries(currentFolder, dictionaryFileNameRegex);
            }

            if (!dictionaries.Any())
            {
                return false;
            }

            LookUpDictionary = new Dictionary<int, List<string>>();
            var sortedDictionaryModifiedTime = File.GetLastWriteTime(dictionaries[0]);
            var dictionaryFileInfo = File.GetLastWriteTime(DictionaryFile);
            var programmingDictModifiedTime = File.GetLastWriteTime(ProgrammingDictionaryFile);
            var customDictionaryModifiedTime = File.GetLastWriteTime(CustomDictionaryFile);
            if (sortedDictionaryModifiedTime < new[] {dictionaryFileInfo, programmingDictModifiedTime, customDictionaryModifiedTime}.Max())
            {
                PrepareDictionaries();
                dictionaries = GetFormattedDictionaries(currentFolder, dictionaryFileNameRegex);
            }

            foreach (var d in dictionaries)
            {
                var length = dictionaryFileNameRegex.Match(d).Groups[1].Value;
                var lengthNumber = int.Parse(length);
                LookUpDictionary[lengthNumber] = File.ReadAllLines(d).ToList();
            }

            return true;
        }

        private void CheckLine(string file, string line, Dictionary<int, List<string>> dictionary, ConcurrentDictionary<string, string> cachedDictionary, List<Regex> regexes)
        {
            var trimmedLine = line.Trim();
            regexes.ForEach(i => trimmedLine = i.Replace(trimmedLine, ""));
            var words = Regex.Matches(trimmedLine, @"([A-Za-z]+)")
                .OfType<Match>()
                .Select(m => m.Value)
                .ToArray();
            CheckWord(file, dictionary, cachedDictionary, trimmedLine, words);
        }

        private void CheckWord(string file, Dictionary<int, List<string>> dictionary, ConcurrentDictionary<string, string> cachedDictionary, string line, string[] words)
        {
            foreach (var word in words)
            {
                // may contain camel cases
                var englishWord = new Regex("[^a-zA-Z]+").Replace(word, "");
                if (englishWord.Length < Settings.IgnoreIfLengthLessThan)
                {
                    continue;
                }

                CheckCamelCase(file, dictionary, cachedDictionary, line, englishWord);
            }
        }

        private void CheckCamelCase(string file, Dictionary<int, List<string>> dictionary, ConcurrentDictionary<string, string> cachedDictionary, string line, string camelCaseWord)
        {
            var singleWords = camelCaseWord.SplitCamelCase().ToList();
            var singleWordsLowerCase = singleWords.ConvertAll(d => d.ToLower());
            var allCombos = new List<string>();
            for (var i = 0; i < singleWords.Count; i++)
            {
                for (var j = 2; j <= 3 && i + j <= singleWords.Count; j++)
                {
                    allCombos.Add(string.Join(string.Empty, singleWords.GetRange(i, j)));
                }
            }

            foreach (var singleWord in singleWordsLowerCase)
            {
                if (singleWord.Length < Settings.IgnoreIfLengthLessThan)
                {
                    continue;
                }

                if (UnknownWordsDictionary.ContainsKey(singleWord))
                {
                    UnknownWordsDictionary[singleWord].Add(new WordLocation(file, line));
                    continue;
                }

                lock (cachedDictionary)
                {
                    if (cachedDictionary.ContainsKey(singleWord))
                    {
                        AddToCollocation(allCombos, singleWord);
                        continue;
                    }
                }

                if (!dictionary.ContainsKey(singleWord.Length)
                    || dictionary[singleWord.Length].BinarySearch(singleWord) < 0)
                {

                    UnknownWordsDictionary[singleWord] = new HashSet<WordLocation> {new WordLocation(file, line)};
                    continue;
                }

                cachedDictionary[singleWord] = singleWord;
                AddToCollocation(allCombos, singleWord);
            }
        }

        private void AddToCollocation(List<string> allCombos, string singleWord)
        {
            if (allCombos.Count <= 0) return;
            var hasValue = Collocations.TryGetValue(singleWord, out var collocation);
            if (!hasValue)
            {
                collocation = new HashSet<string>();
                Collocations[singleWord] = collocation;
            }

            foreach (var c in allCombos)
            {
                collocation.Add(c);
            }
        }

        private void SaveSettings()
        {
            Settings.SourceFilePath = SourceFilePath;
            Settings.ShowFileDetails = ShowFileDetails;
            Settings.CanSortDictionary = CanSortDictionary;
            if (!string.IsNullOrWhiteSpace(ExcludeFolders))
            {
                var extensions = ExcludeFolders.Substring(1, ExcludeFolders.Length - 2);
                Settings.ExcludeFolders = new Regex(@""";\s+""").Split(extensions).Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(FileExtensions))
            {
                Settings.FileExtensions = new Regex(@"[^\*\.\w]+").Split(FileExtensions).Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(ExcludeLinesRegex))
            {
                Settings.IgnoredContents = ExcludeLinesRegex.Split(Environment.NewLine).ToList();
            }

            File.WriteAllText(SettingFile, JsonConvert.SerializeObject(Settings, Formatting.Indented));
        }

        private static List<string> GetFormattedDictionaries(string currentFolder, Regex dictionaryFileNameRegex)
        {
            var folder = currentFolder + "\\" + DictionaryFolder;
            if (!Directory.Exists(folder))
            {
                return new List<string>();
            }

            var allFilesInCurrentFolder = Directory.GetFiles(folder);
            var dictionaries = allFilesInCurrentFolder.Where(i => dictionaryFileNameRegex.Match(i).Success);
            return dictionaries.ToList();
        }

        private void DisplayWords()
        {
            if (UnknownWordsDictionary == null)
            {
                return;
            }

            Status = "Preparing results for displaying";
            var orderedWords = UnknownWordsDictionary.Keys.OrderBy(i => i).ToArray();
            var words = new List<WordInfo>();

            if (ShowFileDetails)
            {
                foreach (var word in orderedWords)
                {
                    var suggestions = GetSuggestions(word);
                    words.Add(new WordInfo(word, UnknownWordsDictionary[word].ToList(), suggestions));
                }
            }
            else
            {
                foreach (var word in orderedWords)
                {
                    var suggestions = GetSuggestions(word);
                    words.Add(new WordInfo(word, suggestions));
                }
            }

            WordsTable = new ObservableCollection<WordInfo>(words);
            OnPropertyChanged(nameof(ShowDataGrid));

            if (ShowTextBox)
            {
                var w = new string[orderedWords.Length];
                for (var i = 0; i < words.Count; i++)
                {
                    var word = words[i];
                    var suggestion = string.IsNullOrWhiteSpace(word.Suggestions) ? "" : Environment.NewLine + Indentation + "[Suggestion(s): " + word.Suggestions.Replace(Environment.NewLine, ", ") + "]";
                    w[i] = word.Word + suggestion
                                     + Environment.NewLine + Indentation + word.Location.Replace(Environment.NewLine, Environment.NewLine + Indentation) + Environment.NewLine;
                }

                Words = string.Join(Environment.NewLine, w);
            }

            UnknownWordsStat = GetUnknownWordsStat();
            Status = words.Count == 0 ? "No spelling errors." : "Completed";
        }

        private string GetSuggestions(string word)
        {
            if (word.Length <= 3)
            {
                return null;
            }

            var minEditDistance = Math.Min(3, word.Length / 3);
            var suggestions = new HashSet<string>();

            GetSuggestion(word, CachedLookUpDictionary.Keys, ref minEditDistance, ref suggestions);

            foreach (var key in Collocations.Keys)
            {
                if (!word.Contains(key)) continue;
                foreach (var w in Collocations[key])
                {
                    if (string.Equals(word, w, StringComparison.OrdinalIgnoreCase))
                    {
                        suggestions.Add(w);
                    }
                }
            }

            if (suggestions.Count == 0)
            {
                GetSuggestion(word, -1, ref minEditDistance, ref suggestions);
                GetSuggestion(word, 0, ref minEditDistance, ref suggestions);
                GetSuggestion(word, 1, ref minEditDistance, ref suggestions);

                if (suggestions.Count == 0)
                {
                    GetSuggestion(word, -2, ref minEditDistance, ref suggestions);
                    GetSuggestion(word, 2, ref minEditDistance, ref suggestions);
                }
            }

            return string.Join(Environment.NewLine, suggestions);
        }

        private void GetSuggestion(string word, int deltaLength, ref int minEditDistance, ref HashSet<string> suggestions)
        {
            var possibleWordLength = word.Length + deltaLength;
            if (LookUpDictionary.ContainsKey(possibleWordLength))
            {
                GetSuggestion(word, LookUpDictionary[possibleWordLength], ref minEditDistance, ref suggestions);
            }
        }

        private void GetSuggestion(string word, IEnumerable<string> dictionary, ref int minEditDistance, ref HashSet<string> suggestions)
        {
            foreach (var p in dictionary)
            {
                var distance = DamerauLevenshteinDistance.Compute(p, word, minEditDistance);
                if (distance < minEditDistance)
                {
                    minEditDistance = distance;
                    suggestions = new HashSet<string> {p};
                }
                else if (distance == minEditDistance)
                {
                    suggestions.Add(p);
                }
            }
        }
    }
}
