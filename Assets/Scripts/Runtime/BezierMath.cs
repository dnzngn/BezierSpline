using UnityEngine;

/// <summary>
/// Bezier eğrisi için saf matematik fonksiyonları.
/// ÖNEMLİ: Vector3.Lerp KULLANILMAZ! Doğrudan Bernstein polinomları kullanılır.
/// 
/// Bezier Eğrisi Nedir?
/// --------------------
/// N adet kontrol noktası ile tanımlanan parametrik bir eğridir.
/// t parametresi 0'dan 1'e gider:
///   - t=0 → eğrinin başlangıç noktası (ilk kontrol noktası)
///   - t=1 → eğrinin bitiş noktası (son kontrol noktası)
///   - t=0.5 → eğrinin "ortası" (ama geometrik orta değil!)
///
/// Formül: B(t) = Σ(i=0..n) C(n,i) * (1-t)^(n-i) * t^i * P_i
/// Burada:
///   - n = derece (kontrol noktası sayısı - 1)
///   - C(n,i) = binomial katsayı = n! / (i! * (n-i)!)
///   - P_i = i'inci kontrol noktası
///   - t = 0 ile 1 arasında parametre
/// </summary>
public static class BezierMath
{
    // =========================================================================
    // YARDIMCI MATEMATİK FONKSİYONLARI
    // =========================================================================

    /// <summary>
    /// Faktöriyel hesaplama: n! = n * (n-1) * (n-2) * ... * 1
    /// Örnek: 4! = 4 * 3 * 2 * 1 = 24
    /// Bezier formülündeki binomial katsayı için lazım.
    /// </summary>
    private static long Factorial(int n)
    {
        long result = 1;
        for (int i = 2; i <= n; i++)
            result *= i;
        return result;
    }

    /// <summary>
    /// Binomial katsayı: C(n, k) = n! / (k! * (n-k)!)
    /// "n elemanın k'lı kombinasyonu" - olasılık dersinden hatırlarsın.
    /// Bezier formülünde her kontrol noktasının ağırlığını belirler.
    /// 
    /// Örnek: C(3,1) = 3! / (1! * 2!) = 6 / 2 = 3
    /// </summary>
    private static long BinomialCoefficient(int n, int k)
    {
        return Factorial(n) / (Factorial(k) * Factorial(n - k));
    }

    /// <summary>
    /// Bernstein bazis polinomu: B(i, n, t) = C(n,i) * t^i * (1-t)^(n-i)
    /// 
    /// Bu fonksiyon, t parametresinde i'inci kontrol noktasının
    /// ne kadar etkili olduğunu hesaplar.
    /// 
    /// Örnek (Kübik Bezier, n=3):
    ///   - B(0,3,t) = (1-t)^3         → başlangıç noktasının etkisi
    ///   - B(1,3,t) = 3*t*(1-t)^2     → 1. referans noktasının etkisi
    ///   - B(2,3,t) = 3*t^2*(1-t)     → 2. referans noktasının etkisi
    ///   - B(3,3,t) = t^3             → bitiş noktasının etkisi
    /// </summary>
    private static float BernsteinBasis(int i, int n, float t)
    {
        return BinomialCoefficient(n, i) * Mathf.Pow(t, i) * Mathf.Pow(1f - t, n - i);
    }

    // =========================================================================
    // ANA BEZİER FONKSİYONLARI
    // =========================================================================

    /// <summary>
    /// Bezier eğrisi üzerinde t parametresindeki noktayı hesaplar.
    /// Bu fonksiyon projenin KALBİDİR - her şey buna dayanır.
    /// 
    /// Formül: B(t) = Σ(i=0..n) Bernstein(i,n,t) * P_i
    /// 
    /// Her kontrol noktasının ağırlıklı ortalamasını alıyoruz.
    /// Ağırlıklar Bernstein polinomları ile belirleniyor.
    /// 
    /// Parametreler:
    ///   controlPoints: Kontrol noktaları dizisi (en az 2 nokta)
    ///   t: 0.0 ile 1.0 arasında parametre (eğri üzerindeki konum)
    /// </summary>
    public static Vector3 EvaluateCurve(Vector3[] controlPoints, float t)
    {
        // Derece = kontrol noktası sayısı - 1
        // 4 nokta → 3. derece (kübik), 5 nokta → 4. derece, vs.
        int n = controlPoints.Length - 1;

        Vector3 point = Vector3.zero;

        // Her kontrol noktasının katkısını topla
        for (int i = 0; i <= n; i++)
        {
            // Bernstein ağırlığı * kontrol noktası pozisyonu
            float basis = BernsteinBasis(i, n, t);
            point += basis * controlPoints[i];
        }

        return point;
    }

