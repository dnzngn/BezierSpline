using UnityEngine;
using UnityEditor;

/// <summary>
/// Bezier Spline Editor Penceresi.
/// 
/// EditorWindow Nedir?
/// -------------------
/// Unity editöründe özel bir pencere (window) oluşturmanızı sağlar.
/// Inspector veya Scene view gibi, kendi panelimizi yapıyoruz.
/// Menüden açılır ve sürüklenip yerleştirilebilir.
/// 
/// Case'in istediği pencere:
///   - "Curve Second Phase" başlığı
///   - Distance (float) + slider + "Distance Mode" butonu
///   - Count (int) + slider + "Count Mode" butonu
///   - Cancel butonu
/// 
/// IMGUI Nedir?
/// ------------
/// Unity'nin eski (ama hâlâ çok kullanılan) GUI sistemi.
/// OnGUI() fonksiyonunda her frame UI elemanları "çizilir".
/// GUILayout.Button(), EditorGUILayout.FloatField() gibi fonksiyonlarla
/// buton, slider, text field vs. oluşturulur.
/// </summary>
public class BezierEditorWindow : EditorWindow
{
    // =========================================================================
    // DEĞİŞKENLER
    // =========================================================================

    // Distance Mode ayarları
    private float distance = 1f;         // Her node arası mesafe
    private float minDistance = 0.1f;     // Minimum mesafe (çok küçük olmasın)
    private float maxDistance = 20f;      // Maksimum mesafe

    // Count Mode ayarları
    private int count = 5;               // Oluşturulacak node sayısı
    private int minCount = 2;            // Minimum 2 node (başlangıç + bitiş)
    private int maxCount = 100;          // Maksimum node sayısı

    // Sahnedeki BezierSpline referansı
    private BezierSpline targetSpline;

    // =========================================================================
    // PENCERE AÇMA
    // =========================================================================

    /// <summary>
    /// Menüden pencereyi açmak için.
    /// Unity menüsüne "Tools > Bezier Spline Editor" ekler.
    /// 
    /// [MenuItem] → Unity'nin üst menüsüne yeni bir madde ekler.
    /// GetWindow<T>() → Pencereyi açar (zaten açıksa öne getirir).
    /// </summary>
    [MenuItem("Tools/Bezier Spline Editor")]
    public static void ShowWindow()
    {
        // Pencereyi aç ve başlığını ayarla
        BezierEditorWindow window = GetWindow<BezierEditorWindow>("Curve Second Phase");

        // Minimum pencere boyutu
        window.minSize = new Vector2(350, 300);
    }

    // =========================================================================
    // GUI ÇİZİMİ (Her frame çağrılır)
    // =========================================================================

    /// <summary>
    /// Pencerenin içeriğini çizer. IMGUI kullanarak.
    /// Her frame yeniden çizilir (immediate mode).
    /// </summary>
    private void OnGUI()
    {
        // --- BAŞLIK ---
        // Büyük, renkli başlık (case'deki gibi)
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 20,
            normal = { textColor = new Color(0.2f, 0.6f, 1f) }, // Mavi renk
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("Curve Second Phase", titleStyle, GUILayout.Height(30));

        EditorGUILayout.Space(15);

        // --- HEDEF SPLİNE SEÇİMİ ---
        // Sahnede BezierSpline bul veya kullanıcının seçmesini iste
        FindTargetSpline();

        if (targetSpline == null)
        {
            // Sahnede BezierSpline yoksa, oluşturma butonu göster
            EditorGUILayout.HelpBox(
                "Sahnede BezierSpline bulunamadı.\nÖnce bir tane oluşturun.",
                MessageType.Warning
            );

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Bezier Spline Oluştur", GUILayout.Height(35)))
            {
                CreateBezierSpline();
            }

            return; // Spline yoksa geri kalan UI'ı gösterme
        }

        // Aktif spline bilgisi
        EditorGUILayout.HelpBox(
            $"Aktif Spline: {targetSpline.gameObject.name}\n" +
            $"Kontrol Noktası: {targetSpline.controlPoints.Length} | " +
            $"Derece: {targetSpline.controlPoints.Length - 1}",
            MessageType.Info
        );

        EditorGUILayout.Space(15);

        // =====================================================================
        // DISTANCE MODE
        // =====================================================================

        // Kırmızı renkli "Distance" etiketi (case'deki gibi)
        GUIStyle redLabel = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            normal = { textColor = new Color(0.9f, 0.3f, 0.3f) } // Kırmızı
        };
        EditorGUILayout.LabelField("Distance", redLabel);

