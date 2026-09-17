using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using KrokoshaCasualtiesMP;
using UnityEngine;
using UnityEngine.Tilemaps;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace CasualtiesTogetherRandomHostTools;

public static class SaveWorldTilesCommand
{
    private static readonly List<string> _savedFiles = [];
    private static string _showedWarningForFilepath = null;
    private static string SaveDirPath => Path.Combine(Application.persistentDataPath, "SavedWorldTiles");

    public static void UpdateFiles()
    {
        try
        {
            _savedFiles.Clear();
            var dirPath = SaveDirPath;
            var dir = Directory.CreateDirectory(dirPath);
            foreach (var fileInfo in dir.EnumerateFiles("*.bin"))
                _savedFiles.Add(fileInfo.Name);
        }
        catch (Exception ex)
        {
            Plugin.PrintWarning("Failed to read files: " + ex);
        }
    }

    public static void Register()
    {
        var comm = new Command("SaveWorldTiles", "Save the world's tiles", args =>
        {
            Con.ConFailIfNetworkIsRunningAndIsClient();
            ConsoleScript.instance.CheckForWorld();
            ConsoleScript.instance.CheckArgumentCount(args, 1);

            var dirPath = SaveDirPath;
            Directory.CreateDirectory(dirPath);
            var filename = args[1];
            if (!filename.EndsWith(".bin", StringComparison.InvariantCultureIgnoreCase))
                filename += ".bin";
            var filePath = Path.Combine(dirPath, filename);

            if (File.Exists(filePath))
            {
                if (_showedWarningForFilepath == null || _showedWarningForFilepath != filePath)
                {
                    Con.con.LogToConsole($"You are about to overwrite \"{filePath}\"!");
                    Con.con.LogToConsole($"Retype this command to confirm.");
                    _showedWarningForFilepath = filePath;
                    return;
                }
            }

            var worldBlocks = WorldGeneration.world.worldBlocks;
            var fluidBlocks = FluidManager.main.fluid;

            using var fs = File.Create(filePath);
            using var gzip = new GZipStream(fs, CompressionLevel.Fastest);
            using var writer = new BinaryWriter(gzip);

            {
                writer.Write(worldBlocks.GetLength(0));
                writer.Write(worldBlocks.GetLength(1));

                var bytes = new byte[Buffer.ByteLength(worldBlocks)];
                Buffer.BlockCopy(worldBlocks, 0, bytes, 0, bytes.Length);

                writer.Write(bytes.Length);
                writer.Write(bytes);
            }

            {
                writer.Write(fluidBlocks.GetLength(0));
                writer.Write(fluidBlocks.GetLength(1));

                var bytes = new byte[Buffer.ByteLength(fluidBlocks)];
                Buffer.BlockCopy(fluidBlocks, 0, bytes, 0, bytes.Length);

                writer.Write(bytes.Length);
                writer.Write(bytes);
            }

            UpdateFiles();

            ConsoleScript.instance.LogToConsole($"World saved to \"{filePath}\"!");

        }, new Dictionary<int, List<string>>
        {
            {0, _savedFiles}
        }, [
            ("name", "What the file should be named")
        ]);
        Con.RegisterCommand(comm);

        comm = new Command("LoadWorldTiles", "Load the world's tiles", args =>
        {
            UpdateFiles();

            Con.ConFailIfNetworkIsRunningAndIsClient();
            ConsoleScript.instance.CheckForWorld();
            ConsoleScript.instance.CheckArgumentCount(args, 1);

            var dirPath = SaveDirPath;
            Directory.CreateDirectory(dirPath);
            var filename = args[1];
            var filePath = Path.Combine(dirPath, filename);

            if (!File.Exists(filePath))
                throw new Exception($"File \"{filePath}\" does not exist!");

            using var fs = File.OpenRead(filePath);
            using var gzip = new GZipStream(fs, CompressionMode.Decompress);
            using var reader = new BinaryReader(gzip);

            {
                var blockRows = WorldGeneration.world.worldBlocks.GetLength(0);
                var blockCols = WorldGeneration.world.worldBlocks.GetLength(1);

                var rows = reader.ReadInt32();
                var cols = reader.ReadInt32();

                if (blockRows != rows || blockCols != cols)
                    throw new Exception($"World sizes don't match! Saved: {cols}x{rows}, Generated: {blockCols}x{blockRows}");

                var byteCount = reader.ReadInt32();

                var bytes = reader.ReadBytes(byteCount);

                var worldBlocks = new ushort[rows, cols];
                Buffer.BlockCopy(bytes, 0, worldBlocks, 0, byteCount);

                WorldGeneration.world.worldBlocks = worldBlocks;
            }

            {
                var fluidRows = WorldGeneration.world.worldBlocks.GetLength(0);
                var fluidCols = WorldGeneration.world.worldBlocks.GetLength(1);

                var rows = reader.ReadInt32();
                var cols = reader.ReadInt32();

                if (fluidRows != rows || fluidCols != cols)
                    throw new Exception($"World sizes don't match! Saved: {cols}x{rows}, Generated: {fluidCols}x{fluidRows}");

                var byteCount = reader.ReadInt32();

                var bytes = reader.ReadBytes(byteCount);

                var fluidBlocks = new byte[rows, cols];
                Buffer.BlockCopy(bytes, 0, fluidBlocks, 0, byteCount);

                FluidManager.main.fluid = fluidBlocks;
            }

            WorldGeneration.world.StartCoroutine(UpdateWorldCoro());

        }, new Dictionary<int, List<string>> {
            {0, _savedFiles}
        }, [
            ("file", $"Which file within \"{SaveDirPath}\" to load")
        ]);
        Con.RegisterCommand(comm);

        UpdateFiles();
    }

    private static IEnumerator UpdateWorldCoro()
    {
        var world = WorldGeneration.world;
        if (world == null || world.generatingWorld || !world.worldExists)
            yield break;

        ConsoleScript.instance.LogToConsole($"Destroying entities...");
        yield return null;
        if (world == null || world.generatingWorld || !world.worldExists)
            yield break;

        foreach (var component in UnityEngine.Object.FindObjectsOfType<BuildingEntity>())
            UnityEngine.Object.Destroy(component.gameObject);

        ConsoleScript.instance.LogToConsole($"Destroying items...");
        yield return null;
        if (world == null || world.generatingWorld || !world.worldExists)
            yield break;
        foreach (var allItem in Item.allItems)
        {
            if (allItem.transform.parent == null || allItem.transform.parent.name == "DOSPAWN")
                UnityEngine.Object.Destroy(allItem.gameObject);
        }

        ConsoleScript.instance.LogToConsole($"Destroying backgrounds...");
        yield return null;
        if (world == null || world.generatingWorld || !world.worldExists)
            yield break;
        world.backgrounds.Clear();

        for (int chunkX = 0; chunkX < world.chunkWidth; chunkX++)
        {
            for (int chunkY = 0; chunkY < world.chunkHeight; chunkY++)
            {
                Con.con.LogToConsole($"Loading: {chunkX * world.chunkWidth + chunkY}/{world.chunkWidth * world.chunkHeight - 1}");
                world.UpdateChunk(new Vector2Int(chunkX, chunkY));
                world.chunks[chunkX, chunkY].gameObject.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
                yield return null;
                if (world == null || world.generatingWorld || !world.worldExists)
                    yield break;
            }
        }

        ConsoleScript.instance.LogToConsole($"World tiles have been loaded!");
    }
}
