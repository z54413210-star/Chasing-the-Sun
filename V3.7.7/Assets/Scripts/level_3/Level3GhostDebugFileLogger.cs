using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class Level3GhostDebugFileLogger
{
    private const string FileName = "Level3GhostRespawnDebug.log";

    private static readonly object SyncRoot = new object();

    private static bool _initialized;
    private static string _filePath;
    private static bool _pathEchoedToConsole;

    public static string CurrentFilePath
    {
        get
        {
            EnsureInitialized();
            return _filePath;
        }
    }

    public static void Log(string message, UnityEngine.Object context = null, bool echoToConsole = false)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        EnsureInitialized();

        var line =
            $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}";

        lock (SyncRoot)
        {
            try
            {
                File.AppendAllText(_filePath, line, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[L3-GhostDebug] Failed to append log file '{_filePath}': {ex.Message}", context);
            }
        }

        if (echoToConsole)
        {
            Debug.Log(message, context);
        }
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            var basePath = Application.persistentDataPath;
            if (string.IsNullOrWhiteSpace(basePath))
            {
                basePath = Application.dataPath;
            }

            Directory.CreateDirectory(basePath);
            _filePath = Path.Combine(basePath, FileName);
            File.WriteAllText(
                _filePath,
                $"[L3-GhostDebug] Session started {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}{Environment.NewLine}",
                Encoding.UTF8);

            _initialized = true;

            if (!_pathEchoedToConsole)
            {
                Debug.Log($"[L3-GhostDebug] Writing file logs to: {_filePath}");
                _pathEchoedToConsole = true;
            }
        }
    }
}