    /// <summary>
    /// Bezier eğrisinin t parametresindeki TEĞET vektörünü (türevini) hesaplar.
    /// Teğet vektörü, eğrinin o noktadaki yönünü gösterir.
    /// 
    /// Türev formülü: B'(t) = n * Σ(i=0..n-1) Bernstein(i,n-1,t) * (P_{i+1} - P_i)
    /// 
    /// Yani: bir derece düşük Bezier eğrisi, ama kontrol noktaları olarak
    /// orijinal noktaların FARKLARINI kullanıyoruz.
    /// 
    /// Bu fonksiyon şunlar için lazım:
    ///   - Eğrinin yönünü bilmek (normal hesaplama için)
    ///   - Sol/sağ şerit offset'i hesaplamak
    /// </summary>
    public static Vector3 EvaluateTangent(Vector3[] controlPoints, float t)
    {
        int n = controlPoints.Length - 1;

        // Tek nokta varsa türev sıfırdır
        if (n < 1) return Vector3.forward;

        Vector3 tangent = Vector3.zero;

        // Türev: n * Σ Bernstein(i, n-1, t) * (P[i+1] - P[i])
        for (int i = 0; i <= n - 1; i++)
        {
            float basis = BernsteinBasis(i, n - 1, t);
            // Ardışık kontrol noktalarının farkı
            Vector3 diff = controlPoints[i + 1] - controlPoints[i];
            tangent += basis * diff;
        }

        tangent *= n; // n ile çarp (türev formülünün parçası)

        return tangent;
    }

    /// <summary>
    /// Eğrinin t noktasındaki NORMAL vektörünü hesaplar.
    /// Normal = teğete DİK olan vektör.
    /// 
    /// Yol/şerit yapıyoruz, yere paralel bir düzlemde çalışıyoruz.
    /// Bu yüzden "yukarı" vektörü (Vector3.up) ile teğetin çapraz çarpımını alıyoruz.
    /// 
    /// Sonuç: teğete dik, yere paralel bir vektör → şerit offset yönü
    /// 
    ///   Teğet →  (eğri yönü)
    ///   Normal ↑  (sola/sağa offset yönü)
    ///   Up ↑      (yukarı)
    /// </summary>
    public static Vector3 GetNormal(Vector3[] controlPoints, float t)
    {
        Vector3 tangent = EvaluateTangent(controlPoints, t).normalized;

        // Çapraz çarpım: tangent × up = sağ/sol yön
        // Bu bize yere paralel, teğete dik bir vektör verir
        Vector3 normal = Vector3.Cross(tangent, Vector3.up).normalized;

        // Eğer teğet tam yukarı bakıyorsa (dejenere durum), alternatif kullan
        if (normal.sqrMagnitude < 0.001f)
            normal = Vector3.Cross(tangent, Vector3.forward).normalized;

        return normal;
    }

    // =========================================================================
    // ARC-LENGTH PARAMETRİZASYONU
    // =========================================================================
    // 
    // PROBLEM: Bezier eğrisinde t parametresi EŞİT ARALIKLI değildir!
    // t=0.5 eğrinin geometrik ortası DEĞİLDİR.
    // Eğri keskin döndüğü yerlerde noktalar sıkışır, düz yerlerde ayrılır.
    //
    // ÇÖZÜM: Arc-length parametrizasyonu
    // Eğriyi çok sayıda küçük parçaya böl, gerçek mesafeleri hesapla,
    // sonra istenen mesafeye karşılık gelen t değerini bul.
    // =========================================================================

    /// <summary>
    /// Eğri boyunca kümülatif mesafe tablosu oluşturur.
    /// 
    /// Nasıl çalışır:
    /// 1. Eğriyi 'sampleCount' parçaya böl
    /// 2. Her parçanın uzunluğunu hesapla
    /// 3. Kümülatif (toplam) mesafeleri bir dizide sakla
    /// 
    /// Dönen dizi: [0, d1, d1+d2, d1+d2+d3, ..., toplamUzunluk]
    /// İndeks i → t = i / sampleCount parametresine karşılık gelir
    /// </summary>
    public static float[] BuildArcLengthTable(Vector3[] controlPoints, int sampleCount = 1000)
    {
        float[] table = new float[sampleCount + 1];
        table[0] = 0f; // Başlangıçta mesafe sıfır

        Vector3 previousPoint = EvaluateCurve(controlPoints, 0f);

        for (int i = 1; i <= sampleCount; i++)
        {
            // t parametresi: 0'dan 1'e doğru ilerle
            float t = (float)i / sampleCount;
            Vector3 currentPoint = EvaluateCurve(controlPoints, t);

            // Önceki nokta ile şimdiki nokta arasındaki mesafeyi ekle
            float segmentLength = Vector3.Distance(previousPoint, currentPoint);
            table[i] = table[i - 1] + segmentLength;

            previousPoint = currentPoint;
        }

        return table;
    }

    /// <summary>
    /// Arc-length tablosundan eğrinin toplam uzunluğunu döndürür.
    /// Tablonun son elemanı = toplam uzunluk.
    /// </summary>
    public static float GetTotalLength(float[] arcLengthTable)
    {
        return arcLengthTable[arcLengthTable.Length - 1];
    }

