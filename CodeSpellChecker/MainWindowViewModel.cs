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
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;

namespace CodeSpellChecker
{
    public class MainWindowViewModel : ViewModelBase
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

        private RelayCommand _prepareDictionariesCommand;

        public RelayCommand PrepareDictionariesCommand =>
            _prepareDictionariesCommand ?? (_prepareDictionariesCommand = new RelayCommand(PrepareDictionaries));

        private RelayCommand _sortDictionariesCommand;

        public RelayCommand SortDictionariesCommand =>
            _sortDictionariesCommand ?? (_sortDictionariesCommand = new RelayCommand(SortDictionaries));

        private void SortDictionaries()
        {
            SortDictionary(DictionaryFile);
            SortDictionary(ProgrammingDictionaryFile);
            SortDictionary(CustomDictionaryFile);
        }

        private void SortDictionary(string filePath)
        {
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
                if (Set(ref _sourceFilePath, value))
                {
                    StartCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _words;

        public string Words
        {
            get => _words;
            set => Set(ref _words, value);
        }

        private bool? _showFileDetails;

        public bool ShowFileDetails
        {
            get => (_showFileDetails ?? (_showFileDetails = Settings.ShowFileDetails)).Value;
            set
            {
                if (Set(ref _showFileDetails, value))
                {
                    RunAsyncTask(DisplayWords);
                }
            }
        }

        private string _excludeLinesRegex;

        public string ExcludeLinesRegex
        {
            get => _excludeLinesRegex ?? (_excludeLinesRegex = string.Join("\n", Settings.IgnoredContents));
            set => Set(ref _excludeLinesRegex, value);
        }

        private string _status;

        public string Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        private double _progress;

        public double Progress
        {
            get => _progress;
            set => Set(ref _progress, value);
        }

        private string _fileExtensions;

        public string FileExtensions
        {
            get => _fileExtensions ?? (_fileExtensions = string.Join("; ", Settings.FileExtensions));
            set => Set(ref _fileExtensions, value);
        }

        private string _excludeFolders;

        public string ExcludeFolders
        {
            get => _excludeFolders ?? (_excludeFolders = "\"" + string.Join("\"; \"", Settings.ExcludeFolders) + "\"");
            set => Set(ref _excludeFolders, value);
        }


        public const string CustomDictionaryFile = "Dictionary_Custom.txt";

        public const string ProgrammingDictionaryFile = "Dictionary_Programming.txt";

        public const string DictionaryFile = "Dictionary.txt";

        public ConcurrentDictionary<string, List<WordLocation>> UnknownWordsDictionary;

        private bool _isStartButtonEnabled = true;

        private bool _isProgressVisible = false;

        public bool IsProgressVisible
        {
            get => _isProgressVisible;
            set => Set(ref _isProgressVisible, value);
        }

        private bool _showTextBox;

        public bool ShowTextBox
        {
            get => _showTextBox;
            set
            {
                if (Set(ref _showTextBox, value))
                {
                    RunAsyncTask(DisplayWords);
                    RaisePropertyChanged(nameof(ShowDataGrid));
                }
            }
        }

        public bool ShowDataGrid => !ShowTextBox;

        private string _unknownWordsStat;

        public string UnknownWordsStat
        {
            get => _unknownWordsStat;
            set => Set(ref _unknownWordsStat, value);
        }

        private string _minimumWordLength;

        public string MinimumWordLength
        {
            get => _minimumWordLength ?? (_minimumWordLength = Settings.IgnoreIfLengthLessThan.ToString());
            set
            {
                if (Set(ref _minimumWordLength, value))
                {
                    var isInt = int.TryParse(_minimumWordLength, out var v);
                    if (isInt)
                    {
                        Settings.IgnoreIfLengthLessThan = v;
                    }
                    else
                    {
                        Set(ref _minimumWordLength, Settings.IgnoreIfLengthLessThan.ToString());
                    }
                }
            }
        }

        private ObservableCollection<WordInfo> _wordsTable;

        public ObservableCollection<WordInfo> WordsTable
        {
            get => _wordsTable;
            set => Set(ref _wordsTable, value);
        }

        private void ChangeStartCommandCanExecute(bool isStartButtonEnabled)
        {
            var dispatcher = GetDispatcher();
            dispatcher?.Invoke(() =>
            {
                _isStartButtonEnabled = isStartButtonEnabled;
                StartCommand.RaiseCanExecuteChanged();
            }, DispatcherPriority.Send);
        }

        private static Dispatcher GetDispatcher()
        {
            var app = Application.Current;
            return app?.Dispatcher;
        }

        private int _totalFiles;

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
                Status = $"Completed ({timeElapsed}, Analysed {_totalFiles} files)";
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

        private void Analyse()
        {
            //LoadSettings();

            Progress = 0;
            _totalFiles = 0;
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

            var dictionaryFileNameRegex = new Regex(string.Format(FormattedDictionaryFileName, "([0-9]+)"));
            var dictionaries = GetFormattedDictionaries(currentFolder, dictionaryFileNameRegex);
            if (!dictionaries.Any())
            {
                PrepareDictionaries();
                dictionaries = GetFormattedDictionaries(currentFolder, dictionaryFileNameRegex);
            }

            if (!dictionaries.Any())
            {
                MessageBox.Show("Cannot load dictionaries files", "Error", MessageBoxButton.OK);
                return;
            }

            LookUpDictionary = new Dictionary<int, List<string>>();
            foreach (var d in dictionaries)
            {
                var length = dictionaryFileNameRegex.Match(d).Groups[1].Value;
                var lengthNumber = int.Parse(length);
                LookUpDictionary[lengthNumber] = File.ReadAllLines(d).ToList();
            }

            var path = SourceFilePath;
            if (!path.EndsWith("\\"))
            {
                path += "\\";
            }

            var allFiles = new List<string>();
            foreach (var ext in Settings.FileExtensions)
            {
                allFiles.AddRange(GetAllFiles(path, "*" + ext, info =>
                      Settings.FileExtensions.Any(i => Path.GetExtension(info.Name) == i)
                      && Settings.ExcludeFolders.TrueForAll(
                          i => (info.Directory?.FullName.Replace(path, @"\") + "\\").Contains(i) == false
                      )));
            }

            UnknownWordsDictionary = new ConcurrentDictionary<string, List<WordLocation>>();
            var regexes = Settings.IgnoredContents.Select(i => new Regex(i)).ToList();
            _totalFiles = allFiles.Count;
            var processedFiles = 0;
            Parallel.ForEach(allFiles, file =>
            {
                var lines = File.ReadAllLines(file);
                Parallel.ForEach(lines, line =>
                {
                    try
                    {
                        CheckLine(file, line, LookUpDictionary, regexes);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message, "Error", MessageBoxButton.OK);
                    }
                });

                processedFiles++;
                Status = processedFiles + "/" + _totalFiles;
                Progress = (double)processedFiles * 100 / _totalFiles;
            });

            DisplayWords();
        }

        private void CheckLine(string file, string line, Dictionary<int, List<string>> dictionary, List<Regex> regexes)
        {
            var trimmedLine = line.Trim();
            var onlyWords = trimmedLine.Replace("_", " ");
            regexes.ForEach(i => onlyWords = i.Replace(onlyWords, ""));
            var words = Regex.Matches(onlyWords, @"(\w+)")
                .OfType<Match>()
                .Select(m => m.Value)
                .ToArray();
            CheckWord(file, dictionary, trimmedLine, words);
        }

        private void CheckWord(string file, Dictionary<int, List<string>> dictionary, string trimmedLine, string[] words)
        {
            foreach (var word in words)
            {
                // may contain camel cases
                var englishWord = new Regex("[^a-zA-Z]+").Replace(word, "");
                if (englishWord.Length < Settings.IgnoreIfLengthLessThan)
                {
                    continue;
                }

                var singleWords = SplitCamelCase(englishWord);
                foreach (var singleWord in singleWords)
                {
                    if (singleWord.Length < Settings.IgnoreIfLengthLessThan)
                    {
                        continue;
                    }

                    var lowerWord = singleWord.ToLower();
                    if (UnknownWordsDictionary.ContainsKey(lowerWord))
                    {
                        UnknownWordsDictionary[lowerWord].Add(new WordLocation(file, trimmedLine, UnknownWordsDictionary[lowerWord][0].Suggestions));
                        continue;
                    }

                    if (!dictionary.ContainsKey(lowerWord.Length)
                        || dictionary[lowerWord.Length].BinarySearch(lowerWord) < 0)
                    {
                        var suggestions = GetSuggestion(lowerWord);
                        UnknownWordsDictionary[lowerWord] = new List<WordLocation>()
                                    {new WordLocation(file, trimmedLine, suggestions)};
                    }
                }
            }
        }

        private void SaveSettings()
        {
            Settings.SourceFilePath = SourceFilePath;
            Settings.ShowFileDetails = ShowFileDetails;
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
                Settings.IgnoredContents = ExcludeLinesRegex.Split('\n').ToList();
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
                    words.Add(new WordInfo(word, UnknownWordsDictionary[word]));
                }
            }
            else
            {
                foreach (var word in orderedWords)
                {
                    words.Add(new WordInfo(word, UnknownWordsDictionary[word][0].Suggestions));
                }
            }

            WordsTable = new ObservableCollection<WordInfo>(words);
            if (ShowTextBox)
            {
                var w = new string[orderedWords.Length];
                for (var i = 0; i < words.Count; i++)
                {
                    var word = words[i];
                    var suggestion = string.IsNullOrWhiteSpace(word.Suggestions) ? "" : "\n    [Do you mean: " + word.Suggestions.Replace("\n", ", ") + "]";
                    w[i] = word.Word + suggestion
                            + "\n    " + string.Join("\n    ", word.Location) + "\n";
                }

                Words = string.Join("\n", w);
            }

            UnknownWordsStat = GetUnknownWordsStat();
            Status = "Completed";
        }

        private string GetSuggestion(string word)
        {
            if (word.Length <= 3)
            {
                return null;
            }

            var minEditDistance = Math.Min(3, word.Length / 3);
            var suggestions = new List<string>();
            GetSuggestion(word, word.Length - 1, ref minEditDistance, ref suggestions);
            GetSuggestion(word, word.Length, ref minEditDistance, ref suggestions);
            GetSuggestion(word, word.Length + 1, ref minEditDistance, ref suggestions);
            return string.Join("\n", suggestions);
        }

        private void GetSuggestion(string word, int length, ref int minEditDistance, ref List<string> suggestions)
        {
            var dictionary = LookUpDictionary[length];
            foreach (var p in dictionary)
            {
                var distance = LevenshteinDistance.Compute(p, word);
                if (distance < minEditDistance)
                {
                    minEditDistance = distance;
                    suggestions = new List<string>() { p };
                }
                else if (distance == minEditDistance)
                {
                    suggestions.Add(p);
                }
            }
        }

        private string[] SplitCamelCase(string word)
        {
            var words = Regex.Matches(word, "(^[a-z]+|[A-Z]+(?![a-z])|[A-Z][a-z]+)")
                .OfType<Match>()
                .Select(m => m.Value)
                .ToArray();
            return words;
        }

        public static IEnumerable<string> GetAllFiles(string path, string mask, Func<FileInfo, bool> checkFile = null)
        {
            if (string.IsNullOrEmpty(mask))
                mask = "*.*";
            var files = Directory.GetFiles(path, mask, SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (checkFile == null || checkFile(new FileInfo(file)))
                    yield return file;
            }
        }
    }
}
