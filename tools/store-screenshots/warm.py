"""Ekranlarda görünecek mekanların fotoğraflarını sırayla önbelleğe alır (Google kotasını boşa harcamadan).
Kullanım: python3 warm.py [API] [adet]   ör. python3 warm.py http://localhost:5098 12"""
import json, sys, time, urllib.parse, urllib.request
API = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5098"
N = int(sys.argv[2]) if len(sys.argv) > 2 else 12
def get(u):
    with urllib.request.urlopen(API + u, timeout=90) as r:
        return json.load(r)
Q = "/api/recommendations?lat=40.9885&lng=29.025&intent=Kahve&radiusMeters=5000"
items = [i for i in get(Q)["items"] if "starbucks" not in i["name"].lower()][:N]
for i in items:
    if not i.get("photoUrl"):
        try:
            get("/api/venues/" + urllib.parse.quote(i["placeId"], safe=""))
        except Exception as e:
            print("hata", i["name"], e)
        time.sleep(1.5)
items = [i for i in get(Q)["items"] if "starbucks" not in i["name"].lower()][:N]
for i in items:
    print(("FOTO " if i.get("photoUrl") else "---- ") + i["name"], i.get("googleRating"), i.get("googleRatingCount"))
print("fotoğraflı", sum(1 for i in items if i.get("photoUrl")), "/", len(items))
