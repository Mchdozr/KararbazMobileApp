package com.mchdozr.nereye.widget

import android.app.Application
import android.appwidget.AppWidgetManager
import android.content.Intent
import android.net.Uri
import android.view.View
import android.widget.FrameLayout
import android.widget.TextView
import androidx.test.core.app.ApplicationProvider
import com.mchdozr.nereye.R
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config
import org.robolectric.annotation.GraphicsMode
import android.graphics.Bitmap
import android.graphics.Canvas
import java.io.File

/**
 * Eklentinin ürettiği NearbyWidgetProvider'ı gerçek Android çizim koduyla (Robolectric) dener.
 * Örnek veri uygulamanın widgetSnapshot() çıktısıdır (generate.mts): yazan ve okuyan aynı biçimde buluşur.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [36], qualifiers = "tr")
class NearbyWidgetProviderTest {
    private val app: Application = ApplicationProvider.getApplicationContext()
    private val file get() = File(app.filesDir, "widget-nearby.json")

    @Before
    fun clean() {
        file.delete()
    }

    private fun snapshot(ageMs: Long): String {
        val raw = javaClass.classLoader!!.getResource("snapshot.json")!!.readText()
        return JSONObject(raw).put("updatedAt", System.currentTimeMillis() - ageMs).toString()
    }

    private fun render(): View = NearbyWidgetProvider.build(app).apply(app, FrameLayout(app))

    private fun View.text(id: Int) = findViewById<TextView>(id).text.toString()

    private fun View.shown(id: Int) = findViewById<View>(id).visibility == View.VISIBLE

    @Test
    fun fresh_snapshot_shows_title_time_and_rows() {
        file.writeText(snapshot(ageMs = 10 * 60 * 1000))
        val view = render()

        assertEquals("Kahve · Kadıköy", view.text(R.id.nereye_title))
        assertEquals("Güncelleme 20:00", view.text(R.id.nereye_updated))
        assertEquals(false, view.shown(R.id.nereye_empty))
        assertEquals("Kronotrop Moda", view.text(R.id.nereye_name1))
        assertEquals("23:00'e kadar açık · 8 dk yürüme", view.text(R.id.nereye_meta1))
        assertEquals("Fazıl Bey", view.text(R.id.nereye_name2))
        assertEquals(true, view.shown(R.id.nereye_row1))
        assertEquals(true, view.shown(R.id.nereye_row2))
        // Reklam kartı bileşene girmez; üçüncü satır gizli.
        assertEquals(false, view.shown(R.id.nereye_row3))
    }

    @Test
    fun row_tap_opens_the_venue_deep_link() {
        file.writeText(snapshot(ageMs = 0))
        val view = render()

        view.findViewById<View>(R.id.nereye_row2).performClick()
        val started = shadowOf(app).nextStartedActivity
        assertNotNull("satıra dokununca etkinlik açılmalı", started)
        assertEquals(Intent.ACTION_VIEW, started.action)
        assertEquals(Uri.parse("nereyego://venue/google%3AChIJ%20x%2Fy"), started.data)
        assertEquals(app.packageName, started.`package`)
    }

    @Test
    fun header_tap_opens_the_app() {
        file.writeText(snapshot(ageMs = 0))
        render().findViewById<View>(R.id.nereye_widget_root).performClick()
        assertEquals(Uri.parse("nereyego://"), shadowOf(app).nextStartedActivity.data)
    }

    @Test
    fun stale_snapshot_asks_to_open_the_app() {
        file.writeText(snapshot(ageMs = 7L * 60 * 60 * 1000))
        val view = render()

        assertEquals("Yakındaki açık mekanlar", view.text(R.id.nereye_title))
        assertEquals("", view.text(R.id.nereye_updated))
        assertEquals(true, view.shown(R.id.nereye_empty))
        assertEquals("Güncel liste için Kararbaz'ı aç.", view.text(R.id.nereye_empty))
        assertEquals(false, view.shown(R.id.nereye_row1))
    }

    @Test
    fun missing_or_broken_file_does_not_crash() {
        assertEquals(true, render().shown(R.id.nereye_empty))
        file.writeText("{bozuk")
        assertEquals(true, render().shown(R.id.nereye_empty))
        file.writeText(JSONObject(snapshot(0)).put("items", org.json.JSONArray()).toString())
        assertEquals(true, render().shown(R.id.nereye_empty))
    }

    @Test
    @Config(qualifiers = "en")
    fun english_device_gets_english_texts() {
        val view = render()
        assertEquals("Open places nearby", view.text(R.id.nereye_title))
        assertEquals("Open Kararbaz to refresh the list.", view.text(R.id.nereye_empty))
    }

    @Test
    fun system_update_goes_through_the_registered_provider() {
        file.writeText(snapshot(ageMs = 0))
        val manager = shadowOf(AppWidgetManager.getInstance(app))
        val id = manager.createWidget(NearbyWidgetProvider::class.java, R.layout.nereye_nearby_widget)
        val view = manager.getViewFor(id)
        assertEquals("Kahve · Kadıköy", view.findViewById<TextView>(R.id.nereye_title).text.toString())

        // Manifest'teki alıcı ve bileşen bilgisi (30 dk, 4×2) eklentinin ürettiğiyle aynı.
        val info = app.resources.getXml(R.xml.nereye_nearby_widget_info)
        var period: String? = null
        while (info.next() != org.xmlpull.v1.XmlPullParser.END_DOCUMENT) {
            if (info.eventType == org.xmlpull.v1.XmlPullParser.START_TAG && info.name == "appwidget-provider") {
                period = info.getAttributeValue("http://schemas.android.com/apk/res/android", "updatePeriodMillis")
            }
        }
        assertEquals("1800000", period)
    }

    /** Gerçek Android çizim motoruyla (Robolectric yerel grafik) 4×2 önizleme: build/widget-preview-*.png. */
    @Test
    @GraphicsMode(GraphicsMode.Mode.NATIVE)
    fun renders_a_preview_image() {
        val density = app.resources.displayMetrics.density
        fun save(name: String) {
            val view = render()
            val w = (320 * density).toInt()
            val h = (180 * density).toInt()
            view.measure(View.MeasureSpec.makeMeasureSpec(w, View.MeasureSpec.EXACTLY), View.MeasureSpec.makeMeasureSpec(h, View.MeasureSpec.EXACTLY))
            view.layout(0, 0, w, h)
            val bitmap = Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888)
            view.draw(Canvas(bitmap))
            val out = File(System.getProperty("user.dir"), "build/widget-preview-$name.png")
            out.parentFile!!.mkdirs()
            out.outputStream().use { bitmap.compress(Bitmap.CompressFormat.PNG, 100, it) }
            // Boş bir tuval değil: arka plan rengi çizildi.
            assertEquals(true, bitmap.getPixel(w / 2, h - 4) != 0)
        }
        file.writeText(snapshot(ageMs = 0))
        save("dolu")
        file.writeText(snapshot(ageMs = 7L * 60 * 60 * 1000))
        save("bayat")
    }
}
