using System;
using System.IO;
using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// セーブデータのファイル I/O。JsonUtility で JSON シリアライズし、
    /// <see cref="Application.persistentDataPath"/>/save/slot0.json に書き出す。
    /// tempfile + atomic rename で書き込み途中の電源断耐性を持たせる。
    /// MVP は単一スロット。将来複数スロットに拡張する余地は残してある。
    /// </summary>
    public static class SaveSystem
    {
        const string SaveDir = "save";
        const string SlotFileName = "slot0.json";

        static string SaveDirFull => Path.Combine(Application.persistentDataPath, SaveDir);
        static string FullPath => Path.Combine(SaveDirFull, SlotFileName);
        static string TempPath => FullPath + ".tmp";

        /// <summary>SaveData を JSON に直列化してディスクに書き出す。成功時 true。</summary>
        public static bool Save(SaveData data)
        {
            if (data == null) return false;
            try
            {
                Directory.CreateDirectory(SaveDirFull);
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(TempPath, json);
                if (File.Exists(FullPath)) File.Delete(FullPath);
                File.Move(TempPath, FullPath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Save 失敗: {e.Message}");
                return false;
            }
        }

        /// <summary>ディスクから SaveData をロードする。ファイルが無いか壊れていれば false。</summary>
        public static bool TryLoad(out SaveData data)
        {
            data = null;
            if (!File.Exists(FullPath)) return false;
            try
            {
                string json = File.ReadAllText(FullPath);
                data = JsonUtility.FromJson<SaveData>(json);
                return data != null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Load 失敗: {e.Message}");
                return false;
            }
        }

        /// <summary>セーブファイルが存在するか。</summary>
        public static bool HasSave() => File.Exists(FullPath);

        /// <summary>セーブファイルを削除する。デバッグ用 / 新規ゲーム用。</summary>
        public static void Delete()
        {
            if (File.Exists(FullPath)) File.Delete(FullPath);
        }
    }
}
