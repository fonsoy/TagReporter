using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Concurrent;
using System.Drawing;
using System.Text.RegularExpressions;
using TagLib;

namespace TagReporter
{
    class Program
    {
        const int mandatoryArgs = 2;
        const char optionStart = '/';
        const char optionDelimiter = ':';
        static string folder;
        static int maxRecursionDepth;
        static int numberOfFiles;
        static HashSet<string> acceptedExtensions = new HashSet<string>{".mp3", ".flac", ".aac", ".ogg", ".aiff", ".ape", ".wav"};
        static Dictionary<string, string> genreMap = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) { { "Trip-Hop", "Trip Hop" }, { "Hip-Hop", "Hip Hop" }, { "DM BM", "Doom Black Metal" } };
        public static int FalseFileCounter { get; set; }
        public static bool CheckForMetal { get; set; }
        static void Main(string[] args)
        {    
#if DEBUG
            args = new string[2];
            args[0] = @"E:\Media\Music\Test";
            args[1] = "0";
#endif
            ParseArguments(args);

            System.IO.Directory.CreateDirectory(@"C:\temp\");

            ListAllFoldersUnder(folder, maxRecursionDepth);
            Console.WriteLine("Klaar");
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }

        public static void ParseArguments(string[] arguments)
        {
            if (arguments.Length < mandatoryArgs)
                WriteError("Not all arguments are specified");

            arguments = arguments.Take(mandatoryArgs).Where(arg => !string.IsNullOrWhiteSpace(arg)).ToArray();
            if (arguments.Where(arg => arg.StartsWith(optionStart.ToString())).Count() > 0)
                WriteError(string.Format("First {0} arguments cannot be options, see usage", mandatoryArgs));

            try
            {

                for (int i = 0; i <= mandatoryArgs; i++)
                {
                    switch (i)
                    {
                        case 0:
                            folder = arguments[i];
                            if (!Directory.Exists(folder)) 
                            {
                                WriteError("folder does not exist");
                            }
                            break;
                        case 1:
                            maxRecursionDepth = int.Parse(arguments[i]);
                            if (maxRecursionDepth == 0)
                            {
                                maxRecursionDepth = int.MaxValue;
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (InvalidCastException ex)
            {
                WriteError(string.Format("in argument \"{0}\"", ex.Message));
            }

        }
        public static void ConsoleAndLog(string message, bool writeLine = true, bool noLog = false)
        {
            if (writeLine)
            {
                Console.WriteLine(message);
            }
            else
            {
                Console.Write(message);
            }
            if (!noLog)
            {
                var writer = new StreamWriter(@"C:\temp\log.txt", true);
                writer.WriteLine(message);
                writer.Close();
            }
        }
        public static void WriteError(string err)
        {
            ConsoleAndLog("");
            ConsoleAndLog("Error: " + err);
            ConsoleAndLog("");

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
            System.Environment.Exit(0);
        }
        public static void WriteWarning(string err)
        {
            ConsoleAndLog("");
            ConsoleAndLog("Warning: " + err);
            ConsoleAndLog("");
        }
        public static void ListAllFoldersUnder(string folder, int depth)
        {
            depth--;
            foreach (string file in Directory.GetFiles(folder).Where(k => acceptedExtensions.Contains(Path.GetExtension(k))))
            {
                numberOfFiles++;
                if (numberOfFiles % 100 == 0)
                {
                    ConsoleAndLog(numberOfFiles.ToString() + " ", false);
                }

                CheckTags(file, folder);
            }
            if (depth > 0) { 
                foreach (string dir in Directory.GetDirectories(folder))
                {
                    ListAllFoldersUnder(dir, depth--);
                }
            }
        }
        public static void CheckTags(string mp3file, string folder)
        {
            try
            {
                var tagFile = TagLib.File.Create(mp3file);

                //werkt met track# titel artiest albumnaam jaar genre
                var probleem = new StringBuilder();

                try
                {
                    fixGenres(mp3file, tagFile);
                }
                catch (Exception e)
                {
                    probleem.Append(e.Message);
                }
                if(string.IsNullOrEmpty(tagFile.Tag.Title))
                    probleem.Append("   - Titel ontbreekt \n");
                if (tagFile.Tag.Year == default(uint))
                    probleem.Append("   - Jaar ontbreekt \n");
                if (tagFile.Tag.Track == default(uint))
                    probleem.Append("   - Nummer ontbreekt \n");
                if (tagFile.Tag.Album == null)
                    probleem.Append("   - Albumnaam ontbreekt \n");
                if (tagFile.Tag.Genres.Length == 0)
                    probleem.Append("   - Genre ontbreekt \n");
                if (tagFile.Tag.Performers.Length == 0)
                    probleem.Append("   - Artiest ontbreekt \n");
                if (tagFile.Tag.Pictures.Length == 0)
                {
                    probleem.Append("   - Albumart is leeg \n");
                    try
                    {
                        FixAlbumArt(mp3file, folder, tagFile);
                    }
                    catch (Exception e)
                    {
                        probleem.Append("     Er is iets misgegaan bij fixen: " + e.Message + "\n");
                    }
                }

                string filename = Path.GetFileNameWithoutExtension(mp3file);
                int tracknumber;
                if (!int.TryParse(filename.Substring(0,2), out tracknumber))
                {
                    probleem.Append("   - Filenaam tracknummer ontbreekt");
                }
                if(probleem.ToString() != "")
                {
                    WriteWarning("Tag probleem: " + mp3file + "\r\n" + probleem.ToString());
                }
                else
                {
                    FalseFileCounter++;
                }
            }
            catch (Exception e)
            {
                WriteWarning("Probleem met dit bestand: " + mp3file + "\r\nError: " + e.ToString());
            }

        }
        public static void fixGenres(string path, TagLib.File tagFile)
        {
            var genres = tagFile.Tag.Genres;
            var genresLong = String.Join("/", genres);
            string[] newGenres = genres.AsEnumerable().Select(k => (genreMap.ContainsKey(k) ? genreMap[k] : k)).ToArray();

            var newGenresLong = String.Join("/", newGenres);

            if (!newGenresLong.ToLower().Contains("metal") && CheckForMetal)
            {
                throw new Exception("    - Metal verwacht in genre");
            }

            if (newGenresLong != genresLong)
            {
                tagFile.Tag.Genres = newGenres;
                tagFile.Save();
                throw new Exception(string.Format("   - Genre(s) aangepast van \"{0}\" naar \"{1}\"", genresLong, newGenresLong));
            }
        }
        public static void FixAlbumArt(string mp3file, string path, TagLib.File tagFile)
        {
            foreach (string file in Directory.GetFiles(path))
            {
                if (file.Split('\\')[file.Split('\\').Length - 1].Equals("Folder.jpg") 
                    || file.Split('\\')[file.Split('\\').Length - 1].Equals("cover.jpg"))
                {
                    try
                    {
                        IPicture newArt = new Picture(file);
                        tagFile.Tag.Pictures = new IPicture[1] { newArt };
                        tagFile.Save();
                        return;
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }
                }
            }
            throw new Exception("Geen Folder.jpg gevonden");
        }
    }
}
