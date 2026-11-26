
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace CoffeeVR.Interaction
{
    public class RefrigeratorDoorController : MonoBehaviour
    {
        [Header("Door Settings")]
        [SerializeField] private Transform doorPivot;

        [Header("Door Sound Effects")]
        [SerializeField] private AudioSource doorAudioSource;
        [SerializeField] private AudioClip doorOpenSound;
        [SerializeField] private AudioClip doorCloseSound;

        [SerializeField] private float openAngle = -120f;

        [SerializeField] private float closedAngle = 0f;

        [SerializeField] private float openSpeed = 2f;

        private bool isOpen = false;

        private Quaternion targetRotation;

        public Transform DoorPivot => doorPivot;

        private void Start()
        {
            if (doorPivot == null)
            {
                Debug.LogError("RefrigeratorDoorController requires a door pivot.", this);
                enabled = false;
                return;
            }

            targetRotation = Quaternion.Euler(
                doorPivot.localEulerAngles.x,
                closedAngle,
                doorPivot.localEulerAngles.z
            );

            doorPivot.localRotation = targetRotation;
            EnsureMilkIsGrabbable();
        }

        private void EnsureMilkIsGrabbable()
        {
            // The milk bottle is a hierarchy of visual child objects. Put one grab target on
            // the hierarchy root so the complete bottle moves as a single object.
            var transforms = GetComponentsInChildren<Transform>(true);
            Transform milk = null;
            foreach (var child in transforms)
            {
                if (child != transform && child.name == "milk")
                {
                    milk = child;
                    break;
                }
            }

            if (milk == null || milk.GetComponent<XRGrabInteractable>() != null)
                return;

            var renderers = milk.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            var colliders = milk.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                var box = milk.gameObject.AddComponent<BoxCollider>();
                box.center = milk.InverseTransformPoint(bounds.center);
                var scale = milk.lossyScale;
                box.size = new Vector3(
                    bounds.size.x / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
                    bounds.size.y / Mathf.Max(Mathf.Abs(scale.y), 0.0001f),
                    bounds.size.z / Mathf.Max(Mathf.Abs(scale.z), 0.0001f));
                colliders = new Collider[] { box };
            }

            var body = milk.GetComponent<Rigidbody>();
            if (body == null)
                body = milk.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var grab = milk.gameObject.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.throwOnDetach = false;
            grab.colliders.Clear();
            foreach (var itemCollider in colliders)
            {
                if (itemCollider != null && itemCollider.enabled && !itemCollider.isTrigger)
                    grab.colliders.Add(itemCollider);
            }

            milk.gameObject.AddComponent<GrabInteractable>();
        }

        private void Update()
        {
            if (doorPivot == null)
                return;

            doorPivot.localRotation = Quaternion.Slerp(
                doorPivot.localRotation,
                targetRotation,
                Time.deltaTime * openSpeed
            );
        }

        public void ToggleDoor()
        {
            isOpen = !isOpen;

            if (doorAudioSource != null)
            {
                if (isOpen && doorOpenSound != null)
                {
                    doorAudioSource.PlayOneShot(doorOpenSound);
                }
                else if (!isOpen && doorCloseSound != null)
                {
                    doorAudioSource.PlayOneShot(doorCloseSound);
                }
            }

            float targetAngle = isOpen ? openAngle : closedAngle;

            targetRotation = Quaternion.Euler(
                doorPivot.localEulerAngles.x,
                targetAngle,
                doorPivot.localEulerAngles.z
            );
        }
    }
}
