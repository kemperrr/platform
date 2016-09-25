﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GTA;
using GTA.UI;
using GTANetwork.Javascript;
using GTANetwork.Util;
using GTANetworkShared;

namespace GTANetwork.Networking
{
    public static class DownloadManager
    {
        private static ScriptCollection PendingScripts = new ScriptCollection() { ClientsideScripts = new List<ClientsideScript>()};

        public static Dictionary<string, string> FileIntegrity = new Dictionary<string, string>();

        private static string[] _allowedFiletypes = new[]
        {
            "audio/basic",
            "audio/mid",
            "audio/wav",
            "image/gif",
            "image/jpeg",
            "image/pjpeg",
            "image/png",
            "image/x-png",
            "image/tiff",
            "image/bmp",
            "video/avi",
            "video/mpeg",
            "audio/mpeg",
            "text/plain",
            "application/x-font-ttf",
        };

        public static bool ValidateExternalMods(List<string> whitelist)
        {
            // Enumerate modules?

            foreach (var asiMod in Directory.GetFiles("/", "*.asi"))
            {
                var filename = Path.GetFileName(asiMod);
                if (filename != null &&
                    (filename.ToLower() == "scripthookvdotnet.asi" ||
                     filename.ToLower() == "scripthookv.asi"))
                {
                    continue;
                }

                if (!whitelist.Contains(HashFile(asiMod))) return false;
            }

            return true;
        }

        public static string HashFile(string path)
        {
            byte[] myData;

            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(path))
            {
                myData = md5.ComputeHash(stream);
            }

            return myData.Select(byt => byt.ToString("x2")).Aggregate((left, right) => left + right);
        }

        public static bool CheckFileIntegrity()
        {
            foreach (var pair in FileIntegrity)
            {
                byte[] myData;

                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(FileTransferId._DOWNLOADFOLDER_ + pair.Key))
                {
                    myData = md5.ComputeHash(stream);
                }

                string hash = myData.Select(byt => byt.ToString("x2")).Aggregate((left, right) => left + right);

                LogManager.DebugLog("GOD: " + pair.Value + " == " + hash);

                if (hash != pair.Value) return false;
            }

