using System;
using System.Collections;
using Random = UnityEngine.Random;

namespace CasualtiesTogetherRandomHostTools;

public static class WorldCorrupt
{
    private const int ChunkSize = 8;
    
    public static IEnumerator DoCorrupt()
    {
        Plugin.Logger.LogInfo("DoCorrupt start");

        var world = WorldGeneration.world;
        for (var bx = 0; bx < world.width / ChunkSize; bx++)
        {
            for (var by = 0; by < world.height / ChunkSize; by++)
            {
                var tx = Random.RandomRangeInt(0, 3) * (Random.value < 0.5 ? -1 : 1);
                var ty = Random.RandomRangeInt(0, 3) * (Random.value < 0.5 ? -1 : 1);
                if (bx + tx < 0 || (bx + tx) * ChunkSize + ChunkSize >= world.width)
                    tx *= -1;
                if (by + ty < 0 || (by + ty) * ChunkSize + ChunkSize >= world.height)
                    ty *= -1;
                SwapWorldChunks(bx * ChunkSize, by * ChunkSize, (bx + tx) * ChunkSize, (by + ty) * ChunkSize);
            }
            yield return null;
        }

        for (var col = 0; col < world.width; col++)
        {
            if (Random.value < 0.98)
                continue;
            var tcol = Random.RandomRangeInt(0, (int)world.width);
            SwapWorldCols(col, tcol);
        }
        
        yield return null;
        
        world.UpdateWorld();
        
        yield return null;
    }
    
    private static unsafe void SwapWorldChunks(int x1, int y1, int x2, int y2)
    {
        var tiles = WorldGeneration.world.worldBlocks;
        int height = (int)WorldGeneration.world.height;
        const int colBytes = ChunkSize * sizeof(ushort);
        
        fixed (ushort* basePtr = tiles)
        {
            ushort* tmpCol = stackalloc ushort[ChunkSize];
            for (var col = 0; col < ChunkSize; col++)
            {
                ushort* p1 = basePtr + (x1 + col) * height + y1;
                ushort* p2 = basePtr + (x2 + col) * height + y2;
                Buffer.MemoryCopy(p1, tmpCol, colBytes, colBytes);
                Buffer.MemoryCopy(p2, p1, colBytes, colBytes);
                Buffer.MemoryCopy(tmpCol, p2, colBytes, colBytes);
            }
        }
    }
    
    private static unsafe void SwapWorldCols(int col1, int col2)
    {
        var tiles = WorldGeneration.world.worldBlocks;
        int height = (int)WorldGeneration.world.height;
        int colBytes = height * sizeof(ushort);
        
        fixed (ushort* basePtr = tiles)
        {
            ushort* tmpCol = stackalloc ushort[height];
            ushort* p1 = basePtr + col1 * height;
            ushort* p2 = basePtr + col2 * height;
            Buffer.MemoryCopy(p1, tmpCol, colBytes, colBytes);
            Buffer.MemoryCopy(p2, p1, colBytes, colBytes);
            Buffer.MemoryCopy(tmpCol, p2, colBytes, colBytes);
        }
    }
    
    public static unsafe void ReplaceAllLiquids(byte newLiquid)
    {
        var f = FluidManager.main.fluid;
        var length = f.LongLength;
        
        fixed (byte* basePtr = f)
        {
            for (var i = 0L; i < length; i++)
            {
                if (*(basePtr + i) != 0)
                    *(basePtr + i) = newLiquid;
            }
        }
    }
}