        // Slider + float field yan yana
        EditorGUILayout.BeginHorizontal();
        distance = EditorGUILayout.Slider(distance, minDistance, maxDistance);
        EditorGUILayout.EndHorizontal();

        // "Distance Mode" butonu
        if (GUILayout.Button("Distance Mode", GUILayout.Height(30)))
        {
            // Undo kaydı al (Ctrl+Z desteği)
            Undo.RecordObject(targetSpline, "Distance Mode");
            targetSpline.CreateNodesByDistance(distance);
            // Scene view'ı yenile
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(15);

        // =====================================================================
        // COUNT MODE
        // =====================================================================

        EditorGUILayout.LabelField("Count", redLabel);

        // Slider + int field
        EditorGUILayout.BeginHorizontal();
        count = EditorGUILayout.IntSlider(count, minCount, maxCount);
        EditorGUILayout.EndHorizontal();

        // "Count Mode" butonu
        if (GUILayout.Button("Count Mode", GUILayout.Height(30)))
        {
            Undo.RecordObject(targetSpline, "Count Mode");
            targetSpline.CreateNodesByCount(count);
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(20);

        // =====================================================================
        // CANCEL BUTONU
        // =====================================================================

        // Kırmızı "Cancel" butonu → tüm oluşturulmuş node'ları siler
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f); // Kırmızımsı arka plan
        if (GUILayout.Button("Cancel", GUILayout.Height(35)))
        {
            Undo.RecordObject(targetSpline, "Cancel");
            targetSpline.ClearNodes();
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white; // Arka plan rengini sıfırla
    }

    // =========================================================================
    // YARDIMCI FONKSİYONLAR
    // =========================================================================

    /// <summary>
    /// Sahnede BezierSpline bileşeni olan bir GameObject arar.
    /// Önce kullanıcının seçtiği objeye bakar, yoksa sahnede arar.
    /// </summary>
    private void FindTargetSpline()
    {
        // Eğer zaten geçerli bir referansımız varsa, kullan
        if (targetSpline != null) return;

        // Kullanıcının seçtiği objede BezierSpline var mı?
        if (Selection.activeGameObject != null)
        {
            targetSpline = Selection.activeGameObject.GetComponent<BezierSpline>();
            if (targetSpline != null) return;
        }

        // Sahnedeki ilk BezierSpline'ı bul
        // FindFirstObjectByType: Unity'nin yeni API'si (FindObjectOfType'ın modern versiyonu)
        targetSpline = FindFirstObjectByType<BezierSpline>();
    }

    /// <summary>
    /// Yeni bir BezierSpline GameObject'i oluşturur.
    /// "Bezier Spline Oluştur" butonuna basılınca çağrılır.
    /// </summary>
    private void CreateBezierSpline()
    {
        // Yeni boş GameObject oluştur
        GameObject splineObj = new GameObject("BezierSpline");

        // BezierSpline bileşenini ekle
        targetSpline = splineObj.AddComponent<BezierSpline>();

        // Undo kaydı (Ctrl+Z ile geri alınabilir)
        Undo.RegisterCreatedObjectUndo(splineObj, "Create Bezier Spline");

        // Oluşturulan objeyi seç (Inspector'da görünsün)
        Selection.activeGameObject = splineObj;

        // Scene view'ı yenile
        SceneView.RepaintAll();

        Debug.Log("BezierSpline oluşturuldu! Scene view'da kontrol noktalarını sürükleyebilirsiniz.");
    }

    /// <summary>
    /// Pencere her odaklandığında (focus), sahneyi kontrol et.
    /// Kullanıcı farklı bir obje seçmiş olabilir.
    /// </summary>
    private void OnFocus()
    {
        // Referansı sıfırla, yeniden aransın
        targetSpline = null;
    }

    /// <summary>
    /// Selection değiştiğinde çağrılır.
    /// Pencereyi yeniden çiz ki yeni seçilen objeyi gösterelim.
    /// </summary>
    private void OnSelectionChange()
    {
        targetSpline = null;
        Repaint(); // Pencereyi yeniden çiz
    }
}
