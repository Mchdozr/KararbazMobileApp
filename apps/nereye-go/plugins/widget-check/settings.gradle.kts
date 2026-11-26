// Ana ekran bileşeninin (plugins/withNearbyWidget.js) yerel derleme ve Robolectric testi.
// Kaynaklar eklentiden üretilir: önce `node --experimental-strip-types generate.mts`. Bkz. docs/ana-ekran-bileseni.md.
pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositories {
        google()
        // Robolectric'in 160 MB'lık yerel çalışma zamanı Maven Central'da sık 429 alır; önceden indirilmişse yerelden.
        mavenLocal { content { includeGroup("org.robolectric") } }
        mavenCentral()
    }
}

rootProject.name = "widget-check"
