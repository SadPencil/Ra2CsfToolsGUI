﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;
using Microsoft.Win32;
using SadPencil.Ra2CsfFile;
using MadMilkman.Ini;
using System.Diagnostics;

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
        public string Version { get; } = "v1.0.0-alpha";

        public string TranslationNeededPlaceholder { get; } = "TODO_Translation_Needed";

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
                            csf = CsfFile.LoadFromIniFile(fs);
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
                    file.WriteIniFile(fs);
                }
            }, (defaultExtension == ".csf") ? "Westwood RA2 string table files (*.csf)|*.csf" : "SadPencil.Ra2CsfFile.Ini files (*.ini)|*.ini");

        }

        private void GeneralSaveIniFileGUI(IniFile ini)
        {
            GeneralSaveFileGUI(fs =>
            {
                using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    ini.Save(sw);
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

        private IniFile GetNewIniFileFromCsfFile(CsfFile csf)
        {
            IniFile ini = new IniFile();
            using (var ms = new MemoryStream())
            {
                csf.WriteIniFile(ms);
                using (var msCopy = new MemoryStream(ms.GetBuffer()))
                {
                    using (var sr = new StreamReader(msCopy, new UTF8Encoding(false)))
                    {
                        ini.Load(sr);
                    }
                }

                return ini;
            }
        }


        private static string GetIniLabelValueKeyName(int valueIndex, int lineIndex) => ((valueIndex == 1) ? "Value" : $"Value{valueIndex}") + ((lineIndex == 1) ? String.Empty : $"Line{lineIndex}");

        private IniFile GeneralProceedWithCsfIniLabels(CsfFile csf, Action<IniSection, IniKey, int, int> action)
        {

            var ini = GetNewIniFileFromCsfFile(csf);

            // proceed with ini
            const string INI_FILE_HEADER_SECTION_NAME = "SadPencil.Ra2CsfFile.Ini";
            var labelSections = ini.Sections.Where(section => section.Name != INI_FILE_HEADER_SECTION_NAME);
            foreach (var labelSection in labelSections)
            {
                string labelName = labelSection.Name;
                for (int iValue = 1; ; iValue++)
                {
                    bool valueExist = false;
                    for (int iLine = 1; ; iLine++)
                    {
                        string keyName = GetIniLabelValueKeyName(iValue, iLine);
                        var value = labelSection.Keys.FirstOrDefault(key => key.Name == keyName);

                        if (value == null)
                        {
                            break;
                        }

                        valueExist = true;

                        action.Invoke(labelSection, value, iValue, iLine);
                    }
                    if (!valueExist)
                    {
                        break;
                    }
                }
            }
            return ini;
        }

        private static string GetIniLabelCustomKeyName(string name, int valueIndex, int lineIndex) => ((valueIndex == 1) ? name : $"{name}{valueIndex}") + ((lineIndex == 1) ? String.Empty : $"Line{lineIndex}");

        private void TranslationNew_SaveIniFile_Click(object sender, RoutedEventArgs e)
        {
            GeneralTryCatchGUI(() =>
            {
                if (TranslationNew_File == null)
                {
                    throw new Exception("Please load a string table file first.");
                }

                var ini = GeneralProceedWithCsfIniLabels(TranslationNew_File, (section, value, iValue, iLine) =>
                {
                    _ = section.Keys.Add(GetIniLabelCustomKeyName("Upstream", iValue, iLine), value.Value);
                    value.Value = TranslationNeededPlaceholder;
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

                var upstream = new Dictionary<string, List<(int iValue, int iLine, string value)>>();

                _ = GeneralProceedWithCsfIniLabels(TranslationTile_UpstreamFile, (section, value, iValue, iLine) =>
                 {
                     if (!upstream.ContainsKey(section.Name))
                     {
                         upstream.Add(section.Name, new List<(int iValue, int iLine, string value)>());
                     }
                     upstream[section.Name].Add((iValue, iLine, value.Value));
                 });

                var ini = GetNewIniFileFromCsfFile(TranslationTile_TranslatedFile);
                foreach (var keyValuePair in upstream)
                {
                    var labelName = keyValuePair.Key;
                    IniSection labelSection;
                    bool translationExist = ini.Sections.Contains(labelName);
                    if (translationExist)
                    {
                        labelSection = ini.Sections[labelName];
                    }
                    else
                    {
                        labelSection = ini.Sections.Add(labelName);
                    }

                    foreach ((var iValue, var iLine, var value) in keyValuePair.Value)
                    {
                        _ = labelSection.Keys.Add(GetIniLabelCustomKeyName("Upstream", iValue, iLine), value);
                    }
                    foreach ((var iValue, var iLine, var value) in keyValuePair.Value)
                    {
                        if (!translationExist)
                        {
                            _ = labelSection.Keys.Add(GetIniLabelValueKeyName(iValue, iLine), TranslationNeededPlaceholder);
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

                var diffDict = new Dictionary<string, (List<string> oldValues, List<string> newValues)>();
                var oldDict = TranslationUpdate_OldUpstreamFile.Labels;
                var newDict = TranslationUpdate_NewUpstreamFile.Labels;
                var labelKeys = oldDict.Keys.Union(newDict.Keys);
                foreach (var labelName in oldDict.Keys.Union(newDict.Keys))
                {
                    bool found;
                    found = oldDict.TryGetValue(labelName, out var oldValues);
                    if (!found)
                    {
                        oldValues = new List<string>();
                    }
                    found = newDict.TryGetValue(labelName, out var newValues);
                    if (!found)
                    {
                        newValues = new List<string>();
                    }

                    bool equal = oldValues.SequenceEqual(newValues);
                    if (!equal)
                    {
                        diffDict[labelName] = (oldValues, newValues);
                    }
                }

                // -------

                var upstreamOld = new Dictionary<string, List<(int iValue, int iLine, string value)>>();

                _ = GeneralProceedWithCsfIniLabels(TranslationUpdate_OldUpstreamFile, (section, value, iValue, iLine) =>
                {
                    if (!upstreamOld.ContainsKey(section.Name))
                    {
                        upstreamOld.Add(section.Name, new List<(int iValue, int iLine, string value)>());
                    }
                    upstreamOld[section.Name].Add((iValue, iLine, value.Value));
                });

                // -------

                var upstreamNew = new Dictionary<string, List<(int iValue, int iLine, string value)>>();

                _ = GeneralProceedWithCsfIniLabels(TranslationUpdate_NewUpstreamFile, (section, value, iValue, iLine) =>
                {
                    if (!upstreamNew.ContainsKey(section.Name))
                    {
                        upstreamNew.Add(section.Name, new List<(int iValue, int iLine, string value)>());
                    }
                    upstreamNew[section.Name].Add((iValue, iLine, value.Value));
                });

                // ------- 
                // delete items that needs updates
                var ini = GetNewIniFileFromCsfFile(TranslationUpdate_OldTranslatedFile);
                foreach (var labelName in diffDict.Keys)
                {
                    _ = ini.Sections.Remove(labelName);
                }

                // -------
                // add upstream info to .ini

                foreach (var labelName in labelKeys)
                {
                    IniSection labelSection;
                    bool translationExist = ini.Sections.Contains(labelName);
                    if (translationExist)
                    {
                        labelSection = ini.Sections[labelName];
                    }
                    else
                    {
                        labelSection = ini.Sections.Add(labelName);
                    }

                    bool hasDifference = diffDict.ContainsKey(labelName);

                    if (upstreamOld.ContainsKey(labelName))
                    {
                        if (hasDifference)
                        {
                            foreach ((var iValue, var iLine, var value) in upstreamNew[labelName])
                            {
                                _ = labelSection.Keys.Add(GetIniLabelCustomKeyName("UpstreamOld", iValue, iLine), value);
                            }
                        }
                    }

                    if (upstreamNew.ContainsKey(labelName))
                    {
                        foreach ((var iValue, var iLine, var value) in upstreamNew[labelName])
                        {
                            _ = labelSection.Keys.Add(GetIniLabelCustomKeyName("Upstream", iValue, iLine), value);
                        }
                        foreach ((var iValue, var iLine, var value) in upstreamNew[labelName])
                        {
                            if (!translationExist)
                            {
                                _ = labelSection.Keys.Add(GetIniLabelValueKeyName(iValue, iLine), TranslationNeededPlaceholder);
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
