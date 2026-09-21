const intents = ['Kahve', 'Yemek', 'Eglence', 'Kultur', 'AcikHava', 'Gece'];
const vibes = ['Sessiz', 'Laptop', 'Manzarali', 'Romantik', 'Ucuz', 'Premium', 'CocukDostu', 'GecSaat', 'Tarihi'];

String intentLabel(String intent) => switch (intent) {
      'Kahve' => 'Kahve',
      'Yemek' => 'Yemek',
      'Eglence' => 'Eğlence',
      'Kultur' => 'Kültür',
      'AcikHava' => 'Açık hava',
      'Gece' => 'Gece',
      _ => intent,
    };
