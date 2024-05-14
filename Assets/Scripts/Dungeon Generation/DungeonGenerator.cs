using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DelaunatorSharp;
using DelaunatorSharp.Unity.Extensions;
using Dungeon_Generation.Graph;
using Dungeon_Generation.QuadTree;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class Cell
{
    public Vector2 BottomLeft { get; set; }
    public float width { get; set; }
    public float height { get; set; }

    public int id;

    public Vector2 center
    {
        // Calculate the center based on the top-left corner and dimensions
        get => BottomLeft + new Vector2(width / 2f, height / 2f);
    }

    public Cell(Vector2 bottomLeft, float width, float height, int id = -1)
    {
        BottomLeft = bottomLeft;
        this.width = width;
        this.height = height;
        this.id = id;
    }

    public bool Overlaps(Cell other)
    {
        // Calculate the min and max points for both cells
        Vector2 thisMin = BottomLeft;
        Vector2 thisMax = BottomLeft + new Vector2(width, height);
        Vector2 otherMin = other.BottomLeft;
        Vector2 otherMax = other.BottomLeft + new Vector2(other.width, other.height);

        // Check if there's no overlap along any axis
        if (thisMin.x >= otherMax.x || thisMax.x <= otherMin.x || thisMin.y >= otherMax.y || thisMax.y <= otherMin.y)
        {
            return false;
        }

        // There is an overlap along both axes
        return true;
    }

    public bool Contains(Vector2 point)
    {
        Vector2 topRightCorner = BottomLeft + new Vector2(width, height);

        bool isInsideX = (point.x >= BottomLeft.x) && (point.x <= topRightCorner.x);
        bool isInsideY = (point.y >= BottomLeft.y) && (point.y <= topRightCorner.y);

        return isInsideX && isInsideY;
    }
}

public struct LineSegment
{
    public Vector2 start;
    public Vector2 end;
    
    public Vector2 GetDirection()
    {
        return (end - start).normalized;
    }

    // Method to get the perpendicular vector to the line segment
    public Vector2 GetPerpendicular()
    {
        Vector2 direction = GetDirection();
        return new Vector2(-direction.y, direction.x);
    }
}

public struct Dungeon
{
    public List<Cell> Rooms;
    public DungeonGraph<Cell> RoomGraph;
    public QuadTree<Cell> CellQT;
}


public class DungeonGenerator : MonoBehaviour
{
    [Range(5, 250)]
    public int NumCellsToGenerate = 150;
    public float meanWidth = 8;
    public float meanHeight = 6;
    public float standardDeviationWidth = 2.2f;
    public float standardDeviationHeight = 2.5f;

    public int minRoomArea = 16;
    public float additionalEdgesFraction = 0.15f;
    
    public float radiusDivider = 10f;
    public const float gridCellSize = 1f;
    private float radius;

    private int width, height;


    private QuadTree<Cell> cellQuadTree;
    private List<Cell> cells = new List<Cell>();
    private List<Cell> snappedCells = new List<Cell>();
    private List<Cell> rooms = new List<Cell>();
    private HashSet<Cell> corridorCells = new HashSet<Cell>();
    private List<Rigidbody2D> cellRigidbodies = new List<Rigidbody2D>();

    private Dictionary<Cell, Rigidbody2D> cellToRigidBody = new Dictionary<Cell, Rigidbody2D>();

    private DungeonGraph<int> graph;
    private List<GraphEdge<int>> mst;
    private List<GraphEdge<int>> finalEdges;
    private List<List<LineSegment>> corridors;

    private bool hasSpawnedRigidBodies = false;
    private bool hasSetSeperatedPosition;
    private bool drawRigidBodies = true;
    private bool drawCells = true;
    private bool drawSnappedCells = true;
    private bool drawDelaunay = false;
    private bool drawMst = false;
    private bool drawFinalGraph = false;
    private bool drawRooms = false;
    private bool drawCorridors = false;

    public static DungeonGenerator Instance;
    
