using IniParser.Model;
using IniParser.Model.Configuration;
using IniParser.Parser;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Ra2CsfToolsGUI.JsonExtensions;
using Ra2CsfToolsGUI.YamlExtensions;
using SadPencil.Ra2CsfFile;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace Ra2CsfToolsGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            this.InitializeComponent();

            this.DataContext = this;

            string[] arguments = Environment.GetCommandLineArgs();

            this.WatchConfigStr = this.LoadWatchConfig();

            if (arguments.Length >= 2)
            {
                string filename = arguments[1];
                this.GeneralTryCatchGUI(() =>
                {
                    this.Convert_CsfFile = this.GeneralLoadCsfIniFile(filename);
                    this.UI_FormatConverterTabItem.IsSelected = true;
                    this.StartedFromCsf = true;
                });
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public string Version { get; } = "v1.5.0-rc2";
        public string ApplicationName { get; } = "Ra2CsfToolsGUI";
        public string WindowTitle { get; } = "Ra2CsfToolsGUI (by SadPencil)";

        private const string WatchModeConfigFile = "watch_mode_config.dat";

        private const int WatchModeMaxRetries = 3;

        private bool StartedFromCsf = false;

        private bool _AdvancedMode = false;
        public bool AdvancedMode
        {
            get => this._AdvancedMode;
            set
            {
                this._AdvancedMode = value;
                this.NotifyPropertyChanged();
            }
        }

        public string TranslationNeededPlaceholder { get; } = "TODO_Translation_Needed";
        public string TranslationDeleteNeededPlaceholder { get; } = "TODO_Translation_Delete_Needed";
        public string MissingLabelPlaceholder { get; } = "TODO_Missing_Label";

        private bool _Encoding1252ReadWorkaround = true;
        public bool Encoding1252ReadWorkaround
        {
            get => this._Encoding1252ReadWorkaround;
            set
            {
                this._Encoding1252ReadWorkaround = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool _Encoding1252WriteWorkaround = false;
        public bool Encoding1252WriteWorkaround
        {
            get => this._Encoding1252WriteWorkaround;
            set
            {
                this._Encoding1252WriteWorkaround = value;
                this.NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// This method is called by the Set accessor of each property. <br/>
        /// The CallerMemberName attribute that is applied to the optional propertyName parameter causes the property name of the caller to be substituted as an argument.  
        /// </summary>
        /// <param name="propertyName"></param>
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        private CsfFileOptions GetCsfFileOptions() => new()
        {
            Encoding1252ReadWorkaround = this.Encoding1252ReadWorkaround,
            Encoding1252WriteWorkaround = this.Encoding1252WriteWorkaround,
        };

        private static IniParserConfiguration SadPencilCsfToolIniParserConfiguration { get; } = new IniParserConfiguration()
        {
            AllowDuplicateKeys = false,
            AllowDuplicateSections = false,
            AllowKeysWithoutSection = false,
            CommentRegex = new Regex("a^"), // match nothing
            CaseInsensitive = true,
            AssigmentSpacer = string.Empty,
            SectionRegex = new Regex("^(\\s*?)\\[{1}\\s*[\\p{L}\\p{P}\\p{M}_\\\"\\'\\{\\}\\#\\+\\;\\*\\%\\(\\)\\=\\?\\&\\$\\^\\<\\>\\`\\^|\\,\\:\\/\\.\\-\\w\\d\\s\\\\\\~]+\\s*\\](\\s*?)$"),
        };

        private static IniParserConfiguration GeneralIniParserConfiguration { get; } = new IniParserConfiguration()
        {
            AllowDuplicateKeys = true,
            AllowDuplicateSections = true,
            CaseInsensitive = true,
            AssigmentSpacer = string.Empty,
        };

        private static IniDataParser GetSadPencilCsfToolIniDataParser() => new(SadPencilCsfToolIniParserConfiguration);

        private static IniDataParser GetGeneralIniDataParser() => new(GeneralIniParserConfiguration);

        private void MessageBoxPanic(Exception ex) => this.Dispatcher.Invoke(() =>
        {
            _ = MessageBox.Show(this, ex.Message, string.Format("Error - {0}", this.ApplicationName), MessageBoxButton.OK, MessageBoxImage.Error);
        });

        private static IniData ParseIni(Stream stream, IniDataParser parser)
        {
            using (var sr = new StreamReader(stream, new UTF8Encoding(false)))
            {
                string iniContent = sr.ReadToEnd();
                return parser.Parse(iniContent);
            }
        }

        private static IniData ParseSadPencilCsfToolIni(Stream stream) => ParseIni(stream, GetSadPencilCsfToolIniDataParser());

        private static Dictionary<string, List<(int iLine, string value)>> LoadIniValuesFromCsfFile(CsfFile csf)
        {
            var dict = new Dictionary<string, List<(int iLine, string value)>>(StringComparer.InvariantCultureIgnoreCase);

            _ = GeneralProcessCsfIniLabels(csf, (sectionName, keyName, value, iLine) =>
            {
                if (!dict.ContainsKey(sectionName))
                {
                    dict.Add(sectionName, []);
                }
                dict[sectionName].Add((iLine, value));
            });

            return dict;
        }

        private string Convert_CsfFile_FileName = null;
        private CsfFile _Convert_CsfFile = null;
        private CsfFile Convert_CsfFile
        {
            get => this._Convert_CsfFile;
            set
            {
                this._Convert_CsfFile = value;
                if (value != null)
                {
                    this.Convert_CsfFile_Content = GetIniContentFromCsfFile(value);
                    this.Convert_CsfFile_Tips = string.Format("This string table file contains {0} labels, with language {1}.", value.Labels.Count, value.Language);
                }
                else
                {
                    this.Convert_CsfFile_Content = null;
                    this.Convert_CsfFile_Tips = null;
                }
            }
        }

        private string _Convert_CsfFile_Content = null;
        public string Convert_CsfFile_Content
        {
            get => this._Convert_CsfFile_Content;
            private set
            {
                this._Convert_CsfFile_Content = value;
                this.NotifyPropertyChanged();
            }
        }

        private string _Convert_CsfFile_Tips = null;
        public string Convert_CsfFile_Tips
        {
            get => this._Convert_CsfFile_Tips;
            private set
            {
                this._Convert_CsfFile_Tips = value;
                this.NotifyPropertyChanged();
            }
        }

        private void Convert_LoadFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            (this.Convert_CsfFile, this.Convert_CsfFile_FileName) = this.GeneralLoadCsfIniFileGUI();
        });

        private CsfFile GeneralLoadCsfIniFile(string filepath)
        {
            string fileext = Path.GetExtension(filepath);
            switch (fileext)
            {
                case ".csf":
                    using (var fs = File.Open(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        return CsfFile.LoadFromCsfFile(fs, this.GetCsfFileOptions());
                    }
                // break;
                case ".ini":
                    using (var fs = File.Open(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        return CsfFileIniHelper.LoadFromIniFile(fs, this.GetCsfFileOptions());
                    }
                // break;
                case ".yaml":
                    using (var fs = File.Open(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        return CsfFileYamlExtension.LoadFromYamlFile(fs, this.GetCsfFileOptions());
                    }
                // break;
                case ".json":
                    using (var fs = File.Open(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        return CsfFileJsonExtension.LoadFromJsonFile(fs, this.GetCsfFileOptions());
                    }
                // break;
                default:
                    throw new Exception("Unexpected file extension. Only .csf, .ini, .yaml, and .json files are accepted.");
            }
        }

        private (CsfFile csfFile, string fileName) GeneralLoadCsfIniFileGUI()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "String table files (*.csf;*.ini;*.yaml;*.json)|*.csf;*.ini;*.yaml;*.json|Westwood RA2 string table files (*.csf)|*.csf|SadPencil.Ra2CsfFile.Ini files (*.ini)|*.ini|SadPencil.Ra2CsfFile.Yaml files (*.yaml)|*.yaml|JSON files (*.json)|*.json",
            };
            if (openFileDialog.ShowDialog(this).GetValueOrDefault())
            {
                string filepath = openFileDialog.FileName;
                var csf = this.GeneralLoadCsfIniFile(filepath);
                Debug.Assert(csf != null);

                _ = MessageBox.Show(this, string.Format("File loaded successfully. This string table file contains {0} labels, with language {1}.", csf.Labels.Count, csf.Language), "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                string filename = Path.GetFileNameWithoutExtension(filepath);
                // Only preserve substrings before the first dot, if there is a dot
                filename = filename.Split('.')[0];

                return (csf, filename);
            }

            return (null, null);
        }

        private string OpenFolderGUI()
        {
            // https://stackoverflow.com/a/1922230
            var openFolderDialog = new CommonOpenFileDialog() { IsFolderPicker = true };
            return openFolderDialog.ShowDialog(this) == CommonFileDialogResult.Ok ? openFolderDialog.FileName : null;
        }

        private void GeneralSaveFileGUI(Action<Stream> saveAction, string filter, string defaultFileName = null)
        {
            var saveFileDialog = new SaveFileDialog()
            {
                Filter = filter,
            };

            if (!string.IsNullOrEmpty(defaultFileName))
            {
                saveFileDialog.FileName = defaultFileName;
            }

            if (saveFileDialog.ShowDialog(this).GetValueOrDefault())
            {
                string filename = saveFileDialog.FileName;
                using (var fs = File.Open(filename, FileMode.Create))
                {
                    saveAction.Invoke(fs);
                }
                if (MessageBox.Show(this, "File saved successfully. Would you like to open the file in File Explorer?", "Success", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    var process = new Process();
                    process.StartInfo.FileName = "explorer.exe";
                    process.StartInfo.Arguments = string.Format("/select, \"{0}\"", filename);
                    _ = process.Start();
                }
            }
        }

        private void GeneralSaveCsfIniFileGUI(CsfFile file, string defaultExtension = ".ini", string defaultFileName = null)
        {
            Debug.Assert(new List<string>() { ".ini", ".csf", ".yaml", ".json" }.Contains(defaultExtension));

            if (file == null)
            {
                throw new Exception("Please load a string table file first.");
            }

            this.GeneralSaveFileGUI(fs =>
            {
                if (defaultExtension == ".csf")
                {
                    file.WriteCsfFile(fs);
                }
                else if (defaultExtension == ".ini")
                {
                    CsfFileIniHelper.WriteIniFile(file, fs);
                }
                else if (defaultExtension == ".yaml")
                {
                    file.WriteYamlFile(fs);
                }
                else if (defaultExtension == ".json")
                {
                    file.WriteJsonFile(fs);
                }

            }, defaultExtension switch
            {
                ".csf" => "Westwood RA2 string table files (*.csf)|*.csf",
                ".ini" => "SadPencil.Ra2CsfFile.Ini files (*.ini)|*.ini",
                ".yaml" => "SadPencil.Ra2CsfFile.Yaml files (*.yaml)|*.yaml",
                ".json" => "JSON files (*.json)|*.json",
                _ => throw new Exception("Unexpected file extension."),
            }, defaultFileName);
        }

        private void GeneralSaveIniFileGUI(IniData ini, string defaultFileName = null) => this.GeneralSaveFileGUI(fs =>
        {
            using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
            {
                sw.Write(ini.ToString());
            }
        }, "SadPencil.Ra2CsfFile.Ini files (*.ini)|*.ini", defaultFileName);

        private void GeneralTryCatchGUI(Action action)
        {
#if DEBUG
            action.Invoke();
#else
            try
            {
                action.Invoke();
            }
            catch (Exception ex)
            {
                Debugger.Break();
                this.MessageBoxPanic(ex);
            }
#endif
        }

        private void Convert_SaveAsIni_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.GeneralSaveCsfIniFileGUI(this.Convert_CsfFile, ".ini", this.Convert_CsfFile_FileName);
        });

        private void Convert_SaveAsYaml_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.GeneralSaveCsfIniFileGUI(this.Convert_CsfFile, ".yaml", this.Convert_CsfFile_FileName);
        });

        private void Convert_SaveAsJson_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.GeneralSaveCsfIniFileGUI(this.Convert_CsfFile, ".json", this.Convert_CsfFile_FileName);
        });

        private void Convert_SaveAsCsf_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.GeneralSaveCsfIniFileGUI(this.Convert_CsfFile, ".csf", this.Convert_CsfFile_FileName);
        });

        private CsfFile LabelOverride_UpstreamFile = null;
        private void LabelOverride_LoadUpstreamFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            (this.LabelOverride_UpstreamFile, _) = this.GeneralLoadCsfIniFileGUI();
        });

        private CsfFile LabelOverride_CurrentFile = null;
        private string LabelOverride_CurrentFile_FileName = null;
        private void LabelOverride_LoadCurrentFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            (this.LabelOverride_CurrentFile, this.LabelOverride_CurrentFile_FileName) = this.GeneralLoadCsfIniFileGUI();
        });

        private CsfFile LabelOverride_DoWork()
        {
            if (this.LabelOverride_UpstreamFile == null || this.LabelOverride_CurrentFile == null)
            {
                throw new Exception("Please load the string table files first.");
            }

            var upstreamFile = this.LabelOverride_UpstreamFile;
            var currentFile = this.LabelOverride_CurrentFile.Clone() as CsfFile;

            var keysCopy = currentFile.Labels.Keys.ToList();

            foreach (string label in keysCopy)
            {
                if (string.IsNullOrEmpty(label) || !upstreamFile.Labels.ContainsKey(label))
                {
                    continue;
                }

                string value = currentFile.Labels[label];

                string newLabel = upstreamFile.Labels.Keys.FirstOrDefault(k => string.Equals(k, label, StringComparison.InvariantCultureIgnoreCase));
                Debug.Assert(!string.IsNullOrEmpty(newLabel));

                bool existed = currentFile.RemoveLabel(label);
                Debug.Assert(existed);

                existed = currentFile.AddLabel(newLabel, value);
                Debug.Assert(!existed);
            }

            return currentFile;
        }

        private void LabelOverride_SaveCsfFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            var outputFile = this.LabelOverride_DoWork();
            this.GeneralSaveCsfIniFileGUI(outputFile, ".csf", this.LabelOverride_CurrentFile_FileName);
        });

        private void LabelOverride_SaveIniFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            var outputFile = this.LabelOverride_DoWork();
            this.GeneralSaveCsfIniFileGUI(outputFile, ".ini", this.LabelOverride_CurrentFile_FileName);
        });

        private CsfFile TranslationNew_File = null;
        private string TranslationNew_FileName = null;

        private void TranslationNew_LoadFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            (this.TranslationNew_File, this.TranslationNew_FileName) = this.GeneralLoadCsfIniFileGUI();
        });

        private static string GetIniContentFromCsfFile(CsfFile csf)
        {
            using (var ms = new MemoryStream())
            {
                CsfFileIniHelper.WriteIniFile(csf, ms);
                using (var msCopy = new MemoryStream(ms.ToArray()))
                {
                    using (var sr = new StreamReader(msCopy, new UTF8Encoding(false)))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
        }

        private static IniData GetNewIniFileFromCsfFile(CsfFile csf)
        {
            using (var ms = new MemoryStream())
            {
                CsfFileIniHelper.WriteIniFile(csf, ms);
                using (var msCopy = new MemoryStream(ms.ToArray()))
                {
                    return ParseSadPencilCsfToolIni(msCopy);
                }
            }
        }

        private static string GetIniLabelValueKeyName(int lineIndex) => "Value" + ((lineIndex == 1) ? string.Empty : string.Format("Line{0}", lineIndex));

        private static IniData GeneralProcessCsfIniLabels(CsfFile csf, Action<string, string, string, int> valueAction = null, Action<string, KeyDataCollection> sectionAction = null)
        {
            var ini = GetNewIniFileFromCsfFile(csf);

            // process ini
            const string INI_FILE_HEADER_SECTION_NAME = "SadPencil.Ra2CsfFile.Ini";
            // load all labels
            var labelSections = new Dictionary<string, KeyDataCollection>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var (k, v) in ini.Sections.Where(section => section.SectionName != INI_FILE_HEADER_SECTION_NAME)
                .Select(section => (section.SectionName, ini.Sections[section.SectionName])))
            {
                labelSections.Add(k, v);
            }

            foreach (var keyValuePair in labelSections)
            {
                string labelName = keyValuePair.Key;
                var key = keyValuePair.Value;

                for (int iLine = 1; ; iLine++)
                {
                    string keyName = GetIniLabelValueKeyName(iLine);

                    if (!key.ContainsKey(keyName))
                    {
                        break;
                    }

                    string value = key[keyName];

                    valueAction?.Invoke(labelName, keyName, value, iLine);

                }

                sectionAction?.Invoke(labelName, key);
            }
            return ini;
        }

        private static string GetIniLabelCustomKeyName(string name, int lineIndex) => name + ((lineIndex == 1) ? string.Empty : string.Format("Line{0}", lineIndex.ToString(CultureInfo.InvariantCulture)));

        private void TranslationNew_SaveIniFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            if (this.TranslationNew_File == null)
            {
                throw new Exception("Please load a string table file first.");
            }

            var upstream = LoadIniValuesFromCsfFile(this.TranslationNew_File);
            var ini = GeneralProcessCsfIniLabels(this.TranslationNew_File, null, (labelName, key) =>
            {
                foreach ((int iLine, string value) in upstream[labelName])
                {
                    _ = key.AddKey(GetIniLabelCustomKeyName("Upstream", iLine), value);
                    Debug.Assert(key.ContainsKey(GetIniLabelValueKeyName(iLine)));
                    key[GetIniLabelValueKeyName(iLine)] = this.TranslationNeededPlaceholder;
                }
            });

            // save ini file
            this.GeneralSaveIniFileGUI(ini, this.TranslationNew_FileName);
        });

        private CsfFile TranslationTile_UpstreamFile = null;
        private CsfFile TranslationTile_TranslatedFile = null;
        private string TranslationTile_TranslatedFile_FileName = null;
        private void TranslationTile_LoadUpstreamFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            (this.TranslationTile_UpstreamFile, _) = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationTile_LoadTranslatedFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            (this.TranslationTile_TranslatedFile, this.TranslationTile_TranslatedFile_FileName) = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationTile_SaveIniFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            if (this.TranslationTile_UpstreamFile == null || this.TranslationTile_TranslatedFile == null)
            {
                throw new Exception("Please load the string table files first.");
            }

            var upstream = LoadIniValuesFromCsfFile(this.TranslationTile_UpstreamFile);

            var ini = GetNewIniFileFromCsfFile(this.TranslationTile_TranslatedFile);
            foreach (var keyValuePair in upstream)
            {
                string labelName = keyValuePair.Key;
                bool translationExist = ini.Sections.ContainsSection(labelName);
                if (!translationExist)
                {
                    _ = ini.Sections.AddSection(labelName);
                }

                var labelSection = ini.Sections[labelName];

                foreach ((int iLine, string value) in keyValuePair.Value)
                {
                    _ = labelSection.AddKey(GetIniLabelCustomKeyName("Upstream", iLine), value);
                }
                foreach ((int iLine, string value) in keyValuePair.Value)
                {
                    if (!translationExist)
                    {
                        _ = labelSection.AddKey(GetIniLabelValueKeyName(iLine), this.TranslationNeededPlaceholder);
                    }
                }
            }

            // TODO: for those keys exist in translated files but not in the upstream files, mark them up

            // save ini file
            this.GeneralSaveIniFileGUI(ini, this.TranslationTile_TranslatedFile_FileName);
        });

        private CsfFile TranslationUpdate_OldUpstreamFile = null;
        private CsfFile TranslationUpdate_NewUpstreamFile = null;
        private CsfFile TranslationUpdate_OldTranslatedFile = null;
        private string TranslationUpdate_NewUpstreamFile_FileName = null;
        private void TranslationUpdate_LoadOldUpstreamFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            (this.TranslationUpdate_OldUpstreamFile, _) = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationUpdate_LoadNewUpstreamFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            (this.TranslationUpdate_NewUpstreamFile, this.TranslationUpdate_NewUpstreamFile_FileName) = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationUpdate_LoadOldTranslatedFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            (this.TranslationUpdate_OldTranslatedFile, _) = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationUpdate_SaveIniFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            if (this.TranslationUpdate_OldUpstreamFile == null || this.TranslationUpdate_NewUpstreamFile == null || this.TranslationUpdate_OldTranslatedFile == null)
            {
                throw new Exception("Please load the string table files first.");
            }

            var diffDict = new Dictionary<string, (string oldValue, string newValue)>(StringComparer.InvariantCultureIgnoreCase);
            var oldDict = this.TranslationUpdate_OldUpstreamFile.Labels;
            var newDict = this.TranslationUpdate_NewUpstreamFile.Labels;
            var labelKeys = oldDict.Keys.Union(newDict.Keys);
            foreach (string labelName in labelKeys)
            {
                bool found;
                found = oldDict.TryGetValue(labelName, out string oldValue);
                if (!found)
                {
                    oldValue = string.Empty;
                }
                found = newDict.TryGetValue(labelName, out string newValue);
                if (!found)
                {
                    newValue = string.Empty;
                }

                if (oldValue != newValue)
                {
                    diffDict[labelName] = (oldValue, newValue);
                }
            }

            // -------

            var upstreamOld = LoadIniValuesFromCsfFile(this.TranslationUpdate_OldUpstreamFile);
            var upstreamNew = LoadIniValuesFromCsfFile(this.TranslationUpdate_NewUpstreamFile);

            // ------- 
            // delete items that needs updates
            var ini = GetNewIniFileFromCsfFile(this.TranslationUpdate_OldTranslatedFile);
            foreach (string labelName in diffDict.Keys)
            {
                _ = ini.Sections.RemoveSection(labelName);
            }

            // -------
            // add upstream info to .ini

            foreach (string labelName in labelKeys)
            {
                bool translationExist = ini.Sections.ContainsSection(labelName);
                if (!translationExist)
                {
                    _ = ini.Sections.AddSection(labelName);
                }
                var labelSection = ini.Sections[labelName];

                bool hasDifference = diffDict.ContainsKey(labelName);

                if (upstreamOld.ContainsKey(labelName))
                {
                    if (hasDifference)
                    {
                        foreach ((int iLine, string value) in upstreamOld[labelName])
                        {
                            _ = labelSection.AddKey(GetIniLabelCustomKeyName("UpstreamOld", iLine), value);
                        }
                    }
                }

                if (upstreamNew.ContainsKey(labelName))
                {
                    foreach ((int iLine, string value) in upstreamNew[labelName])
                    {
                        _ = labelSection.AddKey(GetIniLabelCustomKeyName("UpstreamNew", iLine), value);
                    }
                    foreach ((int iLine, string value) in upstreamNew[labelName])
                    {
                        if (!translationExist)
                        {
                            _ = labelSection.AddKey(GetIniLabelValueKeyName(iLine), this.TranslationNeededPlaceholder);
                        }
                    }
                    Debug.Assert(labelSection.ContainsKey(GetIniLabelValueKeyName(1)));
                }

            }

            // TODO: for those keys exist in translated files but not in the upstream files, mark them up

            // save ini
            this.GeneralSaveIniFileGUI(ini, this.TranslationUpdate_NewUpstreamFile_FileName);
        });

        private CsfFile TranslationOverride_UpstreamFile = null;
        private CsfFile TranslationOverride_TranslatedFile = null;
        private string TranslationOverride_TranslatedFile_FileName = null;

        private void TranslationOverride_LoadUpstreamFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            (this.TranslationOverride_UpstreamFile, _) = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationOverride_LoadTranslatedFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            (this.TranslationOverride_TranslatedFile, this.TranslationOverride_TranslatedFile_FileName) = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationOverride_SaveIniFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            if (this.TranslationOverride_UpstreamFile == null || this.TranslationOverride_TranslatedFile == null)
            {
                throw new Exception("Please load the string table files first.");
            }

            var oldDict = this.TranslationOverride_UpstreamFile.Labels;
            var newDict = this.TranslationOverride_TranslatedFile.Labels;
            var labelKeys = oldDict.Keys.Union(newDict.Keys);

            var newCsf = new CsfFile(this.GetCsfFileOptions())
            {
                Language = this.TranslationOverride_TranslatedFile.Language,
                Version = this.TranslationOverride_TranslatedFile.Version,
            };

            foreach (string labelName in labelKeys)
            {
                bool found;
                string value;
                found = newDict.TryGetValue(labelName, out string newValue);
                if (found)
                {
                    value = newValue;
                }
                else
                {
                    found = oldDict.TryGetValue(labelName, out string oldValue);
                    Debug.Assert(found);
                    value = oldValue;
                }

                _ = newCsf.AddLabel(labelName, value);
            }

            // save ini
            this.GeneralSaveCsfIniFileGUI(newCsf, ".ini", this.TranslationOverride_TranslatedFile_FileName);
        });

        private CsfFile TranslationUpdateCheck_OldUpstreamFile = null;
        private CsfFile TranslationUpdateCheck_NewUpstreamFile = null;
        private CsfFile TranslationUpdateCheck_OldTranslatedFile = null;
        private CsfFile TranslationUpdateCheck_NewTranslatedFile = null;
        private string TranslationUpdateCheck_NewTranslatedFile_FileName = null;

        private void TranslationUpdateCheck_LoadOldUpstreamFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            (this.TranslationUpdateCheck_OldUpstreamFile, _) = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationUpdateCheck_LoadNewUpstreamFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            (this.TranslationUpdateCheck_NewUpstreamFile, _) = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationUpdateCheck_LoadOldTranslatedFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            (this.TranslationUpdateCheck_OldTranslatedFile, _) = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationUpdateCheck_LoadNewTranslatedFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            (this.TranslationUpdateCheck_NewTranslatedFile, this.TranslationUpdateCheck_NewTranslatedFile_FileName) = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationUpdateCheck_SaveIniFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            if (this.TranslationUpdateCheck_OldUpstreamFile == null ||
            this.TranslationUpdateCheck_NewUpstreamFile == null ||
            this.TranslationUpdateCheck_OldTranslatedFile == null ||
            this.TranslationUpdateCheck_NewTranslatedFile == null)
            {
                throw new Exception("Please load the string table files first.");
            }

            var diffDict = new Dictionary<string, (string oldValue, string newValue)>(StringComparer.InvariantCultureIgnoreCase);
            var oldUpstreamDict = this.TranslationUpdateCheck_OldUpstreamFile.Labels;
            var newUpstreamDict = this.TranslationUpdateCheck_NewUpstreamFile.Labels;
            var upstreamLabelKeys = oldUpstreamDict.Keys.Union(newUpstreamDict.Keys);
            foreach (string labelName in upstreamLabelKeys)
            {
                bool found;
                found = oldUpstreamDict.TryGetValue(labelName, out string oldValue);
                if (!found)
                {
                    oldValue = string.Empty;
                }
                found = newUpstreamDict.TryGetValue(labelName, out string newValue);
                if (!found)
                {
                    newValue = string.Empty;
                }

                if (oldValue != newValue)
                {
                    diffDict[labelName] = (oldValue, newValue);
                }
            }

            // -------

            var oldTransDict = this.TranslationUpdateCheck_OldTranslatedFile.Labels;
            var newTransDict = this.TranslationUpdateCheck_NewTranslatedFile.Labels;
            var transLabelKeys = oldTransDict.Keys.Union(newTransDict.Keys);
            var allLabelKeys = transLabelKeys.Union(upstreamLabelKeys);

            var newCsf = new CsfFile(this.GetCsfFileOptions())
            {
                Language = this.TranslationUpdateCheck_NewTranslatedFile.Language,
                Version = this.TranslationUpdateCheck_NewTranslatedFile.Version,
            };

            foreach (string labelName in allLabelKeys)
            {
                bool oldTransExist = oldTransDict.TryGetValue(labelName, out string oldTransValue);
                bool newTransExist = newTransDict.TryGetValue(labelName, out string newTransValue);
                //bool oldUpstreamExist = oldUpstreamDict.TryGetValue(labelName, out var oldUpstreamValue);
                //bool newUpstreamExist = newUpstreamDict.TryGetValue(labelName, out var newUpstreamValue);

                bool skipLabel = false;
                string value;
                if (newUpstreamDict.Keys.Contains(labelName))
                {
                    if (diffDict.ContainsKey(labelName))
                    {
                        if (!newTransExist)
                        {
                            value = this.TranslationNeededPlaceholder;
                        }
                        else
                        {
                            value = (oldTransExist && oldTransValue != newTransValue) || (!oldTransExist) ? newTransValue : this.TranslationNeededPlaceholder;
                        }
                    }
                    else
                    {
                        value = newTransDict.ContainsKey(labelName) ? newTransDict[labelName] : this.TranslationNeededPlaceholder;
                    }

                }
                else
                {
                    if (newTransExist)
                    {
                        value = this.TranslationDeleteNeededPlaceholder;
                    }
                    else
                    {
                        value = null;
                        skipLabel = true;
                    }
                }

                if (!skipLabel)
                {
                    _ = newCsf.AddLabel(labelName, value);
                }
            }

            // -------

            var upstreamOld = LoadIniValuesFromCsfFile(this.TranslationUpdateCheck_OldUpstreamFile);
            var upstreamNew = LoadIniValuesFromCsfFile(this.TranslationUpdateCheck_NewUpstreamFile);
            var transOld = LoadIniValuesFromCsfFile(this.TranslationUpdateCheck_OldTranslatedFile);
            var transNew = LoadIniValuesFromCsfFile(this.TranslationUpdateCheck_NewTranslatedFile);

            // -------
            // add upstream info to .ini
            var ini = GetNewIniFileFromCsfFile(newCsf);

            foreach (string labelName in allLabelKeys)
            {
                bool translationExist = ini.Sections.ContainsSection(labelName);
                if (!translationExist)
                {
                    _ = ini.Sections.AddSection(labelName);
                }
                var labelSection = ini.Sections[labelName];

                bool hasDifference = diffDict.ContainsKey(labelName);

                if (upstreamOld.ContainsKey(labelName))
                {
                    if (hasDifference)
                    {
                        foreach ((int iLine, string value) in upstreamOld[labelName])
                        {
                            _ = labelSection.AddKey(GetIniLabelCustomKeyName("UpstreamOld", iLine), value);
                        }
                    }
                }

                if (upstreamNew.ContainsKey(labelName))
                {
                    foreach ((int iLine, string value) in upstreamNew[labelName])
                    {
                        _ = labelSection.AddKey(GetIniLabelCustomKeyName("UpstreamNew", iLine), value);
                    }
                }

                if (transOld.ContainsKey(labelName))
                {
                    Debug.Assert(transOld.ContainsKey(labelName));
                    foreach ((int iLine, string value) in transOld[labelName])
                    {
                        _ = labelSection.AddKey(GetIniLabelCustomKeyName("TranslationOld", iLine), value);
                    }
                }

                if (transNew.ContainsKey(labelName))
                {
                    foreach ((int iLine, string value) in transNew[labelName])
                    {
                        _ = labelSection.AddKey(GetIniLabelCustomKeyName("TranslationNew", iLine), value);
                    }
                }
            }

            // TODO: for those keys exist in translated files but not in the upstream files, mark them up

            // save ini
            this.GeneralSaveIniFileGUI(ini, this.TranslationUpdateCheck_NewTranslatedFile_FileName);

        });

        public string WatchConfigStr { get; set; }

        private static List<FileSystemWatcher> Watches { get; set; } = [];

        private void WatchMode_Confirm_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            foreach (var watcher in Watches)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            this.SaveWatchConfig(this.WatchConfigStr);

            this.ReInitWatches();

            _ = MessageBox.Show(this, "Your changes have been saved successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

        });

        private string GetWatchConfigFilePath()
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SadPencil", "Ra2CsfToolsGUI");
            if (!Directory.Exists(appDataPath))
            {
                _ = Directory.CreateDirectory(appDataPath);
            }
            return Path.Combine(appDataPath, WatchModeConfigFile);
        }

        private void SaveWatchConfig(string configStr)
        {
            using (var sw = new StreamWriter(this.GetWatchConfigFilePath(), append: false))
            {
                sw.Write(configStr);
            }
        }

        private string LoadWatchConfig()
        {
            string savedPath = this.GetWatchConfigFilePath();

            if (File.Exists(savedPath))
            {
                using (var sr = new StreamReader(savedPath))
                {
                    return sr.ReadToEnd();
                }
            }

            return string.Empty;
        }

        private void ReInitWatches()
        {
            try
            {
                foreach (var watched in Watches)
                {
                    watched.EnableRaisingEvents = false;
                    watched.Dispose();
                }
                Watches.Clear();

                if (string.IsNullOrWhiteSpace(this.WatchConfigStr))
                {
                    return;
                }

                string[] lines = this.WatchConfigStr.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    string[] items = line.Split(',');

                    if (items.Length < 2)
                    {
                        throw new Exception(string.Format("Invalid watch config line: {0}", line));
                    }

                    string source = items[0].Trim();
                    string target = items[1].Trim();

                    var sourceFileInfo = new FileInfo(source);
                    var fileSystemWatcher = new FileSystemWatcher
                    {
                        Path = sourceFileInfo.DirectoryName,
                        Filter = sourceFileInfo.Name,
                        NotifyFilter = NotifyFilters.LastWrite,
                    };
                    Watches.Add(fileSystemWatcher);
                    fileSystemWatcher.Changed += async (s, e) =>
                    {
                        Debug.WriteLine(string.Format("Event 'FileSystemWatcher.Changed' triggered. Source: {0}", source));
                        try
                        {
                            int tryCount = 0;
                            int maxRetries = WatchModeMaxRetries;
                            bool success = false;

                            while (!success && tryCount < maxRetries)
                            {
                                try
                                {
                                    var csf = this.GeneralLoadCsfIniFile(source);
                                    using (var fs = File.Open(target, FileMode.Create))
                                    {
                                        csf.WriteCsfFile(fs);
                                    }
                                    success = true;
                                }
                                catch (IOException)
                                {
                                    if (tryCount < maxRetries - 1)
                                    {
                                        tryCount++;
                                        await Task.Delay(1000);
                                    }
                                    else
                                    {
                                        throw; // Throw IOException if all retries fail
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            this.MessageBoxPanic(ex);
                        }
                    };
                    fileSystemWatcher.EnableRaisingEvents = true;

                }

            }
            catch (Exception ex)
            {
                foreach (var watched in Watches)
                {
                    watched.EnableRaisingEvents = false;
                    watched.Dispose();
                }
                Watches.Clear();

                this.MessageBoxPanic(ex);
            }
        }

        private void Window_Drop(object sender, DragEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
            {
                string[] droppedFilePaths = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                if (droppedFilePaths.Length != 1)
                {
                    throw new Exception("Only one file is allowed for drag & drop.");
                }
                string filename = droppedFilePaths[0];

                this.Convert_CsfFile = this.GeneralLoadCsfIniFile(filename);
                this.UI_FormatConverterTabItem.IsSelected = true;
            }
        });

        private void Window_Content_Rendered(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(this.WatchConfigStr) && !this.StartedFromCsf)
            {
                var result = MessageBox.Show(this, "Watch mode is configured. Do you want to start it?", "Information", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    this.ReInitWatches();
                    this.AdvancedMode = true;
                    this.UI_WatchModeTabItem.IsSelected = true;
                }
            }
        }

        private CsfFile LabelCheck_CsfFile = null;
        private string LabelCheck_CsfFile_FileName = null;
        private void LabelCheck_LoadCsfFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            (this.LabelCheck_CsfFile, this.LabelCheck_CsfFile_FileName) = this.GeneralLoadCsfIniFileGUI();
        });

        private string LabelCheck_MapFolder = null;
        private void LabelCheck_SelectMapFolder_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.LabelCheck_MapFolder = this.OpenFolderGUI();
        });

        private void LabelCheck_SaveIniFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            if (this.LabelCheck_CsfFile == null)
            {
                throw new Exception("Please load a string table file first.");
            }

            if (string.IsNullOrEmpty(this.LabelCheck_MapFolder))
            {
                throw new Exception("Please select the map folder first.");
            }

            if (!Directory.Exists(this.LabelCheck_MapFolder))
            {
                throw new Exception(string.Format("Folder {0} does not exist!", this.LabelCheck_MapFolder));
            }

            // Enumerate all .map/.ypr files
            var mapFiles = Directory.EnumerateFiles(this.LabelCheck_MapFolder, "*.*", SearchOption.AllDirectories)
                .Where(file => file.EndsWith(".map", StringComparison.InvariantCultureIgnoreCase) ||
                               file.EndsWith(".ypr", StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            // Save the labels
            var mapLabels = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            // Treat each map file as an ini file
            foreach (string mapFile in mapFiles)
            {
                try
                {
                    // ini-parser-netstandard
                    IniData mapIni;
                    using (Stream mapFileStream = File.Open(mapFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        mapIni = ParseIni(mapFileStream, GetGeneralIniDataParser());
                    }

                    // 1. For all sections, check if there is a key named UIName. The value of this key is the label name.
                    foreach (var section in mapIni.Sections)
                    {
                        if (section.Keys.ContainsKey("UIName"))
                        {
                            string labelName = section.Keys["UIName"];
                            if (!CsfFile.ValidateLabelName(labelName))
                            {
                                throw new Exception(string.Format("Invalid characters found in label name \"{0}\".", labelName));
                            }
                            _ = mapLabels.Add(labelName);
                        }
                    }

                    // 2. Get the [Actions] section. Iterate all key value pairs. For each key pair, the key is ignored, while the value can be treated as a comma-separated list of strings.
                    // The first element is an integer representing how many actions there.
                    // Then, for each action, there are 8 elements. The first one is an integer representing the action type. We only care about type 11.
                    // The third element is the label name. Rest elements are ignored.

                    if (mapIni.Sections.ContainsSection("Actions"))
                    {
                        var actionsSection = mapIni.Sections["Actions"];
                        foreach (var key in actionsSection)
                        {
                            string[] actionParts = key.Value.Split(',');
                            if (int.TryParse(actionParts[0], out int actionCount) && actionCount > 0)
                            {
                                for (int i = 1; i < actionCount * 8; i += 8)
                                {
                                    if (i + 1 < actionParts.Length && int.TryParse(actionParts[i], out int actionType) && actionType == 11)
                                    {
                                        string labelName = actionParts[i + 2];
                                        if (!CsfFile.ValidateLabelName(labelName))
                                        {
                                            throw new Exception(string.Format("Invalid characters found in label name \"{0}\".", labelName));
                                        }
                                        _ = mapLabels.Add(labelName);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(this, string.Format("Failed to read map file {0}: {1}", mapFile, ex.Message), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    continue;
                }
            }

            var outputFile = this.LabelCheck_CsfFile.Clone() as CsfFile;

            // For each CSF label read from map files, if it's missing in the output file, add it with a placeholder value.
            int missingLabelCount = 0;
            foreach (string labelName in mapLabels)
            {
                if (!CsfFile.ValidateLabelName(labelName))
                {
                    throw new Exception(string.Format("Invalid characters found in label name \"{0}\".", labelName));
                }

                if (!outputFile.Labels.ContainsKey(labelName))
                {
                    _ = outputFile.AddLabel(labelName, this.MissingLabelPlaceholder);
                    ++missingLabelCount;
                }
            }

            _ = MessageBox.Show(this, string.Format("{0} labels are missing.", missingLabelCount), "Result", MessageBoxButton.OK, MessageBoxImage.Information);

            // Okay. Save the file.
            var ini = GetNewIniFileFromCsfFile(outputFile);
            this.GeneralSaveIniFileGUI(ini, this.LabelCheck_CsfFile_FileName);
        });

        private void ConvertCsfFileContentTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null & textBox.SelectionLength > 0)
            {
                textBox.SelectionLength = 0; // Reset selection to prevent lag from large highlights
            }
        }

        private void ConvertCsfFileContentTextBox_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
        {
            // Disable context menu
            e.Handled = true;
        }

        private void SwitchLanguageButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: support more than two languages

            bool isChinese = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.InvariantCultureIgnoreCase);
            if (isChinese)
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            }
            else
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("zh-Hans");
            }

            // Reload the main window to apply the new language
            var oldWindow = Application.Current.MainWindow;
            var newWindow = new MainWindow();
            Application.Current.MainWindow = newWindow;
            newWindow.Show();
            oldWindow.Close();
        }
    }
}
