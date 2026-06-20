using System;
using System.Collections.Generic;
using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// セーブデータの全状態を保持する [Serializable] DTO。
    /// JsonUtility で 1 ファイルに直列化される。バージョン互換は version フィールドで管理予定。
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public double savedAtUnix;

        public List<string> progressFlags = new();
        public List<InventorySlotRecord> inventory = new();
        public string selectedBeaconItemId;
        public List<PlacedBeaconRecord> placedBeacons = new();
    }

    [Serializable]
    public struct InventorySlotRecord
    {
        public string itemKindId;
        public int count;
    }

    [Serializable]
    public struct PlacedBeaconRecord
    {
        public string itemKindId;   // BeaconKind を ItemKind.Id で逆引きする
        public Vector3 position;
        public Quaternion rotation;
    }
}
