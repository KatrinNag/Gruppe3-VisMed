
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;


using UnityEngine.InputSystem;


public class XRManager : MonoBehaviour
{
    // Typisierte Actions-Klasse (wird von Unity generiert) 
    private InputSystem_Actions controls;
    //public float heightSpeed = 1.0f; // Geschwindigkeit der Höhenänderung (z.B. per Stick)
    public float heightButtonSpeed1 = 0.2f; // Geschwindigkeit für Taste X/Y (langsamer)
    public float rotationSpeed = 60f; // Rotationsgeschwindigkeit in Grad pro Sekunde
    public float panSpeed = 1.0f; // Geschwindigkeit für Panning
    public GameObject objectToCycle; 
    public List<GameObject> models = new List<GameObject>();
    private int currentModelIndex = -1;
    private bool prevPrimaryButton = false;
    private bool prevSecondaryButton = false;
    private XROrigin xrOrigin;
    private float targetHeight;
    private float targetYRotation;
    private Vector3 targetPan;

    private Vector2 leftStickInput = Vector2.zero;
    private Vector2 rightStickInput = Vector2.zero;
    
    /// <summary>
    /// Start is called before the first frame update       
    /// </summary>
    void Start()
    {
        // Controls zuweisen und Events abonnieren
        controls = new InputSystem_Actions();
        controls.XR.PrevModel.performed += ctx => { Debug.Log("🔵 BUTTON A (PrevModel) PRESSED"); PrevModel(); };          // A Button Pressed
        controls.XR.NextModel.performed += ctx => { Debug.Log("🔴 BUTTON B (NextModel) PRESSED"); NextModel(); };          // B Button Pressed
        
        // Event für linken Stick (Move) abonnieren
        controls.XR.LeftStickMove.performed += ctx => leftStickInput = ctx.ReadValue<Vector2>();
        controls.XR.LeftStickMove.canceled += ctx => leftStickInput = Vector2.zero;
         controls.XR.LeftStickMove.Enable();
        // Event für rechten Stick (Drehung) abonnieren
        controls.XR.RightStickMove.performed += ctx => rightStickInput = ctx.ReadValue<Vector2>();
        controls.XR.RightStickMove.canceled += ctx => rightStickInput = Vector2.zero;
          controls.XR.RightStickMove.Enable();
        controls.Enable();

        Debug.Log("✅ XRManager initialized - A/B buttons are active for model switching");

        xrOrigin = GetComponent<XROrigin>();
        if (xrOrigin == null)
            xrOrigin = FindFirstObjectByType<XROrigin>();

        if (xrOrigin != null && xrOrigin.CameraFloorOffsetObject != null)
        {
            var pos = xrOrigin.CameraFloorOffsetObject.transform.localPosition;
            targetHeight = pos.y;
            targetYRotation = xrOrigin.CameraFloorOffsetObject.transform.localEulerAngles.y;
            targetPan = new Vector3(pos.x, 0f, pos.z);
        }
        else
        {
            Debug.LogWarning("XROrigin or its CameraFloorOffsetObject not found. Movement/rotation using xrOrigin will be skipped until it's available.");
        }

        // Modelle aus dem GameObject "alle" im Scene-Hierarchiebaum befüllen
        GameObject modelsParent = GameObject.Find("alle");
        // Wenn kein "alle"-Parent vorhanden ist, fallback auf objectToCycle oder dieses GameObject
        if (modelsParent == null)
        {
            if (objectToCycle == null)
            {
                objectToCycle = this.gameObject;
            }
            modelsParent = objectToCycle;
        }

        if (modelsParent != null)
        {
            models.Clear();
            foreach (Transform child in modelsParent.transform)
            {
                models.Add(child.gameObject);
            }

            // Sortiere die Modelle nach der Zahl am Anfang des Namens (z.B. "1_T2", "2_T3", etc.)
            models.Sort((a, b) =>
            {
                // Extrahiere die führende Zahl aus dem Namen
                string aName = a.name;
                string bName = b.name;
                
                // Parsen der Zahl (z.B. "17_Kreuzbein" -> 17)
                int aNum = ExtractLeadingNumber(aName);
                int bNum = ExtractLeadingNumber(bName);
                
                return aNum.CompareTo(bNum);
            });

            Debug.Log($"✅ Loaded {models.Count} models in order: {string.Join(", ", models.ConvertAll(m => m.name))}");

            // Show all models initially
            if (models.Count > 0)
            {
                currentModelIndex = -1; // -1 bedeutet: Alle Modelle anzeigen
                ShowAllModels();
            }
        }
    }

