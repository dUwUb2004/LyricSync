plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
    id("org.jetbrains.kotlin.plugin.compose") version "2.2.10"
}

android {
    namespace = "com.skdushow.mc2pc"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.skdushow.mc2pc"
        minSdk = 26
        targetSdk = 36
        versionCode = 1
        versionName = "1.0"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_11
        targetCompatibility = JavaVersion.VERSION_11
    }

    kotlinOptions {
        jvmTarget = "11"
    }

    buildFeatures {
        compose = true
    }

    composeOptions {
        kotlinCompilerExtensionVersion = "1.5.8"
    }
}

dependencies {
    implementation(libs.appcompat)
    implementation(libs.material)
    implementation("androidx.core:core-ktx:1.12.0")
    implementation("androidx.media3:media3-common:1.2.1")
    
    // Compose Material 3
    implementation("androidx.compose.material3:material3:1.2.0")
    implementation("androidx.compose.material3:material3-window-size-class:1.2.0")
    
    // Compose UI
    implementation("androidx.compose.ui:ui:1.6.1")
    implementation("androidx.compose.ui:ui-tooling-preview:1.6.1")
    implementation("androidx.compose.ui:ui-graphics:1.6.1")
    
    // Compose Activity
    implementation("androidx.activity:activity-compose:1.8.2")
    
    // Compose Icons
    implementation("androidx.compose.material:material-icons-core:1.6.1")
    implementation("androidx.compose.material:material-icons-extended:1.6.1")
    
    // Compose Foundation
    implementation("androidx.compose.foundation:foundation:1.6.1")
    implementation("androidx.compose.animation:animation:1.6.1")
    
    // Compose Runtime
    implementation("androidx.compose.runtime:runtime:1.6.1")
    implementation("androidx.compose.runtime:runtime-livedata:1.6.1")
    
    implementation(libs.core.ktx)

    testImplementation(libs.junit)
    androidTestImplementation(libs.ext.junit)
    androidTestImplementation(libs.espresso.core)
}
