# CHANGELOG – Gruppe3-VisMed
Nagele, Rausch, Janesch

Diese Datei dokumentiert die funktionalen und szenischen Erweiterungen des Unity-XR-Projekts.

---

## Änderungen in XRManager.cs

- Implementierung einer Einzel-Durchschaltung der Wirbel-Kinder
- Implementierung von Rotationen in X- und Y-Richtung

---

## Funktionale Implementierungen

- **Kinder- bzw. Modell-Durchschaltung**:  
  Die Kind-Objekte eines Parent-Containers (Standard-Name: "alle") lassen sich einzeln durchschalten.  
  Mit den Controller-Buttons (`NextModel`/`PrevModel`, z.B. A/B) wechselt die Ansicht von "alle" zu einem einzelnen Modell und zurück.  
  Bei Einzelanzeige werden alle anderen Modelle deaktiviert.

- **Selektierbare Teile ("Bones")**:  
  Für ein angezeigtes Modell werden alle Kind-Transforms als auswählbare Teile erfasst.  
  Mehrfache Button-Presses schalten durch diese Teile.

- **Rotation in X und Y**:  
  Für einen ausgewählten Wirbel sind Rotationen um die X- und Y-Achse implementiert.  
  Die Steuerung erfolgt über die Thumbsticks (links/rechts für Y, oben/unten für X)  
  bzw. Fallback-Tasten (D-Pad / Keyboard).  
  Rotationen werden lokal auf das Modell bzw. Teil angewendet.

- **Bewegung / Panning**:  
  Wenn "alle" angezeigt werden, kann das Parent-Objekt mit dem linken Stick
  in X/Z verschoben werden.

---

## Ergänzungen in der Unity-Szene (Umgebung & Objekte)

- Einrichtung eines XR Origin (VR) als Basis für die XR-Interaktion
- Integration des XR Interaction Toolkits inklusive neuem Input System
- Einbindung des XR Device Simulators zur Durchführung von Tests ohne VR-Brille
- Strukturierung der Wirbel-Modelle in einem Parent-Container ("alle")
- Erstellung bzw. Anpassung der Wirbel-Prefabs
- Einrichtung einer gezielten Beleuchtung (Light Cone)
- Implementierung eines Global Volume zur Steuerung von Helligkeit und Atmosphäre
- Anpassung von Material- und Texturzuweisungen (Behebung pinker Shader-Probleme)
- Optimierung der Szene-Helligkeit (Fokus auf Modell statt gesamter Umgebung)
- Debug-Ausgaben im Play Mode zur Überprüfung der Controller-Eingaben

---

## Verwendete Assets für Umgebung und Hintergrund

Zur Gestaltung der Umgebung wurden folgende Asset-Pakete integriert
und an die Szene angepasst:

- **Art_Prototype_Stylized** – Skelette, die aus Gräbern steigen
- **GVOZDY** – Grabsteine
- **LiquidFire Assets** – Mausoleum
- **Planets of the Solar System 3D** – Mond

Die Assets wurden hinsichtlich Material, Skalierung und Beleuchtung angepasst,
um eine konsistente Darstellung mit dem Wirbel-Modell zu gewährleisten.
