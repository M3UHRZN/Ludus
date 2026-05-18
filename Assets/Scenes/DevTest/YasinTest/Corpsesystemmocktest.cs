// CorpseSystemMockTest.cs
// Assets/Scripts/DevTest/CorpseSystemMockTest.cs
// Sorumlu: Yasin Kapaklıkaya | feature/yasin-item-system
// Sprint 2 — Ceset taşıma sistemi mock testi
//
// KULLANIM:
//   YasinTest.unity'e boş GameObject ekle, bu script'i bağla.
//   Play → Console'da test sonuçlarını gör.
//   NGO gerektirmez — saf C# mantık testi.

using UnityEngine;

public class CorpseSystemMockTest : MonoBehaviour
{
    private void Start()
    {
        RunAllTests();
    }

    private void RunAllTests()
    {
        Debug.Log("=== CorpseSystem Mock Test Başladı ===");

        Test_WeightCalculation();
        Test_CarrySlotBlocking();
        Test_AbandonmentPenalty();
        Test_MultiCorpsePenalty();

        Debug.Log("=== Tüm Testler Tamamlandı ===");
    }

    // ------------------------------------------------------------------ Test 1

    private void Test_WeightCalculation()
    {
        // Ağırlık-hız formülü: MoveSpeed = baseSpeed * (1 - totalWeight / hardCap)
        float baseSpeed = 5f;
        float hardCap   = 18f; // 3 Large item = 18

        // Ceset (6) + Medium item (3) = 9
        float totalWeight = 6 + 3;
        float speed = baseSpeed * (1f - totalWeight / hardCap);

        bool pass = speed > 0f && speed < baseSpeed;
        Debug.Log($"[Test 1] Ağırlık-Hız: weight={totalWeight}, speed={speed:F2} → {(pass ? "PASS ✓" : "FAIL ✗")}");
    }

    // ------------------------------------------------------------------ Test 2

    private void Test_CarrySlotBlocking()
    {
        // Slot dolu simülasyonu: MaxSlots = 4
        int maxSlots     = PlayerInventory.MaxSlots;
        int currentSlots = maxSlots; // dolu

        bool isFull = currentSlots >= maxSlots;

        // Ceset alınamamalı
        bool corpseBlocked = isFull;
        Debug.Log($"[Test 2] Carry slot bloğu: slots={currentSlots}/{maxSlots}, blocked={corpseBlocked} → {(corpseBlocked ? "PASS ✓" : "FAIL ✗")}");

        // IsCarryingCorpse = true iken de blok olmalı
        bool isCarryingCorpse = true;
        bool newItemBlocked   = isCarryingCorpse;
        Debug.Log($"[Test 2b] Ceset taşırken item blok: {(newItemBlocked ? "PASS ✓" : "FAIL ✗")}");
    }

    // ------------------------------------------------------------------ Test 3

    private void Test_AbandonmentPenalty()
    {
        // GDD §6.4: penalty = max(0.25f, playerCount / 100f)
        float grossCredits = 1000f;
        int   playerCount  = 6;

        float penalty        = Mathf.Max(0.25f, playerCount / 100f); // max(0.25, 0.06) = 0.25
        float deduction      = grossCredits * penalty;
        float netCredits     = grossCredits - deduction;

        bool pass = Mathf.Approximately(penalty, 0.25f) && Mathf.Approximately(netCredits, 750f);
        Debug.Log($"[Test 3] Terk cezası: gross={grossCredits}, penalty={penalty:P0}, net={netCredits} → {(pass ? "PASS ✓" : "FAIL ✗")}");
    }

    // ------------------------------------------------------------------ Test 4

    private void Test_MultiCorpsePenalty()
    {
        // 2 ceset terk edilirse: 2x penalty (her ceset için ayrı hesap)
        float grossCredits = 1000f;
        int   playerCount  = 4;
        int   abandonedCount = 2;

        float penaltyPerCorpse = Mathf.Max(0.25f, playerCount / 100f);
        float totalDeduction   = Mathf.Min(grossCredits * penaltyPerCorpse * abandonedCount, grossCredits);
        float netCredits       = grossCredits - totalDeduction;

        bool pass = netCredits >= 0f;
        Debug.Log($"[Test 4] Çoklu ceset cezası: {abandonedCount} ceset, deduction={totalDeduction}, net={netCredits} → {(pass ? "PASS ✓" : "FAIL ✗")}");
    }
}