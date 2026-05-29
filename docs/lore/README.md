# VoidHaul Lore Dokumantasyonu

Bu klasor oyunun resmi lore / dunya kurgu dokumanlarini tutar. Sahne /
karakter / mekanik kararlarinin "neden" sorusu burada cevap bulur.

## Dosyalar

### `HAUL_Ilk_Vaaz_Gecesi_Lore.pdf`
Ana lore dokumani. Oyunun bashlangic kurgusu, "ilk vaaz gecesi" olayi,
priest (Type B) ve robot (Type A) karakter geri planlari, mekan
hikayesi ve ana tema. Bireysel rapor + sunum hazirliginda referans
olarak kullanilir.

## Kullanim

- Yeni bir feature / NPC / mekanik tasarlanirken lore'a uygunluk
  kontrolu icin buradan baslanir.
- Hocaya proje teslim edilirken `docs/` klasoru tasarim dokumantasyonu
  olarak gosterilebilir.
- Lore guncellemesi gerekirse PDF'i yeniden yazip ayni isimle uzerine
  yazmak yeterli (git diff binary olur ama versiyon tarihi kalir).

## Pattern Showcase ile Iliski

Lore'daki "priest sezgisi" → kodda `LureBehavior` (Strategy pattern,
PR #112) ve `FearSystem` priest proximity (Observer pattern, PR #112)
olarak somutlasti. Lore <-> kod iliski izini raporda gostermek icin
buraya bakilabilir.
