using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Bezier Spline ana bileşeni.
/// Bu script bir GameObject'e eklenir ve Bezier eğrisi verilerini tutar.
/// 
/// [ExecuteAlways] → Bu script PLAY MODE'a gerek kalmadan, EDIT MODE'da da çalışır!
/// Case'in en önemli gereksinimi bu: "Must run in edit mode, not in play mode."
/// 
/// Kullanım:
/// 1. Boş bir GameObject oluştur
/// 2. Bu scripti ekle
/// 3. Scene view'da kontrol noktalarını sürükle
/// 4. Editor Window'dan Distance/Count mode ile node'ları yerleştir
/// </summary>
[ExecuteAlways]
public class BezierSpline : MonoBehaviour
{
    // =========================================================================
    // AYARLAR (Inspector'da görünecek)
    // =========================================================================

    [Header("Kontrol Noktaları")]
    [Tooltip("Bezier eğrisinin kontrol noktaları. İlk ve son = eğrinin uçları, ortadakiler = şekil veren referans noktaları")]
    public Vector3[] controlPoints = new Vector3[]
    {
        // Varsayılan 4 kontrol noktası (kübik Bezier = 3. derece)
        // Düz bir S şeklinde başlangıç konfigürasyonu
        new Vector3(0f, 0f, 0f),      // Başlangıç (kontrol noktası)
        new Vector3(5f, 0f, 5f),      // 1. referans noktası
        new Vector3(10f, 0f, -5f),    // 2. referans noktası
        new Vector3(15f, 0f, 0f)      // Bitiş (kontrol noktası)
    };

    [Header("Şerit Ayarları")]
    [Tooltip("İki şerit arasındaki toplam genişlik (metre cinsinden)")]
    [Range(1f, 20f)]
    public float laneWidth = 4f;

    [Header("Çizim Ayarları")]
    [Tooltip("Eğri çiziminde kullanılacak nokta sayısı. Yüksek = daha pürüzsüz ama daha yavaş")]
    [Range(10, 200)]
    public int curveResolution = 50;

    [Header("Node Ayarları")]
    [Tooltip("Oluşturulan node GameObject'lerinin listesi")]
    public List<GameObject> generatedNodes = new List<GameObject>();

    [Tooltip("Oluşturulan yol mesh'inin GameObject'i")]
    public GameObject roadMeshObject;

    // =========================================================================
    // ŞERİT (LANE) FONKSİYONLARI
    // =========================================================================

    /// <summary>
    /// Sol şerit kontrol noktalarını döndürür.
    /// Merkez eğriden laneWidth/2 kadar sola (normal yönünde) kaydırılmış.
    /// </summary>
    public Vector3[] GetLeftLaneControlPoints()
    {
        return BezierMath.GetOffsetControlPoints(controlPoints, laneWidth / 2f);
    }

    /// <summary>
    /// Sağ şerit kontrol noktalarını döndürür.
    /// Merkez eğriden laneWidth/2 kadar sağa (negatif normal yönünde) kaydırılmış.
    /// </summary>
    public Vector3[] GetRightLaneControlPoints()
    {
        return BezierMath.GetOffsetControlPoints(controlPoints, -laneWidth / 2f);
    }

    /// <summary>
    /// Sol şerit üzerindeki t parametresindeki noktayı hesaplar.
    /// </summary>
    public Vector3 GetLeftLanePoint(float t)
    {
        return BezierMath.GetOffsetPoint(controlPoints, t, laneWidth / 2f);
    }

    /// <summary>
    /// Sağ şerit üzerindeki t parametresindeki noktayı hesaplar.
    /// </summary>
    public Vector3 GetRightLanePoint(float t)
    {
        return BezierMath.GetOffsetPoint(controlPoints, t, -laneWidth / 2f);
    }

    // =========================================================================
    // NODE OLUŞTURMA
    // =========================================================================

    /// <summary>
    /// DISTANCE MODE: Belirli mesafe aralıklarıyla her iki şeritte node oluşturur.
    /// 
    /// Her 'distance' birimde bir küçük küre (sphere) GameObject'i yerleştirir.
    /// Bu node'lar daha sonra mesh oluşturmak için kullanılacak.
    /// </summary>
    public void CreateNodesByDistance(float distance)
    {
        // Önce eski node'ları temizle
        ClearNodes();

        // Sol ve sağ şerit kontrol noktaları
        Vector3[] leftCP = GetLeftLaneControlPoints();
        Vector3[] rightCP = GetRightLaneControlPoints();

        // Her şerit için mesafe bazlı noktalar hesapla
        Vector3[] leftPoints = BezierMath.GetPointsByDistance(leftCP, distance);
        Vector3[] rightPoints = BezierMath.GetPointsByDistance(rightCP, distance);

        // Node GameObject'lerini oluştur
        CreateNodeGameObjects(leftPoints, "LeftNode");
        CreateNodeGameObjects(rightPoints, "RightNode");

        // Mesh oluştur (sol ve sağ noktalar arasında)
        // İki şeridin aynı sayıda noktaya sahip olması lazım mesh için
        // Bu yüzden count mode ile eşit sayıda nokta alalım
        int nodeCount = Mathf.Min(leftPoints.Length, rightPoints.Length);
        GenerateRoadMesh(leftCP, rightCP, nodeCount);
    }

