using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;

namespace CodeSpellChecker
{
    //TODO config extensions
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


        public const string CustomDictionaryFile = "Dictionary_Custom.txt";

        public const string DictionaryFile = "Dictionary.txt";

        public ConcurrentDictionary<string, List<WordLocation>> UnknownWordsDictionary;

        private bool _isStartButtonEnabled = true;

        private bool _isProgressVisible = false;

        public bool IsProgressVisible
        {
            get => _isProgressVisible;
            set => Set(ref _isProgressVisible, value);
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
                    Stopwatch sp = new Stopwatch();
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
            Status = "Starting...";
            Settings.SourceFilePath = SourceFilePath;
            Settings.ShowFileDetails = ShowFileDetails;
            if (!Directory.Exists(SourceFilePath))
            {
                MessageBox.Show($"Wrong directory \"{SourceFilePath}\"", "Error", MessageBoxButton.OK);
                return;
            }

            File.WriteAllText(SettingFile, JsonConvert.SerializeObject(Settings, Formatting.Indented));

            var dictionary = File.ReadAllLines(DictionaryFile).ToList();
            if (File.Exists(CustomDictionaryFile))
            {
                dictionary.AddRange(File.ReadAllLines(CustomDictionaryFile));
            }

            dictionary = dictionary.ConvertAll(d => d.Trim().ToLower());
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
                    var trimmedLine = line.Trim();
                    var onlyWords = trimmedLine.Replace("_", " ");
                    regexes.ForEach(i => i.Replace(onlyWords, ""));
                    var words = Regex.Matches(onlyWords, @"(\w+)")
                        .OfType<Match>()
                        .Select(m => m.Value)
                        .ToArray();
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

                            if (!dictionary.Contains(lowerWord))
                            {
                                UnknownWordsDictionary[lowerWord] = new List<WordLocation>()
                                    {new WordLocation(file, trimmedLine)};
                            }
                        }
                    }
                });

                processedFiles++;
                Status = processedFiles + "/" + _totalFiles;
                Progress = (double)processedFiles * 100 / _totalFiles;
            });

            DisplayWords();
        }

        private void DisplayWords()
        {
            Status = "Preparing results for displaying";
            var orderedWords = UnknownWordsDictionary.Keys.OrderBy(i => i).ToArray();
            if (ShowFileDetails)
            {
                for (int i = 0; i < orderedWords.Length; i++)
                {
                    orderedWords[i] += "\n    " + string.Join("\n    ", UnknownWordsDictionary[orderedWords[i]]) + "\n";
                }
            }

            Words = string.Join("\n", orderedWords);
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
