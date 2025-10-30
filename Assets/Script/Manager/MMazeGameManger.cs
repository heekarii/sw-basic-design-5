using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using TMPro; // ✅ TextMeshPro용

public class MMazeGameManager : MonoBehaviour
{
    [Header("RQ_ID 4301~4312 | Maze Settings")]
    [Tooltip("미로 가로/세로 크기 (요구: 50x50)")]
    public int width = 50;
    public int height = 50;

    [Tooltip("0=길, 1=벽, 2=플레이어, 3=탈출구")]
    public int[,] maze;

    [Header("Tilemap & Tiles")]
    public Tilemap Tilemap_Maze;
    public TileBase Tile_Wall;
    public TileBase Tile_Floor;

    [Header("Game Runtime")]
    [Tooltip("제한시간(초) - 요구: 50s")]
    public float timeLimit = 50f;
    private float timeLeft;
    private bool isRunning;

    [Tooltip("성공 시 플레이어 이동속도 증가값")]
    public float speedBonus = 0.2f;

    [Header("UI Components")]
    [Tooltip("왼쪽 상단 제한시간 표시 Text (TMP)")]
    public TextMeshProUGUI Text_Timer; // ✅ TMP 연결

    private Vector2Int playerPos;
    private Vector2Int exitPos;

    private readonly Dictionary<KeyCode, Vector2Int> inputMap = new()
    {
        { KeyCode.W, Vector2Int.up },
        { KeyCode.S, Vector2Int.down },
        { KeyCode.A, Vector2Int.left },
        { KeyCode.D, Vector2Int.right }
    };

    private void Start() { StartGame(); } // 테스트 시 자동 실행

    // ====== StartGame ======
    public void StartGame()
    {
        timeLeft = timeLimit;
        isRunning = true;

        InitMaze();
        GenerateMazeDFS();
        PlaceStartAndExit();
        ShowMaze();

        UpdateTimerUI();
        Debug.Log("[MMazeGameManager] StartGame() → 미로 생성 및 게임 시작");
    }

    // ====== InitMaze ======
    public void InitMaze()
    {
        width = (width % 2 == 0) ? width - 1 : width;
        height = (height % 2 == 0) ? height - 1 : height;

        maze = new int[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                maze[x, y] = 1;
    }

    // ====== GenerateMazeDFS ======
    private void GenerateMazeDFS()
    {
        Stack<Vector2Int> stack = new();
        Vector2Int start = new(1, 1);
        maze[start.x, start.y] = 0;
        stack.Push(start);

        Vector2Int[] dirs = { Vector2Int.up * 2, Vector2Int.down * 2, Vector2Int.left * 2, Vector2Int.right * 2 };

        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            Shuffle(dirs);
            foreach (var dir in dirs)
            {
                var next = cur + dir;
                if (InBounds2(next, width, height) && maze[next.x, next.y] == 1)
                {
                    maze[cur.x + dir.x / 2, cur.y + dir.y / 2] = 0;
                    maze[next.x, next.y] = 0;
                    stack.Push(next);
                }
            }
        }
    }

    // ====== ShowMaze ======
    public void ShowMaze()
    {
        Tilemap_Maze.ClearAllTiles();
        int w = maze.GetLength(0);
        int h = maze.GetLength(1);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var pos = new Vector3Int(x, y, 0);
                if (maze[x, y] == 1) Tilemap_Maze.SetTile(pos, Tile_Wall);
                else                 Tilemap_Maze.SetTile(pos, Tile_Floor);
                // 기본 색은 흰색(또는 원하는 바닥색)
                Tilemap_Maze.SetColor(pos, Color.white);
            }
        }

        // ✅ 입구/출구 색 강조
        var startPos = new Vector3Int(playerPos.x, playerPos.y, 0);
        var exitPos3 = new Vector3Int(exitPos.x, exitPos.y, 0);

        Tilemap_Maze.SetTile(startPos, Tile_Floor);
        Tilemap_Maze.SetColor(startPos, Color.green);

        Tilemap_Maze.SetTile(exitPos3, Tile_Floor);
        Tilemap_Maze.SetColor(exitPos3, Color.red);
    }


    // ====== HandleInput ======
    public void HandleInput(Vector3 dir3D)
    {
        if (!isRunning) return;

        var dir = new Vector2Int(
            dir3D.x > 0 ? 1 : dir3D.x < 0 ? -1 : 0,
            dir3D.y > 0 ? 1 : dir3D.y < 0 ? -1 : 0
        );
        if (dir.x != 0 && dir.y != 0) return;
        if (dir == Vector2Int.zero) return;

        Vector2Int next = playerPos + dir;
        if (!InBounds2(next, width, height)) return;

        if (maze[next.x, next.y] == 1)
        {
            if (IsCollision() == 1) EndGame(0);
            return;
        }

        maze[playerPos.x, playerPos.y] = 0;
        playerPos = next;
        maze[playerPos.x, playerPos.y] = 2;

        IsCompleteGame();
    }

    public int IsCollision() => 1;

    public void IsCompleteGame()
    {
        if (playerPos == exitPos) EndGame(1);
    }

    public void EndGame(int result)
    {
        if (!isRunning) return;
        isRunning = false;

        if (result == 1)
        {
            Debug.Log("🎉 [MMazeGameManager] 성공: 탈출 성공!");
            SendPlayer_Speed();
        }
        else
        {
            Debug.Log("❌ [MMazeGameManager] 실패: 벽 충돌 또는 시간 초과");
        }
    }

    public void SendPlayer_Speed()
    {
        Debug.Log($"[MMazeGameManager] 이동속도 +{speedBonus} 전달 (Player 연동 필요)");
        // FindObjectOfType<Player>()?.AddMoveSpeed(speedBonus);
    }

    private void Update()
    {
        if (!isRunning) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            EndGame(0);
            return;
        }

        UpdateTimerUI();

        foreach (var kv in inputMap)
        {
            if (Input.GetKeyDown(kv.Key))
            {
                var v2 = kv.Value;
                HandleInput(new Vector3(v2.x, v2.y, 0f));
                break;
            }
        }
    }

    // ====== Timer UI 업데이트 ======
    private void UpdateTimerUI()
    {
        if (Text_Timer != null)
        {
            Text_Timer.text = $"Time: {timeLeft:F1}s";
        }
    }

    // ====== 출발/도착 배치 ======
    private void PlaceStartAndExit()
    {
        playerPos = new Vector2Int(1, 1);
        maze[playerPos.x, playerPos.y] = 2;

        // 출구는 하단 근처의 길(0) 위에 배치
        for (int x = width - 2; x > width / 2; x--)
        {
            for (int y = height - 2; y > height / 2; y--)
            {
                if (maze[x, y] == 0)
                {
                    exitPos = new Vector2Int(x, y);
                    maze[exitPos.x, exitPos.y] = 3;
                    return;
                }
            }
        }
    }

    private static bool InBounds2(Vector2Int p, int w, int h)
        => p.x >= 0 && p.y >= 0 && p.x < w && p.y < h;

    private static void Shuffle(Vector2Int[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            int r = Random.Range(i, arr.Length);
            (arr[i], arr[r]) = (arr[r], arr[i]);
        }
    }
}