    /// <summary>
    /// COUNT MODE: Belirli sayıda eşit aralıklı node oluşturur.
    /// </summary>
    public void CreateNodesByCount(int count)
    {
        ClearNodes();

        Vector3[] leftCP = GetLeftLaneControlPoints();
        Vector3[] rightCP = GetRightLaneControlPoints();

        Vector3[] leftPoints = BezierMath.GetPointsByCount(leftCP, count);
        Vector3[] rightPoints = BezierMath.GetPointsByCount(rightCP, count);

        CreateNodeGameObjects(leftPoints, "LeftNode");
        CreateNodeGameObjects(rightPoints, "RightNode");

        GenerateRoadMesh(leftCP, rightCP, count);
    }

    /// <summary>
    /// Verilen pozisyonlarda küçük küre (sphere) GameObject'leri oluşturur.
    /// Bu küçük küreler, eğri üzerindeki node'ları temsil eder.
    /// </summary>
    private void CreateNodeGameObjects(Vector3[] positions, string prefix)
    {
        for (int i = 0; i < positions.Length; i++)
        {
            // Küçük bir küre oluştur (görsel temsil için)
            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            node.name = $"{prefix}_{i}";
            node.transform.position = positions[i];
            node.transform.localScale = Vector3.one * 0.3f; // Küçük küre
            node.transform.SetParent(this.transform); // Bu GameObject'in çocuğu yap

            generatedNodes.Add(node);
        }
    }

    /// <summary>
    /// Tüm oluşturulmuş node'ları temizler.
    /// Yeni node'lar oluşturmadan önce çağrılır.
    /// 
    /// DestroyImmediate kullanıyoruz çünkü Edit Mode'dayız.
    /// Play Mode'da Destroy() kullanılır, Edit Mode'da DestroyImmediate() şart!
    /// </summary>
    public void ClearNodes()
    {
        foreach (var node in generatedNodes)
        {
            if (node != null)
                DestroyImmediate(node);
        }
        generatedNodes.Clear();

        if (roadMeshObject != null)
        {
            DestroyImmediate(roadMeshObject);
            roadMeshObject = null;
        }
    }

    // =========================================================================
    // MESH OLUŞTURMA
    // =========================================================================

