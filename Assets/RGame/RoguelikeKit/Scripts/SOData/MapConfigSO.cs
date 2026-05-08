#region

using System;
using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    [CreateAssetMenu(fileName = "WaveConfig", menuName = "RGame/RoguelikeKit/Map Config")]
    public class MapConfigSO : ScriptableObject
    {
        [Tooltip("Array of tiles used to generate the map. Each tile has a sprite and a weight.")]
        public TileData[] tiles;

        public int Width = 50;
        public int Height = 50;

        [Range(0, 100)] public int PropCount = 15;
    }

    [Serializable]
    public class TileData
    {
        [Tooltip("The sprite used for this tile.")]
        public Sprite sprite;

        [Tooltip("The weight of this tile for random generation. Higher weight means higher probability of being chosen.")]
        public int weight;
    }
}