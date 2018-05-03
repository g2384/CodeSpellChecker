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
            _startCommand ?? (_startCommand = new RelayCommand(AnalyseAsync, () => _isStartButtonEnabled && !string.IsNullOrWhiteSpace(SourceFilePath)));

        private RelayCommand _prepareDictionaries;

        public RelayCommand PrepareDictionaries =>
            _prepareDictionaries ?? (_prepareDictionaries = new RelayCommand(PrepareDictionariesCommand));

        private RelayCommand<WordInfo> _addToCustomDictionaryCommand;

        public RelayCommand<WordInfo> AddToCustomDictionaryCommand =>
            _addToCustomDictionaryCommand ?? (_addToCustomDictionaryCommand = new RelayCommand<WordInfo>(AddToCustomDictionary));

        private RelayCommand<WordInfo> _addToStandardDictionaryCommand;

        public RelayCommand<WordInfo> AddToStandardDictionaryCommand =>
            _addToStandardDictionaryCommand ?? (_addToStandardDictionaryCommand = new RelayCommand<WordInfo>(AddToStandardDictionary));

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
            Status = wordInfo.Word + " is added to " + dictionaryFileName;
        }

        public const string FormattedDictionaryFileName = "~Dictionary{0}.txt";

        private void PrepareDictionariesCommand()
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
            entries = entries.ConvertAll(d => d.Trim().ToLower());
            var groups = entries.GroupBy(i => i.Length);
            foreach (var g in groups)
            {
                File.WriteAllLines(string.Format(FormattedDictionaryFileName, g.Key), g.ToArray());
            }
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

        private bool _showFileDetails;

        public bool ShowFileDetails
        {
            get => _showFileDetails;
            set
            {
                if (Set(ref _showFileDetails, value))
                {
                    DisplayWords();
                }
            }
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

        public async void AnalyseAsync()
        {
            await Task.Run(() =>
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
                }
                finally
                {
                    ChangeStartCommandCanExecute(true);
                }
            });
        }

        private void Analyse()
        {
            Progress = 0;
            _totalFiles = 0;
            UnknownWordsStat = "";
            Status = "Starting...";
            if (!Directory.Exists(SourceFilePath))
            {
                MessageBox.Show($"Wrong directory \"{SourceFilePath}\"", "Error", MessageBoxButton.OK);
                return;
            }

            StoreSettings();

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
                PrepareDictionariesCommand();
                dictionaries = GetFormattedDictionaries(currentFolder, dictionaryFileNameRegex);
            }

            if (!dictionaries.Any())
            {
                MessageBox.Show("Cannot load dictionaries files", "Error", MessageBoxButton.OK);
                return;
            }

            var dictionary = new Dictionary<int, string[]>();
            foreach (var d in dictionaries)
            {
                var length = dictionaryFileNameRegex.Match(d).Groups[1].Value;
                int lengthNumber = int.Parse(length);
                dictionary[lengthNumber] = File.ReadAllLines(d).ToArray();
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
                          i => info.Directory?.FullName.Replace(path, @"\").Contains(i) == false
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
                        CheckLine(file, line, dictionary, regexes);
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

        private void CheckLine(string file, string line, Dictionary<int, string[]> dictionary, List<Regex> regexes)
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

        private void CheckWord(string file, Dictionary<int, string[]> dictionary, string trimmedLine, string[] words)
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
                        UnknownWordsDictionary[lowerWord].Add(new WordLocation(file, trimmedLine));
                        continue;
                    }

                    if (!dictionary.ContainsKey(lowerWord.Length)
                        || !dictionary[lowerWord.Length].Contains(lowerWord))
                    {
                        UnknownWordsDictionary[lowerWord] = new List<WordLocation>()
                                    {new WordLocation(file, trimmedLine)};
                    }
                }
            }
        }

        private void StoreSettings()
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

            File.WriteAllText(SettingFile, JsonConvert.SerializeObject(Settings, Formatting.Indented));
        }

        private static List<string> GetFormattedDictionaries(string currentFolder, Regex dictionaryFileNameRegex)
        {
            var allFilesInCurrentFolder = Directory.GetFiles(currentFolder);
            var dictionaries = allFilesInCurrentFolder.Where(i => dictionaryFileNameRegex.Match(i).Success);
            return dictionaries.ToList();
        }

        private void DisplayWords()
        {
            Status = "Preparing results for displaying";
            var orderedWords = UnknownWordsDictionary.Keys.OrderBy(i => i).ToArray();
            WordsTable = new ObservableCollection<WordInfo>(); ;

            if (ShowFileDetails)
            {
                for (var i = 0; i < orderedWords.Length; i++)
                {
                    var word = orderedWords[i];
                    orderedWords[i] += "\n    " + string.Join("\n    ", UnknownWordsDictionary[word]) + "\n";
                    WordsTable.Add(new WordInfo(word, UnknownWordsDictionary[word]));
                }
            }
            else
            {
                foreach (var word in orderedWords)
                {
                    WordsTable.Add(new WordInfo(word));
                }
            }

            Words = string.Join("\n", orderedWords);
            UnknownWordsStat = orderedWords.Length + " words";
            Status = "Completed";
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
