using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using TMPro;

public class MMazeGameManager : MonoBehaviour
{
    [Header("Maze Size (cells)")]
    public int width = 20;
    public int height = 20; // 0=길, 1=벽, 2=표시용, 3=출구
    private int[,] maze;

    [Header("Tilemaps")]
    public Tilemap Tilemap_Maze;    // 벽/바닥/시작/출구 전체 렌더
    public Tilemap Tilemap_Trail;   // (선택) 기존 셀단위 트레일용 - 이번 버전에선 미사용

    [Header("Tiles (pre-colored assets)")]
    public TileBase Tile_Wall;      // wall_10.png
    public TileBase Tile_Floor;     // floor_10.png
    public TileBase Tile_Start;     // start_10.png (optional)
    public TileBase Tile_Exit;      // exit_10.png  (optional)

    [Header("Player (free-move)")]
    public Sprite playerSprite5px;  // player_5.png (PPU=10)
    public float moveSpeed = 4f;

    [Header("Player Size (world units)")]
    [Tooltip("플레이어 보이는 크기(정사각). 벽=1.0 → 요청: 0.35")]
    public float playerWorldSize = 0.5f;
    private Vector2 playerHalf;     // 충돌 AABB 반폭

    private GameObject playerGO;
    private Vector2 playerPosW;     // 월드 좌표(자유 이동)

    [Header("UI / Gameplay")]
    public TextMeshProUGUI Text_Timer;
    public float timeLimit = 50f;
    public float speedBonus = 1f;

    private float timeLeft;
    private bool isRunning;
    private Vector2Int exitPos;

    [Header("Camera / View")]
    [Tooltip("탑뷰에서 미로 전체 반경 + 여백(유닛). 여백 커질수록 화면에 검정여백이 생김")]
    public float cameraPadding = 2f;

    // 셀→월드 변환 오프셋(미로 중앙을 (0,0)에)
    private Vector2 gridOffset; // (-width/2+0.5, -height/2+0.5)

    // ===== Trail(발자국) : 스프라이트 스탬프 방식 =====
    [Header("Trail (sprite stamps)")]
    [Tooltip("trail_10.png (PPU=10) 할당")]
    public Sprite trailSprite;
    [Tooltip("새 발자국을 남길 최소 이동거리(유닛)")]
    public float trailStep = 0.08f;      // 더 촘촘/성긴 간격 조절
    [Tooltip("발자국 정렬: Z, SortingOrder는 스프라이트렌더러에서 조정")]
    public int trailSortingOrder = 4;

    private Transform trailRoot;         // 발자국 부모
    private Vector2 lastTrailPos;        // 마지막 발자국 위치
    private readonly Queue<SpriteRenderer> trailPool = new(); // 간단 풀(선택)
    public int trailPoolMax = 2000;      // 메모리 방지용 상한 (원하면 0=무제한)

    // ---------- Unity ----------
    private void Start() => StartGame();

    private void Update()
    {
        if (!isRunning) return;

        // 타이머
        timeLeft -= Time.deltaTime;
        if (Text_Timer) Text_Timer.text = $"Time: {timeLeft:F1}s";
        if (timeLeft <= 0f) { EndGame(false); return; }

        HandleInput_FreeMove();   // 자유 이동 + 벽 닿으면 실패
        StampTrailIfNeeded();     // 플레이어 크기의 발자국을 현재/지나간 자리에만
        CheckComplete();          // 출구 도달 판정
    }

    // ---------- Flow ----------
    public void StartGame()
    {
        timeLeft = timeLimit;
        isRunning = true;

        InitMaze();
        GenerateMazeDFS();
        PlaceStartAndExit();

        SetupGridAndCameraTopView();
        DrawWholeMazeOnce();

        SpawnPlayerSpriteAtStart(); // 크기=playerWorldSize로 스케일, 충돌 AABB 동기화
        SetupTrailRoot();

        // 시작 지점에도 첫 발자국
        lastTrailPos = playerPosW - Vector2.one * 999f;
        StampTrailIfNeeded(force:true);

        if (Text_Timer) Text_Timer.text = $"Time: {timeLeft:F1}s";
        Debug.Log("[MMaze] Start (TopView + FreeMove, size=0.35, wall touch=fail, sprite trail)");
    }

    public void EndGame(bool isSuccess)
    {
        if (!isRunning) return;
        isRunning = false;

        if (isSuccess)
        {
            Debug.Log("🎉 성공: 출구 도달");
            SendPlayer_Speed();
        }
        else
        {
            Debug.Log("❌ 실패: 시간 초과 또는 벽 접촉");
        }
    }

    public void SendPlayer_Speed()
    {
        Debug.Log($"Success");
    }

