using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HLDS_Launcher
{
    /*
     * CHANGELOG:
     * 
     * v1.3
     *   Added ability to set rcon_password and sv_password.
     *   Ability to generate random rcon password.
     *   Ability to select maps from maps folder or mapcycle file.
     *   Option to set LAN mode.
     *   Ability to copy public IP to clipboard with single click.
     *   Option to change launcher behaviour after server starts (keep launcher open, minimize or close launcher).
     * 
     * v1.2
     *   Added bots support for ReGame.dll (Only CS 1.6).
     *   Added option to set server IP address.
     *   Added button to edit mapcycle file.
     *   Show public ip directly on application instead of redirecting to website.
     *   Improved launcher log to detect if server crashed/closed unexpectedly.
     *   Now server doesn't restart automatically when the user stops it.
     *   Random mapcycle now creates a new file named mapcycle_random.txt and use it for map rotation.
     * 
     * v1.1
     *   Added option to randomize mapcycle.
     * 
     * v1.0
     *   Initial release.
     */

    public partial class MainWindow : Window
    {
        string _game;
        string _mapcyclefile;
        string _map;
        string _maxPlayers;
        string _svPw;
        string _rconPw;
        string _ip;
        string _port;
        string _vac;
        string _bots;
        string _lan;

        bool loadMapsFromFolder = true;
        bool writeLog = false;

        List<Scripts.Game> games = new List<Scripts.Game>();
        List<string> gameFolders = new List<string>();
        List<string> gameNames = new List<string>();

        Process hlds;
        ProcessPriorityClass priority;

        System.Net.WebClient webClient;

        private static RNGCryptoServiceProvider randomNG = new RNGCryptoServiceProvider();

        internal static readonly char[] chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();

        public MainWindow()
        {
            CheckEXE();
            InitializeComponent();
            LoadGames();
            CheckGamesAvailable();
            SetMaxPlayersItems();
            LoadUserSettings();

            webClient = new System.Net.WebClient();
            GetPublicIP();
        }

        // Check if HLDS.exe exists in the same location.
        private void CheckEXE()
        {
            if (!File.Exists("HLDS.exe"))
            {
                MessageDialog msg = new MessageDialog("HLDS.exe not found.\nThis launcher must be located in the same directory as hlds.exe.",
                    "Error - HLDS Launcher", false, true)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                msg.ShowDialog();
                ExitApplication();
            }
        }

        // Load games/mods
        private void LoadGames()
        {
            string[] folders = Directory.GetDirectories(".");

            // Check each folder to find out if it's a game/mod.
            foreach (string folder in folders)
            {
                // If folder "valve" exists, add hldm to game list.
                if (folder == ".\\valve")
                {
                    AddGame("Half-Life Deathmatch", "valve", "", folders);
                }
                else if (!folder.Contains("_"))
                {
                    // If it's a game/mod, get name and check for gameplay type.
                    if (File.Exists(folder + "\\liblist.gam"))
                    {
                        StreamReader sr = new StreamReader(folder + "\\liblist.gam");
                        string line, gameName = "", fallbackdir = "";
                        bool isMultiplayer = false, isSingleplayer = false;

                        // Get game name, gameplay type and fallback directory from liblist.gam.
                        while (!sr.EndOfStream)
                        {
                            line = sr.ReadLine();

                            // Get game/mod name.
                            if (line.StartsWith("game "))
                            {
                                gameName = line.Replace("game \"", "");
                                gameName = gameName.Remove(gameName.LastIndexOf('"'));
                            }
                            // Get gameplay type.
                            else if (line.StartsWith("type "))
                            {
                                isMultiplayer = line.Contains("multiplayer");
                                isSingleplayer = !isMultiplayer;
                            }
                            // Get fallback directory if exists.
                            else if (line.StartsWith("fallback_dir "))
                            {
                                fallbackdir = line.Replace("fallback_dir \"", "");
                                fallbackdir = fallbackdir.Remove(fallbackdir.LastIndexOf('"'));
                            }
                        }
                        // If game/mod is multiplayer, add name to list.
                        if (isMultiplayer == true)
                        {
                            AddGame(gameName, folder.Remove(0, 2), fallbackdir, folders);
                        }
                        // If gameplay type is unknown, search if server.cfg exists and add the game to list if true.
                        else if (isSingleplayer == false)
                        {
                            if (File.Exists(folder + "\\server.cfg"))
                            {
                                AddGame(gameName, folder.Remove(0, 2), fallbackdir, folders);
                            }
                        }
                    }
                }
            }
            // Add games to game list.
            gameList.ItemsSource = gameNames;
        }

        private void AddGame(string name, string folderName, string fallbackDir, string[] folders)
        {
            Scripts.Game game = new Scripts.Game
            {
                Name = name,
                ShortName = folderName,
                FallbackDir = fallbackDir
            };
            game.GetExtraFolders(folders);
            game.LoadMaps(loadMapsFromFolder);

            games.Add(game);
            gameNames.Add(game.Name);
            gameFolders.Add(game.ShortName);
        }

        // Make sure that there is at least 1 game in HLDS.
        private void CheckGamesAvailable()
        {
            if (games.Count <= 0)
            {
                MessageDialog msg = new MessageDialog("No games/mods have been found. Please check your HLDS installation.",
                    "Error - HLDS Launcher", false, true)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                msg.ShowDialog();
                ExitApplication();
            }
        }

        // Generate max player combobox items.
        private void SetMaxPlayersItems()
        {
            for (int i = 0; i < 32; i++)
            {
                maxPlayersList.Items.Add(i + 1);
            }
            maxPlayersList.SelectedIndex = maxPlayersList.Items.Count-1;
        }

        // Load user settings.
        private void LoadUserSettings()
        {
            gameList.SelectedIndex = Properties.Settings.Default.gameNameIndex;
            maxPlayersList.SelectedIndex = Properties.Settings.Default.maxPlayers;
            svPassword_Textbox.Text = Properties.Settings.Default.svPassword;
            rconPassword_Textbox.Text = Properties.Settings.Default.rconPassword;
            ip_TextBox.Text = Properties.Settings.Default.localIP;
            port.Text = Properties.Settings.Default.port;
            priorityList.SelectedIndex = Properties.Settings.Default.priorityIndex;
            randomMapcycle.IsChecked = Properties.Settings.Default.randomMapcycle;
            secureVAC.IsChecked = Properties.Settings.Default.vac;
            enableBots.IsChecked = Properties.Settings.Default.bots;
            autoRestart.IsChecked = Properties.Settings.Default.autoRestart;
            enableLog.IsChecked = Properties.Settings.Default.enableLogging;
            mapsList.SelectedIndex = Properties.Settings.Default.gameMapIndex;
            loadMapsFromFolder = Properties.Settings.Default.loadMapsFromFolder;
            serverLaunchOptionsList.SelectedIndex = Properties.Settings.Default.serverLaunchOptionsList;

            ToggleButtonMapSource();
        }

        // Save user settings.
        private void SaveUserSettings()
        {
            Properties.Settings.Default.gameNameIndex = gameList.SelectedIndex;
            Properties.Settings.Default.gameMapIndex = mapsList.SelectedIndex;
            Properties.Settings.Default.maxPlayers = maxPlayersList.SelectedIndex;
            Properties.Settings.Default.svPassword = svPassword_Textbox.Text;
            Properties.Settings.Default.rconPassword = rconPassword_Textbox.Text;
            Properties.Settings.Default.localIP = ip_TextBox.Text;
            Properties.Settings.Default.port = port.Text;
            Properties.Settings.Default.priorityIndex = priorityList.SelectedIndex;
            Properties.Settings.Default.randomMapcycle = (bool)randomMapcycle.IsChecked;
            Properties.Settings.Default.vac = (bool)secureVAC.IsChecked;
            Properties.Settings.Default.bots = (bool)enableBots.IsChecked;
            Properties.Settings.Default.autoRestart = (bool)autoRestart.IsChecked;
            Properties.Settings.Default.enableLogging = (bool)enableLog.IsChecked;
            Properties.Settings.Default.loadMapsFromFolder = loadMapsFromFolder;
            Properties.Settings.Default.serverLaunchOptionsList = serverLaunchOptionsList.SelectedIndex;
            Properties.Settings.Default.Save();
        }

        // Randomize mapcycle.
        private void RandomMapCycle()
        {
            if (gameList.Items.Count <= 1)
            {
                return;
            }
            List<string> mapList = new List<string>();
            Random random = new Random();

            mapList.AddRange(games[gameList.SelectedIndex].Maps);
            mapList.RemoveAt(0); // Remove "<Random Map>" entry.
            StreamWriter sw = new StreamWriter(".\\" + games[gameList.SelectedIndex].ShortName + "\\mapcycle_random.txt");

            // Add selected map at first in the random mapcycle.
            string mapName = _map.Remove(0, 6);
            sw.WriteLine(mapName);
            mapList.Remove(mapName);

            while (mapList.Count > 0)
            {
                int i = random.Next(0, mapList.Count-1);
                sw.WriteLine(mapList[i]);
                mapList.RemoveAt(i);
            }
            sw.Close();
            _mapcyclefile = " +mapcyclefile mapcycle_random.txt ";
        }

        // Start hlds.exe
        private void StartHLDS()
        {
            hlds = new Process();
            hlds.StartInfo.FileName = "hlds.exe";
            hlds.StartInfo.Arguments = "-console" + _game + _maxPlayers + _ip + _port + _vac + _lan + _mapcyclefile + _map + _bots + _rconPw + _svPw;
            hlds.EnableRaisingEvents = true;
            hlds.Exited += new EventHandler(Hlds_Exited);
            hlds.Start();
            hlds.PriorityClass = priority;
            WriteToLog("Server started with parameters: " + hlds.StartInfo.Arguments);

            SetWindowTitle("HLDS - " + gameNames[gameList.SelectedIndex] + " (Running)");
        }

        // Stop server and restore UI.
        private void StopServer(int exitCode = 0)
        {
            gameList.IsEnabled = true;
            mapsList.IsEnabled = true;
            buttonToggleMapsSource.IsEnabled = true;
            maxPlayersList.IsEnabled = true;
            port.IsEnabled = true;
            secureVAC.IsEnabled = true;
            enableBots.IsEnabled = games[gameList.SelectedIndex].ShortName == "cstrike";
            autoRestart.IsEnabled = true;
            enableLog.IsEnabled = true;
            randomMapcycle.IsEnabled = true;
            ip_TextBox.IsEnabled = true;
            svPassword_Textbox.IsEnabled = true;
            rconPassword_Textbox.IsEnabled = true;
            buttonRandomRCON.IsEnabled = true;
            LANMode.IsEnabled = true;
            priorityList.IsEnabled = true;
            serverLaunchOptionsList.IsEnabled = true;

            buttonStart.IsEnabled = true;
            buttonStart.Visibility = Visibility.Visible;

            buttonStop.IsEnabled = false;
            buttonStop.Visibility = Visibility.Hidden;

            SetWindowTitle("HLDS Launcher");

            if (exitCode == 0)
            {
                WriteToLog("Server stopped by user.");
            }
            else if (exitCode == -1)
            {
                WriteToLog("Dedicated Server error.");
            }

            // Restore window.
            if (this.WindowState != WindowState.Normal)
            {
                this.WindowState = WindowState.Normal;
            }
        }

        private void SetWindowTitle(string text)
        {
            this.Title = text;
            windowTitle.Text = text;
        }

        private void ExitApplication()
        {
            Application.Current.Shutdown();
        }

        private void WriteToLog(string line)
        {
            if (writeLog == true)
            {
                TextWriter wr = File.AppendText("HLDSLauncher.log");
                wr.WriteLine(DateTime.Now.ToString() + " : " + line);
                wr.Close();
            }
        }

        // Get public IP from ipify.org.
        private void GetPublicIP()
        {
            publicIP_Text.Text = "...";
            webClient.DownloadStringCompleted += new DownloadStringCompletedEventHandler(PublicIP_DownloadStringCompleted);
            webClient.DownloadStringAsync(new Uri("https://api.ipify.org"));
        }

        private void StartServer()
        {
            SetServerVariables();
            ManageStartingMap();
            SetProcessPriority();
            ManageRandomMapcycle();
            SetLauncherLog();
            SaveUserSettings();
            StartHLDS();
            ManageWindow();
        }

        private void SetServerVariables()
        {
            _game = " -game " + games[gameList.SelectedIndex].ShortName;
            _map = " +map " + mapsList.SelectionBoxItem;
            _maxPlayers = " +maxplayers " + (maxPlayersList.SelectedIndex + 1);
            _svPw = svPassword_Textbox.Text.Length > 0 ? " +sv_password " + svPassword_Textbox.Text : "";
            _rconPw = rconPassword_Textbox.Text.Length > 0 ? " +rcon_password " + rconPassword_Textbox.Text : "";
            _ip = ip_TextBox.Text.Length > 0 ? " +ip " + ip_TextBox.Text : "";
            _port = port.Text.Length > 0 ? " +port " + port.Text : "";
            _vac = secureVAC.IsChecked == true ? "" : " -insecure ";
            _bots = enableBots.IsEnabled && enableBots.IsChecked == true ? " -bots " : "";
            _lan = LANMode.IsChecked == true ? " +sv_lan 1 " : " +sv_lan 0 ";
        }

        private void ManageStartingMap()
        {
            // If selected map is "Random Map", choose a map randomly.
            if (mapsList.SelectedIndex == 0)
            {
                Random rand = new Random();
                _map = " +map " + mapsList.Items[rand.Next(1, mapsList.Items.Count-1)].ToString();
            }
            // If map name starts with "-", create a cfg file and change command to "exec". 
            // If this isn't done, the server will not read the map command properly and will not load the map.
            if (mapsList.SelectionBoxItem.ToString().StartsWith("-"))
            {
                TextWriter tw = new StreamWriter("hldslauncher_loadmap.cfg");
                tw.WriteLine("map " + mapsList.SelectionBoxItem.ToString());
                tw.Close();
                _map = "+exec hldslauncher_loadmap.cfg";
            }
        }

        private void SetProcessPriority()
        {
            switch (priorityList.SelectedIndex)
            {
                case 0:
                    priority = ProcessPriorityClass.Normal;
                    break;
                case 1:
                    priority = ProcessPriorityClass.AboveNormal;
                    break;
                case 2:
                    priority = ProcessPriorityClass.High;
                    break;
                case 3:
                    priority = ProcessPriorityClass.RealTime;
                    break;
            }
        }

        private void ManageRandomMapcycle()
        {
            if (randomMapcycle.IsChecked == true)
            {
                RandomMapCycle();
            }
            else
            {
                _mapcyclefile = "";
            }
        }

        private void SetLauncherLog()
        {
            // Check if Launcher should write to log.
            writeLog = (bool)enableLog.IsChecked;
        }

        private void ManageWindow()
        {
            // If launcher is set up to close after server starts, close application. Else disable some UI options.
            if (serverLaunchOptionsList.SelectedIndex == 2)
            {
                ExitApplication();
            }
            else
            {
                gameList.IsEnabled = false;
                mapsList.IsEnabled = false;
                buttonToggleMapsSource.IsEnabled = false;
                maxPlayersList.IsEnabled = false;
                port.IsEnabled = false;
                secureVAC.IsEnabled = false;
                enableBots.IsEnabled = false;
                autoRestart.IsEnabled = false;
                enableLog.IsEnabled = false;
                randomMapcycle.IsEnabled = false;
                ip_TextBox.IsEnabled = false;
                svPassword_Textbox.IsEnabled = false;
                rconPassword_Textbox.IsEnabled = false;
                buttonRandomRCON.IsEnabled = false;
                priorityList.IsEnabled = false;
                LANMode.IsEnabled = false;
                serverLaunchOptionsList.IsEnabled = false;

                buttonStart.IsEnabled = false;
                buttonStart.Visibility = Visibility.Hidden;

                buttonStop.IsEnabled = true;
                buttonStop.Visibility = Visibility.Visible;

                // Minimize to taskbar.
                if (serverLaunchOptionsList.SelectedIndex == 1)
                {
                    MinimizeWindow();
                }
            }
        }

        private void MinimizeWindow()
        {
            this.WindowState = WindowState.Minimized;
        }

        // Generate random password.
        private void RandomPassword()
        {
            byte[] randomNumber = new byte[40];
            randomNG.GetBytes(randomNumber);

            StringBuilder str = new StringBuilder(10);
            for (int i = 0; i < 10; i++)
            {
                var rand = BitConverter.ToUInt32(randomNumber, i * 4);
                var idx = rand % chars.Length;
                str.Append(chars[idx]);
            }
            rconPassword_Textbox.Text = str.ToString();
        }

        private void ToggleButtonMapSource()
        {
            if (loadMapsFromFolder == true)
            {
                textblockMapsSource.Text = "Showing maps from 'maps' folder";
                buttonToggleMapsSource.ToolTip = "Show maps from 'maps' folder.";
            }
            else
            {
                textblockMapsSource.Text = "Showing maps from mapcycle file";
                buttonToggleMapsSource.ToolTip = "Show maps from mapcycle file.";
            }
        }

        /********************
         *      BUTTONS
         ********************/

        private void VersionTextblock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            AboutWindow abw = new AboutWindow
            {
                Owner = this
            };
            abw.ShowDialog();
        }

        private void ButtonOpenGameFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(".\\" + games[gameList.SelectedIndex].ShortName))
            {
                Process.Start("explorer.exe", ".\\" + games[gameList.SelectedIndex].ShortName);
            }
            else
            {
                MessageDialog msg = new MessageDialog("Folder \"" + games[gameList.SelectedIndex].ShortName + "\" doesn't exists.", "Error", false)
                {
                    Owner = this
                };
                msg.ShowDialog();
            }
        }

        private void ButtonToggleMapsSource_Click(object sender, RoutedEventArgs e)
        {
            loadMapsFromFolder = !loadMapsFromFolder;
            ToggleButtonMapSource();

            games[gameList.SelectedIndex].LoadMaps(loadMapsFromFolder);
            mapsList.ItemsSource = games[gameList.SelectedIndex].Maps;
            mapsList.SelectedIndex = 0;
            mapsList.Items.Refresh();
        }

        private void ButtonRandomRCON_Click(object sender, RoutedEventArgs e)
        {
            RandomPassword();
        }

        private void ButtonGetIP_Click(object sender, RoutedEventArgs e)
        {
            buttonGetIP.IsEnabled = false;
            publicIP_Text.IsEnabled = false;
            GetPublicIP();
        }

        private void ButtonEditServerCFG_Click(object sender, RoutedEventArgs e)
        {
            string serverFilePath = ".\\" + games[gameList.SelectedIndex].ShortName + "\\server.cfg";
            if (File.Exists(serverFilePath))
            {
                Process.Start(serverFilePath);
            }
            else
            {
                MessageDialog msg = new MessageDialog("File \"server.cfg\" not found in path: " + serverFilePath, "Error - HLDS Launcher", false)
                {
                    Owner = this
                };
                msg.ShowDialog();
            }
        }

        private void ButtonEditMapcycle_Click(object sender, RoutedEventArgs e)
        {
            string serverFilePath = ".\\" + games[gameList.SelectedIndex].ShortName + "\\mapcycle.txt";
            if (File.Exists(serverFilePath))
            {
                Process.Start(serverFilePath);
            }
            else
            {
                MessageDialog msg = new MessageDialog("File \"mapcycle.txt\" not found in path: " + serverFilePath, "Error - HLDS Launcher", false)
                {
                    Owner = this
                };
                msg.ShowDialog();
            }
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            StartServer();
        }

        private void ButtonStop_Click(object sender, RoutedEventArgs e)
        {
            MessageDialog msg = new MessageDialog("Are you sure you want to stop the server?", "Stop server")
            {
                Owner = this
            };
            if (msg.ShowDialog() == true)
            {
                StopServer();
                hlds.CloseMainWindow();
                hlds.Close();
                hlds.Refresh();
            }
        }

        private void ButtonMinimizeWindow(object sender, RoutedEventArgs e)
        {
            MinimizeWindow();
        }

        private void ButtonExit_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }

        /********************
         *      EVENTS
         ********************/
        
        private void GameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gameList.Items.Count > 0)
            {
                games[gameList.SelectedIndex].LoadMaps(loadMapsFromFolder);
                mapsList.ItemsSource = games[gameList.SelectedIndex].Maps;

                if (gameList.SelectedIndex != Properties.Settings.Default.gameNameIndex)
                {
                    mapsList.SelectedIndex = 0;
                }

                if (games[gameList.SelectedIndex].ShortName == "cstrike")
                {
                    enableBots.IsEnabled = true;
                }
                else
                {
                    enableBots.IsEnabled = false;
                }
            }
        }

        private void Grid_TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void Hlds_Exited(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (hlds.ExitCode != 0 && hlds.ExitCode != -1)
                {
                    WriteToLog("Server process ended unexpectedly. Game: " + games[gameList.SelectedIndex].ShortName);

                    if (autoRestart.IsChecked == true)
                    {
                        WriteToLog("Restarting server...");

                        ManageStartingMap();
                        ManageRandomMapcycle();
                        StartHLDS();
                        return;
                    }
                }
                StopServer(hlds.ExitCode);
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle, null);

        }

        private void PublicIP_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                publicIP_Text.Text = "Failed";
            }
            else
            {
                publicIP_Text.Text = e.Result;
                publicIP_Text.IsEnabled = true;
            }
            buttonGetIP.IsEnabled = true;
        }

        private void PublicIP_Text_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Clipboard.SetText(publicIP_Text.Text);
        }

        private void ServerLaunchOptionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (serverLaunchOptionsList.SelectedIndex == 2)
            {
                autoRestart.IsEnabled = false;
                enableLog.IsEnabled = false;
            }
            else
            {
                autoRestart.IsEnabled = true;
                enableLog.IsEnabled = true;
            }
        }

        private void TextChanged_OnlyAlphanumeric(object sender, TextChangedEventArgs e)
        {
            var textBoxSender = (TextBox)sender;
            var cursorPosition = textBoxSender.SelectionStart;
            textBoxSender.Text = Regex.Replace(textBoxSender.Text, "[^0-9a-zA-Z]", "");
            textBoxSender.SelectionStart = cursorPosition;
        }
        
        private void TextChanged_OnlyIP(object sender, TextChangedEventArgs e)
        {
            var textBoxSender = (TextBox)sender;
            var cursorPosition = textBoxSender.SelectionStart;
            textBoxSender.Text = Regex.Replace(textBoxSender.Text, "[^0-9.]", "");
            textBoxSender.SelectionStart = cursorPosition;
        }
        
        private void TextChanged_OnlyNumbers(object sender, TextChangedEventArgs e)
        {
            var textBoxSender = (TextBox)sender;
            var cursorPosition = textBoxSender.SelectionStart;
            textBoxSender.Text = Regex.Replace(textBoxSender.Text, "[^0-9]", "");
            textBoxSender.SelectionStart = cursorPosition;
        }
    }
}