using UnityEngine;
using UnityEngine.InputSystem;

public class VRHandGripController : MonoBehaviour
{
    [Header("Grip Input")]
    public InputActionReference gripAction;

    [Header("Hand Bones")]
    public Transform[] fingerBones;

    [Header("Finger Bend")]
    public float bendAngle = 30f;
    public float smoothSpeed = 12f;

    private float gripValue;
    private float smoothedGrip;

    private Quaternion[] originalRotations;

    private void Start()
    {
        originalRotations = new Quaternion[fingerBones.Length];

        for (int i = 0; i < fingerBones.Length; i++)
        {
            if (fingerBones[i] != null)
            {
                originalRotations[i] = fingerBones[i].localRotation;
            }
        }
    }
    private void OnEnable()
    {
        if (gripAction != null && gripAction.action != null)
            gripAction.action.Enable();
    }

    private void OnDisable()
    {
        if (gripAction != null && gripAction.action != null)
            gripAction.action.Disable();
    }

    private void Update()
    {
        if (gripAction != null && gripAction.action != null)
        {
            gripValue = gripAction.action.ReadValue<float>();
        }
    }

    private void LateUpdate()
    {
        smoothedGrip = Mathf.MoveTowards(
            smoothedGrip,
            gripValue,
            Time.deltaTime * smoothSpeed
        );

        for (int i = 0; i < fingerBones.Length; i++)
        {
            Transform bone = fingerBones[i];

            if (bone == null)
                continue;

            Quaternion bendRotation = Quaternion.Euler(
                bendAngle * smoothedGrip,
                0f,
                0f
            );

            bone.localRotation = originalRotations[i] * bendRotation;
        }
    }

}
