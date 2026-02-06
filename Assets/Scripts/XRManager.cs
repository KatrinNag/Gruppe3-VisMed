using System;
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
    private Vector2 playerMoveInput = Vector2.zero; // from Player.Move (D-pad)
    // Bone selection
    private List<Transform> bones = new List<Transform>();
    private int currentBoneIndex = -1;
    private Dictionary<Transform, Vector3> boneOriginalLocalPositions = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, Quaternion> boneOriginalLocalRotations = new Dictionary<Transform, Quaternion>();
    private List<Transform> originalBoneOrder = new List<Transform>(); // Store original order for reset
    private Dictionary<GameObject, Quaternion> modelOriginalLocalRotations = new Dictionary<GameObject, Quaternion>();
    private Dictionary<GameObject, Vector3> modelOriginalLocalPositions = new Dictionary<GameObject, Vector3>();
    public float bonePullDistance = 0.25f; // how far the selected bone is pulled out
    public float boneRotateSpeed = 90f; // degrees per second when rotating a bone
    
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
        // Also listen to Player.Move so D-pad can be used for rotation (and movement)
        try
        {
            controls.Player.Move.performed += ctx => { playerMoveInput = ctx.ReadValue<Vector2>(); Debug.Log($"Player.Move.performed: {playerMoveInput}"); };
            controls.Player.Move.canceled += ctx => { playerMoveInput = Vector2.zero; Debug.Log("Player.Move.canceled"); };
            controls.Player.Move.Enable();
        }
        catch (Exception)
        {
            // Player map might not exist in some setups; ignore if missing
        }
        // Note: Shuffle function removed. No Player.Attack / Player.Interact handlers for shuffle/reset.
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
        else
        {
            // If modelsParent was found and objectToCycle is still null, set it to modelsParent
            if (objectToCycle == null)
            {
                objectToCycle = modelsParent;
            }
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

            // Store original local rotations and positions for each model so we can reset when switching
            modelOriginalLocalRotations.Clear();
            modelOriginalLocalPositions.Clear();
            foreach (var m in models)
            {
                if (m != null)
                {
                    modelOriginalLocalRotations[m] = m.transform.localRotation;
                    modelOriginalLocalPositions[m] = m.transform.localPosition;
                }
            }

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
        // Debug: Log playerMoveInput every frame it has input
        if (playerMoveInput != Vector2.zero)
        {
            Debug.Log($"playerMoveInput current value: {playerMoveInput}");
        }

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

        // Dual-stick rotation: left stick controls X (pitch), right stick controls Y (yaw)
        float rotY = rightStickInput.x; // yaw
        float rotX = leftStickInput.y;  // pitch

        // Fallbacks for yaw (right stick): use Gamepad rightStick horizontal as primary fallback
        if (Mathf.Abs(rotY) < 0.05f)
        {
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp != null)
            {
                var rs = gp.rightStick.ReadValue();
                if (Mathf.Abs(rs.x) > 0.05f) rotY = rs.x;
            }
        }
        
        // Then try Player.Move horizontal (D-pad left/right) or dpad/keyboard
        if (Mathf.Abs(rotY) < 0.1f && Mathf.Abs(playerMoveInput.x) > 0.1f)
        {
            rotY = playerMoveInput.x;
        }
        if (Mathf.Abs(rotY) < 0.05f)
        {
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp != null)
            {
                var d = gp.dpad.ReadValue();
                if (Mathf.Abs(d.x) > 0.05f) rotY = d.x;
            }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) rotY = -1f;
                else if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) rotY = 1f;
            }
        }

        // Fallbacks for pitch (left stick vertical): use dpad vertical or keyboard arrows if needed
        if (Mathf.Abs(rotX) < 0.05f)
        {
            var gp2 = UnityEngine.InputSystem.Gamepad.current;
            if (gp2 != null)
            {
                var d2 = gp2.dpad.ReadValue();
                if (Mathf.Abs(d2.y) > 0.05f) rotX = d2.y;
            }
            var kb2 = UnityEngine.InputSystem.Keyboard.current;
            if (kb2 != null)
            {
                if (kb2.downArrowKey.isPressed || kb2.sKey.isPressed) rotX = -1f;
                else if (kb2.upArrowKey.isPressed || kb2.wKey.isPressed) rotX = 1f;
            }
        }

        bool didRotate = false;
        float dead = 0.01f; // Reduced dead zone to catch smaller stick inputs

        // If a bone is selected, rotate the bone itself around its local axes (so it spins in place)
        if (currentModelIndex != -1 && currentBoneIndex != -1 && bones != null && currentBoneIndex < bones.Count)
        {
            var bone = bones[currentBoneIndex];
            if (Mathf.Abs(rotX) > dead)
            {
                float angle = rotX * boneRotateSpeed * Time.deltaTime;
                bone.Rotate(Vector3.right, angle, Space.Self);
                didRotate = true;
            }
            if (Mathf.Abs(rotY) > dead)
            {
                float angle = rotY * boneRotateSpeed * Time.deltaTime;
                bone.Rotate(Vector3.up, angle, Space.Self);
                didRotate = true;
            }
            if (didRotate) Debug.Log($"Rotating bone in place {currentBoneIndex} by X={rotX:F2}, Y={rotY:F2}");
        }
        // If a single model is shown (not "all") and no bone is selected, rotate that model using both sticks
        else if (currentModelIndex != -1 && currentModelIndex < models.Count && models[currentModelIndex] != null)
        {
            var m = models[currentModelIndex].transform;
            if (Mathf.Abs(rotX) > dead)
            {
                float rX = rotX * rotationSpeed * Time.deltaTime;
                m.Rotate(Vector3.right, rX, Space.Self);
                didRotate = true;
            }
            if (Mathf.Abs(rotY) > dead)
            {
                float rY = -rotY * rotationSpeed * Time.deltaTime; // keep previous yaw sign
                m.Rotate(Vector3.up, rY, Space.Self);
                didRotate = true;
            }
            if (didRotate) Debug.Log($"Rotating model in place {currentModelIndex} by X={rotX:F2}, Y={rotY:F2}");
        }

        // Linker Thumbstick: Objekt im Raum bewegen (X/Z) ABER nur wenn kein Knochen ausgewählt ist
        // and only when showing ALL models (currentModelIndex == -1). When a model is shown or a bone is selected,
        // the sticks are used for rotation.
        if (objectToCycle != null && currentModelIndex == -1 && currentBoneIndex == -1 && (Mathf.Abs(leftStickInput.x) > 0.1f || Mathf.Abs(leftStickInput.y) > 0.1f) && xrOrigin != null && xrOrigin.Camera != null)
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

        // Keyboard shortcuts for shuffle/reset removed.

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
        // When showing only one model, populate selectable bones and reset selection
        PopulateBonesForCurrentModel();
        DeselectBone();
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
        // remember previous model to reset its rotation when switching
        int prevModel = currentModelIndex;

        // If currently showing all models, go to first model
        if (currentModelIndex == -1)
        {
            currentModelIndex = 0;
            ShowOnlyModel(currentModelIndex);
            Debug.Log($"📍 Showing Model {currentModelIndex} (of {models.Count})");
        }
        else
        {
            // If bones are available, advance bone selection
            if (bones != null && bones.Count > 0)
            {
                int prevBone = currentBoneIndex;
                currentBoneIndex++;
                if (currentBoneIndex >= bones.Count)
                    currentBoneIndex = -1; // cycle back to no bone selected

                UpdateBoneSelection(prevBone, currentBoneIndex);
                Debug.Log($"Selected bone {currentBoneIndex} (of {bones.Count})");
            }
            else
            {
                currentModelIndex++;
                if (currentModelIndex >= models.Count)
                {
                    // After last model show all and reset previous model
                    if (prevModel >= 0)
                        ResetModelTransform(prevModel);

                    ShowAllModels();
                    currentModelIndex = -1;
                    Debug.Log("🔄 Showing ALL models");
                }
                else
                {
                    // Switching from one model to another: reset previous
                    if (prevModel >= 0 && prevModel != currentModelIndex)
                        ResetModelTransform(prevModel);

                    ShowOnlyModel(currentModelIndex);
                    Debug.Log($"📍 Showing Model {currentModelIndex} (of {models.Count})");
                }
            }
        }
    }

    /// <summary>
    /// Zeigt das vorherige Modell an oder alle Modelle, wenn am Anfang
    /// </summary>
    private void PrevModel()
    {
        // remember previous model to reset its rotation when switching
        int prevModel = currentModelIndex;

        // From "all" go to last model
        if (currentModelIndex == -1)
        {
            currentModelIndex = models.Count - 1;
            ShowOnlyModel(currentModelIndex);
            Debug.Log($"📍 Showing Model {currentModelIndex} (of {models.Count})");
        }
        else
        {
            if (bones != null && bones.Count > 0)
            {
                int prevBone = currentBoneIndex;
                currentBoneIndex--;
                if (currentBoneIndex < -1)
                    currentBoneIndex = bones.Count - 1;

                UpdateBoneSelection(prevBone, currentBoneIndex);
                Debug.Log($"🦴 Selected bone {currentBoneIndex} (of {bones.Count})");
            }
            else
            {
                currentModelIndex--;
                if (currentModelIndex < 0)
                {
                    // Before first model show all and reset previous model
                    if (prevModel >= 0)
                        ResetModelTransform(prevModel);

                    ShowAllModels();
                    currentModelIndex = -1;
                    Debug.Log("🔄 Showing ALL models");
                }
                else
                {
                    // switching from one model to another: reset previous
                    if (prevModel >= 0 && prevModel != currentModelIndex)
                        ResetModelTransform(prevModel);

                    ShowOnlyModel(currentModelIndex);
                    Debug.Log($"📍 Showing Model {currentModelIndex} (of {models.Count})");
                }
            }
        }
    }

    private void UpdateBoneSelection(int prevIndex, int newIndex)
    {
        // Reset previous bone
        if (prevIndex >= 0 && prevIndex < bones.Count)
        {
            ResetBoneTransform(bones[prevIndex]);
        }

        // Select new bone
        if (newIndex >= 0 && newIndex < bones.Count)
        {
            SelectBone(newIndex);
        }
        else
        {
            currentBoneIndex = -1;
        }
    }

    private void PopulateBonesForCurrentModel()
    {
        bones.Clear();
        boneOriginalLocalPositions.Clear();
        boneOriginalLocalRotations.Clear();

        if (currentModelIndex >= 0 && currentModelIndex < models.Count && models[currentModelIndex] != null)
        {
            // Collect all child transforms (recursive) except the root model transform
            var all = models[currentModelIndex].GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t == models[currentModelIndex].transform) continue;
                bones.Add(t);
                boneOriginalLocalPositions[t] = t.localPosition;
                boneOriginalLocalRotations[t] = t.localRotation;
            }
        }
        Debug.Log($"PopulateBones: found {bones.Count} bones for model {currentModelIndex}");
    }

    private void SelectBone(int index)
    {
        if (index < 0 || index >= bones.Count) return;
        currentBoneIndex = index;
        var bone = bones[index];
        if (!boneOriginalLocalPositions.ContainsKey(bone))
        {
            boneOriginalLocalPositions[bone] = bone.localPosition;
            boneOriginalLocalRotations[bone] = bone.localRotation;
        }

        // Move bone along the camera direction in world space but apply as local offset
        Vector3 dir = Vector3.zero;
        if (xrOrigin != null && xrOrigin.Camera != null)
            dir = (bone.position - xrOrigin.Camera.transform.position).normalized;
        else
            dir = bone.forward;

        // Convert desired world position into localPosition relative to parent
        Vector3 targetWorld = bone.position + dir * bonePullDistance;
        if (bone.parent != null)
            bone.localPosition = bone.parent.InverseTransformPoint(targetWorld);
        else
            bone.position = targetWorld;
    }

    private void DeselectBone()
    {
        if (currentBoneIndex >= 0 && currentBoneIndex < bones.Count)
        {
            ResetBoneTransform(bones[currentBoneIndex]);
        }
        currentBoneIndex = -1;
    }

    private void ResetBoneTransform(Transform bone)
    {
        if (bone == null) return;
        if (boneOriginalLocalPositions.ContainsKey(bone))
            bone.localPosition = boneOriginalLocalPositions[bone];
        if (boneOriginalLocalRotations.ContainsKey(bone))
            bone.localRotation = boneOriginalLocalRotations[bone];
    }

    /// <summary>
    /// Restores the original rotation of a model (if known).
    /// </summary>
    private void ResetModelTransform(int modelIndex)
    {
        if (modelIndex < 0 || modelIndex >= models.Count) return;
        var m = models[modelIndex];
        if (m == null) return;
        if (modelOriginalLocalRotations.ContainsKey(m))
        {
            m.transform.localRotation = modelOriginalLocalRotations[m];
        }
        if (modelOriginalLocalPositions.ContainsKey(m))
        {
            m.transform.localPosition = modelOriginalLocalPositions[m];
        }
    }
}