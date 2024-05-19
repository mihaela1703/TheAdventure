using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Timers; // Adaugăm biblioteca necesară pentru Timer
using Silk.NET.Maths;
using Silk.NET.SDL;
using TheAdventure.Models;
using TheAdventure.Models.Data;


namespace TheAdventure
{
    public class Engine
    {
        private readonly Dictionary<int, GameObject> _gameObjects = new();
        private readonly Dictionary<string, TileSet> _loadedTileSets = new();
        private readonly System.Timers.Timer _bombTimer; // Modificăm bombTimer ca non-nullable și îl definim ca readonly
        private static readonly Random random = new Random(); // Definim random ca static readonly
        private Level? _currentLevel;
        private PlayerObject? _player; // Definim _player ca nullable
        private readonly GameRenderer _renderer;
        private readonly Input _input;

        private DateTimeOffset _lastUpdate = DateTimeOffset.Now;
        private DateTimeOffset _lastPlayerUpdate = DateTimeOffset.Now;

        public Engine(GameRenderer renderer, Input input)
        {
            _bombTimer = new System.Timers.Timer(2500); // Inițializăm _bombTimer
            _bombTimer.Elapsed += OnTimedEvent;
            _bombTimer.AutoReset = true;
            _bombTimer.Enabled = true;

            _renderer = renderer;
            _input = input;

            // Eliminăm evenimentul OnMouseClick pentru a nu mai plasa bombe pe click
            // _input.OnMouseClick += (_, coords) => AddBomb(coords.x, coords.y);
        }

        public void InitializeWorld()
        {
            var jsonSerializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            var levelContent = File.ReadAllText(Path.Combine("Assets", "terrain.tmj"));

            var level = JsonSerializer.Deserialize<Level>(levelContent, jsonSerializerOptions);
            if (level == null) return;
            foreach (var refTileSet in level.TileSets)
            {
                var tileSetContent = File.ReadAllText(Path.Combine("Assets", refTileSet.Source));
                if (!_loadedTileSets.TryGetValue(refTileSet.Source, out var tileSet))
                {
                    tileSet = JsonSerializer.Deserialize<TileSet>(tileSetContent, jsonSerializerOptions);
                    if (tileSet != null){
                        foreach (var tile in tileSet.Tiles)
                    {
                        var internalTextureId = _renderer.LoadTexture(Path.Combine("Assets", tile.Image), out _);
                        tile.InternalTextureId = internalTextureId;
                    }

                    _loadedTileSets[refTileSet.Source] = tileSet;
                    }
                    
                }
                if (tileSet != null){
                refTileSet.Set = tileSet;
                }
            }

            _currentLevel = level;
            var spriteSheet = SpriteSheet.LoadSpriteSheet("player.json", "Assets", _renderer);
            if(spriteSheet != null){
                _player = new PlayerObject(spriteSheet, 100, 100);
            }
            _renderer.SetWorldBounds(new Rectangle<int>(0, 0, _currentLevel.Width * _currentLevel.TileWidth,
                _currentLevel.Height * _currentLevel.TileHeight));
        }

        public void ProcessFrame()
        {
            var currentTime = DateTimeOffset.Now;
            var secsSinceLastFrame = (currentTime - _lastUpdate).TotalSeconds;
            _lastUpdate = currentTime;

            bool up = _input.IsUpPressed();
            bool down = _input.IsDownPressed();
            bool left = _input.IsLeftPressed();
            bool right = _input.IsRightPressed();
            bool isAttacking = _input.IsKeyAPressed();
            bool addBomb = _input.IsKeyBPressed();

            if(_player != null) // Verificăm dacă _player nu este null
            {
                if(isAttacking)
                {
                    var dir = up ? 1: 0;
                    dir += down? 1 : 0;
                    dir += left? 1: 0;
                    dir += right ? 1 : 0;
                    if(dir <= 1){
                        _player.Attack(up, down, left, right);
                    }
                    else{
                        isAttacking = false;
                    }
                }
                if (!isAttacking && _currentLevel != null)
                {
                    _player.UpdatePlayerPosition(up ? 1.0 : 0.0, down ? 1.0 : 0.0, left ? 1.0 : 0.0, right ? 1.0 : 0.0,
                        _currentLevel.Width * _currentLevel.TileWidth, _currentLevel.Height * _currentLevel.TileHeight,
                        secsSinceLastFrame);
                }
            }
            var itemsToRemove = new List<int>();
            itemsToRemove.AddRange(GetAllTemporaryGameObjects().Where(gameObject => gameObject.IsExpired)
                .Select(gameObject => gameObject.Id).ToList());

            foreach (var gameObjectId in itemsToRemove)
            {
                var gameObject = _gameObjects[gameObjectId];
                if(gameObject is TemporaryGameObject){
                    var tempObject = (TemporaryGameObject)gameObject;
                    if(_player != null) // Verificăm dacă _player nu este null
                    {
                        var deltaX = Math.Abs(_player.Position.X - tempObject.Position.X);
                        var deltaY = Math.Abs(_player.Position.Y - tempObject.Position.Y);
                        if(deltaX < 32 && deltaY < 32){
                            _player.GameOver();
                        }
                    }
                }
                _gameObjects.Remove(gameObjectId);
            }
        }

