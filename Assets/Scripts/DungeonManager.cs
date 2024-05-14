using System;
using System.Collections.Generic;
using UnityEngine;

public class DungeonManager : MonoBehaviour
{
    private Dungeon? dungeon;
    public Material roomMaterial;
    public Material corridorMaterial;

    private List<Cell> allCells = new List<Cell>();
    private void Start()
    {
        DungeonGenerator.Instance.OnDungeonGenerated += OnDungeonGenerated;
        
        DungeonGenerator.Instance.GenerateDungeon();
    }
    
    
    void OnDungeonGenerated(Dungeon generatedDungeon)
    {
        Debug.Log("Dungeon Generated, dungeon is null: " + (dungeon == null));
        dungeon = generatedDungeon;
        allCells = dungeon?.CellQT.GetAllData();
        Debug.Log("Dungeon is null: " + (dungeon == null));


        Mesh dungeonMesh = GenerateDungeonFloorMesh();

        // Create a new GameObject to hold the mesh.
        GameObject dungeonFloorObject = new GameObject("Dungeon Floor");
        MeshFilter meshFilter = dungeonFloorObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = dungeonFloorObject.AddComponent<MeshRenderer>();
        dungeonFloorObject.transform.SetParent(transform);

        // Assign the generated mesh to the MeshFilter.
        meshFilter.mesh = dungeonMesh;

        // Assign materials to the renderer based on your room and corridor materials.
        meshRenderer.material = roomMaterial;
    }

    private Mesh GenerateDungeonFloorMesh()
    {
        Mesh mesh = new Mesh();

        // Create lists to store mesh data.
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // Iterate through rooms and add room rectangles to the mesh.
        foreach (var cell in allCells)
        {
            // Define room vertices and triangles for the floor.
            Vector3 roomMin = new Vector3(cell.BottomLeft.x, 0f, cell.BottomLeft.y);
            Vector3 roomMax = new Vector3(cell.BottomLeft.x + cell.width, 0f, cell.BottomLeft.y + cell.height);

            int startIndex = vertices.Count;

            // Define room vertices (clockwise) for the floor.
            vertices.Add(roomMin);
            vertices.Add(new Vector3(roomMin.x, 0f, roomMax.z));
            vertices.Add(roomMax);
            vertices.Add(new Vector3(roomMax.x, 0f, roomMin.z));

            // Define room triangles for the floor.
            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);
        }

