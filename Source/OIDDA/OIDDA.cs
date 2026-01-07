using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FlaxEditor.Content.Settings;
using FlaxEngine;
using FlaxEngine.Json;
using FlaxEngine.Utilities;

namespace OIDDA;

/// <summary>
/// OIDDA class.
/// </summary>
public static class OIDDA
{
    static string _folderName;
    static bool _useEncryption;
    static bool _useHash;
    static string _encryptPassword;
    static string _folderDirectoryPath;
    static string _saveNameFile;
    static string _saveNameFilepath;
    static bool _verboseLogging;

    public static string LocalPath
    {
        get
        {
            #if FLAX_EDITOR
            var settings = GameSettings.Load();
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), settings.CompanyName, settings.ProductName);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
            #else
             if (!Directory.Exists(Globals.ProductLocalFolder))
                Directory.CreateDirectory(Globals.ProductLocalFolder);
            return Globals.ProductLocalFolder;
            #endif
        }
    }

    static Dictionary<string, string> _saveData;
    static CancellationTokenSource _cancellationToken;

    public static event Action SavedData, SaveDataFailed, LoadedData, LoadedDataFailed;

    internal static void Initialize(OIDDASettings settings)
    {
        Initialize(settings.FolderName, settings.OIDDASaveName , settings.VerboseLogging, settings.UseHash, settings.UseEncryption, settings.Password);
    }

    internal static void Initialize(string FolderName, string SaveFileName , bool VerboseLogging = false, bool UseHash = true, bool Encript = false, string Password = null)
    {
        _folderDirectoryPath = Path.Combine(LocalPath, _folderName = FolderName);
        if(!Directory.Exists(_folderDirectoryPath)) Directory.CreateDirectory(_folderDirectoryPath);
        _saveNameFilepath = Path.Combine(_folderDirectoryPath,$"{_saveNameFile = SaveFileName}.save");
        _useEncryption = Encript;
        _useHash = UseHash;
        _encryptPassword = Password;
        #if BUILD_RELEASE
        _verboseLogging = false;
        #else
        _verboseLogging = VerboseLogging;
        #endif
        _cancellationToken = new();
        _saveData = new();
    }

    public static async Task Deinitialize()
    {
        if (_cancellationToken != null) await _cancellationToken.CancelAsync();

        // Clear cached data
        _saveData?.Clear();
        _saveData = null;
        _cancellationToken?.Dispose();
    }

}
