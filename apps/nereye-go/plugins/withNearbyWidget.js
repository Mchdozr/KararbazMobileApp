// Android ana ekran bileşeni: "Yakındaki açık mekanlar".
// Expo config eklentisi; prebuild sırasında Kotlin sağlayıcıyı, düzeni ve manifest kaydını üretir.
// Veri: uygulama ana sayfa listesi gelince filesDir/widget-nearby.json yazar (lib/widget-data.ts);
// bileşen 30 dk'da bir (Android'in en kısa periyodu) o dosyayı okur. 6 saatten eski veri gösterilmez.
// Satıra dokununca nereyego://venue/{id} açılır. iOS için WidgetKit uzantısı ayrıca gerekir (yapılmadı).
const { AndroidConfig, withAndroidManifest, withDangerousMod } = require('expo/config-plugins');
const fs = require('fs');
const path = require('path');

const RECEIVER = '.widget.NearbyWidgetProvider';

function kotlinSource(pkg) {
  return `package ${pkg}.widget

import android.app.PendingIntent
import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.view.View
import android.widget.RemoteViews
import ${pkg}.R
import org.json.JSONObject
import java.io.File

/** Yakındaki açık mekanlar. Veriyi uygulamanın yazdığı filesDir/widget-nearby.json dosyasından okur. */
class NearbyWidgetProvider : AppWidgetProvider() {
  override fun onUpdate(context: Context, manager: AppWidgetManager, ids: IntArray) {
    for (id in ids) {
      manager.updateAppWidget(id, build(context))
    }
  }

  companion object {
    private const val MAX_AGE_MS = 6L * 60 * 60 * 1000
    private val ROWS = intArrayOf(R.id.nereye_row1, R.id.nereye_row2, R.id.nereye_row3)
    private val NAMES = intArrayOf(R.id.nereye_name1, R.id.nereye_name2, R.id.nereye_name3)
    private val METAS = intArrayOf(R.id.nereye_meta1, R.id.nereye_meta2, R.id.nereye_meta3)

    fun build(context: Context): RemoteViews {
      val views = RemoteViews(context.packageName, R.layout.nereye_nearby_widget)
      views.setOnClickPendingIntent(R.id.nereye_widget_root, open(context, "nereyego://", 0))
      val data = read(context)
      val fresh = data != null && System.currentTimeMillis() - data.optLong("updatedAt") < MAX_AGE_MS
      val items = if (fresh) data!!.optJSONArray("items") else null
      if (!fresh || items == null || items.length() == 0) {
        views.setTextViewText(R.id.nereye_title, context.getString(R.string.nereye_widget_title))
        views.setTextViewText(R.id.nereye_updated, "")
        views.setViewVisibility(R.id.nereye_empty, View.VISIBLE)
        for (row in ROWS) views.setViewVisibility(row, View.GONE)
        return views
      }
      views.setTextViewText(R.id.nereye_title, data!!.optString("title"))
      views.setTextViewText(R.id.nereye_updated, data.optString("updatedLabel"))
      views.setViewVisibility(R.id.nereye_empty, View.GONE)
      for (i in ROWS.indices) {
        val item = items.optJSONObject(i)
        if (item == null) {
          views.setViewVisibility(ROWS[i], View.GONE)
          continue
        }
        views.setViewVisibility(ROWS[i], View.VISIBLE)
        views.setTextViewText(NAMES[i], item.optString("name"))
        views.setTextViewText(METAS[i], item.optString("meta"))
        views.setOnClickPendingIntent(ROWS[i], open(context, "nereyego://venue/" + Uri.encode(item.optString("placeId")), i + 1))
      }
      return views
    }

    private fun read(context: Context): JSONObject? = try {
      val file = File(context.filesDir, "widget-nearby.json")
      if (file.exists()) JSONObject(file.readText()) else null
    } catch (e: Exception) {
      null
    }

    private fun open(context: Context, url: String, code: Int): PendingIntent {
      val intent = Intent(Intent.ACTION_VIEW, Uri.parse(url))
        .setPackage(context.packageName)
        .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP)
      return PendingIntent.getActivity(context, code, intent, PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
    }
  }
}
`;
}

function row(n) {
  return `    <LinearLayout
        android:id="@+id/nereye_row${n}"
        android:layout_width="match_parent"
        android:layout_height="wrap_content"
        android:layout_marginTop="6dp"
        android:background="@drawable/nereye_widget_row"
        android:orientation="vertical"
        android:paddingStart="10dp"
        android:paddingEnd="10dp"
        android:paddingTop="6dp"
        android:paddingBottom="6dp">
        <TextView
            android:id="@+id/nereye_name${n}"
            android:layout_width="match_parent"
            android:layout_height="wrap_content"
            android:ellipsize="end"
            android:maxLines="1"
            android:textColor="#FFFFFF"
            android:textSize="14sp"
            android:textStyle="bold" />
        <TextView
            android:id="@+id/nereye_meta${n}"
            android:layout_width="match_parent"
            android:layout_height="wrap_content"
            android:ellipsize="end"
            android:maxLines="1"
            android:textColor="#C7E3D7"
            android:textSize="12sp" />
    </LinearLayout>`;
}

