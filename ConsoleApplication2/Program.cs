using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Concurrent;
using System.Drawing;
using TagLib;

namespace TagReporter
{
    class Program
    {
        static int mismatchedFiles;
        static int numberOfFiles;
        static bool checkForMetal;
        static StringBuilder log;
        static void Main(string[] args)
        {
            falseFileCounter = 0;
            string path = @"Z:\Muziek\Pop & Rock";
            //string path = @"C:\\Users\\Fritz\\Desktop\\Raketkanon";
            getCheckForMetal = false;
            log = new StringBuilder();
            log.Clear();

            ListAllFoldersUnder(path, 0);
            WriteLogFile();
            Console.Write("\nKlaar");
            Console.ReadLine();
        }
        public static void ListAllFoldersUnder(string path, int indent)
        {
            foreach(string file in Directory.GetFiles(path))
            {
                numberOfFiles++;
                if (numberOfFiles % 100 == 0)
                    Console.Write(numberOfFiles.ToString() + " ");
                if (file.Split('.').Last().Equals("jpg") || file.Split('.').Last().Equals("jpeg") || file.Split('.').Last().Equals("gif")
                    || file.Split('.').Last().Equals("txt") || file.Split('.').Last().Equals("nfo") || file.Split('.').Last().Equals("db")
                    || file.Split('.').Last().Equals("log") || file.Split('.').Last().Equals("bmp") || file.Split('.').Last().Equals("png")
                    || file.Split('.').Last().Equals("old") || file.Split('.').Last().Equals("m3u"))
                {
                    return;
                }
                CheckTags(file, path);
                
            }
            foreach (string folder in Directory.GetDirectories(path))
            {
                ListAllFoldersUnder(folder, indent + 2);
            }
        }
        public static void CheckTags(string mp3file, string path)
        {
            try
            {
                TagLib.File tagFile = TagLib.File.Create(mp3file);

                //werkt met track# titel artiest albumnaam jaar genre
                StringBuilder probleem = new StringBuilder();
                probleem.Clear();

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
                        FixAlbumArt(mp3file, path, tagFile);
                    }
                    catch (Exception e)
                    {
                        probleem.Append("     Er is iets misgegaan bij fixen: " + e.Message + "\n");
                    }
                }
                char[] splitter = { '\\' }; //controle op bestandsnaam
                string filename = mp3file.Split(splitter)[mp3file.Split(splitter).Length - 1];
                int tracknumber;
                if (!int.TryParse(filename.Substring(0,2), out tracknumber))
                {
                    probleem.Append("   - Filenaam tracknummer ontbreekt");
                }
                if(!string.IsNullOrEmpty(probleem.ToString()))
                {
                    log.Append("Tag probleem: " + mp3file);
                    log.Append(probleem.ToString());
                }
                else if (falseFileCounter < 10)
                {
                    falseFileCounter++;
                }
            }
            catch (Exception e)
            {
                log.Append("Probleem met dit bestand: \n" + mp3file + "\nError: " + e.ToString());
            }

        }
        public static void fixGenres(string path, TagLib.File tagFile)
        {
            string genres = RepairGenres(tagFile);
            string newLongGenre = genres;
                if (genres.ToLower().Contains("trip-hop"))
                {
                    //newLongGenre = genres.Replace("trip-hop", "Hip Hop");
                    newLongGenre = genres.Replace("Trip-Hop", "Trip Hop");
                    //newLongGenre = genres.Replace("Trip-hop", "Hip Hop");
                    log.Append("Vervang genre: " + genres + " voor " + newLongGenre);
                }
                else if (genres.ToLower().Contains("Hip-Hop") ||
                    genres.ToLower().Contains("hip-hop") ||
                    genres.ToLower().Contains("hip-hop"))
                {
                    //newLongGenre = genres.Replace("hip-hop", "Hip Hop");
                    newLongGenre = genres.Replace("Hip-Hop", "Hip Hop");
                    //newLongGenre = genres.Replace("Hip-hop", "Hip Hop");
                    log.Append("Vervang genre: " + genres + " voor " + newLongGenre);
                }
                else if (!genres.ToLower().Contains("metal") && getCheckForMetal)
                {
                    throw new Exception("    - Metal verwacht in genre");
                }
            
            string[] genreArray = new string[1];
            if (!newLongGenre.Equals(genres))
            {
                
                genreArray[0] = newLongGenre;
                tagFile.Tag.Genres = genreArray;
                tagFile.Save();
            }
        }//werkt alleen met 'hip-hop' en 'metal'
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
        public static string RepairGenres(TagLib.File tagFile)
        {
            if (tagFile.Tag.Genres.Length == 1)
            {
                return tagFile.Tag.Genres[0];
            }
            StringBuilder genres = new StringBuilder();
            for (int i = 0; i < tagFile.Tag.Genres.Length; i++)
            {
                genres.Append(tagFile.Tag.Genres[i].ToString());
                genres.Append('/');
            }
            string result = genres.ToString().Trim('/');
            return result;
        }
        public static void WriteLogFile()
        {
            System.IO.File.WriteAllText(@"C:\temp\log.txt", log.ToString());
        }
        public static int falseFileCounter
        {
            get { return mismatchedFiles; }
            set { mismatchedFiles = value; }
        }
        public static int getNumberOfFiles
        {
            get { return numberOfFiles; }
            set { getNumberOfFiles = value; }
        }
        public static bool getCheckForMetal
        {
            get { return checkForMetal; }
            set { checkForMetal = value; }
        }
    }
}