    // ---------- Maze build ----------
    public void InitMaze()
    {
        // DFS는 홀수 격자 권장 (짝수면 -1 보정)
        if (width % 2 == 0)  width  -= 1;
        if (height % 2 == 0) height -= 1;

        maze = new int[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                maze[x, y] = 1; // 전부 벽
    }

    private void GenerateMazeDFS()
    {
        Stack<Vector2Int> st = new();
        Vector2Int start = new(1, 1);
        maze[start.x, start.y] = 0;
        st.Push(start);

        Vector2Int[] dirs = { Vector2Int.up * 2, Vector2Int.down * 2, Vector2Int.left * 2, Vector2Int.right * 2 };

        while (st.Count > 0)
        {
            var cur = st.Pop();
            Shuffle(dirs);
            foreach (var d in dirs)
            {
                var n = cur + d;
                if (InBounds(n) && maze[n.x, n.y] == 1)
                {
                    maze[cur.x + d.x / 2, cur.y + d.y / 2] = 0; // 사이벽 허물기
                    maze[n.x, n.y] = 0;
                    st.Push(n);
                }
            }
        }
    }

    private void PlaceStartAndExit()
    {
        // 시작점은 (1,1) 셀
        maze[1, 1] = 0;

        // 출구: 우하단 근처의 길 셀
        Vector2Int candidate = new(width - 2, height - 2);
        if (maze[candidate.x, candidate.y] == 1)
        {
            bool found = false;
            for (int x = width - 2; x >= 1 && !found; x--)
                for (int y = height - 2; y >= 1 && !found; y--)
                    if (maze[x, y] == 0) { candidate = new Vector2Int(x, y); found = true; }
        }
        exitPos = candidate;
        maze[exitPos.x, exitPos.y] = 3;
    }

    // ---------- Render (Top View) ----------
    private void SetupGridAndCameraTopView()
    {
        gridOffset = new Vector2(-width / 2f + 0.5f, -height / 2f + 0.5f);

        if (Tilemap_Maze)   Tilemap_Maze.transform.position = (Vector3)gridOffset;
        if (Tilemap_Trail)  Tilemap_Trail.transform.position = (Vector3)gridOffset;

        var cam = Camera.main;
        if (cam)
        {
            cam.orthographic    = true;
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.transform.position = new Vector3(0, 0, -10);

            // ★ 여백 추가: 반경(=max/2) + cameraPadding
            cam.orthographicSize = Mathf.Max(width, height) * 0.5f + cameraPadding;
        }
    }

    private void DrawWholeMazeOnce()
    {
        if (!Tilemap_Maze || !Tile_Wall || !Tile_Floor) return;
        Tilemap_Maze.ClearAllTiles();

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            var c = new Vector3Int(x, y, 0);
            int v = maze[x, y];
            Tilemap_Maze.SetTile(c, v == 1 ? Tile_Wall : Tile_Floor);
        }

        if (Tile_Start) Tilemap_Maze.SetTile(new Vector3Int(1, 1, 0), Tile_Start);
        if (Tile_Exit)  Tilemap_Maze.SetTile(new Vector3Int(exitPos.x, exitPos.y, 0), Tile_Exit);
    }

    // ---------- Player (free-move) ----------
    private void SpawnPlayerSpriteAtStart()
    {
        Vector2 startWorld = CellCenterWorld(new Vector2Int(1, 1));

        playerGO = new GameObject("Player_FreeMove");
        var sr = playerGO.AddComponent<SpriteRenderer>();
        sr.sprite = playerSprite5px;  // 어떤 픽셀 크기라도 OK (스케일로 맞춤)
        sr.sortingOrder = 10;

        // 스프라이트의 기본 월드 크기(유닛) 계산
        Vector2 spriteUnits = sr.sprite.rect.size / sr.sprite.pixelsPerUnit; // 예: 5x5, PPU=10 → 0.5x0.5
        float baseSize = Mathf.Max(spriteUnits.x, spriteUnits.y);

        // 목표 시각 크기(playerWorldSize)에 맞춰 스케일
        float scale = (baseSize > 0f) ? (playerWorldSize / baseSize) : 1f;
        playerGO.transform.localScale = new Vector3(scale, scale, 1f);

        // 충돌 반폭도 시각 크기에 맞춤
        playerHalf = Vector2.one * (playerWorldSize * 0.5f);

        playerGO.transform.position = startWorld;
        playerPosW = startWorld;
    }

    private void HandleInput_FreeMove()
    {
        // WASD/화살표
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2 dir = new(h, v);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        Vector2 delta = dir * moveSpeed * Time.deltaTime;
        if (delta == Vector2.zero) return;

        TryMove_FailOnTouch(delta);          // 벽 닿으면 즉시 실패
        if (playerGO) playerGO.transform.position = playerPosW;
    }

