using UnityEngine;
using UnityEditor;

/// <summary>
/// BezierSpline bileşeninin Custom Editor'ı.
/// 
/// Custom Editor Nedir?
/// --------------------
/// Unity'de bir bileşenin Inspector görünümünü ve Scene view davranışını
/// özelleştirmenizi sağlar. Bu sayede:
///   - Scene view'da sürüklenebilir kontrol noktaları (handles) ekleyebiliriz
///   - Inspector'da özel butonlar ve düzenler oluşturabiliriz
///   - Eğrileri scene'de interaktif şekilde düzenleyebiliriz
/// 
/// [CustomEditor(typeof(BezierSpline))] → "Bu editor, BezierSpline bileşeni içindir"
/// 
/// ÖNEMLİ Unity Kavramları:
/// - Handles: Scene view'da 3D sürüklenebilir araçlar (pozisyon, rotasyon vb.)
/// - SerializedObject: Unity'nin undo/redo ve kaydetme sistemiyle uyumlu veri erişimi
/// - SceneView.RepaintAll(): Scene view'ı yeniden çiz (değişiklikler anında görünsün)
/// </summary>
[CustomEditor(typeof(BezierSpline))]
public class BezierSplineEditor : Editor
{
    // Referanslar
    private BezierSpline spline; // Düzenlediğimiz BezierSpline bileşeni

    /// <summary>
    /// Editor etkinleştirildiğinde çağrılır.
    /// BezierSpline bileşenine referans alır.
    /// </summary>
    private void OnEnable()
    {
        // 'target' → Unity'nin bize verdiği, düzenlenen bileşen
        // 'as BezierSpline' → doğru tipe dönüştür
        spline = target as BezierSpline;
    }