            return true;
        }

        private static FileTransferId CurrentFile;
        public static bool StartDownload(int id, string path, FileType type, int len, string md5hash, string resource)
        {
            if (CurrentFile != null)
            {
                LogManager.DebugLog("CurrentFile isn't null -- " + CurrentFile.Type + " " + CurrentFile.Filename);
                return false;
            }

            if ((type == FileType.Normal || type == FileType.Script) && Directory.Exists(FileTransferId._DOWNLOADFOLDER_ + path.Replace(Path.GetFileName(path), "")) &&
                File.Exists(FileTransferId._DOWNLOADFOLDER_ + path))
            {
                byte[] myData;

                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(FileTransferId._DOWNLOADFOLDER_ + path))
                {
                    myData = md5.ComputeHash(stream);
                }

                string hash = myData.Select(byt => byt.ToString("x2")).Aggregate((left, right) => left + right);

                FileIntegrity.Set(path, md5hash);
                
                if (hash == md5hash)
                {
                    if (type == FileType.Script)
                    {
                        PendingScripts.ClientsideScripts.Add(LoadScript(path, resource, File.ReadAllText(FileTransferId._DOWNLOADFOLDER_ + path)));
                    }

                    LogManager.DebugLog("HASH MATCHES, RETURNING FALSE");
                    return false;
                }
            }

            CurrentFile = new FileTransferId(id, path, type, len, resource);
            return true;
        }

        public static ClientsideScript LoadScript(string file, string resource, string script)
        {
            var csScript = new ClientsideScript();

            csScript.Filename = Path.GetFileNameWithoutExtension(file)?.Replace('.', '_');
            csScript.ResourceParent = resource;
            csScript.Script = script;

            return csScript;
        }

        public static void Cancel()
        {
            CurrentFile = null;
        }

        public static void DownloadPart(int id, byte[] bytes)
        {
            if (CurrentFile == null || CurrentFile.Id != id)
            {
                return;
            }
            
            CurrentFile.Write(bytes);
            Screen.ShowSubtitle("Downloading " +
                            ((CurrentFile.Type == FileType.Normal || CurrentFile.Type == FileType.Script)
                                ? CurrentFile.Filename
                                : CurrentFile.Type.ToString()) + ": " +
                            (CurrentFile.DataWritten/(float) CurrentFile.Length).ToString("P"));
        }

        public static void End(int id)
        {
            if (CurrentFile == null || CurrentFile.Id != id)
            {
                Util.Util.SafeNotify($"END Channel mismatch! We have {CurrentFile?.Id} and supplied was {id}");
                return;
            }

            try
            {
                if (CurrentFile.Type == FileType.Map)
                {
                    var obj = Main.DeserializeBinary<ServerMap>(CurrentFile.Data.ToArray()) as ServerMap;
                    if (obj == null)
                    {
                        Util.Util.SafeNotify("ERROR DOWNLOADING MAP: NULL");
                    }
                    else
                    {
                        Main.AddMap(obj);
                    }
                }
                else if (CurrentFile.Type == FileType.Script)
                {
                    try
                    {
                        var scriptText = Encoding.UTF8.GetString(CurrentFile.Data.ToArray());
                        var newScript = LoadScript(CurrentFile.Filename, CurrentFile.Resource, scriptText);
                        PendingScripts.ClientsideScripts.Add(newScript);
                    }
                    catch (ArgumentException)
                    {
                        CurrentFile.Dispose();
                        if (File.Exists(CurrentFile.FilePath))
                        {
                            try { File.Delete(CurrentFile.FilePath); }
                            catch { }
                        }
                    }
                }
                else if (CurrentFile.Type == FileType.EndOfTransfer)
                {
                    Main.StartClientsideScripts(PendingScripts);
                    PendingScripts.ClientsideScripts.Clear();

                    if (Main.JustJoinedServer)
                    {
                        World.RenderingCamera = null;
                        Main.MainMenu.TemporarilyHidden = false;
                        Main.MainMenu.Visible = false;
                        Main.JustJoinedServer = false;
                    }

                    Main.InvokeFinishedDownload();
                }
                else if (CurrentFile.Type == FileType.CustomData)
                {
                    string data = Encoding.UTF8.GetString(CurrentFile.Data.ToArray());

                    JavascriptHook.InvokeCustomDataReceived(CurrentFile.Resource, data);
                }
            }
            finally
            {
                CurrentFile.Dispose();

                if (CurrentFile.Type == FileType.Normal && File.Exists(CurrentFile.FilePath))
                {
                    var mime = MimeTypes.GetMimeType(File.ReadAllBytes(CurrentFile.FilePath), CurrentFile.FilePath);

                    if (!_allowedFiletypes.Contains(mime))
                    {
                        try { File.Delete(CurrentFile.FilePath); }
                        catch { }

                        Screen.ShowNotification("Disallowed file type: " + mime + "~n~" + CurrentFile.Filename);
                    }
                }

                CurrentFile = null;
            }
        }
    }

    public class FileTransferId : IDisposable
    {
        public static string _DOWNLOADFOLDER_ = Main.GTANInstallDir + "\\resources\\";

        public int Id { get; set; }
        public string Filename { get; set; }
        public FileType Type { get; set; }
        public FileStream Stream { get; set; }
        public int Length { get; set; }
        public int DataWritten { get; set; }
        public List<byte> Data { get; set; }
        public string Resource { get; set; }
        public string FilePath { get; set; }

        public FileTransferId(int id, string name, FileType type, int len, string resource)
        {
            Id = id;
            Filename = name;
            Type = type;
            Length = len;
            Resource = resource;

            FilePath = _DOWNLOADFOLDER_ + name;

            if ((type == FileType.Normal || type == FileType.Script) && name != null)
            {
                if (!Directory.Exists(_DOWNLOADFOLDER_ + name.Replace(Path.GetFileName(name), "")))
                    Directory.CreateDirectory(_DOWNLOADFOLDER_ + name.Replace(Path.GetFileName(name), ""));
                Stream = new FileStream(_DOWNLOADFOLDER_ + name,
                    File.Exists(_DOWNLOADFOLDER_ + name) ? FileMode.Truncate : FileMode.CreateNew);
            }

            if (type != FileType.Normal)
            {
                Data = new List<byte>();
            }
        }

        public void Write(byte[] data)
        {
            if (Stream != null)
            {
                Stream.Write(data, 0, data.Length);
            }

            if (Data != null)
            {
                Data.AddRange(data);
            }

            DataWritten += data.Length;
        }

        public void Dispose()
        {
            if (Stream != null)
            {
                Stream.Close();
                Stream.Dispose();
            }

            Stream = null;
        }
    }
}