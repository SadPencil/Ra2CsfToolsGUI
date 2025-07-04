using IniParser.Model;
using IniParser.Model.Configuration;
using IniParser.Parser;
using Microsoft.Win32;
using Ra2CsfToolsGUI.Util;
using SadPencil.Ra2CsfFile;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace Ra2CsfToolsGUI
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            this.InitializeComponent();

            this.DataContext = this;

            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Length >= 2)
            {
                string filename = arguments[1];
                this.GeneralTryCatchGUI(() =>
                {
                    this.Convert_CsfFile = this.GeneralLoadCsfIniFile(filename);
                    this.UI_FormatConverterTabItem.IsSelected = true;
                });
            }

            this.WatchConfigStr = LoadWatchConfig();
            this.ReInitWatches();
          
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public string Version { get; } = "v1.3.1";
        public string ApplicationName { get; } = "Ra2CsfToolsGUI";
        public string WindowTitle { get; } = "Ra2CsfToolsGUI (by SadPencil)";

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
        private CsfFileOptions GetCsfFileOptions() => new CsfFileOptions()
        {
            Encoding1252ReadWorkaround = Encoding1252ReadWorkaround,
            Encoding1252WriteWorkaround = Encoding1252WriteWorkaround,
        };

        private static IniParserConfiguration IniParserConfiguration { get; } = new IniParserConfiguration()
        {
            AllowDuplicateKeys = false,
            AllowDuplicateSections = false,
            AllowKeysWithoutSection = false,
            CommentRegex = new Regex("a^"), // match nothing
            CaseInsensitive = true,
            AssigmentSpacer = string.Empty,
            SectionRegex = new Regex("^(\\s*?)\\[{1}\\s*[\\p{L}\\p{P}\\p{M}_\\\"\\'\\{\\}\\#\\+\\;\\*\\%\\(\\)\\=\\?\\&\\$\\^\\<\\>\\`\\^|\\,\\:\\/\\.\\-\\w\\d\\s\\\\\\~]+\\s*\\](\\s*?)$"),
        };

        private static IniDataParser GetIniDataParser() => new IniDataParser(IniParserConfiguration);

        private static IniData GetIniData() => new IniData() { Configuration = IniParserConfiguration, };

        private void MessageBoxPanic(Exception ex) => _ = MessageBox.Show(this, ex.Message, $"Error - {this.ApplicationName}", MessageBoxButton.OK, MessageBoxImage.Error);

        private static IniData ParseIni(Stream stream)
        {
            var parser = GetIniDataParser();

            using (var sr = new StreamReader(stream, new UTF8Encoding(false)))
            {
                return parser.Parse(sr.ReadToEnd());
            }
        }

        private static Dictionary<string, List<(int iLine, string value)>> LoadIniValuesFromCsfFile(CsfFile csf)
        {
            var dict = new Dictionary<string, List<(int iLine, string value)>>();

            _ = GeneralProceedWithCsfIniLabels(csf, (sectionName, keyName, value, iLine) =>
            {
                if (!dict.ContainsKey(sectionName))
                {
                    dict.Add(sectionName, new List<(int iLine, string value)>());
                }
                dict[sectionName].Add((iLine, value));
            });

            return dict;
        }

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
                    this.Convert_CsfFile_Tips = $"This string table file contains {value.Labels.Count} labels, with language {value.Language}.";
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
            this.Convert_CsfFile = this.GeneralLoadCsfIniFileGUI();
        });

        private CsfFile GeneralLoadCsfIniFile(string filepath)
        {
            string fileext = Path.GetExtension(filepath);
            switch (fileext)
            {
                case ".csf":
                    using (var fs = File.Open(filepath, FileMode.Open))
                    {
                        return CsfFile.LoadFromCsfFile(fs, this.GetCsfFileOptions());
                    };
                // break;
                case ".ini":
                    using (var fs = File.Open(filepath, FileMode.Open))
                    {
                        return CsfFileIniHelper.LoadFromIniFile(fs, this.GetCsfFileOptions());
                    }
                case ".yaml":
                    using (var fs = File.Open(filepath, FileMode.Open))
                    {
                        return CsfFileExtension.LoadFromYamlFile(fs, this.GetCsfFileOptions());
                    }
                // break;
                default:
                    throw new Exception("Unexpected file extension. Only .csf , .ini and .yaml files are accepted.");
            }
        }

        private CsfFile GeneralLoadCsfIniFileGUI()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "String table files (*.csf;*.ini;*.yaml)|*.csf;*.ini;*.yaml|Westwood RA2 string table files (*.csf)|*.csf|SadPencil.Ra2CsfFile.Ini files (*.ini)|*.ini|SadPencil.Ra2CsfFile.Yaml files (*.yaml)|*.yaml",
            };
            if (openFileDialog.ShowDialog(this).GetValueOrDefault())
            {
                string filename = openFileDialog.FileName;
                var csf = this.GeneralLoadCsfIniFile(filename);
                _ = MessageBox.Show(this, $"File loaded successfully. This string table file contains {csf.Labels.Count} labels, with language {csf.Language}.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                Debug.Assert(csf != null);
                return csf;
            }

            return null;
        }

        private void GeneralSaveFileGUI(Action<Stream> saveAction, string filter)
        {
            var saveFileDialog = new SaveFileDialog()
            {
                Filter = filter,
            };
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
                    process.StartInfo.Arguments = $"/select, \"{filename}\"";
                    _ = process.Start();
                };
            }
        }

        private void GeneralSaveCsfIniFileGUI(CsfFile file, string defaultExtension = ".ini")
        {
            Debug.Assert(new List<string>() { ".ini", ".csf" , ".yaml" }.Contains(defaultExtension));

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
                else if(defaultExtension == ".ini")
                {
                    CsfFileIniHelper.WriteIniFile(file, fs);
                }else if(defaultExtension == ".yaml")
                {
                    file.WriteYamlFile(fs);
                }

            }, defaultExtension switch
            {
                ".csf" => "Westwood RA2 string table files (*.csf)|*.csf",
                ".ini" => "SadPencil.Ra2CsfFile.Ini files (*.ini)|*.ini",
                ".yaml" => "SadPencil.Ra2CsfFile.Yaml files (*.yaml)|*.yaml",
                _ => throw new Exception("Unexpected file extension."),
            });
        }

       

        private void GeneralSaveIniFileGUI(IniData ini) => this.GeneralSaveFileGUI(fs =>
        {
            using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
            {
                sw.Write(ini.ToString());
            }
        }, "SadPencil.Ra2CsfFile.Ini files (*.ini)|*.ini");

        private void GeneralTryCatchGUI(Action action)
        {
            try
            {
                action.Invoke();
            }
            catch (Exception ex)
            {
                this.MessageBoxPanic(ex);
            }
        }

        private void Convert_SaveAsIni_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.GeneralSaveCsfIniFileGUI(this.Convert_CsfFile, ".ini");
        });

        private void Convert_SaveAsYaml_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.GeneralSaveCsfIniFileGUI(this.Convert_CsfFile, ".yaml");
        });

        private void Convert_SaveAsCsf_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.GeneralSaveCsfIniFileGUI(this.Convert_CsfFile, ".csf");
        });

        private CsfFile TranslationNew_File = null;

        private void TranslationNew_LoadFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.TranslationNew_File = this.GeneralLoadCsfIniFileGUI();
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
                    return ParseIni(msCopy);
                }
            }
        }

        private static string GetIniLabelValueKeyName(int lineIndex) => "Value" + ((lineIndex == 1) ? string.Empty : $"Line{lineIndex}");

        private static IniData GeneralProceedWithCsfIniLabels(CsfFile csf, Action<string, string, string, int> valueAction = null, Action<string, KeyDataCollection> sectionAction = null)
        {

            var ini = GetNewIniFileFromCsfFile(csf);

            // proceed with ini
            const string INI_FILE_HEADER_SECTION_NAME = "SadPencil.Ra2CsfFile.Ini";
            // load all labels
            var labelSections = new Dictionary<string, KeyDataCollection>();
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

        private static string GetIniLabelCustomKeyName(string name, int lineIndex) => name + ((lineIndex == 1) ? string.Empty : $"Line{lineIndex.ToString(CultureInfo.InvariantCulture)}");

        private void TranslationNew_SaveIniFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            if (this.TranslationNew_File == null)
            {
                throw new Exception("Please load a string table file first.");
            }

            var upstream = LoadIniValuesFromCsfFile(this.TranslationNew_File);
            var ini = GeneralProceedWithCsfIniLabels(this.TranslationNew_File, null, (labelName, key) =>
            {
                foreach ((int iLine, string value) in upstream[labelName])
                {
                    _ = key.AddKey(GetIniLabelCustomKeyName("Upstream", iLine), value);
                    Debug.Assert(key.ContainsKey(GetIniLabelValueKeyName(iLine)));
                    key[GetIniLabelValueKeyName(iLine)] = this.TranslationNeededPlaceholder;
                }
            });

            // save ini file
            this.GeneralSaveIniFileGUI(ini);
        });

        private CsfFile TranslationTile_UpstreamFile = null;
        private CsfFile TranslationTile_TranslatedFile = null;
        private void TranslationTile_LoadUpstreamFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.TranslationTile_UpstreamFile = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationTile_LoadTranslatedFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.TranslationTile_TranslatedFile = this.GeneralLoadCsfIniFileGUI();
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
            this.GeneralSaveIniFileGUI(ini);
        });

        private CsfFile TranslationUpdate_OldUpstreamFile = null;
        private CsfFile TranslationUpdate_NewUpstreamFile = null;
        private CsfFile TranslationUpdate_OldTranslatedFile = null;
        private void TranslationUpdate_LoadOldUpstreamFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.TranslationUpdate_OldUpstreamFile = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationUpdate_LoadNewUpstreamFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.TranslationUpdate_NewUpstreamFile = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationUpdate_LoadOldTranslatedFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.TranslationUpdate_OldTranslatedFile = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationUpdate_SaveIniFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            if (this.TranslationUpdate_OldUpstreamFile == null || this.TranslationUpdate_NewUpstreamFile == null || this.TranslationUpdate_OldTranslatedFile == null)
            {
                throw new Exception("Please load the string table files first.");
            }

            var diffDict = new Dictionary<string, (string oldValue, string newValue)>();
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
            this.GeneralSaveIniFileGUI(ini);
        });

        private CsfFile TranslationOverride_UpstreamFile = null;
        private CsfFile TranslationOverride_TranslatedFile = null;

        private void TranslationOverride_LoadUpstreamFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.TranslationOverride_UpstreamFile = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationOverride_LoadTranslatedFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.TranslationOverride_TranslatedFile = this.GeneralLoadCsfIniFileGUI();
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
            this.GeneralSaveCsfIniFileGUI(newCsf, ".ini");
        });

        private CsfFile TranslationUpdateCheck_OldUpstreamFile = null;
        private CsfFile TranslationUpdateCheck_NewUpstreamFile = null;
        private CsfFile TranslationUpdateCheck_OldTranslatedFile = null;
        private CsfFile TranslationUpdateCheck_NewTranslatedFile = null;

        private void TranslationUpdateCheck_LoadOldUpstreamFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.TranslationUpdateCheck_OldUpstreamFile = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationUpdateCheck_LoadNewUpstreamFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.TranslationUpdateCheck_NewUpstreamFile = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationUpdateCheck_LoadOldTranslatedFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.TranslationUpdateCheck_OldTranslatedFile = this.GeneralLoadCsfIniFileGUI();
        });

        private void TranslationUpdateCheck_LoadNewTranslatedFile_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            this.TranslationUpdateCheck_NewTranslatedFile = this.GeneralLoadCsfIniFileGUI();
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

            var diffDict = new Dictionary<string, (string oldValue, string newValue)>();
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
                            if ((oldTransExist && oldTransValue != newTransValue) || (!oldTransExist))
                            {
                                value = newTransValue;
                            }
                            else
                            {
                                value = this.TranslationNeededPlaceholder;
                            }
                        }
                    }
                    else
                    {
                        if (newTransDict.ContainsKey(labelName))
                        {
                            value = newTransDict[labelName];
                        }
                        else
                        {
                            value = this.TranslationNeededPlaceholder;
                        }
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
            this.GeneralSaveIniFileGUI(ini);

        });

        public string WatchConfigStr { get; set; }

        private static List<FileSystemWatcher> Watches { get; set; } = new List<FileSystemWatcher>();

        private void WatchMode_Confirm_Click(object sender, RoutedEventArgs e) => this.GeneralTryCatchGUI(() =>
        {
            foreach(var watcher in Watches)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            SaveWatchConfig(WatchConfigStr);

            ReInitWatches();

            _ = MessageBox.Show(this, $"Your changes have been saved successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

        });

        private void SaveWatchConfig(string configStr)
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                $"SadPencil{Path.DirectorySeparatorChar}Ra2CsfToolsGUI");

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            using (StreamWriter sw = new StreamWriter(Path.Combine(appDataPath, "watch_mode_config.dat"), false))
            {
                sw.Write(configStr);
            }
        }

        private string LoadWatchConfig()
        {
            string appDataPath = Path.Combine(
              Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
              $"SadPencil{Path.DirectorySeparatorChar}Ra2CsfToolsGUI");

            string savedPath = Path.Combine(appDataPath, "watch_mode_config.dat");

            if (File.Exists(savedPath))
            {
                using (StreamReader sr = new StreamReader(savedPath))
                {
                    return sr.ReadToEnd();
                } 
            }

            return string.Empty;
        }

        private void ReInitWatches()
        {
            Watches.Clear();

            if (string.IsNullOrWhiteSpace(WatchConfigStr))
                return;

            var lines = WatchConfigStr.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var items = line.Split(',');

                if (items.Length < 2)
                    throw new Exception($"Invalid watch config line: {line}");

                var source = items[0].Trim();
                var target = items[1].Trim();

                FileInfo sourceFileInfo = new FileInfo(source);
                var fname = sourceFileInfo.Name;
                var dirName = sourceFileInfo.DirectoryName;
                FileSystemWatcher fileSystemWatcher = new FileSystemWatcher
                {
                    Path = dirName,
                    Filter = fname,
                    NotifyFilter = NotifyFilters.LastWrite,
                };
                Watches.Add(fileSystemWatcher);
                fileSystemWatcher.Changed += (s, e) =>
                {
                    try
                    {
                        var csf = GeneralLoadCsfIniFile(source);
                        using (var fs = File.Open(target, FileMode.Create))
                        {
                            csf.WriteCsfFile(fs);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBoxPanic(ex);
                    }
                };
                fileSystemWatcher.EnableRaisingEvents = true;

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
    }
}