    /// <summary>
    /// Sol ve sağ şerit arasında yol mesh'i oluşturur.
    /// 
    /// Nasıl çalışır:
    /// 1. Sol ve sağ şeritten eşit sayıda nokta al
    /// 2. Her nokta çiftinden bir dörtgen (quad) oluştur
    /// 3. Her dörtgeni 2 üçgene böl (GPU üçgenlerle çalışır)
    /// 
    /// Görsel:
    ///   Sol[0] ---- Sağ[0]
    ///     |  \        |
    ///     |   üçgen1  |
    ///     |        \  |
    ///   Sol[1] ---- Sağ[1]
    ///     |  \        |
    ///     |   üçgen2  |
    ///     |        \  |
    ///   Sol[2] ---- Sağ[2]
    /// </summary>
    public void GenerateRoadMesh(Vector3[] leftCP, Vector3[] rightCP, int vertexCount)
    {
        // Eski mesh'i temizle
        if (roadMeshObject != null)
            DestroyImmediate(roadMeshObject);

        // Yeni GameObject oluştur mesh için
        roadMeshObject = new GameObject("RoadMesh");
        roadMeshObject.transform.SetParent(this.transform);

        // Mesh bileşenlerini ekle
        MeshFilter meshFilter = roadMeshObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = roadMeshObject.AddComponent<MeshRenderer>();
        MeshCollider meshCollider = roadMeshObject.AddComponent<MeshCollider>();

        // Basit bir material oluştur (yeşilimsi yol rengi - case'deki gibi)
        Material roadMaterial = new Material(Shader.Find("Standard"));
        roadMaterial.color = new Color(0.4f, 0.6f, 0.4f, 1f); // Yeşilimsi gri
        meshRenderer.sharedMaterial = roadMaterial;

        // Sol ve sağ şerit noktalarını hesapla
        Vector3[] leftPoints = BezierMath.GetPointsByCount(leftCP, vertexCount);
        Vector3[] rightPoints = BezierMath.GetPointsByCount(rightCP, vertexCount);

        // Mesh oluşturma
        Mesh mesh = new Mesh();
        mesh.name = "RoadMesh";

        // --- VERTICES (Köşe Noktaları) ---
        // Her seviyede 2 nokta (sol + sağ) var
        // Toplam vertex sayısı = vertexCount * 2
        Vector3[] vertices = new Vector3[vertexCount * 2];

        for (int i = 0; i < vertexCount; i++)
        {
            // World space'den local space'e çevir
            // (Mesh vertex'leri GameObject'in local koordinatlarında olmalı)
            vertices[i * 2] = roadMeshObject.transform.InverseTransformPoint(leftPoints[i]);       // Sol
            vertices[i * 2 + 1] = roadMeshObject.transform.InverseTransformPoint(rightPoints[i]);  // Sağ
        }

        // --- TRIANGLES (Üçgenler) ---
        // Her quad (dörtgen) = 2 üçgen = 6 index
        // Toplam quad sayısı = vertexCount - 1
        int[] triangles = new int[(vertexCount - 1) * 6];

        for (int i = 0; i < vertexCount - 1; i++)
        {
            int baseIndex = i * 6;
            int leftCurrent = i * 2;
            int rightCurrent = i * 2 + 1;
            int leftNext = (i + 1) * 2;
            int rightNext = (i + 1) * 2 + 1;

            // Üçgen 1: Sol-Şimdi, Sağ-Şimdi, Sol-Sonraki
            triangles[baseIndex + 0] = leftCurrent;
            triangles[baseIndex + 1] = rightCurrent;
            triangles[baseIndex + 2] = leftNext;

            // Üçgen 2: Sağ-Şimdi, Sağ-Sonraki, Sol-Sonraki
            triangles[baseIndex + 3] = rightCurrent;
            triangles[baseIndex + 4] = rightNext;
            triangles[baseIndex + 5] = leftNext;
        }

        // --- UV KOORDİNATLARI ---
        // Texture mapping için (opsiyonel ama güzel görünüm için)
        Vector2[] uvs = new Vector2[vertexCount * 2];
        for (int i = 0; i < vertexCount; i++)
        {
            float v = (float)i / (vertexCount - 1); // 0'dan 1'e eğri boyunca
            uvs[i * 2] = new Vector2(0f, v);     // Sol kenar
            uvs[i * 2 + 1] = new Vector2(1f, v); // Sağ kenar
        }

        // Mesh'e verileri ata
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();   // Aydınlatma için normal vektörleri hesapla
        mesh.RecalculateBounds();     // Bounding box hesapla (render ve fizik için)

        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh; // Fizik çarpışma için de aynı mesh
    }

    // =========================================================================
    // GIZMO ÇİZİMİ
    // =========================================================================

    /// <summary>
    /// Unity Scene view'da eğrileri çizer.
    /// OnDrawGizmos → Bu GameObject seçili olmasa bile çizer.
    /// OnDrawGizmosSelected → Sadece seçiliyken çizer.
    /// 
    /// Gizmos, Unity'nin debug/görselleştirme sistemidir.
    /// Sadece Scene view'da görünür, Game view'da ve build'de GÖRÜNMEZ.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (controlPoints == null || controlPoints.Length < 2) return;

        // Sol ve sağ şerit kontrol noktaları
        Vector3[] leftCP = GetLeftLaneControlPoints();
        Vector3[] rightCP = GetRightLaneControlPoints();

        // --- MERKEZ EĞRİYİ ÇİZ (sarı) ---
        Gizmos.color = Color.yellow;
        DrawBezierGizmo(controlPoints);

        // --- SOL ŞERİDİ ÇİZ (yeşil) ---
        Gizmos.color = Color.green;
        DrawBezierGizmo(leftCP);

        // --- SAĞ ŞERİDİ ÇİZ (mavi) ---
        Gizmos.color = Color.blue;
        DrawBezierGizmo(rightCP);

        // --- KONTROL NOKTALARINI ÇİZ ---
        for (int i = 0; i < controlPoints.Length; i++)
        {
            // İlk ve son noktalar büyük küre (kontrol noktaları)
            if (i == 0 || i == controlPoints.Length - 1)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(controlPoints[i], 0.4f);
            }
            // Ortadaki noktalar küçük küre (referans noktaları)
            else
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(controlPoints[i], 0.25f);
            }

            // Ardışık kontrol noktaları arasında çizgi çiz
            if (i < controlPoints.Length - 1)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.3f); // Yarı saydam beyaz
                Gizmos.DrawLine(controlPoints[i], controlPoints[i + 1]);
            }
        }
    }

    /// <summary>
    /// Verilen kontrol noktaları ile Bezier eğrisini çizgi segmentleri olarak çizer.
    /// curveResolution kadar parçaya bölüp ardışık noktaları birleştirir.
    /// </summary>
    private void DrawBezierGizmo(Vector3[] cp)
    {
        Vector3 previousPoint = BezierMath.EvaluateCurve(cp, 0f);

        for (int i = 1; i <= curveResolution; i++)
        {
            float t = (float)i / curveResolution;
            Vector3 currentPoint = BezierMath.EvaluateCurve(cp, t);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }
}
