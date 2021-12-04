using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.IO;
using Microsoft.Win32;
using SadPencil.Ra2CsfFile;
using System.Diagnostics;
using IniParser;
using IniParser.Model;
using IniParser.Model.Configuration;
using IniParser.Parser;

namespace Ra2CsfToolsGUI
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = this;
        }
        public string Version { get; } = "v1.2.0-alpha";

        public string TranslationNeededPlaceholder { get; } = "TODO_Translation_Needed";
        public string TranslationDeleteNeededPlaceholder { get; } = "TODO_Translation_Delete_Needed";


        private static IniParserConfiguration IniParserConfiguration { get; } = new IniParserConfiguration()
        {
            AllowDuplicateKeys = false,
            AllowDuplicateSections = false,
            AllowKeysWithoutSection = false,
            CommentRegex = new System.Text.RegularExpressions.Regex("a^"), // match nothing
            CaseInsensitive = true,
        };

        private static IniData GetIniData() => new IniData() { Configuration = IniParserConfiguration, };

        private static IniData ParseIni(Stream stream)
        {
            var parser = new IniDataParser(IniParserConfiguration);

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

        private CsfFile Convert_CsfFile = null;

        private void Convert_LoadFile_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                Convert_CsfFile = GeneralLoadCsfIniFileGUI();
            });
        }

        private CsfFile GeneralLoadCsfIniFileGUI()
        {
            CsfFile csf = null;
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "String table files (*.csf;*.ini)|*.csf;*.ini|Westwood RA2 string table files (*.csf)|*.csf|SadPencil.Ra2CsfFile.Ini files (*.ini)|*.ini",
            };
            if (openFileDialog.ShowDialog(this).GetValueOrDefault())
            {
                var filename = openFileDialog.FileName;
                var fileext = Path.GetExtension(filename);
                switch (fileext)
                {
                    case ".csf":
                        using (var fs = File.Open(filename, FileMode.Open))
                        {
                            csf = CsfFile.LoadFromCsfFile(fs);
                        };
                        break;
                    case ".ini":
                        using (var fs = File.Open(filename, FileMode.Open))
                        {
                            csf = CsfFileIniHelper.LoadFromIniFile(fs);
                        }
                        break;
                    default:
                        throw new Exception("Unexpected file extension. Only .csf and .ini files are accepted.");
                }
                _ = MessageBox.Show(this, $"File loaded successfully. This string table file contains {csf.Labels.Count} labels, with language {csf.Language}.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                Debug.Assert(csf != null);
                return csf;
            }

            return null;
        }

        private void GeneralSaveFileGUI(Action<Stream> saveAction, string filter)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog()
            {
                Filter = filter,
            };
            if (saveFileDialog.ShowDialog(this).GetValueOrDefault())
            {
                var filename = saveFileDialog.FileName;
                using (var fs = File.Open(filename, FileMode.Create))
                {
                    saveAction.Invoke(fs);
                }
                if (MessageBox.Show(this, "File saved successfully. Would you like to open the file in File Explorer?", "Success", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    Process process = new Process();
                    process.StartInfo.FileName = "explorer.exe";
                    process.StartInfo.Arguments = $"/select, \"{filename}\"";
                    process.Start();
                };
            }
        }

        private void GeneralSaveCsfIniFileGUI(CsfFile file, string defaultExtension = ".ini")
        {
            Debug.Assert(new List<string>() { ".ini", ".csf" }.Contains(defaultExtension));

            if (file == null)
            {
                throw new Exception("Please load a string table file first.");
            }

            GeneralSaveFileGUI(fs =>
            {
                if (defaultExtension == ".csf")
                {
                    file.WriteCsfFile(fs);
                }
                else
                {
                    CsfFileIniHelper.WriteIniFile(file, fs);
                }

            }, (defaultExtension == ".csf") ? "Westwood RA2 string table files (*.csf)|*.csf" : "SadPencil.Ra2CsfFile.Ini files (*.ini)|*.ini");

        }

        private void GeneralSaveIniFileGUI(IniData ini)
        {
            GeneralSaveFileGUI(fs =>
            {
                using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    sw.Write(ini.ToString());
                }
            }, "SadPencil.Ra2CsfFile.Ini files (*.ini)|*.ini");
        }

        private void GeneralTryCatchGUI(Action action)
        {
            try
            {
                action.Invoke();
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(this, ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Convert_SaveAsIni_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                GeneralSaveCsfIniFileGUI(Convert_CsfFile, ".ini");
            });
        }

        private void Convert_SaveAsCsf_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                GeneralSaveCsfIniFileGUI(Convert_CsfFile, ".csf");
            });
        }

        private CsfFile TranslationNew_File = null;

        private void TranslationNew_LoadFile_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                TranslationNew_File = GeneralLoadCsfIniFileGUI();
            });
        }

        private static IniData GetNewIniFileFromCsfFile(CsfFile csf)
        {
            using (var ms = new MemoryStream())
            {
                CsfFileIniHelper.WriteIniFile(csf, ms);
                using (var msCopy = new MemoryStream(ms.GetBuffer()))
                {
                    return ParseIni(msCopy);
                }
            }
        }


        private static string GetIniLabelValueKeyName(int lineIndex) => "Value" + ((lineIndex == 1) ? String.Empty : $"Line{lineIndex}");

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

        private static string GetIniLabelCustomKeyName(string name, int lineIndex) => name + ((lineIndex == 1) ? String.Empty : $"Line{lineIndex}");

        private void TranslationNew_SaveIniFile_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                if (TranslationNew_File == null)
                {
                    throw new Exception("Please load a string table file first.");
                }

                var upstream = LoadIniValuesFromCsfFile(TranslationNew_File);
                var ini = GeneralProceedWithCsfIniLabels(TranslationNew_File, null, (labelName, key) =>
                {
                    foreach ((var iLine, var value) in upstream[labelName])
                    {
                        _ = key.AddKey(GetIniLabelCustomKeyName("Upstream", iLine), value);
                        Debug.Assert(key.ContainsKey(GetIniLabelValueKeyName(iLine)));
                        key[GetIniLabelValueKeyName(iLine)] = TranslationNeededPlaceholder;
                    }
                });

                // save ini file
                GeneralSaveIniFileGUI(ini);
            });
        }

        private CsfFile TranslationTile_UpstreamFile = null;
        private CsfFile TranslationTile_TranslatedFile = null;
        private void TranslationTile_LoadUpstreamFile_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                TranslationTile_UpstreamFile = GeneralLoadCsfIniFileGUI();
            });
        }

        private void TranslationTile_LoadTranslatedFile_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                TranslationTile_TranslatedFile = GeneralLoadCsfIniFileGUI();
            });
        }

        private void TranslationTile_SaveIniFile_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                if (TranslationTile_UpstreamFile == null || TranslationTile_TranslatedFile == null)
                {
                    throw new Exception("Please load the string table files first.");
                }

                var upstream = LoadIniValuesFromCsfFile(TranslationTile_UpstreamFile);

                var ini = GetNewIniFileFromCsfFile(TranslationTile_TranslatedFile);
                foreach (var keyValuePair in upstream)
                {
                    var labelName = keyValuePair.Key;
                    bool translationExist = ini.Sections.ContainsSection(labelName);
                    if (!translationExist)
                    {
                        _ = ini.Sections.AddSection(labelName);
                    }

                    var labelSection = ini.Sections[labelName];

                    foreach ((var iLine, var value) in keyValuePair.Value)
                    {
                        _ = labelSection.AddKey(GetIniLabelCustomKeyName("Upstream", iLine), value);
                    }
                    foreach ((var iLine, var value) in keyValuePair.Value)
                    {
                        if (!translationExist)
                        {
                            _ = labelSection.AddKey(GetIniLabelValueKeyName(iLine), TranslationNeededPlaceholder);
                        }
                    }
                }

                // TODO: for those keys exist in translated files but not in the upstream files, mark them up

                // save ini file
                GeneralSaveIniFileGUI(ini);
            });
        }

        private CsfFile TranslationUpdate_OldUpstreamFile = null;
        private CsfFile TranslationUpdate_NewUpstreamFile = null;
        private CsfFile TranslationUpdate_OldTranslatedFile = null;
        private void TranslationUpdate_LoadOldUpstreamFile_Click(object sender, RoutedEventArgs e)
        {

            GeneralTryCatchGUI(() =>
            {
                TranslationUpdate_OldUpstreamFile = GeneralLoadCsfIniFileGUI();
            });
        }

        private void TranslationUpdate_LoadNewUpstreamFile_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                TranslationUpdate_NewUpstreamFile = GeneralLoadCsfIniFileGUI();
            });

        }

        private void TranslationUpdate_LoadOldTranslatedFile_Click(object sender, RoutedEventArgs e)
        {

            GeneralTryCatchGUI(() =>
            {
                TranslationUpdate_OldTranslatedFile = GeneralLoadCsfIniFileGUI();
            });
        }

        private void TranslationUpdate_SaveIniFile_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                if (TranslationUpdate_OldUpstreamFile == null || TranslationUpdate_NewUpstreamFile == null || TranslationUpdate_OldTranslatedFile == null)
                {
                    throw new Exception("Please load the string table files first.");
                }

                var diffDict = new Dictionary<string, (string oldValue, string newValue)>();
                var oldDict = TranslationUpdate_OldUpstreamFile.Labels;
                var newDict = TranslationUpdate_NewUpstreamFile.Labels;
                var labelKeys = oldDict.Keys.Union(newDict.Keys);
                foreach (var labelName in labelKeys)
                {
                    bool found;
                    found = oldDict.TryGetValue(labelName, out var oldValue);
                    if (!found)
                    {
                        oldValue = string.Empty;
                    }
                    found = newDict.TryGetValue(labelName, out var newValue);
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

                var upstreamOld = LoadIniValuesFromCsfFile(TranslationUpdate_OldUpstreamFile);
                var upstreamNew = LoadIniValuesFromCsfFile(TranslationUpdate_NewUpstreamFile);

                // ------- 
                // delete items that needs updates
                var ini = GetNewIniFileFromCsfFile(TranslationUpdate_OldTranslatedFile);
                foreach (var labelName in diffDict.Keys)
                {
                    _ = ini.Sections.RemoveSection(labelName);
                }

                // -------
                // add upstream info to .ini

                foreach (var labelName in labelKeys)
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
                            foreach ((var iLine, var value) in upstreamOld[labelName])
                            {
                                _ = labelSection.AddKey(GetIniLabelCustomKeyName("UpstreamOld", iLine), value);
                            }
                        }
                    }

                    if (upstreamNew.ContainsKey(labelName))
                    {
                        foreach ((var iLine, var value) in upstreamNew[labelName])
                        {
                            _ = labelSection.AddKey(GetIniLabelCustomKeyName("UpstreamNew", iLine), value);
                        }
                        foreach ((var iLine, var value) in upstreamNew[labelName])
                        {
                            if (!translationExist)
                            {
                                _ = labelSection.AddKey(GetIniLabelValueKeyName(iLine), TranslationNeededPlaceholder);
                            }
                        }
                        Debug.Assert(labelSection.ContainsKey(GetIniLabelValueKeyName(1)));
                    }


                }

                // TODO: for those keys exist in translated files but not in the upstream files, mark them up

                // save ini
                GeneralSaveIniFileGUI(ini);
            });
        }

        private CsfFile TranslationOverride_UpstreamFile = null;
        private CsfFile TranslationOverride_TranslatedFile = null;

        private void TranslationOverride_LoadUpstreamFile_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                TranslationOverride_UpstreamFile = GeneralLoadCsfIniFileGUI();
            });
        }

        private void TranslationOverride_LoadTranslatedFile_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                TranslationOverride_TranslatedFile = GeneralLoadCsfIniFileGUI();
            });
        }

        private void TranslationOverride_SaveIniFile_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                if (TranslationOverride_UpstreamFile == null || TranslationOverride_TranslatedFile == null)
                {
                    throw new Exception("Please load the string table files first.");
                }

                var oldDict = TranslationOverride_UpstreamFile.Labels;
                var newDict = TranslationOverride_TranslatedFile.Labels;
                var labelKeys = oldDict.Keys.Union(newDict.Keys);

                var newCsf = new CsfFile()
                {
                    Language = TranslationOverride_TranslatedFile.Language,
                    Version = TranslationOverride_TranslatedFile.Version,
                };


                foreach (var labelName in labelKeys)
                {
                    bool found;
                    string value;
                    found = newDict.TryGetValue(labelName, out var newValue);
                    if (found)
                    {
                        value = newValue;
                    }
                    else
                    {
                        found = oldDict.TryGetValue(labelName, out var oldValue);
                        Debug.Assert(found);
                        value = oldValue;
                    }

                    newCsf.AddLabel(labelName, value);
                }

                // save ini
                GeneralSaveCsfIniFileGUI(newCsf, ".ini");
            });
        }

        private CsfFile TranslationUpdateCheck_OldUpstreamFile = null;
        private CsfFile TranslationUpdateCheck_NewUpstreamFile = null;
        private CsfFile TranslationUpdateCheck_OldTranslatedFile = null;
        private CsfFile TranslationUpdateCheck_NewTranslatedFile = null;

        private void TranslationUpdateCheck_LoadOldUpstreamFile_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                TranslationUpdateCheck_OldUpstreamFile = GeneralLoadCsfIniFileGUI();
            });

        }

        private void TranslationUpdateCheck_LoadNewUpstreamFile_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                TranslationUpdateCheck_NewUpstreamFile = GeneralLoadCsfIniFileGUI();
            });
        }

        private void TranslationUpdateCheck_LoadOldTranslatedFile_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                TranslationUpdateCheck_OldTranslatedFile = GeneralLoadCsfIniFileGUI();
            });
        }

        private void TranslationUpdateCheck_LoadNewTranslatedFile_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                TranslationUpdateCheck_NewTranslatedFile = GeneralLoadCsfIniFileGUI();
            });
        }

        private void TranslationUpdateCheck_SaveIniFile_Click(object sender, RoutedEventArgs e)
        {

            GeneralTryCatchGUI(() =>
            {
                if (TranslationUpdateCheck_OldUpstreamFile == null ||
                TranslationUpdateCheck_NewUpstreamFile == null ||
                TranslationUpdateCheck_OldTranslatedFile == null ||
                TranslationUpdateCheck_NewTranslatedFile == null)
                {
                    throw new Exception("Please load the string table files first.");
                }


                var diffDict = new Dictionary<string, (string oldValue, string newValue)>();
                var oldUpstreamDict = TranslationUpdateCheck_OldUpstreamFile.Labels;
                var newUpstreamDict = TranslationUpdateCheck_NewUpstreamFile.Labels;
                var upstreamLabelKeys = oldUpstreamDict.Keys.Union(newUpstreamDict.Keys);
                foreach (var labelName in upstreamLabelKeys)
                {
                    bool found;
                    found = oldUpstreamDict.TryGetValue(labelName, out var oldValue);
                    if (!found)
                    {
                        oldValue = string.Empty;
                    }
                    found = newUpstreamDict.TryGetValue(labelName, out var newValue);
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

                var oldTransDict = TranslationUpdateCheck_OldTranslatedFile.Labels;
                var newTransDict = TranslationUpdateCheck_NewTranslatedFile.Labels;
                var transLabelKeys = oldTransDict.Keys.Union(newTransDict.Keys);
                var allLabelKeys = transLabelKeys.Union(upstreamLabelKeys);

                var newCsf = new CsfFile()
                {
                    Language = TranslationUpdateCheck_NewTranslatedFile.Language,
                    Version = TranslationUpdateCheck_NewTranslatedFile.Version,
                };

                foreach (var labelName in allLabelKeys)
                {
                    bool oldTransExist = oldTransDict.TryGetValue(labelName, out var oldTransValue);
                    bool newTransExist = newTransDict.TryGetValue(labelName, out var newTransValue);
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
                                value = TranslationNeededPlaceholder;
                            }
                            else
                            {
                                if ((oldTransExist && oldTransValue != newTransValue) || (!oldTransExist))
                                {
                                    value = newTransValue;
                                }
                                else
                                {
                                    value = TranslationNeededPlaceholder;
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
                                value = TranslationNeededPlaceholder;
                            }
                        }

                    }
                    else
                    {
                        if (newTransExist)
                        {
                            value = TranslationDeleteNeededPlaceholder;
                        }
                        else
                        {
                            value = null;
                            skipLabel = true;
                        }
                    }

                    if (!skipLabel)
                    {
                        newCsf.AddLabel(labelName, value);
                    }
                }


                // -------

                var upstreamOld = LoadIniValuesFromCsfFile(TranslationUpdateCheck_OldUpstreamFile);
                var upstreamNew = LoadIniValuesFromCsfFile(TranslationUpdateCheck_NewUpstreamFile);
                var transOld = LoadIniValuesFromCsfFile(TranslationUpdateCheck_OldTranslatedFile);
                var transNew = LoadIniValuesFromCsfFile(TranslationUpdateCheck_NewTranslatedFile);

                // -------
                // add upstream info to .ini
                var ini = GetNewIniFileFromCsfFile(newCsf);

                foreach (var labelName in upstreamLabelKeys)
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
                            foreach ((var iLine, var value) in upstreamOld[labelName])
                            {
                                _ = labelSection.AddKey(GetIniLabelCustomKeyName("UpstreamOld", iLine), value);
                            }
                        }
                    }

                    if (upstreamNew.ContainsKey(labelName))
                    {
                        foreach ((var iLine, var value) in upstreamNew[labelName])
                        {
                            _ = labelSection.AddKey(GetIniLabelCustomKeyName("UpstreamNew", iLine), value);
                        }
                    }

                    if (diffDict.ContainsKey(labelName))
                    {
                        bool oldTransExist = oldTransDict.TryGetValue(labelName, out var oldTransValue);
                        bool newTransExist = newTransDict.TryGetValue(labelName, out var newTransValue);

                        if (newUpstreamDict.Keys.Contains(labelName))
                        {
                            if ((!newTransExist) || (!(((oldTransExist && oldTransValue != newTransValue) || (!oldTransExist)))))
                            {
                                Debug.Assert(newCsf.Labels.ContainsKey(labelName));
                                Debug.Assert(newCsf.Labels[labelName] == TranslationNeededPlaceholder);

                                if (oldTransExist)
                                {
                                    Debug.Assert(transOld.ContainsKey(labelName));
                                    foreach ((var iLine, var value) in transOld[labelName])
                                    {
                                        _ = labelSection.AddKey(GetIniLabelCustomKeyName("TranslationOld", iLine), value);
                                    }
                                }

                                if (newTransExist)
                                {
                                    foreach ((var iLine, var value) in transNew[labelName])
                                    {
                                        _ = labelSection.AddKey(GetIniLabelCustomKeyName("TranslationNew", iLine), value);
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (newTransExist)
                            {
                                Debug.Assert(newCsf.Labels.ContainsKey(labelName));
                                Debug.Assert(newCsf.Labels[labelName] == TranslationDeleteNeededPlaceholder);

                                Debug.Assert(transNew.ContainsKey(labelName));
                                foreach ((var iLine, var value) in transNew[labelName])
                                {
                                    _ = labelSection.AddKey(GetIniLabelCustomKeyName("TranslationNew", iLine), value);
                                }
                            }
                        }

                    }
                }

                // TODO: for those keys exist in translated files but not in the upstream files, mark them up

                // save ini
                GeneralSaveIniFileGUI(ini);

            });
        }
    }
}
