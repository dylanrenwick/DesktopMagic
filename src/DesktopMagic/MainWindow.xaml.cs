﻿using DesktopMagic.Dialogs;
using DesktopMagic.Helpers;
using DesktopMagic.Plugins;

using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopMagic
{
    public partial class MainWindow : Window
    {
        #region Global settings

        internal static Theme Theme = new Theme();
        public static bool EditMode { get; protected set; } = false;

        #endregion Global settings

        #region Music Visualizer Settings

        public static int SpectrumMode { get; protected set; } = 0;
        public static int AmplifierLevel { get; protected set; } = 0;
        public static bool MirrorMode { get; protected set; } = false;
        public static bool LineMode { get; protected set; } = false;
        public static System.Drawing.Color? MusicVisualzerColor { get; protected set; }

        #endregion Music Visualizer Settings

        #region Plugins settings

        internal static Dictionary<string, List<SettingElement>> PluginsSettings { get; set; } = new Dictionary<string, List<SettingElement>>();

        #endregion Plugins settings

        public static List<Window> Windows { get; protected set; } = new List<Window>();
        public static List<string> WindowNames { get; protected set; } = new List<string>();
        private readonly RegistryKey key;
        private readonly System.Windows.Forms.NotifyIcon notifyIcon = new();

        private bool loaded = false;

        private bool blockWindowsClosing = true;

        public MainWindow()
        {
            try
            {
                key = Registry.CurrentUser.CreateSubKey(@"Software\" + App.AppName);

                Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/DesktopMagic;component/icon.ico")).Stream;
                notifyIcon.Click += TaskbarIcon_TrayLeftClick;
                notifyIcon.Visible = true;
                notifyIcon.Text = App.AppName;
                notifyIcon.Icon = new System.Drawing.Icon(iconStream);
                notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();

                InitializeComponent();

                SetLanguageDictionary();

#if DEBUG
                Title = $"{App.AppName} - Dev {Assembly.GetExecutingAssembly().GetName().Version}";
#else
                Title = $"{App.AppName} - {Assembly.GetExecutingAssembly().GetName().Version}";
#endif
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.ToString());
            }
        }

        #region load

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(App.ApplicationDataPath))
                {
                    _ = Directory.CreateDirectory(App.ApplicationDataPath);
                }
                App.Logger.Log("Created ApplicationData Folder", "Main");

                if (!Directory.Exists(App.ApplicationDataPath + "\\Plugins"))
                {
                    _ = Directory.CreateDirectory(App.ApplicationDataPath + "\\Plugins");
                }
                App.Logger.Log("Created Plugins Folder", "Main");

                if (!File.Exists(App.ApplicationDataPath + "\\layouts.save"))
                {
                    File.WriteAllText(App.ApplicationDataPath + "\\layouts.save", ";" + (string)FindResource("default"));
                }
                App.Logger.Log("Created layouts.save file", "Main");

                _ = optionsComboBox.Items.Add(new Tuple<string, int>((string)FindResource("musicVisualizer"), 0));

                //Write To Log File and Load Elements

                App.Logger.Log("Loading Plugin names", "Main");
                LoadPlugins();
                App.Logger.Log("Loading Layout names", "Main");
                LoadLayoutNames();
                App.Logger.Log("Loading Layout", "Main");
                LoadLayout();

                loaded = true;
                App.Logger.Log("Window Loaded", "Main");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void LoadPlugins()
        {
            string PluginsPath = App.ApplicationDataPath + "\\Plugins";

            foreach (string fileName in Directory.GetFiles(PluginsPath, "*.dll"))
            {
                string PluginName = fileName[(fileName.LastIndexOf("\\", StringComparison.InvariantCulture) + 1)..].Replace(fileName[fileName.LastIndexOf(".", StringComparison.InvariantCulture)..], "");
                try
                {
                    _ = Directory.CreateDirectory(PluginsPath + "\\" + PluginName);
                    File.Move(fileName, $"{PluginsPath}\\{PluginName}\\{PluginName}.dll");
                }
                catch { }
            }

            foreach (string directory in Directory.GetDirectories(PluginsPath))
            {
                foreach (string fileName in Directory.GetFiles(directory).Where(s => s.EndsWith(".dll", StringComparison.InvariantCulture) || s.EndsWith(".cs", StringComparison.InvariantCulture)))
                {
                    string badChars = ",#-<>?!=()*,. ";
                    string PluginName = fileName[(fileName.LastIndexOf("\\", StringComparison.InvariantCulture) + 1)..].Replace(fileName[fileName.LastIndexOf(".", StringComparison.InvariantCulture)..], "");
                    string clearPluginName = PluginName;

                    if (PluginName == directory[(directory.LastIndexOf("\\", StringComparison.InvariantCulture) + 1)..])
                    {
                        foreach (char c in badChars)
                        {
                            clearPluginName = clearPluginName.Replace(c, '_');
                        }

                        CheckBox checkBox = new()
                        {
                            Name = "_PluginCb_" + clearPluginName,
                            Content = PluginName
                        };
                        checkBox.Style = (Style)FindResource("MaterialDesignDarkCheckBox");
                        checkBox.Click += CheckBox_Click;

                        bool exists = false;
                        foreach (UIElement item in stackPanel.Children)
                        {
                            if (((CheckBox)item).Name == "_PluginCb_" + clearPluginName)
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                        {
                            _ = stackPanel.Children.Add(checkBox);
                        }
                    }
                }
            }
        }

        #endregion load

        #region Windows

        private void EditCheckBox_Click(object sender, RoutedEventArgs e)
        {
            EditMode = (bool)EditCheckBox.IsChecked;
            SaveLayout();
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            Window window;
            blockWindowsClosing = false;

            switch (checkBox.Name)
            {
                case "TimeCb":
                    window = new TimeWindow();
                    break;

                case "DateCb":
                    window = new DateWindow();
                    break;

                case "CpuUsageCb":
                    window = new CpuUsageWindow();
                    break;

                case "MusicVisualizerCb":
                    window = new MusicVisualizerWindow();
                    break;

                default:
                    if (checkBox.Name.Contains("_PluginCb_"))
                    {
                        window = new PluginWindow(checkBox.Content.ToString())
                        {
                            Title = checkBox.Content.ToString()
                        };
                        break;
                    }
                    else
                    {
                        return;
                    }
            }

            if (!WindowNames.Contains(window.Title))
            {
                _ = Task.Run(() =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (window is PluginWindow pluginWindow)
                        {
                            Action onPluginLoaded = null;
                            onPluginLoaded = () =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    if (!optionsComboBox.Items.Contains(new Tuple<string, int>(checkBox.Content.ToString(), 0)))
                                    {
                                        _ = optionsComboBox.Items.Add(new Tuple<string, int>(checkBox.Content.ToString(), 0));
                                    }
                                    optionsComboBox.SelectedIndex = -1;
                                    optionsComboBox.SelectedIndex = optionsComboBox.Items.IndexOf(new Tuple<string, int>(checkBox.Content.ToString(), 0));
                                    pluginWindow.PluginLoaded -= onPluginLoaded;
                                });
                            };
                            pluginWindow.OnExit += () =>
                            {
                                checkBox.IsChecked = false;
                                CheckBox_Click(checkBox, null);
                            };
                            pluginWindow.PluginLoaded += onPluginLoaded;
                        }
                        else if (window is MusicVisualizerWindow musicVisualizerWindow)
                        {
                            optionsComboBox.SelectedIndex = optionsComboBox.Items.IndexOf(new Tuple<string, int>(checkBox.Content.ToString(), 0));
                        }

                        window.ShowInTaskbar = false;
                        window.Show();
                        window.ContentRendered += DisplayWindow_ContentRendered;
                        window.Closing += DisplayWindow_Closing;
                        Windows.Add(window);
                        WindowNames.Add(window.Title);
                    });
                });
            }
            else
            {
                int index = WindowNames.IndexOf(window.Title);

                Windows[index].Close();
                Windows.RemoveAt(index);
                WindowNames.RemoveAt(index);
                window.Close();
            }
            key.SetValue(checkBox.Name, checkBox.IsChecked.ToString());
            blockWindowsClosing = true;
            SaveLayout();
        }

        private void DisplayWindow_ContentRendered(object sender, EventArgs e)
        {
            WindowPos.SendWpfWindowBack(sender as Window);
            WindowPos.SendWpfWindowBack(sender as Window);
        }

        private void DisplayWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = blockWindowsClosing;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MessageBoxResult msbRes = MessageBox.Show((string)FindResource("wantToCloseProgram"), App.AppName, MessageBoxButton.YesNo);
            e.Cancel = msbRes != MessageBoxResult.Yes;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Visibility = Visibility.Collapsed;
            UpdateLayout();
            foreach (Window window in Windows)
            {
                window.Hide();
            }
            Environment.Exit(0);
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                EditCheckBox.IsChecked = false;
                EditCheckBox_Click(null, null);
                ShowInTaskbar = false;
                Visibility = Visibility.Collapsed;
                foreach (Window item in Windows)
                {
                    WindowPos.SendWpfWindowBack(item);
                    WindowPos.SendWpfWindowBack(item);
                }
            }
        }

        #endregion Windows

        #region options

        private void AmplifierLevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            amplifierLevelLabel.Content = (int)amplifierLevelSlider.Value;
            AmplifierLevel = (int)amplifierLevelSlider.Value;
            key.SetValue("AmplifierLevel", AmplifierLevel);
            SaveLayout();
        }

        private void MirrorModeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            MirrorMode = (bool)mirrorModeCheckBox.IsChecked;
            key.SetValue("MirrorMode", mirrorModeCheckBox.IsChecked.ToString());
            SaveLayout();
        }

        private void LineModeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            LineMode = (bool)lineModeCheckBox.IsChecked;
            key.SetValue("LineMode", lineModeCheckBox.IsChecked.ToString());
            SaveLayout();
        }

        private void MusicVisualizerColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(musicVisualizerColorTextBox.Text) && musicVisualizerColorTextBox.Text != "Default")
            {
                if (musicVisualizerColorTextBox.Text.Length > 0)
                {
                    if (musicVisualizerColorTextBox.Text.ToCharArray()[0] != '#')
                    {
                        musicVisualizerColorTextBox.Text = "#" + musicVisualizerColorTextBox.Text.Replace("#", "");
                    }
                }
                else
                {
                    musicVisualizerColorTextBox.Text = "#";
                    musicVisualizerColorTextBox.Select(musicVisualizerColorTextBox.Text.Length, 0);
                }

                if (musicVisualizerColorTextBox.SelectionStart == 0)
                {
                    if (musicVisualizerColorTextBox.Text.Length <= 2)
                    {
                        musicVisualizerColorTextBox.Select(musicVisualizerColorTextBox.Text.Length, 0);
                    }
                    else
                    {
                        musicVisualizerColorTextBox.Select(1, 0);
                    }
                }

                string hex = musicVisualizerColorTextBox.Text;

                if (MultiColorConverter.TryConvertToSystemColor(hex, out System.Drawing.Color systemColor))
                {
                    MusicVisualzerColor = systemColor;
                    musicVisualizerColorTextBox.Foreground = Brushes.Black;

                    key.SetValue("MusicVisualizerColor", musicVisualizerColorTextBox.Text);
                    SaveLayout();
                }
                else
                {
                    musicVisualizerColorTextBox.Foreground = Brushes.Red;
                }
            }
            else
            {
                MusicVisualzerColor = null;
                key.SetValue("MusicVisualizerColor", musicVisualizerColorTextBox.Text);
                SaveLayout();
            }
        }

        private void FontComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Theme.Font = fontComboBox.SelectedValue.ToString().Replace("System.Windows.Controls.ComboBoxItem: ", "");
            key.SetValue("Font", Theme.Font);
            SaveLayout();
        }

        private void SpectrumModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SpectrumMode = spectrumModeComboBox.SelectedIndex;
            key.SetValue("SpectrumMode", spectrumModeComboBox.SelectedIndex);

            mirrorModeCheckBox.IsEnabled = spectrumModeComboBox.SelectedIndex != 1;
            SaveLayout();
        }

        private void OptionsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (optionsComboBox.SelectedIndex < 1)
            {
                if (musicVisualizerOptionsPanel != null)
                {
                    musicVisualizerOptionsPanel.Visibility = Visibility.Visible;
                    optionsPanel.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                musicVisualizerOptionsPanel.Visibility = Visibility.Collapsed;
                optionsPanel.Visibility = Visibility.Visible;
                optionsPanel.Children.Clear();
                optionsPanel.UpdateLayout();

                bool success = PluginsSettings.TryGetValue(((Tuple<string, int>)optionsComboBox.SelectedItem).Item1.ToString(), out List<SettingElement> settingElements);
                if (!success || settingElements?.Count == 0)
                {
                    _ = optionsPanel.Children.Add(new TextBlock() { Text = (string)FindResource("noOptions") });
                    return;
                }

                foreach (SettingElement settingElement in settingElements)
                {
                    DockPanel stackPanel = new()
                    {
                        LastChildFill = true,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    _ = optionsPanel.Children.Add(stackPanel);

                    TextBlock label = new()
                    {
                        Text = settingElement.Name,
                        Padding = new Thickness(0, 0, 3, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    _ = stackPanel.Children.Add(label);
                    stackPanel.UpdateLayout();
                    label.UpdateLayout();

                    if (settingElement.Element is DesktopMagicPluginAPI.Inputs.Label eLabel)
                    {
                        label.Text = eLabel.Value;
                        label.Margin = new Thickness(0, 5, 3, 0);
                        label.HorizontalAlignment = HorizontalAlignment.Stretch;
                        label.TextWrapping = TextWrapping.WrapWithOverflow;

                        if (eLabel.Bold)
                        {
                            label.FontWeight = FontWeights.Bold;
                        }
                        eLabel.OnValueChanged += () =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                label.Text = eLabel.Value;
                            });
                        };
                    }
                    else if (settingElement.Element is DesktopMagicPluginAPI.Inputs.Button eButton)
                    {
                        Button button = new()
                        {
                            Content = eButton.Value,
                            FontSize = 10,
                            Height = 20,
                            Margin = new Thickness(0, 10, 0, 10),
                            Padding = new Thickness(0),
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        };
                        button.Click += (_s, _e) =>
                        {
                            try
                            {
                                eButton.Click();
                            }
                            catch (Exception ex)
                            {
                                DisplayException(ex.Message);
                            }
                        };
                        eButton.OnValueChanged += () =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                button.Content = eButton.Value;
                            });
                        };

                        _ = stackPanel.Children.Add(button);
                    }
                    else if (settingElement.Element is DesktopMagicPluginAPI.Inputs.CheckBox eCheckBox)
                    {
                        CheckBox checkBox = new()
                        {
                            IsChecked = eCheckBox.Value,
                            Style = (Style)FindResource("MaterialDesignDarkCheckBox"),
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        };
                        checkBox.Click += (_s, _e) =>
                        {
                            try
                            {
                                eCheckBox.Value = checkBox.IsChecked.GetValueOrDefault();
                            }
                            catch (Exception ex)
                            {
                                DisplayException(ex.Message);
                            }
                        };
                        eCheckBox.OnValueChanged += () =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                checkBox.IsChecked = eCheckBox.Value;
                            });
                        };

                        _ = stackPanel.Children.Add(checkBox);
                    }
                    else if (settingElement.Element is DesktopMagicPluginAPI.Inputs.TextBox eTextBox)
                    {
                        TextBox textBox = new()
                        {
                            Text = eTextBox.Value,
                            TextWrapping = TextWrapping.Wrap,
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        };
                        textBox.TextChanged += (_s, _e) =>
                        {
                            try
                            {
                                eTextBox.Value = textBox.Text;
                            }
                            catch (Exception ex)
                            {
                                DisplayException(ex.Message);
                            }
                        };
                        eTextBox.OnValueChanged += () =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                textBox.Text = eTextBox.Value;
                            });
                        };
                        _ = stackPanel.Children.Add(textBox);
                    }
                    else if (settingElement.Element is DesktopMagicPluginAPI.Inputs.IntegerUpDown eIntegerUpDown)
                    {
                        Xceed.Wpf.Toolkit.IntegerUpDown integerUpDown = new()
                        {
                            Value = eIntegerUpDown.Value,
                            Minimum = eIntegerUpDown.Minimum,
                            Maximum = eIntegerUpDown.Maximum,
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        };
                        integerUpDown.ValueChanged += (_s, _e) =>
                        {
                            try
                            {
                                eIntegerUpDown.Value = integerUpDown.Value.GetValueOrDefault();
                            }
                            catch (Exception ex)
                            {
                                DisplayException(ex.Message);
                            }
                        };
                        eIntegerUpDown.OnValueChanged += () =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                integerUpDown.Value = eIntegerUpDown.Value;
                            });
                        };
                        _ = stackPanel.Children.Add(integerUpDown);
                    }
                    else if (settingElement.Element is DesktopMagicPluginAPI.Inputs.Slider eSlider)
                    {
                        Slider slider = new()
                        {
                            Value = eSlider.Value,
                            Minimum = eSlider.Minimum,
                            Maximum = eSlider.Maximum,
                            TickFrequency = 1,
                            IsSnapToTickEnabled = true,
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        };
                        slider.ValueChanged += (_s, _e) =>
                        {
                            try
                            {
                                eSlider.Value = slider.Value;
                            }
                            catch (Exception ex)
                            {
                                DisplayException(ex.Message);
                            }
                        };
                        eSlider.OnValueChanged += () =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                slider.Value = eSlider.Value;
                            });
                        };

                        _ = stackPanel.Children.Add(slider);
                    }
                }
            }
        }

        private void DisplayException(string message)
        {
            App.Logger.Log(message, "PluginInput");
            _ = MessageBox.Show("File execution error:\n" + message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            int index = WindowNames.IndexOf(((Tuple<string, int>)optionsComboBox.SelectedItem).Item1.ToString());

            PluginWindow window = Windows[index] as PluginWindow;
            window?.Exit();
        }

        #endregion options

        private void TextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            int index = 0;
            foreach (FontFamily ff in Fonts.SystemFontFamilies)
            {
                ComboBoxItem comboBoxItem = new()
                {
                    FontFamily = ff,
                    Content = ff.ToString()
                };
                _ = fontComboBox.Items.Add(comboBoxItem);

                if (ff.ToString() == Theme.Font)
                {
                    fontComboBox.SelectedIndex = index;
                }
                index++;
            }
            if (fontComboBox.SelectedIndex == -1)
            {
                fontComboBox.SelectedIndex = 0;
            }
        }

        private void ColorHexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        private void ChangePrimaryColorButton_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog("Set Primary Color", Theme.PrimaryColor);
            if (colorDialog.ShowDialog() == true)
            {
                Theme.PrimaryBrush = colorDialog.ResultBrush;
                Theme.PrimaryColor = colorDialog.ResultColor;
                primaryColorRechtangle.Fill = colorDialog.ResultBrush;

                key.SetValue("PrimaryColor", MultiColorConverter.ConvertToHex(Theme.PrimaryColor));
                SaveLayout();
            }
        }

        private void ChangeSecondaryColorButton_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog("Set Secondary Color", Theme.SecondaryColor);

            if (colorDialog.ShowDialog() == true)
            {
                Theme.SecondaryBrush = colorDialog.ResultBrush;
                Theme.SecondaryColor = colorDialog.ResultColor;
                secondaryColorRechtangle.Fill = colorDialog.ResultBrush;

                key.SetValue("SecondaryColor", MultiColorConverter.ConvertToHex(Theme.SecondaryColor));
                SaveLayout();
            }
        }

        private void ChangeBackgroundColorButton_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog("Set Background Color", Theme.BackgroundColor);
            if (colorDialog.ShowDialog() == true)
            {
                Theme.BackgroundBrush = colorDialog.ResultBrush;
                Theme.BackgroundColor = colorDialog.ResultColor;
                backgroundColorRechtangle.Fill = colorDialog.ResultBrush;

                key.SetValue("BackgroundColor", MultiColorConverter.ConvertToHex(Theme.BackgroundColor));
                SaveLayout();
            }
        }

        #region Layout

        private void LayoutsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            removeLayoutButton.IsEnabled = layoutsComboBox.SelectedIndex != 0;

            if (layoutsComboBox.SelectedIndex == -1 || !loaded)
            {
                return;
            }

            key.SetValue("SelectedLayout", layoutsComboBox.SelectedIndex);

            string[] lines = File.ReadAllLines(App.ApplicationDataPath + "\\layouts.save");
            string[] data = lines[layoutsComboBox.SelectedIndex].Split(';');
            foreach (string dat in data)
            {
                if (dat.Contains(":"))
                {
                    string value = dat[(dat.LastIndexOf(":", StringComparison.InvariantCulture) + 1)..];
                    string name = dat.Replace(":" + value, "");
                    key.SetValue(name, value);
                }
            }
            LoadLayout(false);
            for (int i = 0; i < fontComboBox.Items.Count; i++)
            {
                ComboBoxItem comboBoxItem = (ComboBoxItem)fontComboBox.Items[i];
                if (comboBoxItem.FontFamily.ToString() == Theme.Font)
                {
                    fontComboBox.SelectedIndex = i;
                }
            }
        }

        private void NewLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            InputDialog inputDialog = new((string)FindResource("enterLayoutName"));
            if (inputDialog.ShowDialog() == true)
            {
                string content = "";
                foreach (string valueName in key.GetValueNames())
                {
                    if (valueName != "SelectedLayout")
                    {
                        content += valueName + ":" + key.GetValue(valueName).ToString() + ";";
                    }
                }
                content += inputDialog.ResponseText + "\n";

                File.AppendAllText(App.ApplicationDataPath + "\\layouts.save", content);
                key.SetValue("SelectedLayout", -1);
                LoadLayoutNames();
                layoutsComboBox.SelectedIndex = layoutsComboBox.Items.Count - 1;
            }
        }

        private void RemoveLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (layoutsComboBox.SelectedIndex == -1)
            {
                return;
            }

            List<string> lines = File.ReadAllLines(App.ApplicationDataPath + "\\layouts.save").ToList();
            lines.RemoveAt(layoutsComboBox.SelectedIndex);
            File.WriteAllLines(App.ApplicationDataPath + "\\layouts.save", lines);
            LoadLayoutNames();
            layoutsComboBox.SelectedIndex = 0;
        }

        private void SaveLayout()
        {
            _ = Task.Run(() =>
              {
                  lock (App.ApplicationDataPath)
                  {
                      List<string> lines = File.ReadAllLines(App.ApplicationDataPath + "\\layouts.save").ToList();
                      string content = "";
                      foreach (string valueName in key.GetValueNames())
                      {
                          if (valueName != "SelectedLayout")
                          {
                              content += valueName + ":" + key.GetValue(valueName).ToString() + ";";
                          }
                      }
                      Dispatcher.Invoke(() =>
                      {
                          content += layoutsComboBox.SelectedItem.ToString();
                          lines[layoutsComboBox.SelectedIndex] = content;
                      });
                      File.WriteAllLines(App.ApplicationDataPath + "\\layouts.save", lines);
                  }
              });
        }

        private void LoadLayoutNames()
        {
            layoutsComboBox.Items.Clear();
            string[] lines = File.ReadAllLines(App.ApplicationDataPath + "\\layouts.save");

            foreach (string line in lines)
            {
                string name = line[(line.LastIndexOf(";", StringComparison.InvariantCulture) + 1)..];
                _ = layoutsComboBox.Items.Add(name);
            }
            layoutsComboBox.SelectedIndex = int.Parse(key.GetValue("SelectedLayout", "0").ToString());
        }

        private void LoadLayout(bool minimize = true)
        {
            Theme.Font = key.GetValue("Font", "Segoe UI").ToString();
            spectrumModeComboBox.SelectedIndex = int.Parse(key.GetValue("SpectrumMode", "0").ToString());
            amplifierLevelSlider.Value = int.Parse(key.GetValue("AmplifierLevel", "0").ToString());
            mirrorModeCheckBox.IsChecked = bool.Parse(key.GetValue("MirrorMode", "false").ToString());
            lineModeCheckBox.IsChecked = bool.Parse(key.GetValue("LineMode", "false").ToString());
            musicVisualizerColorTextBox.Text = key.GetValue("MusicVisualizerColor", "").ToString();
            blockWindowsClosing = false;

            string primaryColorHex = key.GetValue("PrimaryColor", "#FFFFFFFF").ToString();
            string secondaryColorHex = key.GetValue("SecondaryColor", "#FFFFFFFF").ToString();
            string backgroundColorHex = key.GetValue("BackgroundColor", "#00FFFFFF").ToString();

            _ = MultiColorConverter.TryConvertToSystemColor(primaryColorHex, out System.Drawing.Color primarySystemColor);
            _ = MultiColorConverter.TryConvertToMediaColor(primaryColorHex, out Color primaryMediaColor);
            _ = MultiColorConverter.TryConvertToSystemColor(secondaryColorHex, out System.Drawing.Color secondarySystemColor);
            _ = MultiColorConverter.TryConvertToMediaColor(secondaryColorHex, out Color secondaryMediaColor);
            _ = MultiColorConverter.TryConvertToSystemColor(backgroundColorHex, out System.Drawing.Color backgroundSystemColor);
            _ = MultiColorConverter.TryConvertToMediaColor(backgroundColorHex, out Color backgroundMediaColor);

            Theme.PrimaryColor = primarySystemColor;
            Theme.PrimaryBrush = new SolidColorBrush(primaryMediaColor);

            Theme.SecondaryColor = secondarySystemColor;
            Theme.SecondaryBrush = new SolidColorBrush(secondaryMediaColor);

            Theme.BackgroundColor = backgroundSystemColor;
            Theme.BackgroundBrush = new SolidColorBrush(backgroundMediaColor);

            primaryColorRechtangle.Fill = Theme.PrimaryBrush;
            secondaryColorRechtangle.Fill = Theme.SecondaryBrush;
            backgroundColorRechtangle.Fill = Theme.BackgroundBrush;

            MusicVisualizerColorTextBox_TextChanged(null, null);
            MirrorModeCheckBox_Click(null, null);
            LineModeCheckBox_Click(null, null);
            SpectrumModeComboBox_SelectionChanged(null, null);
            AmplifierLevelSlider_ValueChanged(null, null);

            foreach (Window window in Windows)
            {
                window.Close();
            }

            EditCheckBox.IsChecked = false;
            EditMode = false;
            blockWindowsClosing = true;
            Windows.Clear();
            WindowNames.Clear();

            IEnumerable<CheckBox> list = stackPanel.Children.OfType<CheckBox>();
            bool showWindow = true;

            try
            {
                foreach (CheckBox checkBox in list)
                {
                    try
                    {
                        if (key.GetValue(checkBox.Name, "False").ToString() == "True")
                        {
                            checkBox.IsChecked = true;
                            CheckBox_Click(checkBox, null);
                            showWindow = false;
                        }
                        else
                        {
                            checkBox.IsChecked = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Log(ex.ToString(), "Main");
                        MessageBox.Show(ex.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log(ex.ToString(), "Main");
                _ = MessageBox.Show(ex.ToString());
            }

            if (!showWindow && minimize)
            {
                WindowState = WindowState.Minimized;
                Window_StateChanged(null, null);
            }
            else
            {
                WindowState = WindowState.Normal;
                Window_StateChanged(null, null);
            }
        }

        #endregion Layout

        private void UpdatePluginsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadPlugins();
            Task.Run(() =>
            {
                foreach (Window window in Windows.ToArray())
                {
                    if (window.GetType() == typeof(PluginWindow))
                    {
                        ((PluginWindow)window).UpdatePluginWindow();
                    }
                }
            });
        }

        private void OpenPluginsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            _ = Process.Start("explorer.exe", App.ApplicationDataPath + "\\Plugins");
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            ScrollViewer scv = (ScrollViewer)sender;
            scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void TaskbarIcon_TrayLeftClick(object sender, EventArgs e)
        {
            for (int i = 0; i < 10; i++)
            {
                ShowInTaskbar = true;
                Visibility = Visibility.Visible;
                SystemCommands.RestoreWindow(this);
                Topmost = true;
                Activate();
                Topmost = false;
            }
        }

        private void SetLanguageDictionary()
        {
            ResourceDictionary dict = new ResourceDictionary();
            string currentCulture = Thread.CurrentThread.CurrentUICulture.ToString();

            if (currentCulture.Contains("de"))
            {
                dict.Source = new Uri("..\\Resources\\StringResources.de.xaml", UriKind.Relative);
            }
            else
            {
                dict.Source = new Uri("..\\Resources\\StringResources.en.xaml", UriKind.Relative);
            }
            Resources.MergedDictionaries.Add(dict);
        }

        private void GithubButton_Click(object sender, RoutedEventArgs e)
        {
            string uri = "https://github.com/Stone-Red-Code/DesktopMagic";
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.UseShellExecute = true;
            psi.FileName = uri;
            _ = Process.Start(psi);
        }

        private void DownloadPluginsButton_Click(object sender, RoutedEventArgs e)
        {
            string uri = "https://github.com/Stone-Red-Code/DesktopMagic-Plugins";
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.UseShellExecute = true;
            psi.FileName = uri;
            _ = Process.Start(psi);
        }
    }
}