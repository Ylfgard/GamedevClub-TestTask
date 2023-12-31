using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System;

using Random = UnityEngine.Random;

namespace MapSystem.TileLayer
{
    internal class TilesGenerator : MonoBehaviour
    {
        public Action<Dictionary<int, List<Vector3Int>>> TileLayerGenerated;
        public Action<int, int> GenerationStarted;
        public Action<int, int, int> SendCost;

        [SerializeField] private MapDataGenerator _dataGenerator;
        [SerializeField] private TileKeeper _tileKeeper;
        [SerializeField] private Tilemap _tilemap;
        private int _width, _hight, _bordersSize;
        private int[,] _mapWeights;
        private List<Vector3Int> _notSpawnedTiles;

        private void Start()
        {
            GenerateNewMap();
        }

        [ContextMenu("Generate New Map")]
        private void GenerateNewMap()
        {
            Random.InitState(_dataGenerator.Seed);
            StartMapGeneration();
        }

        private void StartMapGeneration()
        {
            _width = _dataGenerator.Width;
            _hight = _dataGenerator.Hight;
            _bordersSize = _dataGenerator.BordersSize;
            GenerationStarted?.Invoke(_width, _hight);

            _tilemap.ClearAllTiles();
            _notSpawnedTiles = new List<Vector3Int>();
            _mapWeights = new int[_width + _bordersSize * 2, _hight + _bordersSize * 2];
            
            for (int x = -_bordersSize; x < _width + _bordersSize; x++)
            {
                for (int y = -_bordersSize; y < _hight + _bordersSize; y++)
                {
                    _mapWeights[x + _bordersSize, y + _bordersSize] = _dataGenerator.GetTileWeight(x, y);
                    _notSpawnedTiles.Add(new Vector3Int(x, y));
                }
            }

            Dictionary<int, List<Vector3Int>> freeTiles = new Dictionary<int, List<Vector3Int>>();
            GenerateTiles(true, ref freeTiles);
        }

        private void GenerateTiles(bool firstRun, ref Dictionary<int, List<Vector3Int>> freeTiles)
        {
            int x, y, weight;
            List<Vector3Int> restOfNotSpawnedTiles = new List<Vector3Int>();

            foreach (var tile in _notSpawnedTiles)
            {
                x = tile.x;
                y = tile.y;
                Vector3Int pos = new Vector3Int(x, y, 0);
                x += _bordersSize;
                y += _bordersSize;
                weight = _tileKeeper.TryGetTileMaxWeight(_mapWeights[x, y]);

                bool leftSame = x <= 0;
                bool rightSame = x >= _width - 1;
                bool bottomSame = y <= 0;
                bool topSame = y >= _hight - 1;
                bool topLeftSame = leftSame || topSame;
                bool topRightSame = rightSame || topSame;
                bool bottomLeftSame = leftSame || bottomSame;
                bool bottomRightSame = rightSame || bottomSame;

                if (leftSame == false) leftSame = weight >= _mapWeights[x - 1, y];
                if (rightSame == false) rightSame = weight >= _mapWeights[x + 1, y];
                if (bottomSame == false) bottomSame = weight >= _mapWeights[x, y - 1];
                if (topSame == false) topSame = weight >= _mapWeights[x, y + 1];
                if (topLeftSame == false) topLeftSame = weight >= _mapWeights[x - 1, y + 1];
                if (topRightSame == false) topRightSame = weight >= _mapWeights[x + 1, y + 1];
                if (bottomLeftSame == false) bottomLeftSame = weight >= _mapWeights[x - 1, y - 1];
                if (bottomRightSame == false) bottomRightSame = weight >= _mapWeights[x + 1, y - 1];

                ChoiceRules choiceRules = new ChoiceRules(topLeftSame, topSame, topRightSame, leftSame,
                    rightSame, bottomLeftSame, bottomSame, bottomRightSame);
                TileData tileData = new TileData(weight, firstRun, choiceRules);
                if (_tileKeeper.TryGetTile(tileData, out bool weightChanged))
                {
                    _tilemap.SetTile(pos, tileData.Tile);
                    if (x >= _bordersSize && y >= _bordersSize &&
                        x < _width + _bordersSize && y < _hight + _bordersSize)
                    {
                        SendCost?.Invoke(x - _bordersSize, y - _bordersSize, tileData.Cost);
                        if (weightChanged)
                        {
                            _mapWeights[x, y] = tileData.Weight;
                        
                            if (freeTiles.TryGetValue(_mapWeights[x, y], out var tiles))
                            {
                                tiles.Add(tile);
                            }
                            else
                            {
                                List<Vector3Int> newTiles = new List<Vector3Int>();
                                newTiles.Add(tile);
                                freeTiles.Add(tileData.Weight, newTiles);
                            }
                        }
                    }
                }
                else
                {
                    restOfNotSpawnedTiles.Add(tile);
                }
            }

            _notSpawnedTiles = restOfNotSpawnedTiles;
            if (firstRun)
                GenerateTiles(false, ref freeTiles);
            else
                TileLayerGenerated?.Invoke(freeTiles);
        }
    }
}