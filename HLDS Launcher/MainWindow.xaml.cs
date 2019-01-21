using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HLDS_Launcher
{
    public partial class MainWindow : Window
    {
        string _game;
        string _map;
        string _maxPlayers;
        string _port;
        string _vac;

        bool stopServer = false;
        bool writeLog = false;

        List<Scripts.Game> games = new List<Scripts.Game>();
        List<string> gameFolders = new List<string>();
        List<string> gameNames = new List<string>();

        Process hlds;
        ProcessPriorityClass priority;

        public MainWindow()
        {
            CheckEXE();
            InitializeComponent();
            LoadGames();
            LoadUserValues();
        }

        // Check if HLDS.exe exists in the same folder.
        private void CheckEXE()
        {
            if (!File.Exists("HLDS.exe"))
            {
                System.Windows.MessageBox.Show("HLDS.exe not found. Launcher must be in the same directory.", "HLDS Launcher", MessageBoxButton.OK, 
                    MessageBoxImage.Error);
                ExitApplication();
            }
        }

        // Load user settings.
        private void LoadUserValues()
        {
            gameList.SelectedIndex = Properties.Settings.Default.gameNameIndex;
            maxPlayers.Text = Properties.Settings.Default.maxPlayers;
            port.Text = Properties.Settings.Default.port;
            priorityList.SelectedIndex = Properties.Settings.Default.priorityIndex;
            randomMapcycle.IsChecked = Properties.Settings.Default.randomMapcycle;
            secureVAC.IsChecked = Properties.Settings.Default.vac;
            autoRestart.IsChecked = Properties.Settings.Default.autoRestart;
            enableLog.IsChecked = Properties.Settings.Default.enableLogging;
            mapsList.SelectedIndex = Properties.Settings.Default.gameMapIndex;
            enableLog.IsEnabled = (bool)autoRestart.IsChecked;
        }

        // Save user settings.
        private void SaveUserSettings()
        {
            Properties.Settings.Default.gameNameIndex = gameList.SelectedIndex;
            Properties.Settings.Default.gameMapIndex = mapsList.SelectedIndex;
            Properties.Settings.Default.maxPlayers = maxPlayers.Text;
            Properties.Settings.Default.port = port.Text;
            Properties.Settings.Default.priorityIndex = priorityList.SelectedIndex;
            Properties.Settings.Default.randomMapcycle = (bool)randomMapcycle.IsChecked;
            Properties.Settings.Default.vac = (bool)secureVAC.IsChecked;
            Properties.Settings.Default.autoRestart = (bool)autoRestart.IsChecked;
            Properties.Settings.Default.enableLogging = (bool)enableLog.IsChecked;
            Properties.Settings.Default.Save();
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
            gameList.SelectedIndex = 0;
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
            game.LoadMaps();

            games.Add(game);
            gameNames.Add(game.Name);
            gameFolders.Add(game.ShortName);
        }

        // Randomize mapcycle.
        private void RandomMapCycle()
        {
            List<string> mapList = new List<string>();
            Random random = new Random();

            mapList.AddRange(games[gameList.SelectedIndex].Maps);
            mapList.RemoveAt(0);
            StreamWriter sw = new StreamWriter(".\\" + games[gameList.SelectedIndex].ShortName + "\\mapcycle.txt");

            while (mapList.Count > 0)
            {
                int i = random.Next(0, mapList.Count);
                sw.WriteLine(mapList[i]);
                mapList.RemoveAt(i);
            }
            sw.Close();
        }

        // Start hlds.exe
        private void StartHLDS()
        {
            hlds = new Process();
            hlds.StartInfo.FileName = "hlds.exe";
            hlds.StartInfo.Arguments = "-console" + _game + _maxPlayers + _port + _vac + _map;
            hlds.EnableRaisingEvents = true;
            hlds.Exited += new EventHandler(Hlds_Exited);
            hlds.Start();
            hlds.PriorityClass = priority;
            WriteToLog("Server started.");
        }

        // Stop server and restore UI.
        private void StopServer()
        {
            stopServer = true;
            gameList.IsEnabled = true;
            mapsList.IsEnabled = true;
            maxPlayers.IsEnabled = true;
            port.IsEnabled = true;
            secureVAC.IsEnabled = true;
            autoRestart.IsEnabled = true;
            enableLog.IsEnabled = (bool)autoRestart.IsChecked;

            buttonStart.IsEnabled = true;
            buttonStart.Visibility = Visibility.Visible;

            buttonStop.IsEnabled = false;
            buttonStop.Visibility = Visibility.Hidden;
            WriteToLog("Server stopped by user.");
        }

        private void ExitApplication()
        {
            Application.Current.Shutdown();
        }

        // HLDS exit event handler. Restart server if process exited.
        private void Hlds_Exited(object sender, EventArgs e)
        {
            if (stopServer == false)
            {
                WriteToLog("Server exited or crash. Restarting server..");
                StartHLDS();
            }
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

        // Textbox allow only numbers.
        private void TextInputOnlyNumbers(object sender, TextCompositionEventArgs e) // Text Input event.
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
        private void TextOnlyNumbers(object sender, KeyEventArgs e) // KeyDown event.
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        // Max players value cannot be more than 32.
        private void MaxPlayers_TextChanged(object sender, TextChangedEventArgs e)
        {
            int.TryParse(maxPlayers.Text, out int i);
            if (i > 32)
            {
                maxPlayers.Text = "32";
            }
        }

        // Select/change game in combobox.
        private void GameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gameList.Items.Count > 0)
            {
                mapsList.ItemsSource = games[gameList.SelectedIndex].Maps;
                mapsList.SelectedIndex = 0;
            }            
        }

        // Auto-restart/Enable Logging checkbox states.
        private void AutoRestart_Checked(object sender, RoutedEventArgs e)
        {
            enableLog.IsEnabled = (bool)autoRestart.IsChecked;
        }

        // Button public IP.
        private void ButtonGetIP_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://ipv4.whatismyv6.com/");
        }

        // Button edit server.cfg.
        private void ButtonEditServerCFG_Click(object sender, RoutedEventArgs e)
        {
            string serverFilePath = ".\\" + games[gameList.SelectedIndex].ShortName + "\\server.cfg";
            if (File.Exists(serverFilePath))
            {
                Process.Start(serverFilePath);
            }
            else
            {
                System.Windows.MessageBox.Show("File \"server.cfg\" not found in path: " + serverFilePath, "HLDS Launcher", MessageBoxButton.OK, 
                    MessageBoxImage.Asterisk);
            }
        }

        // Button Start Server
        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            stopServer = false;
            _game = " -game " + games[gameList.SelectedIndex].ShortName;
            _map = " +map " + mapsList.SelectionBoxItem;
            _maxPlayers = " +maxplayers " + maxPlayers.Text;
            _port = " +port " + port.Text;
            _vac = secureVAC.IsChecked == true ? "" : " -insecure ";

            // If selected map is "Random Map", choose a map randomly.
            if (mapsList.SelectedIndex == 0)
            {
                Random rand = new Random();
                _map = " +map " + mapsList.Items[rand.Next(1, mapsList.Items.Count)].ToString();
            }
            // If map name starts with "-", create a cfg file and change command to exec. 
            // If this isn't done, the server will not read the map command properly and will not load the map.
            if (mapsList.SelectionBoxItem.ToString().StartsWith("-"))
            {
                TextWriter tw = new StreamWriter("hldslauncher_loadmap.cfg");
                tw.WriteLine("map " + mapsList.SelectionBoxItem.ToString());
                tw.Close();
                _map = "+exec hldslauncher_loadmap.cfg";
            }
            // Set process priority.
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
            // Random Mapcycle
            if (randomMapcycle.IsChecked == true)
            {
                RandomMapCycle();
            }
            // Check if Launcher should write to log.
            if (autoRestart.IsChecked == true && enableLog.IsChecked == true)
            {
                writeLog = true;
            }
            else
            {
                writeLog = false;
            }
            // Save settings and start server.
            SaveUserSettings();
            StartHLDS();

            // If auto restart is ON, block all fields and show "Stop" button. Else exit launcher.
            if (autoRestart.IsChecked == true)
            {
                gameList.IsEnabled = false;
                mapsList.IsEnabled = false;
                maxPlayers.IsEnabled = false;
                port.IsEnabled = false;
                secureVAC.IsEnabled = false;
                autoRestart.IsEnabled = false;
                enableLog.IsEnabled = false;

                buttonStart.IsEnabled = false;
                buttonStart.Visibility = Visibility.Hidden;

                buttonStop.IsEnabled = true;
                buttonStop.Visibility = Visibility.Visible;
            }
            else
            {
                ExitApplication();
            }
        }

        // Button stop server.
        private void ButtonStop_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Do you want to stop the server?", "Stop server", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                MessageBoxResult.Yes)
            {
                StopServer();
                hlds.CloseMainWindow();
                hlds.Close();
                hlds.Refresh();
            }
        }

        // Button exit
        private void ButtonExit_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }
    }
}