const LAYOUT = `<?xml version="1.0" encoding="utf-8"?>
<LinearLayout xmlns:android="http://schemas.android.com/apk/res/android"
    android:id="@+id/nereye_widget_root"
    android:layout_width="match_parent"
    android:layout_height="match_parent"
    android:background="@drawable/nereye_widget_bg"
    android:orientation="vertical"
    android:padding="12dp">
    <LinearLayout
        android:layout_width="match_parent"
        android:layout_height="wrap_content"
        android:orientation="horizontal">
        <TextView
            android:id="@+id/nereye_title"
            android:layout_width="0dp"
            android:layout_height="wrap_content"
            android:layout_weight="1"
            android:ellipsize="end"
            android:maxLines="1"
            android:text="@string/nereye_widget_title"
            android:textColor="#FFFFFF"
            android:textSize="15sp"
            android:textStyle="bold" />
        <TextView
            android:id="@+id/nereye_updated"
            android:layout_width="wrap_content"
            android:layout_height="wrap_content"
            android:textColor="#A9CDBE"
            android:textSize="11sp" />
    </LinearLayout>
    <TextView
        android:id="@+id/nereye_empty"
        android:layout_width="match_parent"
        android:layout_height="wrap_content"
        android:layout_marginTop="8dp"
        android:text="@string/nereye_widget_empty"
        android:textColor="#E5F1EB"
        android:textSize="13sp"
        android:visibility="gone" />
${[1, 2, 3].map(row).join('\n')}
</LinearLayout>
`;

const INFO = `<?xml version="1.0" encoding="utf-8"?>
<appwidget-provider xmlns:android="http://schemas.android.com/apk/res/android"
    android:description="@string/nereye_widget_description"
    android:initialLayout="@layout/nereye_nearby_widget"
    android:minWidth="250dp"
    android:minHeight="110dp"
    android:previewLayout="@layout/nereye_nearby_widget"
    android:resizeMode="horizontal|vertical"
    android:targetCellWidth="4"
    android:targetCellHeight="2"
    android:updatePeriodMillis="1800000"
    android:widgetCategory="home_screen" />
`;

const BG = `<?xml version="1.0" encoding="utf-8"?>
<shape xmlns:android="http://schemas.android.com/apk/res/android" android:shape="rectangle">
    <gradient android:angle="270" android:endColor="#0A3D2F" android:startColor="#0E6249" />
    <corners android:radius="20dp" />
</shape>
`;

const ROW_BG = `<?xml version="1.0" encoding="utf-8"?>
<shape xmlns:android="http://schemas.android.com/apk/res/android" android:shape="rectangle">
    <solid android:color="#26FFFFFF" />
    <corners android:radius="12dp" />
</shape>
`;

function strings(title, empty, description) {
  return `<?xml version="1.0" encoding="utf-8"?>
<resources>
    <string name="nereye_widget_title">${title}</string>
    <string name="nereye_widget_empty">${empty}</string>
    <string name="nereye_widget_description">${description}</string>
</resources>
`;
}

/** Üretilecek dosyalar (android/ köküne göre). Testler bunu doğrudan denetler. */
function widgetFiles(pkg) {
  const javaDir = path.join('app/src/main/java', ...pkg.split('.'), 'widget');
  const res = 'app/src/main/res';
  return {
    [path.join(javaDir, 'NearbyWidgetProvider.kt')]: kotlinSource(pkg),
    [path.join(res, 'layout/nereye_nearby_widget.xml')]: LAYOUT,
    [path.join(res, 'xml/nereye_nearby_widget_info.xml')]: INFO,
    [path.join(res, 'drawable/nereye_widget_bg.xml')]: BG,
    [path.join(res, 'drawable/nereye_widget_row.xml')]: ROW_BG,
    [path.join(res, 'values/nereye_widget_strings.xml')]: strings(
      'Yakındaki açık mekanlar',
      "Güncel liste için Kararbaz\\'ı aç.",
      'Yakınındaki şu an açık mekanlar',
    ),
    [path.join(res, 'values-en/nereye_widget_strings.xml')]: strings(
      'Open places nearby',
      'Open Kararbaz to refresh the list.',
      'Places open near you right now',
    ),
  };
}

function addReceiver(manifest) {
  const app = AndroidConfig.Manifest.getMainApplicationOrThrow(manifest);
  app.receiver = (app.receiver ?? []).filter((r) => r.$['android:name'] !== RECEIVER);
  app.receiver.push({
    $: { 'android:name': RECEIVER, 'android:exported': 'false', 'android:label': '@string/nereye_widget_title' },
    'intent-filter': [{ action: [{ $: { 'android:name': 'android.appwidget.action.APPWIDGET_UPDATE' } }] }],
    'meta-data': [{ $: { 'android:name': 'android.appwidget.provider', 'android:resource': '@xml/nereye_nearby_widget_info' } }],
  });
  return manifest;
}

function withNearbyWidget(config) {
  config = withAndroidManifest(config, (c) => {
    c.modResults = addReceiver(c.modResults);
    return c;
  });
  return withDangerousMod(config, [
    'android',
    async (c) => {
      const pkg = c.android?.package;
      if (!pkg) {
        throw new Error('withNearbyWidget: android.package gerekli');
      }
      for (const [rel, content] of Object.entries(widgetFiles(pkg))) {
        const file = path.join(c.modRequest.platformProjectRoot, rel);
        fs.mkdirSync(path.dirname(file), { recursive: true });
        fs.writeFileSync(file, content);
      }
      return c;
    },
  ]);
}

module.exports = withNearbyWidget;
module.exports.widgetFiles = widgetFiles;
module.exports.addReceiver = addReceiver;
