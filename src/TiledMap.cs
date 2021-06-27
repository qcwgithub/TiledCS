using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace TiledCS
{
    /// <summary>
    /// Represents a Tiled map
    /// </summary>
    public class TiledMap
    {
        const uint FLIPPED_HORIZONTALLY_FLAG = 0b10000000000000000000000000000000;
        const uint FLIPPED_VERTICALLY_FLAG   = 0b01000000000000000000000000000000;
        const uint FLIPPED_DIAGONALLY_FLAG   = 0b00100000000000000000000000000000;
        
        /// <summary>
        /// How many times we shift the FLIPPED flags to the right in order to store it in a byte.
        /// For example: 0b10100000000000000000000000000000 >> SHIFT_FLIP_FLAG_TO_BYTE = 0b00000101
        /// </summary>
        const int SHIFT_FLIP_FLAG_TO_BYTE = 29;
        
        /// <summary>
        /// Returns the Tiled version used to create this map
        /// </summary>
        public string TiledVersion { get; set; }
        /// <summary>
        /// Returns an array of properties defined in the map
        /// </summary>
        public TiledProperty[] Properties { get; set; }
        /// <summary>
        /// Returns an array of tileset definitions in the map
        /// </summary>
        public TiledMapTileset[] Tilesets { get; set; }
        /// <summary>
        /// Returns an array of layers or null if none were defined
        /// </summary>
        public TiledLayer[] Layers { get; set; }
        /// <summary>
        /// Returns the defined map orientation as a string
        /// </summary>
        public string Orientation { get; set; }
        /// <summary>
        /// Returns the render order as a string
        /// </summary>
        public string RenderOrder { get; set; }
        /// <summary>
        /// The amount of horizontal tiles
        /// </summary>
        public int Width { get; set; }
        /// <summary>
        /// The amount of vertical tiles
        /// </summary>
        public int Height { get; set; }
        /// <summary>
        /// The tile width in pixels
        /// </summary>
        public int TileWidth { get; set; }
        /// <summary>
        /// The tile height in pixels
        /// </summary>
        public int TileHeight { get; set; }

        /// <summary>
        /// Returns an empty instance of TiledMap
        /// </summary>
        public TiledMap()
        {

        }

        /// <summary>
        /// Loads a Tiled map in TMX format and parses it
        /// </summary>
        /// <param name="path">The path to the tmx file</param>
        /// <exception cref="TiledException">Thrown when the map could not be loaded or is not in a correct format</exception>
        public TiledMap(string path)
        {
            var content = "";

            // Check the file
            if (!File.Exists(path))
            {
                throw new TiledException($"{path} not found");
            }
            else
            {
                content = File.ReadAllText(path);
            }

            if (path.EndsWith(".tmx"))
            {
                ParseXml(content);
            }
            else
            {
                throw new TiledException("Unsupported file format");
            }
        }

        /// <summary>
        /// Can be used to parse the content of a TMX map manually instead of loading it using the constructor
        /// </summary>
        /// <param name="xml">The tmx file content as string</param>
        /// <exception cref="TiledException"></exception>
        public void ParseXml(string xml)
        {
            try
            {
                // Load the xml document
                var document = new XmlDocument();
                document.LoadXml(xml);

                var nodeMap = document.SelectSingleNode("map");
                var nodesProperty = nodeMap.SelectNodes("properties/property");
                var nodesLayer = nodeMap.SelectNodes("layer");
                var nodesObjectGroup = nodeMap.SelectNodes("objectgroup");
                var nodesTileset = nodeMap.SelectNodes("tileset");

                this.TiledVersion = nodeMap.Attributes["tiledversion"].Value;
                this.Orientation = nodeMap.Attributes["orientation"].Value;
                this.RenderOrder = nodeMap.Attributes["renderorder"].Value;

                this.Width = int.Parse(nodeMap.Attributes["width"].Value);
                this.Height = int.Parse(nodeMap.Attributes["height"].Value);
                this.TileWidth = int.Parse(nodeMap.Attributes["tilewidth"].Value);
                this.TileHeight = int.Parse(nodeMap.Attributes["tileheight"].Value);

                if (nodesProperty != null) Properties = ParseProperties(nodesProperty);
                if (nodesTileset != null) Tilesets = ParseTilesets(nodesTileset);
                if (nodesLayer != null) Layers = ParseLayers(nodesLayer, nodesObjectGroup);
            }
            catch (Exception ex)
            {
                throw new TiledException("Unable to parse xml data, make sure the xml data represents a valid Tiled map", ex);
            }
        }

        private TiledProperty[] ParseProperties(XmlNodeList nodeList)
        {
            var result = new List<TiledProperty>();

            foreach (XmlNode node in nodeList)
            {
                var property = new TiledProperty();
                property.name = node.Attributes["name"].Value;
                property.type = node.Attributes["type"]?.Value;
                property.value = node.Attributes["value"]?.Value;

                if (property.value == null && node.InnerText != null)
                {
                    property.value = node.InnerText;
                }

                result.Add(property);
            }

            return result.ToArray();
        }

        private TiledMapTileset[] ParseTilesets(XmlNodeList nodeList)
        {
            var result = new List<TiledMapTileset>();

            foreach (XmlNode node in nodeList)
            {
                var tileset = new TiledMapTileset();
                tileset.firstgid = int.Parse(node.Attributes["firstgid"].Value);
                tileset.source = node.Attributes["source"].Value;

                result.Add(tileset);
            }

            return result.ToArray();
        }

        private TiledLayer[] ParseLayers(XmlNodeList nodeListLayers, XmlNodeList nodeListObjGroups)
        {
            var result = new List<TiledLayer>();

            foreach (XmlNode node in nodeListLayers)
            {
                var nodeData = node.SelectSingleNode("data");
                var encoding = nodeData.Attributes["encoding"].Value;
                var attrVisible = node.Attributes["visible"];
                var csvs = nodeData.InnerText.Split(',');
                var nodesProperty = node.SelectNodes("properties/property");

                if (encoding != "csv")
                {
                    throw new TiledException($"Only CSV encoding is currently supported");
                }

                var tiledLayer = new TiledLayer();
                tiledLayer.id = int.Parse(node.Attributes["id"].Value);
                tiledLayer.name = node.Attributes["name"].Value;
                tiledLayer.height = int.Parse(node.Attributes["height"].Value);
                tiledLayer.width = int.Parse(node.Attributes["width"].Value);
                tiledLayer.type = "tilelayer";
                tiledLayer.visible = attrVisible?.Value == "1";
                tiledLayer.data = new int[csvs.Length];
                tiledLayer.dataRotationFlags = new byte[csvs.Length];
                
                // Parse the comma separated csv string and update the inner data as well as the data rotation flags
                for (var i = 0; i < csvs.Length; i++)
                {
                    var rawID = uint.Parse(csvs[i]);
                    var hor = ((rawID & FLIPPED_HORIZONTALLY_FLAG));
                    var ver = ((rawID & FLIPPED_VERTICALLY_FLAG));
                    var dia = ((rawID & FLIPPED_DIAGONALLY_FLAG));
                    tiledLayer.dataRotationFlags[i] = (byte)((hor | ver | dia) >> SHIFT_FLIP_FLAG_TO_BYTE);

                    // assign data to rawID with the rotation flags cleared
                    tiledLayer.data[i] = (int)(rawID & ~(FLIPPED_HORIZONTALLY_FLAG | FLIPPED_VERTICALLY_FLAG | FLIPPED_DIAGONALLY_FLAG));
                }

                if (nodesProperty != null) tiledLayer.Properties = ParseProperties(nodesProperty);
                result.Add(tiledLayer);
            }

            foreach (XmlNode node in nodeListObjGroups)
            {
                var nodesObject = node.SelectNodes("object");
                var nodesProperty = node.SelectNodes("properties/property");

                var tiledLayer = new TiledLayer();
                tiledLayer.id = int.Parse(node.Attributes["id"].Value);
                tiledLayer.name = node.Attributes["name"].Value;
                tiledLayer.objects = ParseObjects(nodesObject);
                tiledLayer.type = "objectgroup";
                tiledLayer.visible = true;

                if (nodesProperty != null) tiledLayer.Properties = ParseProperties(nodesProperty);
                result.Add(tiledLayer);
            }

            return result.ToArray();
        }

        private TiledObject[] ParseObjects(XmlNodeList nodeList)
        {
            var result = new List<TiledObject>();

            foreach (XmlNode node in nodeList)
            {
                var nodesProperty = node.SelectNodes("properties/property");
                    
                var obj = new TiledObject();
                obj.id = int.Parse(node.Attributes["id"].Value);
                obj.name = node.Attributes["name"]?.Value;
                obj.type = node.Attributes["type"]?.Value;
                obj.x = float.Parse(node.Attributes["x"].Value);
                obj.y = float.Parse(node.Attributes["y"].Value);

                if (nodesProperty != null)
                {
                    obj.properties = ParseProperties(nodesProperty);
                }

                if (node.Attributes["width"] != null)
                {
                    obj.width = float.Parse(node.Attributes["width"].Value);
                }
                
                if (node.Attributes["height"] != null)
                {
                    obj.height = float.Parse(node.Attributes["height"].Value);
                }

                if (node.Attributes["rotation"] != null)
                {
                    obj.rotation = int.Parse(node.Attributes["rotation"].Value);
                }
                
                result.Add(obj);
            }

            return result.ToArray();
        }
        
        /* HELPER METHODS */
        /// <summary>
        /// Locates the right TiledMapTileset object for you within the Tilesets array
        /// </summary>
        /// <param name="gid">A value from the TiledLayer.data array</param>
        /// <returns>An element within the Tilesets array or null if no match was found</returns>
        public TiledMapTileset GetTiledMapTileset(int gid)
        {
            if (Tilesets == null)
            {
                return null;
            }
            
            for (var i = 0; i < Tilesets.Length; i++)
            {
                if (i < Tilesets.Length - 1)
                {
                    int gid1 = Tilesets[i + 0].firstgid;
                    int gid2 = Tilesets[i + 1].firstgid;

                    if (gid >= gid1 && gid < gid2)
                    {
                        return Tilesets[i];
                    }
                }
                else
                {
                    return Tilesets[i];
                }
            }

            return new TiledMapTileset();
        }
        /// <summary>
        /// Loads external tilesets and matches them to firstGids from elements within the Tilesets array
        /// </summary>
        /// <param name="src">The folder where the TiledMap file is located</param>
        /// <returns>A dictionary where the key represents the firstGid of the associated TiledMapTileset and the value the TiledTileset object</returns>
        public Dictionary<int, TiledTileset> GetTiledTilesets(string src)
        {
            var tilesets = new Dictionary<int, TiledTileset>();
            var info = new FileInfo(src);
            var srcFolder = info.Directory;

            if (Tilesets == null)
            {
                return tilesets;
            }

            foreach (var mapTileset in Tilesets)
            {
                var path = $"{srcFolder}/{mapTileset.source}";

                if (File.Exists(path))
                {
                    tilesets.Add(mapTileset.firstgid, new TiledTileset(path));
                }
            }

            return tilesets;
        }
        /// <summary>
        /// Locates a specific TiledTile object
        /// </summary>
        /// <param name="mapTileset">An element within the Tilesets array</param>
        /// <param name="tileset">An instance of the TiledTileset class</param>
        /// <param name="gid">An element from within a TiledLayer.data array</param>
        /// <returns>An entry of the TiledTileset.tiles array or null if none of the tile id's matches the gid</returns>
        /// <remarks>Tip: Use the GetTiledMapTileset and GetTiledTilesets methods for retrieving the correct TiledMapTileset and TiledTileset objects</remarks>
        public TiledTile GetTiledTile(TiledMapTileset mapTileset, TiledTileset tileset, int gid)
        {
            foreach (var tile in tileset.Tiles)
            {
                if (tile.id == gid - mapTileset.firstgid)
                {
                    return tile;
                }
            }

            return null;
        }
        /// <summary>
        /// This method can be used to figure out the x and y position on a Tileset image for rendering tiles. 
        /// </summary>
        /// <param name="mapTileset">An element of the Tilesets array</param>
        /// <param name="tileset">An instance of the TiledTileset class</param>
        /// <param name="gid">An element within a TiledLayer.data array</param>
        /// <returns>An int array of length 2 containing the x and y position of the source rect of the tileset image. Multiply the values by the tile width and height in pixels to get the actual x and y position. Returns null if the gid was not found</returns>
        /// <remarks>This method currently doesn't take margin into account</remarks>
        [Obsolete("Please use GetSourceRect instead because with future versions of Tiled this method may no longer be sufficient")]
        public int[] GetSourceVector(TiledMapTileset mapTileset, TiledTileset tileset, int gid)
        {
            var tileHor = 0;
            var tileVert = 0;

            for (var i = 0; i < tileset.TileCount; i++)
            {
                if (i == gid - mapTileset.firstgid)
                {
                    return new[] {tileHor, tileVert};
                }

                // Update x and y position
                tileHor++;

                if (tileHor == tileset.ImageWidth / tileset.TileWidth)
                {
                    tileHor = 0;
                    tileVert++;
                }
            }

            return null;
        }
        
        /// <summary>
        /// This method can be used to figure out the source rect on a Tileset image for rendering tiles.
        /// </summary>
        /// <param name="mapTileset"></param>
        /// <param name="tileset"></param>
        /// <param name="gid"></param>
        /// <returns>An instance of the class TiledSourceRect that represents a rectangle. Returns null if the provided gid was not found within the tileset.</returns>
        public TiledSourceRect GetSourceRect(TiledMapTileset mapTileset, TiledTileset tileset, int gid)
        {
            var tileHor = 0;
            var tileVert = 0;

            for (var i = 0; i < tileset.TileCount; i++)
            {
                if (i == gid - mapTileset.firstgid)
                {
                    var result = new TiledSourceRect();
                    result.x = tileHor * tileset.TileWidth;
                    result.y = tileVert * tileset.TileHeight;
                    result.width = tileset.TileWidth;
                    result.height = tileset.TileHeight;

                    return result;
                }

                // Update x and y position
                tileHor++;

                if (tileHor == tileset.ImageWidth / tileset.TileWidth)
                {
                    tileHor = 0;
                    tileVert++;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks is a tile is flipped horizontally
        /// </summary>
        /// <param name="layer">An entry of the TiledMap.layers array</param>
        /// <param name="tileHor">The tile's horizontal position</param>
        /// <param name="tileVert">The tile's vertical position</param>
        /// <returns>True if the tile was flipped horizontally or False if not</returns>
        public bool IsTileFlippedHorizontal(TiledLayer layer, int tileHor, int tileVert)
        {
            return IsTileFlippedHorizontal(layer, tileHor + (tileVert * layer.width));
        }
        /// <summary>
        /// Checks is a tile is flipped horizontally
        /// </summary>
        /// <param name="layer">An entry of the TiledMap.layers array</param>
        /// <param name="dataIndex">An index of the TiledLayer.data array</param>
        /// <returns>True if the tile was flipped horizontally or False if not</returns>
        public bool IsTileFlippedHorizontal(TiledLayer layer, int dataIndex)
        {
            return (layer.dataRotationFlags[dataIndex] & (FLIPPED_HORIZONTALLY_FLAG >> SHIFT_FLIP_FLAG_TO_BYTE)) > 0;
        }
        /// <summary>
        /// Checks is a tile is flipped vertically
        /// </summary>
        /// <param name="layer">An entry of the TiledMap.layers array</param>
        /// <param name="tileHor">The tile's horizontal position</param>
        /// <param name="tileVert">The tile's vertical position</param>
        /// <returns>True if the tile was flipped vertically or False if not</returns>
        public bool IsTileFlippedVertical(TiledLayer layer, int tileHor, int tileVert)
        {
            return IsTileFlippedVertical(layer, tileHor + (tileVert * layer.width));
        }
        /// <summary>
        /// Checks is a tile is flipped vertically
        /// </summary>
        /// <param name="layer">An entry of the TiledMap.layers array</param>
        /// <param name="dataIndex">An index of the TiledLayer.data array</param>
        /// <returns>True if the tile was flipped vertically or False if not</returns>
        public bool IsTileFlippedVertical(TiledLayer layer, int dataIndex)
        {
            return (layer.dataRotationFlags[dataIndex] & (FLIPPED_VERTICALLY_FLAG >> SHIFT_FLIP_FLAG_TO_BYTE)) > 0;
        }
        /// <summary>
        /// Checks is a tile is flipped diagonally
        /// </summary>
        /// <param name="layer">An entry of the TiledMap.layers array</param>
        /// <param name="tileHor">The tile's horizontal position</param>
        /// <param name="tileVert">The tile's vertical position</param>
        /// <returns>True if the tile was flipped diagonally or False if not</returns>
        public bool IsTileFlippedDiagonal(TiledLayer layer, int tileHor, int tileVert)
        {
            return IsTileFlippedDiagonal(layer, tileHor + (tileVert * layer.width));
        }
        /// <summary>
        /// Checks is a tile is flipped diagonally
        /// </summary>
        /// <param name="layer">An entry of the TiledMap.layers array</param>
        /// <param name="dataIndex">An index of the TiledLayer.data array</param>
        /// <returns>True if the tile was flipped diagonally or False if not</returns>
        public bool IsTileFlippedDiagonal(TiledLayer layer, int dataIndex)
        {
            return (layer.dataRotationFlags[dataIndex] & (FLIPPED_DIAGONALLY_FLAG >> SHIFT_FLIP_FLAG_TO_BYTE)) > 0;
        }
    }
}