    public Action<Dungeon> OnDungeonGenerated; 
    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Two dungeon generators");
            Destroy(gameObject);
        }
        Instance = this;
        
        radius = NumCellsToGenerate / radiusDivider;
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
            SpawnPhysicsBodies();

        if (Input.GetKeyDown(KeyCode.R))
            drawRigidBodies = !drawRigidBodies;
        
        if (Input.GetKeyDown(KeyCode.Y))
            drawRooms = !drawRooms;
        
        if (Input.GetKeyDown(KeyCode.C))
            drawCells = !drawCells;
        
        if (Input.GetKeyDown(KeyCode.S))
            drawSnappedCells = !drawSnappedCells;

        if(Input.GetKeyDown(KeyCode.Return))
        {
            Debug.Log("Any overlaps: " + cells.Any(c =>
            {
                bool anyOverlaps = false;
                foreach (var cell in snappedCells)
                {
                    if (cell == c)
                        continue;
                    if (c.Overlaps(cell))
                        anyOverlaps = true;
                }

                return anyOverlaps;
            }));
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            DetermineRooms();
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            drawMst = !drawMst;
            drawDelaunay = !drawMst;
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            drawFinalGraph = !drawFinalGraph;
            drawMst = !drawFinalGraph;
        }
        
        if (Input.GetKeyDown(KeyCode.P))
        {
            ConvertGraphToCorridors();
            drawFinalGraph = !drawCorridors;
            drawMst = !drawCorridors;
        }
            
    }

    private Stopwatch watch = new Stopwatch();
    public void GenerateDungeon()
    {
        watch.Start();
        GenerateCells();
        SpawnPhysicsBodies();

        StartCoroutine(WaitForRigidbodiesToSleep());
    }

    private IEnumerator WaitForRigidbodiesToSleep()
    {
        while (!RigidbodiesSleeping())
        {
            yield return null;
        }
        SetCellPositions();
        FillGaps();
        DetermineRooms();
        ConvertGraphToCorridors();

        Rect bounds = new Rect(0f, 0f, width, height);
        var cellQT = new QuadTree<Cell>(bounds, 30);
        HashSet<Cell> roomsAndCorridors = new HashSet<Cell>(rooms);
        roomsAndCorridors.UnionWith(corridorCells);
        foreach (var cell in roomsAndCorridors)
        {
            cellQT.Insert(cell, cell.center);
        }

        var roomGraph = graph.CreateNewGraph(finalEdges).SelectGraph(IdToCell);
        
        Dungeon dungeon = new Dungeon { Rooms = rooms, RoomGraph = roomGraph, CellQT = cellQT };
        
        OnDungeonGenerated?.Invoke(dungeon);
        ResetGenerator();
        watch.Stop();
        Debug.Log("Dungeon generation took: " + watch.ElapsedMilliseconds / 1000f + " seconds");
    }

    private void ResetGenerator()
    {
        cellQuadTree = null;
        cells = null;
        snappedCells = null;
        rooms = null;
        corridorCells = null;
        cellRigidbodies = null;

        cellToRigidBody = null;
        graph = null;
        mst = null;
        finalEdges = null;
        corridors = null;

        DestroyChildren();
    }

    private void DestroyChildren()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    private float NormalDistribution(float mean, float standardDeviation)
    {
        // Use the Box-Muller transform to generate samples from a normal distribution
        float u1 = 1.0f - Random.value;
        float u2 = 1.0f - Random.value;
        float z = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        return mean + standardDeviation * z;
    }

    private void GenerateCells()
    {
        for (int i = 0; i < NumCellsToGenerate; i++)
        {
            // Generate normally distributed width and height
            float cellWidth = NormalDistribution(meanWidth, standardDeviationWidth);
            float cellHeight = NormalDistribution(meanHeight, standardDeviationHeight);

            // Round width and height to the nearest positive integers and ensure they are at least 1
            int roundedWidth = Mathf.Max(Mathf.RoundToInt(cellWidth), 1);
            int roundedHeight = Mathf.Max(Mathf.RoundToInt(cellHeight), 1);


            // Calculate random position within the radius
            Vector2 position = GetRandomPointInEllipse(2f * radius,1f * radius) ; //Random.insideUnitCircle * radius;

            // Create the Cell instance and add it to the list
            Cell cell = new Cell(position, roundedWidth, roundedHeight, i);
            cells.Add(cell);
        }
    }

    private Vector2 GetRandomPointInEllipse(float width, float height)
    {
        float angle = 2 * Mathf.PI * Random.value;
        float radius = Mathf.Sqrt(Random.value);

        float x = width * radius * Mathf.Cos(angle);
        float y = height * radius * Mathf.Sin(angle);

        return new Vector2(x,y);
    }

    private void SpawnPhysicsBodies()
    {
        // Disable auto simulation to manually control the physics step

        // Create GameObjects and set up BoxColliders2D and Rigidbodies2D for each cell
        foreach (var cell in cells)
        {
            GameObject cellObject = new GameObject("Cell");
            BoxCollider2D collider = cellObject.AddComponent<BoxCollider2D>();
            var size = new Vector2(cell.width, cell.height);
            Debug.Log("size: " + size);
            collider.size = size;
            Debug.Log("collider size: " + collider.size);

            Rigidbody2D rb = cellObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f; // Set gravity scale to zero to avoid gravity affecting cells
            rb.freezeRotation = true;
            cellRigidbodies.Add(rb);

            cellObject.transform.position = cell.center;
            cellObject.transform.SetParent(transform);
            
            cellToRigidBody.Add(cell, rb);
        }

        var fixedTimeScale = Time.fixedDeltaTime;
        Time.timeScale = 100;
        Time.fixedDeltaTime = fixedTimeScale * Time.deltaTime;

        hasSpawnedRigidBodies = true;
    }
    private bool RigidbodiesSleeping()
    {
        return cellRigidbodies.All(rb => rb.IsSleeping());
    }

    private Vector2 SnapToGrid(Vector2 position, float gridSize)
    {
        float snappedX = Mathf.Round(position.x / gridSize) * gridSize;
        float snappedY = Mathf.Round(position.y / gridSize) * gridSize;
        
        return new Vector2(snappedX, snappedY);
    }

    private void SetCellPositions()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2 snappedPosition = SnapToGrid(cellRigidbodies[i].position, gridCellSize);
            cells[i].BottomLeft = snappedPosition - new Vector2(cells[i].width / 2f, cells[i].height / 2f);
        }
        
        cells.Sort((c1,c2) => (cellToRigidBody[c1].position-Vector2.zero).sqrMagnitude.CompareTo((cellToRigidBody[c2].position-Vector2.zero).sqrMagnitude));
        CorrectAllOverlaps();
        Time.timeScale = 1;
    }

    private void CorrectAllOverlaps()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            CorrectOverlapForCell(i);
        }
    }

    private void CorrectOverlapForCell(int i)
    {
        var cell = cells[i];

        bool positionValid = false;
        bool positionHasChanged = false;
        Vector2 previousPosition = Vector2.zero; // Store the initial position before correction
        Vector2 previousPreviousPosition = Vector2.zero; // Store the previous position before the initial position

        int iterations = 0;

        while (!positionValid && iterations <= 100)
        {
            positionValid = true;
            iterations++;

            for (int j = 0; j < snappedCells.Count; j++)
            {
                var snappedCell = snappedCells[j];
                if (cell == snappedCell)
                    continue;

                if (cell.Overlaps(snappedCell))
                {
                    positionValid = false;
                    var dif = cell.center - snappedCell.center;

                    if (Mathf.Abs(dif.x) > Mathf.Abs(dif.y))
                    {
                        if (dif.x > 0)
                            cell.BottomLeft += Vector2.right;
                        else
                            cell.BottomLeft += Vector2.left;
                    }
                    else
                    {
                        if (dif.y > 0)
                            cell.BottomLeft += Vector2.up;
                        else
                            cell.BottomLeft += Vector2.down;
                    }

                    positionHasChanged = true;
                    break;
                }
            }
            
            // Check if the cell's position remains the same after adjustment
            if (positionHasChanged && cell.center == previousPreviousPosition)
            {
                // If the position hasn't changed or oscillates between two previous positions,
                // break the while loop to prevent infinite loop
                var dif = previousPosition - previousPreviousPosition;

                if (dif.x != 0)
                {
                    cell.width = Mathf.Max(cell.width - 1, 1);

                    if (Mathf.RoundToInt(cell.width) == 1)
                    {
                        cell.BottomLeft += cell.center.y > 0 ? Vector2.up : Vector2.down;
                    }
                }
                else
                {
                    cell.height = Mathf.Max(cell.height - 1, 1);
                    
                    if (Mathf.RoundToInt(cell.height) == 1)
                    {
                        cell.BottomLeft += cell.center.x > 0 ? Vector2.right : Vector2.left;
                    }
                }
            }

            // Update the previous positions for the next iteration
            previousPreviousPosition = previousPosition;
            previousPosition = cell.center;
        }

        // Add the cell back to the snappedCells list after correction
        snappedCells.Add(cell);
    }

    private void FillGaps()
    {
        var minPoint = new Vector2(float.MaxValue, float.MaxValue);
        var maxPoint = new Vector2(float.MinValue, float.MinValue);

        foreach (var cell in cells)
        {
            minPoint = Vector2.Min(minPoint, cell.center - new Vector2(cell.width / 2f, cell.height / 2f));
            maxPoint = Vector2.Max(maxPoint, cell.center + new Vector2(cell.width / 2f, cell.height / 2f));
        }
        
        Debug.Log("minPoint: " + minPoint  + ", maxPoint: " + maxPoint + ", Grid size: " + (maxPoint.x - minPoint.x) + "x" + (maxPoint.y - minPoint.y) );

        var zeroOffset = Vector2.zero - minPoint;

        foreach (var cell in cells)
        {
            cell.BottomLeft += zeroOffset;
        }

        maxPoint += zeroOffset;

        for (var x = 0f; x < maxPoint.x; x += gridCellSize/2)
        {
            for (var y = 0f; y < maxPoint.y; y += gridCellSize/2)
            {
                Vector2 cellPosition = new Vector2(x, y);
                
                Cell newCell = new Cell(cellPosition, gridCellSize / 2, gridCellSize / 2);
                
                bool cellIntersects = snappedCells.Any(cell => cell.Overlaps(newCell));

                if (!cellIntersects)
                {
                    cells.Add(newCell);
                }
            }
        }

        float qtWidth = maxPoint.x - minPoint.x;
        float qtHeight = maxPoint.y - minPoint.y;

        width = Mathf.RoundToInt(qtWidth);
        height = Mathf.RoundToInt(qtHeight);

        Rect bounds = new Rect(0f, 0f, qtWidth, qtHeight);
        int maxCapacityPerNode = 30;

        cellQuadTree = new QuadTree<Cell>(bounds, maxCapacityPerNode);

        foreach (var cell in cells)
        {
            cellQuadTree.Insert(cell, cell.center);
        }
    }

    private void DetermineRooms()
    {
        //Debug.Log("Finding Rooms");
        rooms = new List<Cell>();
        foreach (var cell in cells)
        {
            if (cell.width * cell.height >= minRoomArea)
            {
                rooms.Add(cell);
            }
        }
        
        CreateDelaunay();
    }

    private void CreateDelaunay()
    {
        var centers = rooms.Select(r => r.center).ToList();
        var delaunay = new Delaunator(centers.ToPoints());

        graph = new DungeonGraph<int>();

        foreach (var room in rooms)
        {
            graph.AddNode(room.id);
        }

        foreach (var triangle in delaunay.GetTriangles())
        {
            var triPoints = triangle.Points.ToVectors2();
            var nodeA = graph.Nodes.Find(node => IdToCell(node.Data).center == triPoints[0]);
            var nodeB = graph.Nodes.Find(node => IdToCell(node.Data).center == triPoints[1]);
            var nodeC = graph.Nodes.Find(node => IdToCell(node.Data).center == triPoints[2]);

            var distance1 = Vector2.Distance(IdToCell(nodeA.Data).center, IdToCell(nodeB.Data).center);
            graph.AddEdge(nodeA, nodeB, distance1);
            
            var distance2 = Vector2.Distance(IdToCell(nodeB.Data).center, IdToCell(nodeC.Data).center);
            graph.AddEdge(nodeB, nodeC, distance2);
            
            var distance3 = Vector2.Distance(IdToCell(nodeC.Data).center, IdToCell(nodeA.Data).center);
            graph.AddEdge(nodeC, nodeA, distance3);
        }

        GetMST();
        GetFinalEdges();
        
        drawDelaunay = true;
    }

    private void GetMST()
    {
        mst = graph.MinimumSpanningTree();
    }
    
    private void GetFinalEdges()
    {
        finalEdges = graph.MstAndAddedEdges(additionalEdgesFraction);
    }

    private void ConvertGraphToCorridors()
    {
        Debug.Log("Converting to corridors");
        corridors = new List<List<LineSegment>>();
        foreach (var edge in finalEdges)
        {
            var roomCenterA = IdToCell(edge.From.Data);
            var roomCenterB = IdToCell(edge.To.Data);

            var corridorShape = CalculateCorridor(roomCenterA, roomCenterB);
            corridors.Add(corridorShape);
        }

        

        var watch = new System.Diagnostics.Stopwatch();
        
        watch.Start();

        corridorCells = new HashSet<Cell>();
        object corridorCellsLock = new object();
        
        float searchRadius = 6f;
        Parallel.ForEach(corridors, corridor =>
        {
            foreach (var lineSegment in corridor)
            {
                Rect searchRange = GetSearchRect(lineSegment, searchRadius);

                var cellsInRange = cellQuadTree.QueryRange(searchRange);
                Debug.Log("Cells in range: " + cellsInRange.Count);
                
                foreach (var cell in cellsInRange)
                {
                    if (rooms.Contains(cell))
                        continue;
                    lock(corridorCellsLock)
                    {
                        if (!corridorCells.Contains(cell))
                        {
                            if (LineSegmentIntersectsRectangle(lineSegment, cell))
                            {
                                //Debug.Log("cell: " + cell.id + ", line: [" + lineSegment.start + ", " + lineSegment.end + "]");
                                corridorCells.Add(cell);
                            }
                        }
                    }
                }
            }
        });
        watch.Stop();
        
        Debug.Log("Number of cells: " + cells.Count + ", Number of corridors: " + corridors.Count + ", time to determine corridor cells: " + (watch.ElapsedMilliseconds / 1000f));

        drawCorridors = true;
    }
    
    private Rect GetSearchRect(LineSegment line, float radius)
    {
        // Get the normalized direction vector and perpendicular vector
        Vector2 lineDirection = line.GetDirection();
        Vector2 perpendicular = line.GetPerpendicular();

        // Calculate the offset in the perpendicular direction scaled by the radius
        Vector2 offset = perpendicular * radius;

        // Calculate the four corners of the search Rect
        Vector2 corner1 = line.start + offset;
        Vector2 corner2 = line.start - offset;
        Vector2 corner3 = line.end + offset;
        Vector2 corner4 = line.end - offset;

        // Find the minimum and maximum values for each coordinate
        float minX = Mathf.Min(corner1.x, corner2.x, corner3.x, corner4.x);
        float minY = Mathf.Min(corner1.y, corner2.y, corner3.y, corner4.y);
        float maxX = Mathf.Max(corner1.x, corner2.x, corner3.x, corner4.x);
        float maxY = Mathf.Max(corner1.y, corner2.y, corner3.y, corner4.y);

        // Create the search Rect based on the calculated min and max coordinates
        Rect searchRect = new Rect(minX, minY, maxX - minX, maxY - minY);

        return searchRect;
    }
    
    private List<LineSegment> CalculateCorridor(Cell roomA, Cell roomB)
    {
        Vector2 midpoint = SnapToGrid((roomA.center + roomB.center) / 2f, gridCellSize);
        
        List<LineSegment> corridorShape = new List<LineSegment>();

        bool midpointIsInsideX = midpoint.x >= roomA.BottomLeft.x &&
                                 midpoint.x <= roomA.BottomLeft.x + roomA.width &&
                                 midpoint.x >= roomB.BottomLeft.x &&
                                 midpoint.x <= roomB.BottomLeft.x + roomB.width;
        bool midpointIsInsideY = midpoint.y >= roomA.BottomLeft.y &&
                                 midpoint.y <= roomA.BottomLeft.y + roomA.height &&
                                 midpoint.y >= roomB.BottomLeft.y &&
                                 midpoint.y <= roomB.BottomLeft.y + roomB.height;

        if (midpointIsInsideX)
        {
            corridorShape.Add(new LineSegment{start = new Vector2(midpoint.x, roomA.center.y), end = new Vector2(midpoint.x, roomB.center.y)});
            corridorShape.Add(new LineSegment{start = new Vector2(midpoint.x - (gridCellSize/2f), roomA.center.y), end = new Vector2(midpoint.x - (gridCellSize/2f), roomB.center.y)});
            corridorShape.Add(new LineSegment{start = new Vector2(midpoint.x + (gridCellSize/2f), roomA.center.y), end = new Vector2(midpoint.x + (gridCellSize/2f), roomB.center.y)});

        }
        else if (midpointIsInsideY)
        {
            corridorShape.Add(new LineSegment{ start = new Vector2(roomA.center.x, midpoint.y), end = new Vector2(roomB.center.x, midpoint.y)});
            corridorShape.Add(new LineSegment{ start = new Vector2(roomA.center.x, midpoint.y - (gridCellSize/2f)), end = new Vector2(roomB.center.x, midpoint.y - (gridCellSize/2f))});
            corridorShape.Add(new LineSegment{ start = new Vector2(roomA.center.x, midpoint.y + (gridCellSize/2f)), end = new Vector2(roomB.center.x, midpoint.y + (gridCellSize/2f))});
        }
        else
        {
            corridorShape.Add(new LineSegment{start = roomA.center, end = new Vector2(roomB.center.x, roomA.center.y)});

            if (roomA.center.x < roomB.center.x)
            {
                if (roomA.center.y < roomB.center.y)
                {
                    corridorShape.Add(new LineSegment{start = roomA.center + new Vector2(0, gridCellSize / 2f), end = new Vector2(roomB.center.x - gridCellSize / 2f, roomA.center.y + gridCellSize / 2f)});
                    corridorShape.Add(new LineSegment{start = roomA.center - new Vector2(0, gridCellSize / 2f), end = new Vector2(roomB.center.x + gridCellSize / 2f, roomA.center.y - gridCellSize / 2f)});
                }
                else
                {
                    corridorShape.Add(new LineSegment{start = roomA.center + new Vector2(0, gridCellSize / 2f), end = new Vector2(roomB.center.x + gridCellSize / 2f, roomA.center.y + gridCellSize / 2f)});
                    corridorShape.Add(new LineSegment{start = roomA.center - new Vector2(0, gridCellSize / 2f), end = new Vector2(roomB.center.x - gridCellSize / 2f, roomA.center.y - gridCellSize / 2f)});
                }
            }
            else
            {
                if (roomA.center.y < roomB.center.y)
                {
                    corridorShape.Add(new LineSegment{start = roomA.center + new Vector2(0, gridCellSize / 2f), end = new Vector2(roomB.center.x + gridCellSize/2f, roomA.center.y + gridCellSize / 2f)});
                    corridorShape.Add(new LineSegment{start = roomA.center - new Vector2(0, gridCellSize / 2f), end = new Vector2(roomB.center.x - gridCellSize/2f, roomA.center.y - gridCellSize / 2f)});
                }
                else
                {
                    corridorShape.Add(new LineSegment{start = roomA.center + new Vector2(0, gridCellSize / 2f), end = new Vector2(roomB.center.x - gridCellSize/2f, roomA.center.y + gridCellSize / 2f)});
                    corridorShape.Add(new LineSegment{start = roomA.center - new Vector2(0, gridCellSize / 2f), end = new Vector2(roomB.center.x + gridCellSize/2f, roomA.center.y - gridCellSize / 2f)});
                }
            }
            
            corridorShape.Add(new LineSegment{start = new Vector2(roomB.center.x, roomA.center.y), end = roomB.center});
            
            if (roomA.center.x < roomB.center.x)
            {
                if (roomA.center.y < roomB.center.y)
                {
                    corridorShape.Add(new LineSegment{start = new Vector2(roomB.center.x - gridCellSize / 2f, roomA.center.y + gridCellSize / 2f), end = roomB.center - new Vector2(gridCellSize/2f, 0)});
                    corridorShape.Add(new LineSegment{start = new Vector2(roomB.center.x + gridCellSize / 2f, roomA.center.y - gridCellSize / 2f), end = roomB.center + new Vector2(gridCellSize/2f, 0)});
                }
                else
                {
                    corridorShape.Add(new LineSegment{start = new Vector2(roomB.center.x + gridCellSize / 2f, roomA.center.y + gridCellSize / 2f), end = roomB.center + new Vector2(gridCellSize/2f, 0)});
                    corridorShape.Add(new LineSegment{start = new Vector2(roomB.center.x - gridCellSize / 2f, roomA.center.y - gridCellSize / 2f), end = roomB.center - new Vector2(gridCellSize/2f, 0)});
                }
            }
            else
            {
                if (roomA.center.y < roomB.center.y)
                {
                    corridorShape.Add(new LineSegment{start = new Vector2(roomB.center.x + gridCellSize/2f, roomA.center.y + gridCellSize / 2f), end = roomB.center + new Vector2(gridCellSize/2f, 0)});
                    corridorShape.Add(new LineSegment{start = new Vector2(roomB.center.x - gridCellSize/2f, roomA.center.y - gridCellSize / 2f), end = roomB.center - new Vector2(gridCellSize/2f, 0)});
                }
                else
                {
                    corridorShape.Add(new LineSegment{start = new Vector2(roomB.center.x - gridCellSize/2f, roomA.center.y + gridCellSize / 2f), end = roomB.center - new Vector2(gridCellSize/2f, 0)});
                    corridorShape.Add(new LineSegment{start = new Vector2(roomB.center.x + gridCellSize/2f, roomA.center.y - gridCellSize / 2f), end = roomB.center + new Vector2(gridCellSize/2f, 0)});
                }
            }
        }
        return corridorShape;
    }
    
    bool LineSegmentsIntersect(LineSegment line1, LineSegment line2)
    {
        Vector2 p = line1.start;
        Vector2 r = line1.end - line1.start;
        Vector2 q = line2.start;
        Vector2 s = line2.end - line2.start;

        float crossR_S = Vector3.Cross(r, s).z;
        Vector2 q_p = q - p;
        float t = Vector3.Cross(q_p, s).z / crossR_S;
        float u = Vector3.Cross(q_p, r).z / crossR_S;

        if (crossR_S == 0 && Vector3.Cross(q_p, r).z == 0)
        {
            // The lines are collinear
            float t0 = Vector3.Dot(q_p, r) / r.sqrMagnitude;
            float t1 = t0 + Vector3.Dot(s, r) / r.sqrMagnitude;
            if (t0 > t1)
            {
                (t0, t1) = (t1, t0);
            }
            return !(t1 < 0 || t0 > 1);
        }

        return (crossR_S != 0) && (t >= 0 && t <= 1) && (u >= 0 && u <= 1);
    }
    
    bool LineSegmentIntersectsRectangle(LineSegment line, Cell cell)
    {
        // Convert the rectangle into four line segments
        LineSegment[] cellLines = new LineSegment[4];
        cellLines[0] = new LineSegment { start = cell.BottomLeft, end = cell.BottomLeft + new Vector2(cell.width, 0) };
        cellLines[1] = new LineSegment { start = cell.BottomLeft + new Vector2(cell.width, 0), end = cell.BottomLeft + new Vector2(cell.width, cell.height) };
        cellLines[2] = new LineSegment { start = cell.BottomLeft + new Vector2(cell.width, cell.height), end = cell.BottomLeft + new Vector2(0, cell.height) };
        cellLines[3] = new LineSegment { start = cell.BottomLeft + new Vector2(0, cell.height), end = cell.BottomLeft };

        // Check if any of the line segments intersect with the input line segment
        foreach (LineSegment cellLine in cellLines)
        {
            if (LineSegmentsIntersect(line, cellLine))
            {
                return true;
            }
                
        }

        if (cell.Contains(line.start) && cell.Contains(line.end))
        {
            return true;
        }

        return false;
    }
    
    private Cell IdToCell(int id)
    {
        return cells.Find(c => c.id == id);
    }

    private void FixedUpdate()
    {
        /*if (hasSpawnedRigidBodies && RigidbodiesSleeping() && !hasSetSeperatedPosition)
        {
            drawRigidBodies = false;
            hasSetSeperatedPosition = true;
            //SetCellPositions();
            //FillGaps();
            //DetermineRooms();
        }*/
    }
    
    /*private void OnDrawGizmosSelected()
    {
        // Draw each cell as a rectangle
        if (drawCells)
        {
            Gizmos.color = Color.red;
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                Gizmos.DrawWireCube(cell.center, new Vector3(cell.width, cell.height, 0));
            }
        }

        if(drawRigidBodies)
        {
            if(hasSpawnedRigidBodies)
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < cells.Count; i++)
                {
                    //var pos = rbPositions[i];
                    var cell = cells[i];
                    var cellRB = cellToRigidBody[cell];
                    Gizmos.DrawWireCube(cellRB.position, new Vector3(cell.width, cell.height, 0));
                }
            }
        }
        
        // Draw each cell as a rectangle
        if (drawSnappedCells)
        {
            Gizmos.color = Color.yellow;
            for (var i = 0; i < snappedCells.Count; i++)
            {
                var cell = snappedCells[i];
                Gizmos.DrawWireCube(cell.center, new Vector3(cell.width, cell.height, 0));
                
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.white;
                style.alignment = TextAnchor.MiddleCenter;
                Handles.Label(cell.center, cell.id.ToString(), style);
            }
        }
        
        if (drawRooms)
        {
            Gizmos.color = Color.magenta;
            for (var i = 0; i < rooms.Count; i++)
            {
                var cell = rooms[i];
                Gizmos.DrawWireCube(cell.center, new Vector3(cell.width, cell.height, 0));
            }
        }

        if (drawDelaunay)
        {
            foreach (var node in graph.Nodes)
            {
                if (node.Neighbors != null)
                {
                    foreach (var neighbor in node.Neighbors)
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawLine(IdToCell(node.Data).center, IdToCell(neighbor.To.Data).center);
                    }
                }
            }
        }
        
        if (drawMst)
        {
            Gizmos.color = Color.green;
            foreach (var edge in mst)
            {
                Gizmos.DrawLine(IdToCell(edge.From.Data).center, IdToCell(edge.To.Data).center);
            }
        }
        
        if (drawFinalGraph)
        {
            Gizmos.color = Color.white;
            foreach (var edge in finalEdges)
            {
                Gizmos.DrawLine(IdToCell(edge.From.Data).center, IdToCell(edge.To.Data).center);
            }
        }
        
        if (drawCorridors)
        {
            Gizmos.color = new Color(0/255f, 156f/255f, 140f/255f);
            foreach (var cell in corridorCells)
            {
                Gizmos.DrawWireCube(cell.center, new Vector3(cell.width, cell.height, 0));
            }
            
            /*Gizmos.color = new Color(125f/255f, 52f/255f, 235f/255f);

            foreach (var corridor in corridors)
            {
                foreach (var lineSegment in corridor)
                {
                    Gizmos.DrawLine(lineSegment.start, lineSegment.end);
                }
            }
        }
    }*/
}