        public void RenderFrame()
        {
            _renderer.SetDrawColor(0, 0, 0, 255);
            _renderer.ClearScreen();
            
            if(_player != null) // Verificăm dacă _player nu este null
            {
                _renderer.CameraLookAt(_player.Position.X, _player.Position.Y);
            }

            RenderTerrain();
            RenderAllObjects();

            _renderer.PresentFrame();
        }

        private Tile? GetTile(int id)
        {
            if (_currentLevel == null) return null;
            foreach (var tileSet in _currentLevel.TileSets)
            {
                foreach (var tile in tileSet.Set.Tiles)
                {
                    if (tile.Id == id)
                    {
                        return tile;
                    }
                }
            }

            return null;
        }

        private void OnTimedEvent(object? source, ElapsedEventArgs e) // Modificăm parametrul source ca nullable
        {
            if(source != null) // Verificăm dacă source nu este null
            {
                int numberOfBombs = random.Next(2, 5); 
                for (int i = 0; i < numberOfBombs; i++)
                {
                    PlaceRandomBomb();
                }
            }
        }

        private void PlaceRandomBomb()
        {
            if(_player != null) // Verificăm dacă _player nu este null
            {
                int characterX = _player.Position.X;
                int characterY = _player.Position.Y;

                int offsetX = random.Next(-50, 51); // Random offset între -50 și 50
                int offsetY = random.Next(-50, 51); // Random offset între -50 și 50

                int bombX = characterX + offsetX;
                int bombY = characterY + offsetY;

                AddBomb(bombX, bombY, false);
            }
        }

        private void RenderTerrain()
        {
            if (_currentLevel == null) return;
            for (var layer = 0; layer < _currentLevel.Layers.Length; ++layer)
            {
                var cLayer = _currentLevel.Layers[layer];

                for (var i = 0; i < _currentLevel.Width; ++i)
                {
                    for (var j = 0; j < _currentLevel.Height; ++j)
                    {
                        var cTileId = cLayer.Data[j * cLayer.Width + i] - 1;
                        var cTile = GetTile(cTileId);
                        if (cTile == null) continue;

                        var src = new Rectangle<int>(0, 0, cTile.ImageWidth, cTile.ImageHeight);
                        var dst = new Rectangle<int>(i * cTile.ImageWidth, j * cTile.ImageHeight, cTile.ImageWidth,
                            cTile.ImageHeight);

                        _renderer.RenderTexture(cTile.InternalTextureId, src, dst);
                    }
                }
            }
        }

        private IEnumerable<RenderableGameObject> GetAllRenderableObjects()
        {
            foreach (var gameObject in _gameObjects.Values)
            {
                if (gameObject is RenderableGameObject renderableGameObject)
                {
                    yield return renderableGameObject;
                }
            }
        }

        private IEnumerable<TemporaryGameObject> GetAllTemporaryGameObjects()
        {
            foreach (var gameObject in _gameObjects.Values)
            {
                if (gameObject is TemporaryGameObject temporaryGameObject)
                {
                    yield return temporaryGameObject;
                }
            }
        }

        private void RenderAllObjects()
        {
            foreach (var gameObject in GetAllRenderableObjects())
            {
                gameObject.Render(_renderer);
            }

            if (_player != null) // Verificăm dacă _player nu este null
            {
                _player.Render(_renderer);
            }
        }

        private void AddBomb(int x, int y, bool translateCoordinates = true)
        {
            var translated = translateCoordinates ? _renderer.TranslateFromScreenToWorldCoordinates(x, y) : new Vector2D<int>(x, y);
            
            var spriteSheet = SpriteSheet.LoadSpriteSheet("bomb.json", "Assets", _renderer);
            if(spriteSheet != null){
                spriteSheet.ActivateAnimation("Explode");
                TemporaryGameObject bomb = new(spriteSheet, 2.1, (translated.X, translated.Y));
                _gameObjects.Add(bomb.Id, bomb);
            }
        }
    }
}
