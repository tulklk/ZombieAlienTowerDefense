using System;
using System.IO;
using UnityEngine;

namespace AlienDefense.Save
{
    /// <summary>Raw file I/O for the save profile: atomic write (temp -> backup -> replace) and read-with-fallback.
    /// Contains no serialization/validation/migration logic — SaveService owns that.</summary>
    public sealed class SaveFileRepository
    {
        private readonly string _directory;
        private readonly string _mainPath;
        private readonly string _tempPath;
        private readonly string _backupPath;

        public SaveFileRepository(string directory = null)
        {
            _directory = string.IsNullOrEmpty(directory) ? Application.persistentDataPath : directory;
            _mainPath = Path.Combine(_directory, SaveConstants.ProfileFileName);
            _tempPath = Path.Combine(_directory, SaveConstants.TempFileName);
            _backupPath = Path.Combine(_directory, SaveConstants.BackupFileName);
        }

        public bool TryReadMain(out string json)
        {
            return TryReadFile(_mainPath, out json);
        }

        public bool TryReadBackup(out string json)
        {
            return TryReadFile(_backupPath, out json);
        }

        /// <summary>If a previous write was interrupted after the temp file was written but before it replaced the
        /// main file (main missing, temp present), finishes that write. Call once before the first load.</summary>
        public void RecoverInterruptedWrite()
        {
            try
            {
                if (!File.Exists(_mainPath) && File.Exists(_tempPath))
                {
                    Debug.LogWarning("[SaveFileRepository] Found an orphaned temp save with no main save; promoting it to recover from an interrupted write.");
                    File.Move(_tempPath, _mainPath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveFileRepository] Failed to recover interrupted write: {exception.Message}");
            }
        }

        public SaveWriteResult WriteAtomic(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return SaveWriteResult.Fail("Refused to write an empty save payload.");
            }

            try
            {
                Directory.CreateDirectory(_directory);

                File.WriteAllText(_tempPath, json);

                if (File.Exists(_mainPath))
                {
                    File.Copy(_mainPath, _backupPath, overwrite: true);
                    File.Delete(_mainPath);
                }

                File.Move(_tempPath, _mainPath);

                if (!File.Exists(_mainPath))
                {
                    return SaveWriteResult.Fail("Main save file missing immediately after write.");
                }

                return SaveWriteResult.Ok();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveFileRepository] Atomic write failed: {exception.Message}");
                return SaveWriteResult.Fail(exception.Message);
            }
        }

        /// <summary>Development-only: overwrites the main save with garbage, to manually test backup recovery.
        /// Callers are responsible for gating this to non-release builds (see SaveDebugControls).</summary>
        public void DebugCorruptMainFile()
        {
            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllText(_mainPath, "{ this is not valid json");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveFileRepository] DebugCorruptMainFile failed: {exception.Message}");
            }
        }

        /// <summary>Development-only: deletes main/backup/temp so the next load creates a fresh default profile.
        /// Callers are responsible for gating this to non-release builds (see SaveDebugControls).</summary>
        public void DebugDeleteAllFiles()
        {
            try
            {
                DeleteIfExists(_mainPath);
                DeleteIfExists(_backupPath);
                DeleteIfExists(_tempPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveFileRepository] DebugDeleteAllFiles failed: {exception.Message}");
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static bool TryReadFile(string path, out string json)
        {
            json = null;

            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                string content = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return false;
                }

                json = content;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveFileRepository] Failed to read '{path}': {exception.Message}");
                return false;
            }
        }
    }
}
