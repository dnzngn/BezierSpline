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
    /// ÖNEMLİ: Mesh için sol ve sağ şeritten EŞİT SAYIDA ve HİZALI nokta lazım.
    /// Bunu sağlamak için merkez eğrinin arc-length tablosunu kullanıyoruz.
    /// Aynı t değerlerinden sol ve sağ offset uygulayarak noktalar hep eşleşik kalır.
    /// </summary>
    public void CreateNodesByDistance(float distance)
    {
        // Önce eski node'ları temizle
        ClearNodes();

        // Merkez eğrinin arc-length tablosunu oluştur
        float[] arcTable = BezierMath.BuildArcLengthTable(controlPoints);
        float totalLength = BezierMath.GetTotalLength(arcTable);

        // Kaç nokta olacak? (mesafe bazlı)
        int count = Mathf.Max(2, Mathf.FloorToInt(totalLength / distance) + 1);

        // t parametrelerini hesapla (merkez eğri üzerinde eşit mesafeli)
        float[] tValues = new float[count];
        for (int i = 0; i < count; i++)
        {
            float d = i * distance;
            tValues[i] = BezierMath.DistanceToT(arcTable, d);
        }

        // Sol ve sağ şerit noktalarını hesapla (aynı t değerleriyle → hizalı!)
        Vector3[] leftPoints = new Vector3[count];
        Vector3[] rightPoints = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            leftPoints[i] = GetLeftLanePoint(tValues[i]);
            rightPoints[i] = GetRightLanePoint(tValues[i]);
        }

        // Node GameObject'lerini oluştur
        CreateNodeGameObjects(leftPoints, "LeftNode");
        CreateNodeGameObjects(rightPoints, "RightNode");

        // Mesh oluştur (hizalı noktalarla)
        GenerateRoadMesh(leftPoints, rightPoints);
    }

    /// <summary>
    /// COUNT MODE: Belirli sayıda eşit aralıklı node oluşturur.
    /// Merkez eğri üzerinden t değerleri hesaplanır, sonra sol/sağ offset uygulanır.
    /// </summary>
    public void CreateNodesByCount(int count)
    {
        if (count < 2) count = 2;

        ClearNodes();

        // Merkez eğrinin arc-length tablosunu oluştur
        float[] arcTable = BezierMath.BuildArcLengthTable(controlPoints);
        float totalLength = BezierMath.GetTotalLength(arcTable);

        // Eşit aralıklı t değerleri
        float spacing = totalLength / (count - 1);

        // Sol ve sağ şerit noktaları (hizalı)
        Vector3[] leftPoints = new Vector3[count];
        Vector3[] rightPoints = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            float d = i * spacing;
            float t = BezierMath.DistanceToT(arcTable, d);
            leftPoints[i] = GetLeftLanePoint(t);
            rightPoints[i] = GetRightLanePoint(t);
        }

        CreateNodeGameObjects(leftPoints, "LeftNode");
        CreateNodeGameObjects(rightPoints, "RightNode");

        GenerateRoadMesh(leftPoints, rightPoints);
    }

    /// <summary>
    /// Verilen pozisyonlarda küçük küre (sphere) GameObject'leri oluşturur.
    /// Collider'ı kaldırırız çünkü sadece görsel temsil amaçlı.
    /// </summary>
    private void CreateNodeGameObjects(Vector3[] positions, string prefix)
    {
        for (int i = 0; i < positions.Length; i++)
        {
            // Küçük bir küre oluştur
            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            node.name = $"{prefix}_{i}";
            node.transform.position = positions[i];
            node.transform.localScale = Vector3.one * 0.3f;
            node.transform.SetParent(this.transform);

            // Sphere'lerin collider'ını kaldır (gereksiz, performans için)
            var collider = node.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider);

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
    ///     |   üçgen0  |
    ///     |        \  |
    ///   Sol[1] ---- Sağ[1]
    ///     |  \        |
    ///     |   üçgen1  |
    ///     |        \  |
    ///   Sol[2] ---- Sağ[2]
    /// </summary>
   public void GenerateRoadMesh(Vector3[] leftPoints, Vector3[] rightPoints)
    {
        int vertexCount = leftPoints.Length;

        // Güvenlik kontrolü
        if (vertexCount < 2)
        {
            Debug.LogWarning("Mesh oluşturmak için en az 2 nokta çifti gerekli!");
            return;
        }

        // Eski mesh'i temizle
        if (roadMeshObject != null)
            DestroyImmediate(roadMeshObject);

        // Yeni GameObject
        roadMeshObject = new GameObject("RoadMesh");
        roadMeshObject.transform.SetParent(this.transform);

        MeshFilter meshFilter = roadMeshObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = roadMeshObject.AddComponent<MeshRenderer>();

        // --- MATERIAL ---
        // Çift taraflı render (Cull Off) + yeşilimsi yol rengi
        Material roadMaterial = new Material(Shader.Find("Standard"));
        roadMaterial.color = new Color(0.4f, 0.6f, 0.4f, 1f);
        roadMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        meshRenderer.sharedMaterial = roadMaterial;

        // --- MESH VERİLERİ ---
        Mesh mesh = new Mesh();
        mesh.name = "RoadMesh";

        // VERTICES: Her seviyede sol + sağ = 2 nokta
        Vector3[] vertices = new Vector3[vertexCount * 2];
        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i * 2] = leftPoints[i];       // Çift index = sol
            vertices[i * 2 + 1] = rightPoints[i];  // Tek index = sağ
        }

        // TRIANGLES: Her quad = 2 üçgen = 6 index
        int quadCount = vertexCount - 1;
        int[] triangles = new int[quadCount * 6];

        for (int i = 0; i < quadCount; i++)
        {
            int idx = i * 6;
            int bl = i * 2;           // bottom-left  (sol şimdiki)
            int br = i * 2 + 1;       // bottom-right (sağ şimdiki)
            int tl = (i + 1) * 2;     // top-left     (sol sonraki)
            int tr = (i + 1) * 2 + 1; // top-right    (sağ sonraki)

            // Üçgen 1: bl → tl → br (saat yönünün tersine = ön yüz yukarı)
            triangles[idx + 0] = bl;
            triangles[idx + 1] = tl;
            triangles[idx + 2] = br;

            // Üçgen 2: br → tl → tr
            triangles[idx + 3] = br;
            triangles[idx + 4] = tl;
            triangles[idx + 5] = tr;
        }

        // UV: texture mapping koordinatları
        Vector2[] uvs = new Vector2[vertexCount * 2];
        for (int i = 0; i < vertexCount; i++)
        {
            float v = (float)i / (vertexCount - 1);
            uvs[i * 2] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);
        }

        // Mesh'e ata
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;

        // MESH COLLIDER: sadece yeterli vertex varsa ekle (hatayı önler)
        if (vertexCount >= 3)
        {
            MeshCollider meshCollider = roadMeshObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
        }
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

        // --- MERKEZ EĞRİ (sarı) ---
        Gizmos.color = Color.yellow;
        DrawBezierGizmo(controlPoints);

        // --- SOL ŞERİT (yeşil) ---
        // Doğrudan offset point kullanarak çiziyoruz (daha doğru paralel çizgi)
        Gizmos.color = Color.green;
        DrawBezierGizmoOffset(laneWidth / 2f);

        // --- SAĞ ŞERİT (mavi) ---
        Gizmos.color = Color.blue;
        DrawBezierGizmoOffset(-laneWidth / 2f);

        // --- KONTROL NOKTALARI ---
        for (int i = 0; i < controlPoints.Length; i++)
        {
            // İlk ve son = kontrol noktası (kırmızı, büyük)
            if (i == 0 || i == controlPoints.Length - 1)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(controlPoints[i], 0.4f);
            }
            // Ortadakiler = referans noktası (yeşil, küçük)
            else
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(controlPoints[i], 0.25f);
            }

            // Kontrol poligonu çizgileri (yarı saydam beyaz)
            if (i < controlPoints.Length - 1)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
                Gizmos.DrawLine(controlPoints[i], controlPoints[i + 1]);
            }
        }
    }

    /// <summary>
    /// Merkez eğriyi çizgi segmentleri olarak çizer.
    /// </summary>
    private void DrawBezierGizmo(Vector3[] cp)
    {
        Vector3 prev = BezierMath.EvaluateCurve(cp, 0f);
        for (int i = 1; i <= curveResolution; i++)
        {
            float t = (float)i / curveResolution;
            Vector3 curr = BezierMath.EvaluateCurve(cp, t);
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }
    }

    /// <summary>
    /// Offset'lenmiş şerit eğrisini çizer.
    /// Her noktada merkez eğriden normal yönde offset uygular.
    /// Kontrol noktalarını offset'lemekten DAHA DOĞRU sonuç verir.
    /// </summary>
    private void DrawBezierGizmoOffset(float offset)
    {
        Vector3 prev = BezierMath.GetOffsetPoint(controlPoints, 0f, offset);
        for (int i = 1; i <= curveResolution; i++)
        {
            float t = (float)i / curveResolution;
            Vector3 curr = BezierMath.GetOffsetPoint(controlPoints, t, offset);
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }
    }
}
