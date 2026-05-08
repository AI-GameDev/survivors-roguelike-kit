using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class MapManager : MonoBehaviour
    {
        [SerializeField] private GeneratorMapChannelSO mGeneratorMapChannelSo;
        [SerializeField] private GameObject mProp;
        [SerializeField] private Material mMapMaterial;
        
        private float mTileSize = 1f;

        private void OnEnable()
        {
            mGeneratorMapChannelSo.OnEventRaised += GeneratorMap;
        }

        private void OnDisable()
        {
            mGeneratorMapChannelSo.OnEventRaised -= GeneratorMap;
        }

        private void GeneratorMap(MapConfigSO _config, Transform _parent)
        {
            CreateMap(_config, _parent);
            SpawnProp(_config);
        }

        private void CreateMap(MapConfigSO _config, Transform _parent)
        {
            var totalWeight = 0;
            foreach (var tileData in _config.tiles) totalWeight += tileData.weight;

            var startPos = new Vector3(-_config.Width * 0.5f, -_config.Height * 0.5f, 0);

            for (var y = 0; y < _config.Height; y++)
            {
                for (var x = 0; x < _config.Width; x++)
                {
                    var position = startPos + new Vector3(x, y, 0);
                    var tile = new GameObject($"Tile_{x}_{y}", typeof(SpriteRenderer));
                    tile.transform.position = position;
                    tile.transform.parent = _parent;

                    var spriteRenderer = tile.GetComponent<SpriteRenderer>();
                    spriteRenderer.material = mMapMaterial;
                    var sprite = GetRandomTileSprite(_config);
                    spriteRenderer.sprite = sprite;
                    spriteRenderer.sortingLayerName = "Map";
                    spriteRenderer.sortingOrder = short.MinValue;
                    if (sprite != null)
                    {
                        float pixelsPerUnit = sprite.pixelsPerUnit;
                        float spriteWidth = sprite.rect.width / pixelsPerUnit;
                        float spriteHeight = sprite.rect.height / pixelsPerUnit;
                        
                        float scaleX = mTileSize / spriteWidth;
                        float scaleY = mTileSize / spriteHeight;

                        tile.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                        
                        position = startPos + new Vector3(x * mTileSize, y * mTileSize, 0);
                        tile.transform.position = position;
                    }
                }
            }
        }

        private Sprite GetRandomTileSprite(MapConfigSO _config)
        {
            var totalWeight = 0;
            foreach (var tileData in _config.tiles) totalWeight += tileData.weight;
            
            var randomWeight = Random.Range(0, totalWeight);
            var currentWeight = 0;

            foreach (var tileData in _config.tiles)
            {
                currentWeight += tileData.weight;
                if (randomWeight < currentWeight) return tileData.sprite;
            }

            return null;
        }

        public void SpawnProp(MapConfigSO _config)
        {
            var parent = new GameObject("Props");

            var count = _config.PropCount;

            var rows = Mathf.CeilToInt(Mathf.Sqrt(count));
            var cols = Mathf.CeilToInt((float)count / rows);

            float rowSpacing = (_config.Height * mTileSize) / (rows + 1);
            float colSpacing = (_config.Width * mTileSize) / (cols + 1);

            var startPos = new Vector3(-_config.Width * mTileSize * 0.5f, -_config.Height * mTileSize * 0.5f, 0);

            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < cols; j++)
                {
                    var position = startPos + new Vector3((j + 1) * colSpacing, (i + 1) * rowSpacing, 0);
                    Instantiate(mProp, position, Quaternion.identity, parent.transform);

                    count--;
                    if (count <= 0) return;
                }
            }
        }
    }
}