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

    // Neu: Verwalten der direkten Kinder des aktuell ausgewählten Modells und Index des sichtbaren Kindes
    // currentChildren: Liste der unmittelbaren Child-GameObjects des selektierten Top-Level-Modells.
    // currentChildIndex: -1 bedeutet "gesamtes Modell anzeigen" (alle Kinder aktiv); >= 0 zeigt nur dieses Kind.
    private List<GameObject> currentChildren = new List<GameObject>();
    private int currentChildIndex = -1; // -1 bedeutet: gesamtes Modell ist sichtbar
    
    /// <summary>
    /// Start is called before the first frame update       
    /// </summary>
    void Start()
    {
        // Controls zuweisen und Events abonnieren
        controls = new InputSystem_Actions();
        controls.XR.PrevModel.performed += ctx => PrevModel();
        controls.XR.NextModel.performed += ctx => NextModel();
        // Event für linken Stick (Move) abonnieren
        controls.XR.LeftStickMove.performed += ctx => leftStickInput = ctx.ReadValue<Vector2>();
        controls.XR.LeftStickMove.canceled += ctx => leftStickInput = Vector2.zero;
        // Event für rechten Stick (Drehung) abonnieren
        controls.XR.RightStickMove.performed += ctx => rightStickInput = ctx.ReadValue<Vector2>();
        controls.XR.RightStickMove.canceled += ctx => rightStickInput = Vector2.zero;
        controls.Enable();


        xrOrigin = GetComponent<XROrigin>();
        if (xrOrigin != null)
        {
            var pos = xrOrigin.CameraFloorOffsetObject.transform.localPosition;
            targetHeight = pos.y;
            targetYRotation = xrOrigin.CameraFloorOffsetObject.transform.localEulerAngles.y;
            targetPan = new Vector3(pos.x, 0f, pos.z);
        }

        // Modelle aus dem GameObject "Models" im Scene-Hierarchiebaum befüllen
        GameObject modelsParent = GameObject.Find("Models");
        if (modelsParent != null)
        {
            models.Clear();
            foreach (Transform child in modelsParent.transform)
            {
                models.Add(child.gameObject);
            }

            // Show first model initially
            if (models.Count > 0)
            {
                currentModelIndex = 0;
                ShowOnlyModel(0);
            }
        }
        else
        {
            Debug.LogWarning("Kein GameObject 'Models' gefunden!");
        }
    }

    /// <summary>
    /// Update is called once per frame 
    /// </summary>
    void Update()
    {
        // Rechter Thumbstick: Nur links/rechts für Drehung um Y-Achse jetzt über Input System Event
        // mein Kommentar
        if (objectToCycle != null && Mathf.Abs(rightStickInput.x) > 0.1f)
        {
            float rotY = -rightStickInput.x * rotationSpeed * Time.deltaTime;
            objectToCycle.transform.Rotate(0f, rotY, 0f, Space.Self);
        }

        // Linker Thumbstick: Objekt im Raum bewegen (X/Z) jetzt über Input System Event
        if (objectToCycle != null && leftStickInput.magnitude > 0.1f)
        {
            Vector3 move = new Vector3(leftStickInput.x, 0, leftStickInput.y);
            move = xrOrigin.Camera.transform.TransformDirection(move);
            move.y = 0; // Keine Höhenänderung durch Stick
            objectToCycle.transform.position += move * panSpeed * Time.deltaTime;
        }


        // Rotation direkt setzen
        xrOrigin.CameraFloorOffsetObject.transform.localRotation = Quaternion.Euler(0f, targetYRotation, 0f);

    }

    /// <summary>
    /// Zeigt nur das Modell am angegebenen Index an, alle anderen werden ausgeblendet.
    /// Zusätzlich werden die unmittelbaren Kinder des ausgewählten Modells in currentChildren gesammelt.
    /// Anfangszustand: gesamtes Modell sichtbar (currentChildIndex == -1)
    /// </summary>
    private void ShowOnlyModel(int index)
    {
        // Deaktiviert alle anderen Top-Level-Modelle
        for (int i = 0; i < models.Count; i++)
        {
            if (models[i] != null)
                models[i].SetActive(i == index);
        }

        // Aktuelles Modell merken und seine direkten Kinder sammeln
        currentModelIndex = index;
        currentChildren.Clear();
        var root = models[index];
        if (root != null)
        {
            foreach (Transform child in root.transform)
            {
                if (child != null)
                    currentChildren.Add(child.gameObject);
            }
        }

        // Anfangszustand: gesamtes Modell (alle Kinder) anzeigen
        currentChildIndex = -1;
        ShowWholeModelChildren();
    }

    /// <summary>
    /// Aktiviert alle unmittelbaren Kinder des aktuell ausgewählten Modells ("gesamtes Modell" anzeigen).
    /// </summary>
    private void ShowWholeModelChildren()
    {
        if (currentModelIndex < 0 || currentModelIndex >= models.Count) return;
        var root = models[currentModelIndex];
        if (root == null) return;

        // Sicherstellen, dass das Root-Objekt aktiv ist
        root.SetActive(true);

        // Alle unmittelbaren Kinder aktivieren
        foreach (var child in currentChildren)
        {
            if (child != null)
                child.SetActive(true);
        }
    }

    /// <summary>
    /// Zeigt nur ein einzelnes Kind des aktuell ausgewählten Modells an und blendet die anderen Kinder aus.
    /// childIndex bezieht sich auf die Reihenfolge der immediate children in currentChildren.
    /// </summary>
    private void ShowOnlyChild(int childIndex)
    {
        if (currentModelIndex < 0 || currentModelIndex >= models.Count) return;
        var root = models[currentModelIndex];
        if (root == null) return;

        // Sicherstellen, dass das Root-Objekt aktiv ist
        root.SetActive(true);

        for (int i = 0; i < currentChildren.Count; i++)
        {
            var c = currentChildren[i];
            if (c == null) continue;
            c.SetActive(i == childIndex);
        }

        // Index aktualisieren
        currentChildIndex = childIndex;
    }

    /// <summary>
    /// Zeigt alle Top-Level-Modelle an und setzt die Kind-Auswahl zurück.
    /// </summary>
    private void ShowAllModels()
    {
        for (int i = 0; i < models.Count; i++)
        {
            if (models[i] != null)
                models[i].SetActive(true);
        }

        // Keine Modellauswahl aktiv
        currentModelIndex = -1;
        currentChildren.Clear();
        currentChildIndex = -1;
    }

    /// <summary>
    /// Zeigt das nächste Kind des aktuell ausgewählten Modells an.
    /// Ablauf: gesamtes Modell -> Kind0 -> Kind1 -> ... -> gesamtes Modell (wiederholt).
    /// </summary>
    private void NextModel()
    {
        // Wenn kein Modell ausgewählt ist, nichts tun
        if (currentModelIndex < 0 || currentModelIndex >= models.Count) return;

        if (currentChildren.Count == 0) return;

        if (currentChildIndex == -1)
        {
            // Derzeit gesamtes Modell angezeigt -> erstes Kind anzeigen
            ShowOnlyChild(0);
        }
        else
        {
            currentChildIndex++;
            if (currentChildIndex >= currentChildren.Count)
            {
                // Ende erreicht -> wieder gesamtes Modell anzeigen
                currentChildIndex = -1;
                ShowWholeModelChildren();
            }
            else
            {
                ShowOnlyChild(currentChildIndex);
            }
        }
    }

    /// <summary>
    /// Zeigt das vorherige Kind des aktuell ausgewählten Modells an.
    /// Ablauf rückwärts: gesamtes Modell -> letztes Kind -> ... -> gesamtes Modell.
    /// </summary>
    private void PrevModel()
    {
        if (currentModelIndex < 0 || currentModelIndex >= models.Count) return;
        if (currentChildren.Count == 0) return;

        if (currentChildIndex == -1)
        {
            // Derzeit gesamtes Modell angezeigt -> letztes Kind anzeigen
            currentChildIndex = currentChildren.Count - 1;
            ShowOnlyChild(currentChildIndex);
        }
        else
        {
            currentChildIndex--;
            if (currentChildIndex < 0)
            {
                // Vor dem ersten Kind -> gesamtes Modell anzeigen
                currentChildIndex = -1;
                ShowWholeModelChildren();
            }
            else
            {
                ShowOnlyChild(currentChildIndex);
            }
        }
    }
}