        Vector3 wallHeight = new Vector3(0, 2f, 0);
        foreach (var room in dungeon?.Rooms)
        {
            List<Vector3> wallVertices = new List<Vector3>();
            List<Vector3> wallNormals = new List<Vector3>();
            List<int> wallTriangles = new List<int>();
            
            Vector3 roomMin = new Vector3(room.BottomLeft.x, 0f, room.BottomLeft.y);
            Vector3 roomMax = new Vector3(room.BottomLeft.x + room.width, 0f, room.BottomLeft.y + room.height);
            
            // Define room wall vertices (clockwise) for the floor.
            wallVertices.Add(roomMin);
            wallVertices.Add(roomMax);
            wallVertices.Add(new Vector3(roomMin.x, 0f, roomMax.z));
            wallVertices.Add(new Vector3(roomMax.x, 0f, roomMin.z));
            
            wallNormals.Add((new Vector3(room.center.x, 0f, room.center.y) - roomMin).normalized);
            wallNormals.Add((new Vector3(room.center.x, 0f, room.center.y) - roomMax).normalized);
            wallNormals.Add((new Vector3(room.center.x, 0f, room.center.y) - new Vector3(roomMin.x, 0f, roomMax.z)).normalized);
            wallNormals.Add((new Vector3(room.center.x, 0f, room.center.y) - new Vector3(roomMax.x, 0f, roomMin.z)).normalized);
            
            wallVertices.Add(roomMin);
            wallVertices.Add(roomMax);
            wallVertices.Add(new Vector3(roomMin.x, 0f, roomMax.z));
            wallVertices.Add(new Vector3(roomMax.x, 0f, roomMin.z));
            
            wallNormals.Add((roomMin - new Vector3(room.center.x, 0f, room.center.y)).normalized);
            wallNormals.Add((roomMax - new Vector3(room.center.x, 0f, room.center.y)).normalized);
            wallNormals.Add((new Vector3(roomMin.x, 0f, roomMax.z) - new Vector3(room.center.x, 0f, room.center.y)).normalized);
            wallNormals.Add((new Vector3(roomMax.x, 0f, roomMin.z) - new Vector3(room.center.x, 0f, room.center.y)).normalized);
            
            
            wallVertices.Add(roomMin + wallHeight);
            wallVertices.Add(new Vector3(roomMin.x, 0f, roomMax.z) + wallHeight);
            wallVertices.Add(roomMax + wallHeight);
            wallVertices.Add(new Vector3(roomMax.x, 0f, roomMin.z) + wallHeight);
            
            wallNormals.Add((new Vector3(room.center.x, wallHeight.y, room.center.y) - (roomMin + wallHeight)).normalized);
            wallNormals.Add((new Vector3(room.center.x, wallHeight.y, room.center.y) - (new Vector3(roomMin.x, 0f, roomMax.z) + wallHeight)).normalized);
            wallNormals.Add((new Vector3(room.center.x, wallHeight.y, room.center.y) - (roomMax + wallHeight)).normalized);
            wallNormals.Add((new Vector3(room.center.x, wallHeight.y, room.center.y) - (new Vector3(roomMax.x, 0f, roomMin.z) + wallHeight)).normalized);
            
            wallVertices.Add(roomMin + wallHeight);
            wallVertices.Add(new Vector3(roomMin.x, 0f, roomMax.z) + wallHeight);
            wallVertices.Add(roomMax + wallHeight);
            wallVertices.Add(new Vector3(roomMax.x, 0f, roomMin.z) + wallHeight);
            
            wallNormals.Add(((roomMin + wallHeight) - new Vector3(room.center.x, wallHeight.y, room.center.y)).normalized);
            wallNormals.Add(((new Vector3(roomMin.x, 0f, roomMax.z) + wallHeight) - new Vector3(room.center.x, wallHeight.y, room.center.y)).normalized);
            wallNormals.Add(((roomMax + wallHeight) - new Vector3(room.center.x, wallHeight.y, room.center.y)).normalized);
            wallNormals.Add(((new Vector3(roomMax.x, 0f, roomMin.z) + wallHeight) - new Vector3(room.center.x, wallHeight.y, room.center.y)).normalized);
            
            #region Room Min Walls

            //Walls from roomMin
            int roomMinFirstIndex = wallVertices.IndexOf(roomMin);
            int roomMinSecondIndex = wallVertices.LastIndexOf(roomMin);
            
            int roomMinWallFirstIndex = wallVertices.IndexOf(roomMin + wallHeight);
            int roomMinWallSecondIndex = wallVertices.LastIndexOf(roomMin + wallHeight);
            
            int minWallFirstIndex1 = wallVertices.IndexOf(new Vector3(roomMin.x, 0f, roomMax.z));
            int minWallSecondIndex1 = wallVertices.LastIndexOf(new Vector3(roomMin.x, 0f, roomMax.z));
            
            int minWallFirstIndex2 = wallVertices.IndexOf(new Vector3(roomMax.x, 0f, roomMin.z));
            int minWallSecondIndex2 = wallVertices.LastIndexOf(new Vector3(roomMax.x, 0f, roomMin.z));
            
            int minWallHeightFirstIndex1 = wallVertices.IndexOf(new Vector3(roomMin.x, 0f, roomMax.z) + wallHeight);
            int minWallHeightSecondIndex1 = wallVertices.LastIndexOf(new Vector3(roomMin.x, 0f, roomMax.z) + wallHeight);
            
            int minWallHeightFirstIndex2 = wallVertices.IndexOf(new Vector3(roomMax.x, 0f, roomMin.z) + wallHeight);
            int minWallHeightSecondIndex2 = wallVertices.LastIndexOf(new Vector3(roomMax.x, 0f, roomMin.z) + wallHeight);
            
            
            //Wall 1
            wallTriangles.Add(roomMinWallFirstIndex);
            wallTriangles.Add(minWallHeightFirstIndex1);
            wallTriangles.Add(minWallFirstIndex1);
            
            wallTriangles.Add(roomMinWallFirstIndex);
            wallTriangles.Add(minWallFirstIndex1);
            wallTriangles.Add(roomMinFirstIndex);
            
            //Wall 2
            wallTriangles.Add(roomMinWallFirstIndex);
            wallTriangles.Add(minWallFirstIndex2);
            wallTriangles.Add(minWallHeightFirstIndex2);
            
            wallTriangles.Add(roomMinWallFirstIndex);
            wallTriangles.Add(roomMinFirstIndex);
            wallTriangles.Add(minWallFirstIndex2);
            
            //Wall 1
            wallTriangles.Add(roomMinWallSecondIndex);
            wallTriangles.Add(minWallSecondIndex1);
            wallTriangles.Add(minWallHeightSecondIndex1);
            
            wallTriangles.Add(roomMinWallSecondIndex);
            wallTriangles.Add(roomMinSecondIndex);
            wallTriangles.Add(minWallSecondIndex1);
            
            //Wall 2
            wallTriangles.Add(roomMinWallSecondIndex);
            wallTriangles.Add(minWallHeightSecondIndex2);
            wallTriangles.Add(minWallSecondIndex2);
            
            wallTriangles.Add(roomMinWallSecondIndex);
            wallTriangles.Add(minWallSecondIndex2);
            wallTriangles.Add(roomMinSecondIndex);

            #endregion

            #region Room Max Walls

            //Walls from roomMax
            
            int roomMaxFirstIndex = wallVertices.IndexOf(roomMax);
            int roomMaxSecondIndex = wallVertices.LastIndexOf(roomMax);
            
            int roomMaxWallFirstIndex = wallVertices.IndexOf(roomMax + wallHeight);
            int roomMaxWallSecondIndex = wallVertices.LastIndexOf(roomMax + wallHeight);
            
            int maxWallFirstIndex1 = wallVertices.IndexOf(new Vector3(roomMin.x, 0f, roomMax.z));
            int maxWallSecondIndex1 = wallVertices.LastIndexOf(new Vector3(roomMin.x, 0f, roomMax.z));
            
            int maxWallFirstIndex2 = wallVertices.IndexOf(new Vector3(roomMax.x, 0f, roomMin.z));
            int maxWallSecondIndex2 = wallVertices.LastIndexOf(new Vector3(roomMax.x, 0f, roomMin.z));
            
            int maxWallHeightFirstIndex1 = wallVertices.IndexOf(new Vector3(roomMin.x, 0f, roomMax.z) + wallHeight);
            int maxWallHeightSecondIndex1 = wallVertices.LastIndexOf(new Vector3(roomMin.x, 0f, roomMax.z) + wallHeight);
            
            int maxWallHeightFirstIndex2 = wallVertices.IndexOf(new Vector3(roomMax.x, 0f, roomMin.z) + wallHeight);
            int maxWallHeightSecondIndex2 = wallVertices.LastIndexOf(new Vector3(roomMax.x, 0f, roomMin.z) + wallHeight);
            
            
            //Wall 1
            wallTriangles.Add(roomMaxWallFirstIndex);
            wallTriangles.Add(maxWallFirstIndex1);
            wallTriangles.Add(maxWallHeightFirstIndex1);
            
            wallTriangles.Add(roomMaxWallFirstIndex);
            wallTriangles.Add(roomMaxFirstIndex);
            wallTriangles.Add(maxWallFirstIndex1);
            
            //Wall 2
            wallTriangles.Add(roomMaxWallFirstIndex);
            wallTriangles.Add(maxWallHeightFirstIndex2);
            wallTriangles.Add(maxWallFirstIndex2);
            
            wallTriangles.Add(roomMaxWallFirstIndex);
            wallTriangles.Add(maxWallFirstIndex2);
            wallTriangles.Add(roomMaxFirstIndex);
            
            //Wall 1
            wallTriangles.Add(roomMaxWallSecondIndex);
            wallTriangles.Add(maxWallHeightSecondIndex1);
            wallTriangles.Add(maxWallSecondIndex1);
            
            wallTriangles.Add(roomMaxWallSecondIndex);
            wallTriangles.Add(maxWallSecondIndex1);
            wallTriangles.Add(roomMaxSecondIndex);
            
            //Wall 2
            wallTriangles.Add(roomMaxWallSecondIndex);
            wallTriangles.Add(maxWallSecondIndex2);
            wallTriangles.Add(maxWallHeightSecondIndex2);
            
            wallTriangles.Add(roomMaxWallSecondIndex);
            wallTriangles.Add(roomMaxSecondIndex);
            wallTriangles.Add(maxWallSecondIndex2);

            #endregion

            Mesh wallMesh = new Mesh();
            wallMesh.vertices = wallVertices.ToArray();
            wallMesh.triangles = wallTriangles.ToArray();
            wallMesh.normals = wallNormals.ToArray();
            wallMesh.Optimize();

            GameObject wallObject = new GameObject(room.center + " Walls");
            wallObject.AddComponent<MeshRenderer>().material = roomMaterial;
            wallObject.AddComponent<MeshFilter>().mesh = wallMesh;
            wallObject.transform.SetParent(transform);
        }

        // Set mesh data.
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();

        mesh.Optimize();
        // Calculate normals for lighting.
        mesh.RecalculateNormals();

        return mesh;
    }
    

    private void OnDrawGizmos()
    {
        if(dungeon != null)
        {
            Gizmos.color = Color.red;
            for (var i = 0; i < allCells.Count; i++)
            {
                var cell = allCells[i];
                Gizmos.DrawWireCube(cell.center, new Vector3(cell.width, cell.height, 0));
            }
        }
    }
}