    /// <summary>
    /// Belirli bir mesafeye karşılık gelen t parametresini bulur.
    /// Binary search (ikili arama) kullanır - verimli!
    /// 
    /// Örnek: Eğri 100 birim uzunsa ve distance=50 istiyorsak,
    /// eğrinin tam ortasındaki t değerini bulur (ki t=0.5 olmayabilir!)
    /// </summary>
    public static float DistanceToT(float[] arcLengthTable, float distance)
    {
        int sampleCount = arcLengthTable.Length - 1;
        float totalLength = GetTotalLength(arcLengthTable);

        // Sınır kontrolleri
        if (distance <= 0f) return 0f;
        if (distance >= totalLength) return 1f;

        // Binary search: mesafenin tablodaki konumunu bul
        int low = 0;
        int high = sampleCount;

        while (low < high)
        {
            int mid = (low + high) / 2;
            if (arcLengthTable[mid] < distance)
                low = mid + 1;
            else
                high = mid;
        }

        // İki örnek arasında lineer interpolasyon (daha hassas sonuç)
        if (low > 0)
        {
            float lengthBefore = arcLengthTable[low - 1];
            float lengthAfter = arcLengthTable[low];
            float segmentFraction = (distance - lengthBefore) / (lengthAfter - lengthBefore);

            // t değerini hesapla
            float tBefore = (float)(low - 1) / sampleCount;
            float tAfter = (float)low / sampleCount;
            return tBefore + segmentFraction * (tAfter - tBefore);
        }

        return (float)low / sampleCount;
    }

    /// <summary>
    /// DISTANCE MODE: Belirli mesafe aralıklarıyla eğri üzerinde noktalar üretir.
    /// 
    /// Örnek: Eğri 100 birim, distance=10 → 11 nokta (0, 10, 20, ..., 100)
    /// </summary>
    public static Vector3[] GetPointsByDistance(Vector3[] controlPoints, float distance)
    {
        float[] arcTable = BuildArcLengthTable(controlPoints);
        float totalLength = GetTotalLength(arcTable);

        // Kaç nokta olacak?
        int count = Mathf.FloorToInt(totalLength / distance) + 1;

        Vector3[] points = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            float d = i * distance;
            // Mesafeyi t parametresine çevir
            float t = DistanceToT(arcTable, d);
            // t'deki noktayı hesapla
            points[i] = EvaluateCurve(controlPoints, t);
        }

        return points;
    }

    /// <summary>
    /// COUNT MODE: Belirli sayıda eşit aralıklı noktalar üretir.
    /// 
    /// Örnek: count=5 → 5 nokta, eğri boyunca eşit mesafede dağıtılmış
    /// </summary>
    public static Vector3[] GetPointsByCount(Vector3[] controlPoints, int count)
    {
        if (count < 2) count = 2; // En az 2 nokta (başlangıç + bitiş)

        float[] arcTable = BuildArcLengthTable(controlPoints);
        float totalLength = GetTotalLength(arcTable);

        // Her nokta arası mesafe
        float spacing = totalLength / (count - 1);

        Vector3[] points = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            float d = i * spacing;
            float t = DistanceToT(arcTable, d);
            points[i] = EvaluateCurve(controlPoints, t);
        }

        return points;
    }

    // =========================================================================
    // ŞERİT (LANE) OFFSET FONKSİYONLARI
    // =========================================================================

    /// <summary>
    /// Merkez eğriden belirli bir mesafede offset'lenmiş (kaydırılmış) noktalar üretir.
    /// Sol şerit için pozitif offset, sağ şerit için negatif offset.
    /// 
    /// Bu fonksiyon, merkez Bezier eğrisinden paralel şeritler oluşturmak için kullanılır.
    /// Her noktada eğrinin normalini hesaplar ve o yönde offset uygular.
    /// </summary>
    public static Vector3 GetOffsetPoint(Vector3[] controlPoints, float t, float offset)
    {
        Vector3 point = EvaluateCurve(controlPoints, t);
        Vector3 normal = GetNormal(controlPoints, t);
        return point + normal * offset;
    }

    /// <summary>
    /// Offset'lenmiş eğri için kontrol noktalarını yaklaşık olarak hesaplar.
    /// Her kontrol noktasını normal yönde kaydırır.
    /// 
    /// NOT: Bu tam matematiksel offset değil (Bezier offset curve karmaşıktır),
    /// ama pratik kullanım için yeterli bir yaklaşımdır.
    /// </summary>
    public static Vector3[] GetOffsetControlPoints(Vector3[] controlPoints, float offset)
    {
        Vector3[] offsetPoints = new Vector3[controlPoints.Length];

        for (int i = 0; i < controlPoints.Length; i++)
        {
            float t = (float)i / (controlPoints.Length - 1);
            Vector3 normal = GetNormal(controlPoints, t);
            offsetPoints[i] = controlPoints[i] + normal * offset;
        }

        return offsetPoints;
    }
}
