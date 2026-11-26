// Sürümler uygulamanın derlemesiyle aynı: node_modules/react-native/gradle/libs.versions.toml
// (AGP 9.2.1, compileSdk 37, minSdk 24). AGP 9 Kotlin'i kendisi derler.
plugins {
    id("com.android.library") version "9.2.1"
}

android {
    // Üretilen Kotlin "import com.mchdozr.nereye.R" der: uygulamanın paketiyle aynı ad alanı.
    namespace = "com.mchdozr.nereye"
    compileSdk = 37

    defaultConfig {
        minSdk = 24
    }

    testOptions {
        unitTests.isIncludeAndroidResources = true
    }

    lint {
        abortOnError = true
        warningsAsErrors = false
        checkTestSources = false
    }
}

dependencies {
    testImplementation("junit:junit:4.13.2")
    testImplementation("org.robolectric:robolectric:4.18.1")
}

dependencies {
    testImplementation("androidx.test:core:1.7.0")
}
