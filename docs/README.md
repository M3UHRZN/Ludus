# VoidHaul Dokumantasyon

Proje dokumanlari ve tasarim referanslari. Unity asset disindaki her
sey burada toplanir; boylece `Assets/` siklisinda kaybolmaz, rapor
hazirliginda tek bir kok dizinden taranabilir.

## Yapi

```
docs/
└── lore/             # Oyun dunyasi / kurgu dokumanlari
    ├── HAUL_Ilk_Vaaz_Gecesi_Lore.pdf
    └── README.md
```

Gelecekte eklenecek olasi alt klasorler:
- `design/` — pattern showcase, AI design doc, network mimarisi
- `reports/` — bireysel rapor draftlari
- `screenshots/` — sunum / store sayfasi gorselleri

## Katki Kurali

- Yeni bir doc eklenirse hangi klasore gittigini bu dosyada belirt.
- Buyuk binary'ler icin (PDF, video) Git LFS dusunulebilir; simdilik
  PDF'ler dogrudan repo'da, boyut < 5MB.
- Markdown tercih edilir; harici PDF'ler eklerken yaninda kisa bir
  ozet `.md` koymak okuyucuya yarar.