    /// <summary>
    /// Inspector'da özel UI çizer.
    /// Varsayılan Inspector'ın üstüne ek butonlar ekleriz.
    /// </summary>
    public override void OnInspectorGUI()
    {
        // Varsayılan Inspector'ı çiz (controlPoints, laneWidth vs. görünsün)
        DrawDefaultInspector();

        // Biraz boşluk bırak
        EditorGUILayout.Space(10);

        // --- KONTROL NOKTASI EKLEME BUTONU ---
        // Case'in bonus kısmı: 10'a kadar kontrol noktası
        EditorGUILayout.LabelField("Kontrol Noktası Yönetimi", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal(); // Butonları yan yana koy

        if (GUILayout.Button("Nokta Ekle (+)"))
        {
            AddControlPoint();
        }

        // En az 2 kontrol noktası olmalı, yoksa eğri olmaz
        GUI.enabled = spline.controlPoints.Length > 2;
        if (GUILayout.Button("Nokta Çıkar (-)"))
        {
            RemoveLastControlPoint();
        }
        GUI.enabled = true; // GUI'yi tekrar aktif et

        EditorGUILayout.EndHorizontal();

        // Mevcut kontrol noktası sayısını göster
        EditorGUILayout.HelpBox(
            $"Kontrol Noktası Sayısı: {spline.controlPoints.Length}\n" +
            $"Bezier Derecesi: {spline.controlPoints.Length - 1}\n" +
            $"(Maksimum 10 nokta desteklenir)",
            MessageType.Info
        );

        EditorGUILayout.Space(5);

        // --- TEMİZLEME BUTONU ---
        if (GUILayout.Button("Node'ları Temizle"))
        {
            // Undo kaydı: kullanıcı Ctrl+Z ile geri alabilsin
            Undo.RecordObject(spline, "Clear Nodes");
            spline.ClearNodes();
        }
    }

    /// <summary>
    /// Scene View'da her frame çağrılır.
    /// Burada kontrol noktaları için sürüklenebilir handle'lar çizeriz.
    /// 
    /// Bu fonksiyon sayesinde kullanıcı scene'de kontrol noktalarını
    /// fare ile sürükleyerek eğriyi şekillendirebilir.
    /// 
    /// Handle Türleri:
    /// - Handles.PositionHandle → 3 eksenli (x,y,z) hareket aracı
    /// - Handles.FreeMoveHandle → Serbest hareket (her yöne)
    /// - Handles.RotationHandle → Döndürme aracı
    /// </summary>
    private void OnSceneGUI()
    {
        if (spline.controlPoints == null) return;

        // Her kontrol noktası için bir handle çiz
        for (int i = 0; i < spline.controlPoints.Length; i++)
        {
            // Mevcut pozisyonu al
            Vector3 currentPosition = spline.controlPoints[i];

            // Handle'ın rengini ayarla
            // İlk ve son noktalar kırmızı (kontrol), ortadakiler yeşil (referans)
            if (i == 0 || i == spline.controlPoints.Length - 1)
                Handles.color = Color.red;
            else
                Handles.color = Color.green;

            // Etiket göster (hangi nokta olduğunu anlamak için)
            string label = i == 0 ? "Başlangıç" :
                          i == spline.controlPoints.Length - 1 ? "Bitiş" :
                          $"Ref {i}";
            Handles.Label(currentPosition + Vector3.up * 0.5f, label);

            // --- POZİSYON HANDLE ---
            // EditorGUI.BeginChangeCheck() → "Şimdi değişiklik kontrolü başlat"
            // Eğer kullanıcı handle'ı sürüklerse, EndChangeCheck() true döner
            EditorGUI.BeginChangeCheck();

            // PositionHandle: 3 eksenli ok + düzlem kareleri olan standart Unity aracı
            Vector3 newPosition = Handles.PositionHandle(currentPosition, Quaternion.identity);

            if (EditorGUI.EndChangeCheck())
            {
                // Kullanıcı handle'ı hareket ettirdi!

                // Undo kaydı: Ctrl+Z ile geri alınabilir olsun
                Undo.RecordObject(spline, "Move Control Point");

                // Yeni pozisyonu kaydet
                spline.controlPoints[i] = newPosition;

                // Sahneyi "kirli" (dirty) olarak işaretle
                // Bu sayede Unity, kaydetmeden çıkmak istersen uyarır
                EditorUtility.SetDirty(spline);
            }
        }

        // --- EĞRİ NOKTALARINI SCENE'DE ÇİZ (Handles ile) ---
        DrawCurveHandles();
    }

    /// <summary>
    /// Scene view'da Handles API ile eğrileri çizer.
    /// Gizmos'tan farkı: Handles daha interaktif ve editor-specific.
    /// </summary>
    private void DrawCurveHandles()
    {
        if (spline.controlPoints.Length < 2) return;

        // Merkez çizgi (sarı)
        Handles.color = Color.yellow;
        DrawCurve(spline.controlPoints);

        // Sol şerit (yeşil) - offset kullanarak
        Handles.color = Color.green;
        DrawCurveOffset(spline.laneWidth / 2f);

        // Sağ şerit (mavi) - offset kullanarak
        Handles.color = Color.blue;
        DrawCurveOffset(-spline.laneWidth / 2f);

        // Kontrol noktaları arası bağlantı çizgileri (yarı saydam, kesikli)
        Handles.color = new Color(1f, 1f, 1f, 0.3f);
        for (int i = 0; i < spline.controlPoints.Length - 1; i++)
        {
            Handles.DrawDottedLine(spline.controlPoints[i], spline.controlPoints[i + 1], 4f);
        }
    }

    /// <summary>
    /// Merkez Bezier eğrisini çizer.
    /// </summary>
    private void DrawCurve(Vector3[] cp)
    {
        Vector3 prev = BezierMath.EvaluateCurve(cp, 0f);

        for (int i = 1; i <= spline.curveResolution; i++)
        {
            float t = (float)i / spline.curveResolution;
            Vector3 curr = BezierMath.EvaluateCurve(cp, t);
            Handles.DrawLine(prev, curr);
            prev = curr;
        }
    }

    /// <summary>
    /// Offset'lenmiş şerit eğrisini çizer.
    /// Her noktada merkez eğriden normal yönde offset uygular.
    /// </summary>
    private void DrawCurveOffset(float offset)
    {
        Vector3 prev = BezierMath.GetOffsetPoint(spline.controlPoints, 0f, offset);

        for (int i = 1; i <= spline.curveResolution; i++)
        {
            float t = (float)i / spline.curveResolution;
            Vector3 curr = BezierMath.GetOffsetPoint(spline.controlPoints, t, offset);
            Handles.DrawLine(prev, curr);
            prev = curr;
        }
    }

    // =========================================================================
    // KONTROL NOKTASI EKLEME/ÇIKARMA
    // =========================================================================

    /// <summary>
    /// Eğrinin sonuna yeni bir kontrol noktası ekler.
    /// Son iki noktanın yönünde, biraz ilerisine yerleştirir.
    /// Bonus gereksinim: 10 noktaya kadar desteklenir.
    /// </summary>
    private void AddControlPoint()
    {
        if (spline.controlPoints.Length >= 10)
        {
            EditorUtility.DisplayDialog("Maksimum Nokta",
                "En fazla 10 kontrol noktası ekleyebilirsiniz!", "Tamam");
            return;
        }

        Undo.RecordObject(spline, "Add Control Point");

        int count = spline.controlPoints.Length;
        Vector3[] newPoints = new Vector3[count + 1];

        // Mevcut noktaları kopyala
        System.Array.Copy(spline.controlPoints, newPoints, count);

        // Yeni noktayı son noktanın devamına yerleştir
        Vector3 lastPoint = spline.controlPoints[count - 1];
        Vector3 direction = Vector3.forward; // Varsayılan yön

        if (count >= 2)
        {
            // Son iki nokta arasındaki yönde devam et
            direction = (spline.controlPoints[count - 1] - spline.controlPoints[count - 2]).normalized;
        }

        newPoints[count] = lastPoint + direction * 5f; // 5 birim ileride
        spline.controlPoints = newPoints;

        EditorUtility.SetDirty(spline);
    }

    /// <summary>
    /// Son kontrol noktasını kaldırır.
    /// En az 2 nokta kalmalı (eğri çizmek için minimum gereksinim).
    /// </summary>
    private void RemoveLastControlPoint()
    {
        if (spline.controlPoints.Length <= 2) return;

        Undo.RecordObject(spline, "Remove Control Point");

        int newCount = spline.controlPoints.Length - 1;
        Vector3[] newPoints = new Vector3[newCount];
        System.Array.Copy(spline.controlPoints, newPoints, newCount);
        spline.controlPoints = newPoints;

        EditorUtility.SetDirty(spline);
    }
}