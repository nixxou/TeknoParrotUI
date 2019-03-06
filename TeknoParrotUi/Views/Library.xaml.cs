﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Serialization;
using TeknoParrotUi.Common;
using Microsoft.Win32;
using TeknoParrotUi.UserControls;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for Library.xaml
    /// </summary>
    public partial class Library
    {
        //Defining variables that need to be accessed by all methods
        public JoystickControl Joystick;
        readonly List<GameProfile> _gameNames = new List<GameProfile>();
        readonly GameSettingsControl _gameSettings = new GameSettingsControl();
        private ContentControl _contentControl;

        public Library(ContentControl contentControl)
        {
            InitializeComponent();
            BitmapImage imageBitmap = new BitmapImage(new Uri(
                "pack://application:,,,/TeknoParrotUi;component/Resources/teknoparrot_by_pooterman-db9erxd.png",
                UriKind.Absolute));

            image1.Source = imageBitmap;

            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot"))
            {
                var isPatron = key != null && key.GetValue("PatreonSerialKey") != null;

                if (isPatron)
                    textBlockPatron.Text = "Yes";
            }

            _contentControl = contentControl;
            Joystick =  new JoystickControl(contentControl, this);
        }

        /// <summary>
        /// When the selection in the listbox is changed, this is run. It loads in the currently selected game.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gameList.Items.Count == 0)
                return;
            var modifyItem = (ListBoxItem) ((ListBox) sender).SelectedItem;
            var profile = _gameNames[gameList.SelectedIndex];
            var icon = profile.IconName;
            var imageBitmap = new BitmapImage(File.Exists(icon)
                ? new Uri("pack://siteoforigin:,,,/" + icon, UriKind.Absolute)
                : new Uri("../Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));
            image1.Source = imageBitmap;
            _gameSettings.LoadNewSettings(profile, modifyItem, _contentControl, this);
            Joystick.LoadNewSettings(profile, modifyItem, MainWindow.ParrotData);
            if (!profile.HasSeparateTestMode)
            {
                ChkTestMenu.IsChecked = false;
                ChkTestMenu.IsEnabled = false;
            }
            else
            {
                ChkTestMenu.IsEnabled = true;
                ChkTestMenu.ToolTip = "Enable or disable test mode.";
            }
            gameInfoText.Text = _gameNames[gameList.SelectedIndex].GameInfo.SmallText;
        }

        /// <summary>
        /// This updates the listbox when called
        /// </summary>
        private void ListUpdate()
        {
            gameList.Items.Clear();
            foreach (var gameProfile in GameProfileLoader.UserProfiles)
            {
                var item = new ListBoxItem
                {
                    Content = gameProfile.GameName,
                    Tag = gameProfile
                };

                gameProfile.GameInfo = JoystickHelper.DeSerializeDescription(gameProfile.FileName);

                _gameNames.Add(gameProfile);
                gameList.Items.Add(item);

                if (MainWindow.ParrotData.SaveLastPlayed && gameProfile.GameName == MainWindow.ParrotData.LastPlayed)
                {
                    gameList.SelectedItem = item;
                }
                else
                {
                    gameList.SelectedIndex = 0;
                }
            }

            if (gameList.Items.Count != 0) return;
            if (MessageBox.Show("Looks like you have no games set up. Do you want to add one now?",
                    "No games found", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = new AddGame();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// This executes the code when the library usercontrol is loaded. ATM all it does is load the data and update the list.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Application.Current.Windows.OfType<MainWindow>().Single().LoadParrotData();
            ListUpdate();
        }

        /// <summary>
        /// Validates that the game exists and then runs it with the emulator.
        /// </summary>
        /// <param name="gameProfile">Input profile.</param>
        /// <param name="testMenuString">Command to run test menu.</param>
        /// <param name="exeName">Test menu exe name.</param>
        private void ValidateAndRun(GameProfile gameProfile, string testMenuString, string exeName = "")
        {
            if (!ValidateGameRun(gameProfile))
                return;

            var testMenu = ChkTestMenu.IsChecked;

            var gameRunning = new GameRunning(gameProfile, testMenu, MainWindow.ParrotData, testMenuString,
                gameProfile.TestMenuIsExecutable, exeName, false, false, this);
            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = gameRunning;
        }

        static readonly List<string> RequiredFiles = new List<string>
        {
            "OpenParrot.dll",
            "OpenParrot64.dll",
            "TeknoParrot.dll",
            "OpenParrotLoader.exe",
            "OpenParrotLoader64.exe",
            "BudgieLoader.exe"
        };

        /// <summary>
        /// This validates that the game can be run, checking for stuff like other emulators and incorrect files
        /// </summary>
        /// <param name="gameProfile"></param>
        /// <returns></returns>
        private bool ValidateGameRun(GameProfile gameProfile)
        {
            if (!File.Exists(gameProfile.GamePath))
            {
                MessageBox.Show($"Cannot find game exe at: {gameProfile.GamePath}", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            foreach (var file in RequiredFiles)
            {
                if (!File.Exists(file))
                {
                    MessageBox.Show($"Cannot find {file}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            if (EmuBlacklist.CheckForBlacklist(
                Directory.GetFiles(Path.GetDirectoryName(gameProfile.GamePath) ??
                                   throw new InvalidOperationException())))
            {
                var errorMsg =
                    $"Hold it right there!{Environment.NewLine}it seems you have other emulator already in use.{Environment.NewLine}Please remove the following files from the game directory:{Environment.NewLine}";
                foreach (var fileName in EmuBlacklist.BlacklistedList)
                {
                    errorMsg += fileName + Environment.NewLine;
                }

                MessageBox.Show(errorMsg, "Validation error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!File.Exists(Path.Combine(gameProfile.GamePath, "iDmacDrv32.dll"))) return true;
            var description = FileVersionInfo.GetVersionInfo("iDmacDrv32.dll");
            if (description.FileDescription != "PCI-Express iDMAC Driver Library (DLL)")
            {
                return (MessageBox.Show(
                            "You seem to be using an unofficial iDmacDrv32.dll file! This game may crash or be unstable. Continue?",
                            "Warning", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes);
            }

            return true;
        }

        /// <summary>
        /// This button opens the game settings window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = _gameSettings;
        }

        /// <summary>
        /// This button opens the controller settings option
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Joystick.Listen();
            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = Joystick;
        }

        /// <summary>
        /// This button actually launches the game selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (gameList.Items.Count == 0)
                return;

            var gameProfile = (GameProfile) ((ListBoxItem) gameList.SelectedItem).Tag;

            if (MainWindow.ParrotData.SaveLastPlayed)
            {
                MainWindow.ParrotData.LastPlayed = gameProfile.GameName;
                JoystickHelper.Serialize(MainWindow.ParrotData);
            }

            var testMenuExe = gameProfile.TestMenuIsExecutable ? gameProfile.TestMenuParameter : "";

            var testStr = gameProfile.TestMenuIsExecutable
                ? gameProfile.TestMenuExtraParameters
                : gameProfile.TestMenuParameter;

            ValidateAndRun(gameProfile, testStr, testMenuExe);
        }

        /// <summary>
        /// This starts the MD5 verifier that checks whether a game is a clean dump
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content =
                new VerifyGame(_gameNames[gameList.SelectedIndex].GamePath,
                    _gameNames[gameList.SelectedIndex].ValidMd5);
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            Process.Start("https://wiki.teknoparrot.com/");
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            try
            {
                for (int i = 0; i < gameList.Items.Count; i++)
                {
                    if (File.Exists(_gameNames[i].IconName)) continue;
                    var update =
                        new DownloadWindow(
                            "https://raw.githubusercontent.com/teknogods/TeknoParrotUIThumbnails/master/" +
                            _gameNames[i].IconName, _gameNames[i].IconName, false);
                    update.ShowDialog();
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}