    // ★ 벽에 닿으면 즉시 실패
    private void TryMove_FailOnTouch(Vector2 delta)
    {
        // X축
        Vector2 test = playerPosW + new Vector2(delta.x, 0);
        if (CollidesWithWall(test)) { EndGame(false); return; }
        else                         { playerPosW = test; }

        // Y축
        test = playerPosW + new Vector2(0, delta.y);
        if (CollidesWithWall(test)) { EndGame(false); return; }
        else                         { playerPosW = test; }
    }

    // 플레이어 AABB의 4코너가 벽 셀과 겹치면 true
    private bool CollidesWithWall(Vector2 worldPos)
    {
        Vector2 min = worldPos - playerHalf;
        Vector2 max = worldPos + playerHalf;

        // 바깥 영역은 벽 취급
        if (IsWallAtWorld(min.x, min.y)) return true;
        if (IsWallAtWorld(min.x, max.y)) return true;
        if (IsWallAtWorld(max.x, min.y)) return true;
        if (IsWallAtWorld(max.x, max.y)) return true;
        return false;
    }

    private bool IsWallAtWorld(float wx, float wy)
    {
        int cx = Mathf.FloorToInt(wx - gridOffset.x);
        int cy = Mathf.FloorToInt(wy - gridOffset.y);
        if (cx < 0 || cy < 0 || cx >= width || cy >= height) return true; // 바깥은 벽
        return maze[cx, cy] == 1;
    }

    private Vector2 CellCenterWorld(Vector2Int cell)
    {
        return new Vector2(gridOffset.x + cell.x + 0.5f, gridOffset.y + cell.y + 0.5f);
    }

    // ---------- Trail: sprite stamps ----------
    private void SetupTrailRoot()
    {
        var go = new GameObject("TrailRoot");
        trailRoot = go.transform;
        trailRoot.position = Vector3.zero;
    }

    private void StampTrailIfNeeded(bool force = false)
    {
        if (!trailSprite) return;

        float dist = Vector2.Distance(playerPosW, lastTrailPos);
        if (!force && dist < trailStep) return;

        // 스탬프 1개 찍기 (플레이어 크기와 동일)
        var sr = GetTrailRendererFromPool();
        sr.sprite = trailSprite;
        sr.sortingOrder = trailSortingOrder;

        // trailSprite의 기본 유닛 크기 계산 → playerWorldSize에 맞춰 스케일
        Vector2 spriteUnits = sr.sprite.rect.size / sr.sprite.pixelsPerUnit; // 보통 1.0유닛(10px/PPU10) 가정
        float baseSize = Mathf.Max(spriteUnits.x, spriteUnits.y);
        float scale = (baseSize > 0f) ? (playerWorldSize / baseSize) : 1f;

        var t = sr.transform;
        t.SetParent(trailRoot, false);
        t.position = new Vector3(playerPosW.x, playerPosW.y, 0f);
        t.localScale = new Vector3(scale, scale, 1f);
        sr.enabled = true;

        lastTrailPos = playerPosW;
    }

    private SpriteRenderer GetTrailRendererFromPool()
    {
        // 간단 풀: 상한 넘어가면 가장 오래된 스탬프를 재사용
        if (trailPool.Count > 0)
        {
            var sr = trailPool.Dequeue();
            return sr;
        }
        else
        {
            var go = new GameObject("TrailStamp");
            var sr = go.AddComponent<SpriteRenderer>();
            return sr;
        }
    }

    // 필요 시 발자국 정리함수(선택)
    private void ReturnTrailToPool(SpriteRenderer sr)
    {
        if (!sr) return;
        if (trailPoolMax <= 0 || trailPool.Count < trailPoolMax)
        {
            sr.enabled = false;
            sr.transform.SetParent(trailRoot, false);
            trailPool.Enqueue(sr);
        }
        else
        {
            Destroy(sr.gameObject);
        }
    }

    private void CheckComplete()
    {
        // 출구 셀의 월드 AABB와 플레이어 AABB가 겹치면 성공
        Vector2 exitCenter = CellCenterWorld(exitPos);
        Vector2 exitHalf   = new(0.5f, 0.5f); // 한 칸 = 1×1 유닛
        bool overlap = AABBOverlap(playerPosW, playerHalf, exitCenter, exitHalf);
        if (overlap) EndGame(true);
    }

    private static bool AABBOverlap(Vector2 cA, Vector2 hA, Vector2 cB, Vector2 hB)
    {
        return Mathf.Abs(cA.x - cB.x) <= (hA.x + hB.x) &&
               Mathf.Abs(cA.y - cB.y) <= (hA.y + hB.y);
    }

    // ---------- Utils ----------
    private bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < width && p.y < height;

    private static void Shuffle(Vector2Int[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            int r = Random.Range(i, arr.Length);
            (arr[i], arr[r]) = (arr[r], arr[i]);
        }
    }
}
