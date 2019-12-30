using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HLDS_Launcher.Scripts
{
    public class Game
    {
        public List<string> Maps = new List<string>();
        public List<string> ExtraFolders = new List<string>();
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string FallbackDir { get; set; }

        public void GetExtraFolders(string[] folders)
        {
            if (folders.Length > 0)
            {
                foreach (string folder in folders)
                {
                    if (folder.Contains(ShortName + "_") && Directory.Exists(folder + "\\maps"))
                    {
                        ExtraFolders.Add(folder + "\\maps");
                    }
                }
            }
        }

        public void LoadMaps(bool loadFromFolder)
        {
            Maps.Clear();
            if (loadFromFolder == true)
            {
                GetMapsFromFolder();
            }
            else
            {
                GetMapsFromMapcycle();
            }
        }

        private void GetMapsFromFolder()
        {
            if (Directory.Exists(".\\" + ShortName + "\\maps"))
            {
                List<string> gameMaps = new List<string>();

                // Get all maps from folder and add them to the list. (.bsp files)
                string[] gameMapsMain = Directory.GetFiles(".\\" + ShortName + "\\maps", "*.bsp");
                gameMaps.AddRange(gameMapsMain);

                // If fallback directory is valid, get all maps from it and add them to the list.
                if (FallbackDir != "" && Directory.Exists(".\\" + FallbackDir + "\\maps"))
                {
                    string[] gameMapsFallback = Directory.GetFiles(".\\" + FallbackDir + "\\maps", "*.bsp");
                    gameMaps.AddRange(gameMapsFallback);
                }
                // Get custom maps
                if (ExtraFolders.Count > 0)
                {
                    foreach (string folder in ExtraFolders)
                    {
                        string[] gameMapsCustom = Directory.GetFiles(folder, "*.bsp");
                        gameMaps.AddRange(gameMapsCustom);
                    }
                }
                // Get each map's name from list and add them to maps list.
                foreach (string mapPath in gameMaps)
                {
                    string mapName = Path.GetFileNameWithoutExtension(mapPath);
                    Maps.Add(mapName);
                }
                Maps.Sort();
                Maps.Insert(0, "<Random Map>");
            }
        }

        private void GetMapsFromMapcycle()
        {
            if (File.Exists(".\\" + ShortName + "\\mapcycle.txt"))
            {
                StreamReader sr = new StreamReader(".\\" + ShortName + "\\mapcycle.txt");
                string line;
                while (!sr.EndOfStream)
                {
                    line = sr.ReadLine();

                    if (line.Length > 0 && !line.StartsWith("//") && !line.StartsWith("\\") && !line.StartsWith(";"))
                    {
                        Maps.Add(line);
                    }
                }
                Maps.Sort();
                Maps.Insert(0, "<Random Map>");
            }
            else
            {
                // If mapcycle.txt doesn't exists, get maps from maps folder instead.
                GetMapsFromFolder();
            }
        }
    }
}