    /// <summary>
    /// Update is called once per frame 
    /// </summary>
    void Update()
    {
        // Polling fallback: direkten Wert der Actions pro Frame lesen (Üpolling)
        // if (controls != null)
        // {
        //     if (controls.XR.LeftStickMove.enabled)
        //         leftStickInput = controls.XR.LeftStickMove.ReadValue<Vector2>();
        //     if (controls.XR.RightStickMove.enabled)
        //         rightStickInput = controls.XR.RightStickMove.ReadValue<Vector2>();
        // }

        // Debug-Ausgabe: Thumbstick-Werte loggen (nur wenn Input != 0)
        if (leftStickInput != Vector2.zero || rightStickInput != Vector2.zero)
        {
            Debug.Log($" Input - LeftStick: {leftStickInput}, RightStick: {rightStickInput}");
        }

        // Rechter Thumbstick: Nur links/rechts für Drehung um Y-Achse jetzt über Input System Event
        if (objectToCycle != null && Mathf.Abs(rightStickInput.x) > 0.1f)
        {
            float rotY = -rightStickInput.x * rotationSpeed * Time.deltaTime;
            objectToCycle.transform.Rotate(0f, rotY, 0f, Space.Self);
        }

        // Linker Thumbstick: Objekt im Raum bewegen (X/Z) jetzt über Input System Event
        if (objectToCycle != null && Mathf.Abs(leftStickInput.x) > 0.1f && xrOrigin != null && xrOrigin.Camera != null)
        {
            Vector3 move = new Vector3(leftStickInput.x, 0, leftStickInput.y);
            move = xrOrigin.Camera.transform.TransformDirection(move);
            move.y = 0; // Keine Höhenänderung durch Stick
            objectToCycle.transform.position += move * panSpeed * Time.deltaTime;
        }

        // Rotation direkt setzen (nur wenn xrOrigin verfügbar ist)
        if (xrOrigin != null && xrOrigin.CameraFloorOffsetObject != null)
        {
            xrOrigin.CameraFloorOffsetObject.transform.localRotation = Quaternion.Euler(0f, targetYRotation, 0f);
        }

    }

    /// <summary>
    /// Extrahiert die führende Zahl aus einem String (z.B. "17_Kreuzbein" -> 17)
    /// </summary>
    private int ExtractLeadingNumber(string name)
    {
        string numberStr = "";
        foreach (char c in name)
        {
            if (char.IsDigit(c))
            {
                numberStr += c;
            }
            else
            {
                break;
            }
        }
        
        if (int.TryParse(numberStr, out int number))
        {
            return number;
        }
        return int.MaxValue; // Falls keine Zahl gefunden, ans Ende
    }

    /// <summary>
    /// Zeigt nur das Modell am angegebenen Index an, alle anderen werden ausgeblendet
    /// </summary>
    private void ShowOnlyModel(int index)
    {
        for (int i = 0; i < models.Count; i++)
        {
            if (models[i] != null)
                models[i].SetActive(i == index);
        }
    }

    /// <summary>
    /// Zeigt alle Modelle an
    /// </summary>
    private void ShowAllModels()
    {
        for (int i = 0; i < models.Count; i++)
        {
            if (models[i] != null)
                models[i].SetActive(true);
        }
    }

    /// <summary>
    /// Zeigt das nächste Modell an. Von -1 (alle) -> 0, 1, 2, ... und wieder zu -1
    /// </summary>
    private void NextModel()
    {
        if (currentModelIndex == -1)
        {
            // Von "Alle anzeigen" zum ersten Modell (Index 0)
            currentModelIndex = 0;
            ShowOnlyModel(currentModelIndex);
            Debug.Log($"📍 Showing Model {currentModelIndex} (of {models.Count})");
        }
        else
        {
            currentModelIndex++;
            if (currentModelIndex >= models.Count)
            {
                // Nach dem letzten Modell wieder alle anzeigen
                ShowAllModels();
                currentModelIndex = -1;
                Debug.Log("🔄 Showing ALL models");
            }
            else
            {
                ShowOnlyModel(currentModelIndex);
                Debug.Log($"📍 Showing Model {currentModelIndex} (of {models.Count})");
            }
        }
    }

    /// <summary>
    /// Zeigt das vorherige Modell an oder alle Modelle, wenn am Anfang
    /// </summary>
    private void PrevModel()
    {
        if (currentModelIndex == -1)
        {
            // Von "Alle anzeigen" zum letzten Modell
            currentModelIndex = models.Count - 1;
            ShowOnlyModel(currentModelIndex);
            Debug.Log($"📍 Showing Model {currentModelIndex} (of {models.Count})");
        }
        else
        {
            currentModelIndex--;
            if (currentModelIndex < 0)
            {
                // Vor dem ersten Modell wieder alle anzeigen
                ShowAllModels();
                currentModelIndex = -1;
                Debug.Log("🔄 Showing ALL models");
            }
            else
            {
                ShowOnlyModel(currentModelIndex);
                Debug.Log($"📍 Showing Model {currentModelIndex} (of {models.Count})");
            }
        }
    }
}
